using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings;

/// <summary>
/// Remembers the last saved values of each settings section, so an edit saves only the sections it touched.
/// The settings editors change the live settings objects and report only that something changed.
/// </summary>
public class SettingsChangeTracker
{
    private readonly Dictionary<string, Dictionary<string, object?>> _saved = new();

    public SettingsChangeTracker(IEnumerable<SettingsBase> settings)
    {
        foreach (var section in settings)
        {
            _saved[section.SettingsId] = section.CreateSnapshot();
        }
    }

    /// <summary>
    /// Returns the sections whose values differ from when they were last taken, and records their current values.
    /// </summary>
    public IReadOnlyList<SettingsBase> TakeChanged(IEnumerable<SettingsBase> settings)
    {
        var changed = new List<SettingsBase>();

        foreach (var section in settings)
        {
            var current = section.CreateSnapshot();

            if (_saved.TryGetValue(section.SettingsId, out var saved) && HaveSameValues(saved, current))
                continue;

            _saved[section.SettingsId] = current;
            changed.Add(section);
        }

        return changed;
    }

    private static bool HaveSameValues(Dictionary<string, object?> saved, Dictionary<string, object?> current)
        => saved.Count == current.Count
           && saved.All(entry => current.TryGetValue(entry.Key, out var value) && Equals(entry.Value, value));
}
