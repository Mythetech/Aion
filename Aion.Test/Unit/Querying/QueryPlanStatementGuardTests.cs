using Aion.Contracts.Queries;
using Aion.Core.Database.MySql;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryPlanStatementGuardTests
{
    [Theory]
    [InlineData("SELECT * FROM t")]
    [InlineData("SELECT * FROM t;")]
    [InlineData("UPDATE t SET a = 1 WHERE id = 2 ;  \n")]
    public void SingleStatement_IsAllowed(string query)
    {
        QueryPlanStatementGuard.GetActualPlanRefusal(query).ShouldBeNull();
    }

    [Theory]
    [InlineData("SELECT 1; DELETE FROM t")]
    [InlineData("SELECT 1; -- trailing comment")]
    [InlineData("SELECT 'a;b'")]
    public void TextWithInnerSemicolon_IsRefused(string query)
    {
        QueryPlanStatementGuard.RequireSingleStatement(query).ShouldBe(QueryPlanStatementGuard.MultipleStatementsMessage);
    }

    [Theory]
    [InlineData("SELECT 1 COMMIT DELETE FROM t")]
    [InlineData("exec('rollback')")]
    public void TransactionControl_IsRefused(string query)
    {
        QueryPlanStatementGuard.GetActualPlanRefusal(query).ShouldBe(QueryPlanStatementGuard.TransactionControlMessage);
    }

    [Fact]
    public void IdentifiersContainingTransactionWords_AreAllowed()
    {
        QueryPlanStatementGuard.RejectTransactionControl("SELECT commit_hash FROM rollback_log").ShouldBeNull();
    }

    [Theory]
    [InlineData("SELECT 1", "SELECT")]
    [InlineData("  -- note\n  update t set a = 1", "update")]
    [InlineData("# note\n/* block */ DELETE FROM t", "DELETE")]
    [InlineData("/*!50000 DROP TABLE t */", "/")]
    [InlineData("(SELECT 1)", "(")]
    [InlineData("   ", "")]
    public void MySqlLeadingKeyword_SkipsCommentsButNotExecutableComments(string statement, string expected)
    {
        MySqlStatementGuard.GetLeadingKeyword(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("UPDATE t SET a = 1; SELECT * FROM t;")]
    [InlineData("SET @id = 1; DELETE FROM t WHERE id = @id")]
    public void MySqlTransaction_AllowsDmlAndReads(string query)
    {
        MySqlStatementGuard.GetTransactionRefusal(query).ShouldBeNull();
    }

    [Theory]
    [InlineData("CREATE TABLE x (id int)")]
    [InlineData("UPDATE t SET a = 1; ALTER TABLE t ADD b int")]
    [InlineData("TRUNCATE t")]
    [InlineData("SET autocommit = 1")]
    [InlineData("CALL do_things()")]
    [InlineData("/*!50000 DROP TABLE t */")]
    public void MySqlTransaction_RefusesStatementsThatCommitImplicitly(string query)
    {
        MySqlStatementGuard.GetTransactionRefusal(query).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("COMMIT")]
    [InlineData("rollback")]
    [InlineData("START TRANSACTION")]
    public void MySqlTransaction_RefusesTransactionControl(string query)
    {
        MySqlStatementGuard.GetTransactionRefusal(query)!.ShouldContain("Commit and Rollback buttons");
    }

    [Theory]
    [InlineData("SELECT * FROM t")]
    [InlineData("UPDATE t SET a = 1")]
    [InlineData("DELETE FROM t")]
    public void MySqlActualPlan_AllowsSelectUpdateDelete(string query)
    {
        MySqlStatementGuard.GetActualPlanRefusal(query).ShouldBeNull();
    }

    [Theory]
    [InlineData("DROP TABLE t")]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("SELECT 1; DROP TABLE t")]
    public void MySqlActualPlan_RefusesEverythingElse(string query)
    {
        MySqlStatementGuard.GetActualPlanRefusal(query).ShouldNotBeNull();
    }
}
