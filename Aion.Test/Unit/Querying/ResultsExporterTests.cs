using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.Files;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class ResultsExporterTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IFileSaveService _saveService = Substitute.For<IFileSaveService>();
    private readonly QueryState _state;
    private readonly List<AddNotification> _notifications = [];

    public ResultsExporterTests()
    {
        _state = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        _bus.PublishAsync(Arg.Do<AddNotification>(n => _notifications.Add(n))).Returns(Task.CompletedTask);
    }

    private static QueryResult SampleResult() => new()
    {
        Columns = ["id", "name"],
        Rows = [new Dictionary<string, object> { ["id"] = 1, ["name"] = "widget" }]
    };

    private JsonResultsExporter CreateJsonExporter() =>
        new(_state, Substitute.For<ILogger<JsonResultsExporter>>(), _bus, _saveService);

    private ExcelResultsExporter CreateExcelExporter() =>
        new(_state, Substitute.For<ILogger<ExcelResultsExporter>>(), _bus, _saveService);

    [Fact]
    public async Task JsonExport_Cancelled_SaysJson()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await CreateJsonExporter().Consume(new ExportResultsToJson(SampleResult()));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Message.ShouldBe("JSON export cancelled");
        notification.Severity.ShouldBe(Severity.Info);
    }

    [Fact]
    public async Task JsonExport_Failure_NotifiesError()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).ThrowsAsync(new IOException("disk full"));

        await CreateJsonExporter().Consume(new ExportResultsToJson(SampleResult()));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Message.ShouldBe("Failed to export results to JSON");
        notification.Severity.ShouldBe(Severity.Error);
    }

    [Fact]
    public async Task JsonExport_NoResult_NotifiesWarning()
    {
        await CreateJsonExporter().Consume(new ExportResultsToJson());

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Warning);
        await _saveService.DidNotReceiveWithAnyArgs().SaveFileAsync(default!, default!);
    }

    [Fact]
    public async Task JsonExport_Success_NamesTheFile()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await CreateJsonExporter().Consume(new ExportResultsToJson(SampleResult()));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Success);
        notification.Message.ShouldStartWith("Exported results to query_results_");
        notification.Message.ShouldEndWith(".json");
    }

    [Fact]
    public async Task ExcelExport_Cancelled_SaysExcel()
    {
        _saveService.PromptFileSaveAsync(Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null);

        await CreateExcelExporter().Consume(new ExportResultsToExcel(SampleResult()));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Message.ShouldBe("Excel export cancelled");
        notification.Severity.ShouldBe(Severity.Info);
    }
}
