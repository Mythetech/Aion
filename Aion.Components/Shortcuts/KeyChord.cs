using BlazorMonaco;
using MudBlazor.Utilities;

namespace Aion.Components.Shortcuts;

/// <summary>
/// A shortcut pressed with the platform's command key, ⌘ on macOS and Ctrl elsewhere, and optionally Shift.
/// </summary>
/// <param name="Key">A letter, or a key name such as "Enter".</param>
public sealed record KeyChord(string Key, bool Shift = false)
{
    /// <summary>The accelerator for the desktop native menu, which shows and matches Ctrl as ⌘ on macOS.</summary>
    public string Accelerator => Shift ? $"Ctrl+Shift+{Key}" : $"Ctrl+{Key}";

    /// <summary>The keybinding for a Monaco editor action; Monaco's CtrlCmd is ⌘ on macOS and Ctrl elsewhere.</summary>
    public int EditorKeybinding =>
        (int)KeyMod.CtrlCmd | (Shift ? (int)KeyMod.Shift : 0) | (int)Enum.Parse<KeyCode>(KeyName);

    public JsKey HotkeyKey => Enum.Parse<JsKey>(KeyName);

    /// <summary>
    /// The modifier sets to register a page hotkey under. MudHotkey matches modifiers exactly and doesn't know
    /// the platform, so the chord is registered for both Ctrl and ⌘, as Aion's page hotkeys always were.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<JsKeyModifier>> HotkeyModifiers => Shift ? WithShift : Plain;

    // Shared instances: MudHotkey re-registers with the browser whenever its modifier list is a new object.
    private static readonly IReadOnlyList<IReadOnlyList<JsKeyModifier>> Plain =
        [[JsKeyModifier.ControlLeft], [JsKeyModifier.MetaLeft]];

    private static readonly IReadOnlyList<IReadOnlyList<JsKeyModifier>> WithShift =
        [[JsKeyModifier.ControlLeft, JsKeyModifier.ShiftLeft], [JsKeyModifier.MetaLeft, JsKeyModifier.ShiftLeft]];

    /// <summary>How the shortcut is written for people: "⇧⌘N" on macOS, "Ctrl+Shift+N" elsewhere.</summary>
    public string Display(bool mac) => mac
        ? $"{(Shift ? "⇧" : "")}⌘{(Key == "Enter" ? "↵" : Key)}"
        : $"Ctrl+{(Shift ? "Shift+" : "")}{Key}";

    private string KeyName => Key.Length == 1 ? $"Key{Key}" : Key;
}
