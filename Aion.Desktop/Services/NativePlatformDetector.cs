using Mythetech.Framework.Components.Kbd;

namespace Aion.Desktop.Services;

/// <summary>
/// The desktop app runs natively, so the OS it runs on is the one whose key symbols it shows. Without this
/// the Framework's fallback reports non-macOS and shortcuts read "Ctrl+N" next to a menu that says ⌘N.
/// </summary>
public sealed class NativePlatformDetector : IPlatformDetector
{
    public bool IsMacOS => OperatingSystem.IsMacOS();
}
