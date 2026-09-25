using Aion.Components.AppContextPanel;
using Aion.Components.Shared.Dialogs;
using Aion.Components.Shared.Dialogs.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class AboutShowerTests
{
    [Fact]
    public async Task ShowAbout_OpensTheAboutDialog()
    {
        var bus = Substitute.For<IMessageBus>();
        ShowDialog? published = null;
        bus.PublishAsync(Arg.Do<ShowDialog>(d => published = d)).Returns(Task.CompletedTask);

        await new AboutShower(bus).Consume(new ShowAbout());

        published.ShouldNotBeNull();
        published.Dialog.ShouldBe(typeof(AboutAion));
        published.Options.ShouldBeEquivalentTo(AionDialogs.CreateDefaultOptions());
    }
}
