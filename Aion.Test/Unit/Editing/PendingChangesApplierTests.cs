using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Querying.Editing;
using Aion.Contracts.Queries;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class PendingChangesApplierTests
{
    private readonly EditingFixture _fixture = new();
    private readonly EditState _editState = new() { IsEditMode = true };

    private PendingChangesApplier CreateSut() =>
        new(_fixture.CreateSqlBuilder(), _fixture.Bus, NullLogger<PendingChangesApplier>.Instance);

    private void Rename(int rowIndex, string newName)
    {
        var result = _fixture.CreateEditableResult();
        _editState.UpdateCell(rowIndex, "name", newName, result.Rows[rowIndex]);
    }

    [Fact]
    public async Task Update_AffectingOneRow_Succeeds()
    {
        Rename(0, "O'Brien's Hub");

        await CreateSut().Consume(new ApplyPendingChanges(_editState, _fixture.CreateEditableResult()));

        _fixture.ExecutedSql.ShouldBe(new[] { "UPDATE \"public\".\"users\"\nSET \"name\" = 'O''Brien''s Hub'\nWHERE \"id\" = 1;" });
        _editState.HasChanges.ShouldBeFalse();
        _fixture.Notifications().ShouldContain(n => n.Severity == Severity.Success);
        _fixture.Published().OfType<RunQuery>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Update_AffectingZeroRows_FailsAndKeepsChanges()
    {
        Rename(0, "Ada Lovelace");
        _fixture.EnqueueResult(new QueryResult { RowsAffected = 0 });

        await CreateSut().Consume(new ApplyPendingChanges(_editState, _fixture.CreateEditableResult()));

        var notification = _fixture.Notifications().ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Error);
        notification.Message.ShouldContain("Update of row 1 (id = 1) affected 0 rows instead of 1");
        _editState.HasChanges.ShouldBeTrue();
        _fixture.Published().OfType<RunQuery>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_AffectingSeveralRowsOutsideTransaction_ReportsThatRowsChanged()
    {
        Rename(0, "Ada Lovelace");
        _fixture.EnqueueResult(new QueryResult { RowsAffected = 3 });

        await CreateSut().Consume(new ApplyPendingChanges(_editState, _fixture.CreateEditableResult()));

        var notification = _fixture.Notifications().ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Error);
        notification.Message.ShouldContain("affected 3 rows instead of 1");
        notification.Message.ShouldContain("outside a transaction");
    }

    [Fact]
    public async Task UnknownRowCount_IsNotTreatedAsFailure()
    {
        Rename(0, "Ada Lovelace");
        _fixture.EnqueueResult(new QueryResult { RowsAffected = null });

        await CreateSut().Consume(new ApplyPendingChanges(_editState, _fixture.CreateEditableResult()));

        _fixture.Notifications().ShouldContain(n => n.Severity == Severity.Success);
        _editState.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task MismatchInsideTransaction_RollsBackAndDoesNotCommit()
    {
        Rename(0, "Ada Lovelace");
        Rename(1, "Grace Hopper");
        _fixture.EnqueueResult(new QueryResult { RowsAffected = 1 });
        _fixture.EnqueueResult(new QueryResult { RowsAffected = 0 });

        await CreateSut().Consume(new ApplyPendingChanges(_editState, _fixture.CreateEditableResult()));

        await _fixture.Provider.Received(1).BeginTransactionAsync(Arg.Any<string>());
        await _fixture.Provider.Received(1).RollbackTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
        await _fixture.Provider.DidNotReceive().CommitTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
        await _fixture.Provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var notification = _fixture.Notifications().ShouldHaveSingleItem();
        notification.Message.ShouldContain("Update of row 2 (id = 2) affected 0 rows");
        notification.Message.ShouldContain("rolled back");
        _editState.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public async Task StatementError_RollsBackAndReportsTheChange()
    {
        Rename(0, "Ada Lovelace");
        Rename(1, "Grace Hopper");
        _fixture.EnqueueResult(new QueryResult { Error = "value too long" });

        await CreateSut().Consume(new ApplyPendingChanges(_editState, _fixture.CreateEditableResult()));

        await _fixture.Provider.Received(1).RollbackTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
        _fixture.ExecutedSql.Count.ShouldBe(1);
        _fixture.Notifications().ShouldHaveSingleItem().Message.ShouldContain("Update of row 1 (id = 1) failed: value too long");
    }

    [Fact]
    public async Task ProviderWithoutRowEditing_ExecutesNothing()
    {
        var fixture = new EditingFixture(supportsEditing: false);
        var editState = new EditState { IsEditMode = true };
        var result = fixture.CreateEditableResult();
        editState.UpdateCell(0, "name", "x", result.Rows[0]);
        var sut = new PendingChangesApplier(fixture.CreateSqlBuilder(), fixture.Bus, NullLogger<PendingChangesApplier>.Instance);

        await sut.Consume(new ApplyPendingChanges(editState, result));

        fixture.ExecutedSql.ShouldBeEmpty();
        fixture.Notifications().ShouldHaveSingleItem().Severity.ShouldBe(Severity.Error);
    }
}
