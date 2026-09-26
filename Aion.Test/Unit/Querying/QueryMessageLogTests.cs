using Aion.Components.Querying;
using Aion.Components.Querying.Messages;
using Aion.Contracts.Queries;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryMessageLogTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 26, 14, 2, 0, TimeSpan.Zero);

    private readonly QueryMessageLog _sut = new();
    private readonly Guid _tab = Guid.NewGuid();

    private static QueryResult Rows(int count) => new()
    {
        Columns = ["id"],
        Rows = Enumerable.Range(1, count).Select(i => new Dictionary<string, object> { ["id"] = i }).ToList()
    };

    [Fact]
    public void Transaction_LogsBeginEachStatementWithItsOutcomeAndCommit()
    {
        // Act
        _sut.RecordBegin(_tab, At);
        _sut.RecordRun(_tab, "UPDATE products SET stock = 0 WHERE id = 1", new QueryResult { RowsAffected = 1 }, At, TimeSpan.FromMilliseconds(43));
        _sut.RecordEnd(_tab, committed: true, At.AddSeconds(5));

        // Assert
        _sut.For(_tab).Select(m => (m.Kind, m.Text)).ShouldBe(
        [
            (QueryMessageKind.Transaction, "BEGIN"),
            (QueryMessageKind.Statement, "UPDATE products SET stock = 0 WHERE id = 1"),
            (QueryMessageKind.Success, "1 row affected"),
            (QueryMessageKind.Transaction, "COMMIT")
        ]);
    }

    [Fact]
    public void RolledBackTransaction_EndsWithRollback()
    {
        // Act
        _sut.RecordBegin(_tab, At);
        _sut.RecordEnd(_tab, committed: false, At);

        // Assert
        _sut.For(_tab).Last().Text.ShouldBe("ROLLBACK");
    }

    [Fact]
    public void Outcome_CarriesTheDurationAndTheTimeTheStatementFinished()
    {
        // Act
        _sut.RecordRun(_tab, "DELETE FROM carts", new QueryResult { RowsAffected = 3 }, At, TimeSpan.FromMilliseconds(250));

        // Assert
        var outcome = _sut.For(_tab).Last();
        outcome.Text.ShouldBe("3 rows affected");
        outcome.Duration.ShouldBe(TimeSpan.FromMilliseconds(250));
        outcome.Time.ShouldBe(At.AddMilliseconds(250));
        _sut.For(_tab).First().Time.ShouldBe(At);
    }

    [Fact]
    public void Select_LogsTheRowsItReturned()
    {
        // Act
        _sut.RecordRun(_tab, "SELECT id FROM products", Rows(15), At, TimeSpan.Zero);

        // Assert
        _sut.For(_tab).Last().Text.ShouldBe("15 rows returned");
    }

    [Fact]
    public void Select_ReturningOneRow_SaysRowInTheSingular()
    {
        // Act
        _sut.RecordRun(_tab, "SELECT id FROM products LIMIT 1", Rows(1), At, TimeSpan.Zero);

        // Assert
        _sut.For(_tab).Last().Text.ShouldBe("1 row returned");
    }

    [Fact]
    public void StatementWithoutACount_LogsThatItCompleted()
    {
        // Act
        _sut.RecordRun(_tab, "CREATE TABLE t (id INTEGER)", new QueryResult(), At, TimeSpan.Zero);

        // Assert
        _sut.For(_tab).Last().Text.ShouldBe("Completed");
    }

    [Fact]
    public void FailedStatement_LogsTheErrorWithWhereItIs()
    {
        // Arrange
        var result = new QueryResult { Error = "no such table: prodcts" };
        result.ErrorDetail = QueryErrorNormalizer.Normalize(result.Error, "SELECT *\nFROM prodcts");

        // Act
        _sut.RecordRun(_tab, "SELECT *\nFROM prodcts", result, At, TimeSpan.FromMilliseconds(3));

        // Assert
        var outcome = _sut.For(_tab).Last();
        outcome.Kind.ShouldBe(QueryMessageKind.Error);
        outcome.Text.ShouldBe("Error at line 2, column 6: no such table: prodcts");
    }

    [Fact]
    public void CancelledStatement_LogsThatItWasCancelled()
    {
        // Act
        _sut.RecordRun(_tab, "SELECT pg_sleep(60)", new QueryResult { Error = "Query cancelled", Cancelled = true }, At, TimeSpan.FromSeconds(2));

        // Assert
        var outcome = _sut.For(_tab).Last();
        outcome.Kind.ShouldBe(QueryMessageKind.Info);
        outcome.Text.ShouldBe("Cancelled");
    }

    [Fact]
    public void Statement_IsShownOnOneLineWithoutLeadingCommentsOrTheTerminator()
    {
        // Act
        _sut.RecordRun(_tab, "-- Restock\n-- the first product\nUPDATE products\n    SET stock = 10\n    WHERE id = 1;\n", new QueryResult { RowsAffected = 1 }, At, TimeSpan.Zero);

        // Assert
        var statement = _sut.For(_tab).First();
        statement.Text.ShouldBe("UPDATE products SET stock = 10 WHERE id = 1");
        statement.Detail.ShouldBe("-- Restock\n-- the first product\nUPDATE products\n    SET stock = 10\n    WHERE id = 1;");
    }

    [Fact]
    public void LongStatement_IsShortenedWithAnEllipsis()
    {
        // Arrange
        var sql = "SELECT " + string.Join(", ", Enumerable.Range(1, 60).Select(i => $"column_{i}")) + " FROM wide";

        // Act
        _sut.RecordRun(_tab, sql, Rows(0), At, TimeSpan.Zero);

        // Assert
        var text = _sut.For(_tab).First().Text;
        text.Length.ShouldBe(QueryMessageLog.StatementPreviewLength);
        text.ShouldStartWith("SELECT column_1, column_2");
        text.ShouldEndWith("…");
    }

    [Fact]
    public void Tabs_KeepSeparateLogs()
    {
        // Arrange
        var other = Guid.NewGuid();

        // Act
        _sut.RecordRun(_tab, "SELECT 1", Rows(1), At, TimeSpan.Zero);
        _sut.RecordBegin(other, At);

        // Assert
        _sut.For(_tab).Count.ShouldBe(2);
        _sut.For(other).Select(m => m.Text).ShouldBe(["BEGIN"]);
    }

    [Fact]
    public void Log_KeepsOnlyTheMostRecentLines()
    {
        // Act
        for (var i = 0; i < QueryMessageLog.MaxMessagesPerTab; i++)
        {
            _sut.RecordRun(_tab, $"SELECT {i}", Rows(1), At, TimeSpan.Zero);
        }

        // Assert
        var messages = _sut.For(_tab);
        messages.Count.ShouldBe(QueryMessageLog.MaxMessagesPerTab);
        messages.First().Text.ShouldBe($"SELECT {QueryMessageLog.MaxMessagesPerTab / 2}");
        messages.Last().Text.ShouldBe("1 row returned");
    }

    [Fact]
    public void Forget_DropsTheTabsLog()
    {
        // Arrange
        _sut.RecordBegin(_tab, At);

        // Act
        _sut.Forget(_tab);

        // Assert
        _sut.For(_tab).ShouldBeEmpty();
    }

    [Fact]
    public void Recording_AnnouncesWhichTabsLogChanged()
    {
        // Arrange
        var changed = new List<Guid>();
        _sut.MessagesChanged += changed.Add;

        // Act
        _sut.RecordBegin(_tab, At);
        _sut.RecordRun(_tab, "SELECT 1", Rows(1), At, TimeSpan.Zero);
        _sut.RecordEnd(_tab, committed: true, At);

        // Assert
        changed.ShouldBe([_tab, _tab, _tab]);
    }

    [Theory]
    [InlineData(QueryResultKind.EstimatedPlan, "Estimated plan, statement not run")]
    [InlineData(QueryResultKind.ActualPlan, "Actual plan, changes rolled back")]
    public void PlanRun_SaysWhatHappenedToTheStatement(QueryResultKind kind, string expected)
    {
        // Act
        _sut.RecordRun(_tab, "DELETE FROM carts", new QueryResult(), At, TimeSpan.FromMilliseconds(4), kind);

        // Assert
        var outcome = _sut.For(_tab).Last();
        outcome.Kind.ShouldBe(QueryMessageKind.Success);
        outcome.Text.ShouldBe(expected);
    }
}
