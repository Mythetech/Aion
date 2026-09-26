using Mythetech.Framework.Components.Kbd;

namespace Aion.Components.Shortcuts;

/// <summary>
/// Every keyboard shortcut Aion defines. The editor actions, the page hotkeys, the command palette's hints
/// and the desktop native menu all read them from here, so they can't disagree about what a key does.
/// </summary>
public sealed class AionKeyBindings
{
    private AionKeyBindings(bool browser, bool isMac)
    {
        IsMac = isMac;

        // A browser keeps Cmd/Ctrl+N, +T and +Shift+N for its own windows and tabs and never hands them to the
        // page, and +S saves the page, so the web app neither claims nor advertises them. Tabs save themselves
        // there anyway.
        if (browser) return;

        NewQuery = new KeyChord("N");
        NewQueryAlternate = new KeyChord("T");
        NewConnection = new KeyChord("N", Shift: true);
        SaveQuery = new KeyChord("S");
        CopyQuery = new KeyChord("C", Shift: true);
    }

    public static AionKeyBindings ForDesktop(bool isMac) => new(browser: false, isMac);

    public static AionKeyBindings ForBrowser(bool isMac) => new(browser: true, isMac);

    /// <summary>The bindings for the host the app is running in: the browser build, or the desktop app.</summary>
    public static AionKeyBindings For(IPlatformDetector platform) =>
        new(OperatingSystem.IsBrowser(), platform.IsMacOS);

    public bool IsMac { get; }

    // The query editor sees its keys before the page or the browser does, so its shortcuts work in both hosts.

    /// <summary>Runs the query, or cancels it while it runs. Only while the editor has focus.</summary>
    public KeyChord RunQuery { get; } = new("Enter");

    public KeyChord CommandPalette { get; } = new("K");

    /// <summary>The palette key other editors use; bound in the query editor only.</summary>
    public KeyChord CommandPaletteAlternate { get; } = new("P", Shift: true);

    public KeyChord ExpandSelectStar { get; } = new("E", Shift: true);

    public KeyChord? NewQuery { get; }

    /// <summary>Kept from before New Query moved to ⌘N; not shown in menus or hints.</summary>
    public KeyChord? NewQueryAlternate { get; }

    public KeyChord? NewConnection { get; }

    public KeyChord? SaveQuery { get; }

    public KeyChord? CopyQuery { get; }

    /// <summary>The shortcut as people read it on this platform, or null when there is none.</summary>
    public string? Describe(KeyChord? chord) => chord?.Display(IsMac);
}
