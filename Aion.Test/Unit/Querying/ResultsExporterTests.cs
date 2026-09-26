using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Queries;
using ClosedXML.Excel;
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

    private SelectedRowsExporter CreateSelectedRowsExporter() =>
        new(Substitute.For<ILogger<SelectedRowsExporter>>(), _bus, _saveService);

    private static IXLWorksheet OpenWorksheet(byte[] data)
    {
        var workbook = new XLWorkbook(new MemoryStream(data));
        return workbook.Worksheets.First();
    }

    [Fact]
    public async Task CsvExport_Cancelled_SaysCsv()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var exporter = new CsvResultsExporter(_state, Substitute.For<ILogger<CsvResultsExporter>>(), _bus, _saveService);

        await exporter.Consume(new ExportResultsToCsv(SampleResult()));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Message.ShouldBe("CSV export cancelled");
        notification.Severity.ShouldBe(Severity.Info);
    }

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
        _saveService.ReceivedCalls().ShouldBeEmpty();
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
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<byte[]>()).Returns(false);

        await CreateExcelExporter().Consume(new ExportResultsToExcel(SampleResult()));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Message.ShouldBe("Excel export cancelled");
        notification.Severity.ShouldBe(Severity.Info);
    }

    [Fact]
    public async Task ExcelExport_SavesTheWorkbookBytesThroughTheFileService()
    {
        string? fileName = null;
        byte[]? data = null;
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<byte[]>())
            .Returns(call => { fileName = call.ArgAt<string>(0); data = call.ArgAt<byte[]>(1); return true; });

        await CreateExcelExporter().Consume(new ExportResultsToExcel(SampleResult()));

        fileName.ShouldNotBeNull().ShouldEndWith(".xlsx");
        var sheet = OpenWorksheet(data.ShouldNotBeNull());
        sheet.Cell(1, 1).GetString().ShouldBe("id");
        sheet.Cell(1, 2).GetString().ShouldBe("name");
        sheet.Cell(2, 2).GetString().ShouldBe("widget");
        await _saveService.DidNotReceiveWithAnyArgs().PromptFileSaveAsync(default!, default!);
        _notifications.ShouldHaveSingleItem().Severity.ShouldBe(Severity.Success);
    }

    [Fact]
    public async Task SelectedRowsExcelExport_SavesTheWorkbookBytesThroughTheFileService()
    {
        string? fileName = null;
        byte[]? data = null;
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<byte[]>())
            .Returns(call => { fileName = call.ArgAt<string>(0); data = call.ArgAt<byte[]>(1); return true; });
        var rows = new List<Dictionary<string, object>> { new() { ["id"] = 7, ["name"] = "gadget" } };

        await CreateSelectedRowsExporter().Consume(new ExportSelectedRows(rows, ["id", "name"], "Excel"));

        fileName.ShouldNotBeNull().ShouldEndWith(".xlsx");
        var sheet = OpenWorksheet(data.ShouldNotBeNull());
        sheet.Cell(2, 1).GetString().ShouldBe("7");
        sheet.Cell(2, 2).GetString().ShouldBe("gadget");
        await _saveService.DidNotReceiveWithAnyArgs().PromptFileSaveAsync(default!, default!);
        _notifications.ShouldHaveSingleItem().Severity.ShouldBe(Severity.Success);
    }

    [Fact]
    public async Task SelectedRowsExcelExport_Cancelled_SaysExcel()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<byte[]>()).Returns(false);
        var rows = new List<Dictionary<string, object>> { new() { ["id"] = 7 } };

        await CreateSelectedRowsExporter().Consume(new ExportSelectedRows(rows, ["id"], "Excel"));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Message.ShouldBe("Excel export cancelled");
        notification.Severity.ShouldBe(Severity.Info);
    }

    private CsvResultsExporter CreateCsvExporter() =>
        new(_state, Substitute.For<ILogger<CsvResultsExporter>>(), _bus, _saveService);

    [Fact]
    public async Task CsvExport_OfFilteredRows_SaysHowManyOfTheFetchedRows()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await CreateCsvExporter().Consume(new ExportResultsToCsv(SampleResult(), TotalRows: 15));

        var notification = _notifications.ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Success);
        notification.Message.ShouldStartWith("Exported 1 of 15 rows to query_results_");
        notification.Message.ShouldEndWith(".csv");
    }

    [Fact]
    public async Task JsonExport_OfFilteredRows_SaysHowManyOfTheFetchedRows()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await CreateJsonExporter().Consume(new ExportResultsToJson(SampleResult(), TotalRows: 15));

        _notifications.ShouldHaveSingleItem().Message.ShouldStartWith("Exported 1 of 15 rows to query_results_");
    }

    [Fact]
    public async Task ExcelExport_OfFilteredRows_SaysHowManyOfTheFetchedRows()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<byte[]>()).Returns(true);

        await CreateExcelExporter().Consume(new ExportResultsToExcel(SampleResult(), TotalRows: 15));

        _notifications.ShouldHaveSingleItem().Message.ShouldStartWith("Exported 1 of 15 rows to query_results_");
    }

    [Fact]
    public async Task CsvExport_OfEveryRow_KeepsTheUsualMessage()
    {
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await CreateCsvExporter().Consume(new ExportResultsToCsv(SampleResult(), TotalRows: 1));

        _notifications.ShouldHaveSingleItem().Message.ShouldStartWith("Exported results to query_results_");
    }

    private static QueryResult RepeatedIds()
    {
        var result = new QueryResult();
        var first = result.AddColumn("id");
        var second = result.AddColumn("id");
        result.Rows.Add(new Dictionary<string, object> { [first] = 1, [second] = 2 });
        return result;
    }

    [Fact]
    public async Task CsvExport_WritesTheColumnNamesAsHeaders_AndEachColumnsOwnValue()
    {
        string? csv = null;
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(call => { csv = call.ArgAt<string>(1); return true; });

        await CreateCsvExporter().Consume(new ExportResultsToCsv(RepeatedIds()));

        csv.ShouldNotBeNull().ReplaceLineEndings("\n").ShouldBe("id,id\n1,2\n");
    }

    [Fact]
    public async Task ExcelExport_WritesTheColumnNamesAsHeaders_AndEachColumnsOwnValue()
    {
        byte[]? data = null;
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<byte[]>()).Returns(call => { data = call.ArgAt<byte[]>(1); return true; });

        await CreateExcelExporter().Consume(new ExportResultsToExcel(RepeatedIds()));

        var sheet = OpenWorksheet(data.ShouldNotBeNull());
        (sheet.Cell(1, 1).GetString(), sheet.Cell(1, 2).GetString()).ShouldBe(("id", "id"));
        (sheet.Cell(2, 1).GetString(), sheet.Cell(2, 2).GetString()).ShouldBe(("1", "2"));
    }

    [Fact]
    public async Task SelectedRowsCsvExport_WritesTheGivenHeaders()
    {
        string? csv = null;
        _saveService.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(call => { csv = call.ArgAt<string>(1); return true; });
        var result = RepeatedIds();

        await CreateSelectedRowsExporter().Consume(new ExportSelectedRows(result.Rows, result.Columns, "Csv", result.ColumnNames));

        csv.ShouldNotBeNull().ReplaceLineEndings("\n").ShouldBe("id,id\n1,2\n");
    }

    [Fact]
    public async Task CopyRowAsCsv_WritesTheGivenHeaders()
    {
        var result = RepeatedIds();
        var handler = new ResultClipboardHandler(_bus);

        await handler.Consume(new CopyRowToClipboard(result.Rows[0], result.Columns, "Csv", result.ColumnNames));

        await _bus.Received(1).PublishAsync(Arg.Is<Aion.Components.Infrastructure.Commands.CopyToClipboard>(c =>
            c.Text.ReplaceLineEndings("\n") == "id,id\n1,2\n"));
    }

    [Fact]
    public async Task CopySelectedRows_WritesTheGivenHeaders()
    {
        var result = RepeatedIds();
        var handler = new ResultClipboardHandler(_bus);

        await handler.Consume(new CopySelectedRowsToClipboard(result.Rows, result.Columns, "Csv", result.ColumnNames));

        await _bus.Received(1).PublishAsync(Arg.Is<Aion.Components.Infrastructure.Commands.CopyToClipboard>(c =>
            c.Text.ReplaceLineEndings("\n") == "id,id\n1,2\n"));
    }
}
