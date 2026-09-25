using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Test.TestDoubles;
using Aion.Web.Onboarding;
using Aion.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Web.Onboarding;

public class OnboardingDialogTests : TestContext
{
    private readonly InBrowserEngines _engines = new();
    private readonly ConnectionState _connectionState;

    public OnboardingDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var bus = Substitute.For<IMessageBus>();
        _connectionState = new ConnectionState(_engines.ConnectionService, _engines.Factory, bus, Substitute.For<ILogger<ConnectionState>>());
        Services.AddSingleton(new SampleDatabaseProvisioner(
            _engines.Factory,
            _connectionState,
            new QueryState(bus, Substitute.For<IQuerySaveService>()),
            new IndexedDbStorageService(new JsModuleFake().Runtime),
            bus));
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Dialogs, IDialogReference Dialog)> ShowAsync(bool showScratchOption = true)
    {
        var dialogs = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<OnboardingDialog> { { x => x.ShowScratchOption, showScratchOption } };

        IDialogReference? dialog = null;
        await dialogs.InvokeAsync(async () => dialog = await dialogService.ShowAsync<OnboardingDialog>("Welcome", parameters));
        return (dialogs, dialog!);
    }

    private static async Task SelectPostgresAsync(IRenderedComponent<MudDialogProvider> dialogs)
    {
        await dialogs.FindAll("input.mud-radio-input")[1].ClickAsync(new MouseEventArgs());
    }

    [Fact]
    public async Task StartFromScratch_ReturnsTheChosenEngine()
    {
        var (dialogs, dialog) = await ShowAsync();
        await SelectPostgresAsync(dialogs);

        await dialogs.Find("[data-choice=scratch]").ClickAsync(new MouseEventArgs());

        var result = await dialog.Result;
        result.ShouldNotBeNull().Data.ShouldBe(new OnboardingResult(OnboardingChoice.Scratch, DatabaseType.WasmPostgreSQL));
        _connectionState.Connections.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sample_LoadsIntoTheChosenEngine()
    {
        var (dialogs, dialog) = await ShowAsync();
        await SelectPostgresAsync(dialogs);

        await dialogs.Find("[data-choice=sample]").ClickAsync(new MouseEventArgs());

        var result = await dialog.Result;
        result.ShouldNotBeNull().Data.ShouldBe(new OnboardingResult(OnboardingChoice.Sample, DatabaseType.WasmPostgreSQL));
        _connectionState.Connections.ShouldHaveSingleItem().Type.ShouldBe(DatabaseType.WasmPostgreSQL);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public async Task Options_CanBeChosenFromTheKeyboard(string key)
    {
        var (dialogs, dialog) = await ShowAsync();

        var option = dialogs.Find("[data-choice=scratch]");
        option.GetAttribute("role").ShouldBe("button");
        option.GetAttribute("tabindex").ShouldBe("0");
        await option.KeyDownAsync(new KeyboardEventArgs { Key = key });

        var result = await dialog.Result;
        result.ShouldNotBeNull().Data.ShouldBe(new OnboardingResult(OnboardingChoice.Scratch, DatabaseType.WasmSQLite));
    }

    [Fact]
    public async Task Sample_ShowsProgressUntilItIsLoaded()
    {
        var release = new TaskCompletionSource<QueryResult>();
        _engines.Sqlite.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => release.Task);
        var (dialogs, dialog) = await ShowAsync();

        var click = dialogs.Find("[data-choice=sample]").ClickAsync(new MouseEventArgs());

        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("Loading the sample store into SQLite"));
        dialogs.FindAll("[data-choice]").ShouldBeEmpty();
        dialog.Result.IsCompleted.ShouldBeFalse();

        release.SetResult(new QueryResult());
        await click;
        (await dialog.Result).ShouldNotBeNull().Canceled.ShouldBeFalse();
    }

    [Fact]
    public async Task Sample_WhenLoadingFails_ShowsTheErrorAndStaysOpen()
    {
        _engines.Sqlite.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new QueryResult { Error = "disk is full" }));
        var (dialogs, dialog) = await ShowAsync();

        await dialogs.Find("[data-choice=sample]").ClickAsync(new MouseEventArgs());

        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("disk is full"));
        dialogs.FindAll("[data-choice]").ShouldNotBeEmpty();
        dialog.Result.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadSample_OffersOnlyTheSample()
    {
        var (dialogs, _) = await ShowAsync(showScratchOption: false);

        dialogs.FindAll("[data-choice=scratch]").ShouldBeEmpty();
        dialogs.FindAll("[data-choice=sample]").ShouldHaveSingleItem();
    }
}
