using Photino.Blazor;

namespace Aion.Desktop;

public interface IPhotinoAppProvider
{
    public PhotinoBlazorApp Instance { get; }
}

public class PhotinoAppProvider : IPhotinoAppProvider
    
{
    public PhotinoBlazorApp Instance { get; set; }
}