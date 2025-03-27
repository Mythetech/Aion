using Aion.Components.Connections;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Core.Database;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Interop;
using MudBlazor.Services;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryEditorTests : TestContext
{
    private QueryState _state;
    private ConnectionState _connections;
    private IMessageBus _bus;
    
    public QueryEditorTests()
    {
        Services.AddLogging();
        Services.AddMudServices(x =>
        {
            x.PopoverOptions.CheckForPopoverProvider = false;
        });
        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
        JSInterop.SetupVoid("mudPopover.connect", _ => true);
        JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        JSInterop.Setup<BoundingClientRect[]>("mudResizeObserver.connect", _ => true);
        JSInterop.SetupVoid("blazorMonaco.editor.setWasm", false);
        
        _bus = new InMemoryMessageBus(this.Services, new NullLogger<InMemoryMessageBus>());
        _state = new(_bus, NSubstitute.Substitute.For<IQuerySaveService>());
        _connections = new ConnectionState(Substitute.For<IConnectionService>(), Substitute.For<IDatabaseProviderFactory>(), _bus, new NullLogger<ConnectionState>());
        Services.AddSingleton(_bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(_connections);
    }

    [Fact]
    public void Can_Render_QueryEditor()
    {
        // Arrange & Act
        var cut = RenderComponent<QueryEditor>();
        
        // Assert
        cut.ShouldNotBeNull();
    }

    [Fact]
    public void Can_Render_WithNullActiveQuery()
    {
        // Arrange
        _state.SetActive(null!);
        
        // Act
        var cut = RenderComponent<QueryEditor>();
        
        // Assert
        cut.ShouldNotBeNull();
        
    }
}