using System.Diagnostics;
using Aion.Components.Infrastructure;

namespace Aion.Desktop.Services;

public class LinkOpenService : ILinkOpenService
{
    public void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}