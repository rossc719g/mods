using BepInEx.Configuration;
using System.Text.Json;

namespace ClearWaters;

// BepInEx owns the .cfg format and its explanatory comments. Only SettingsStore
// calls Save, serializing main-thread autosaves with HTTP shutdown requests.
public sealed class ModConfiguration
{
    private readonly ConfigFile file;
    private readonly ConfigEntry<bool> enabled;
    private readonly ConfigEntry<bool> webEnabled;
    private readonly Dictionary<string, ConfigEntry<bool>> effects = new();
    private readonly Action<string> warn;
    private string? legacyToArchive;
    public ConfigEntry<string> BindAddress { get; }
    public ConfigEntry<int> Port { get; }

    public ModConfiguration(ConfigFile file, Action<string> warn)
    {
        this.file = file;
        this.warn = warn;
        file.SaveOnConfigSet = false;
        var existing = ExistingKeys(file.ConfigFilePath);
        string legacyPath = Path.Combine(Path.GetDirectoryName(file.ConfigFilePath)!, "local.dave.clearwaters.settings.json");
        SettingsSnapshot? legacy = null;
        if (File.Exists(legacyPath))
        {
            try
            {
                var imported = new SettingsStore();
                legacy = imported.Change(File.ReadAllBytes(legacyPath));
                legacyToArchive = legacyPath;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { warn("Could not migrate legacy visual settings; keeping the JSON file: " + e.Message); }
        }

        // ConfigFile orders sections alphabetically. These names keep Server
        // first, and binding order keeps Enabled, Port, BindAddress at its top.
        webEnabled = file.Bind("Server", "Enabled", false, "Enable the optional browser controls. Default: off. Set true and restart to open them; the web page can save and stop them during play.");
        Port = file.Bind("Server", "Port", 18780, new ConfigDescription("Browser control port. Restart after changes.", new AcceptableValueRange<int>(1, 65535)));
        BindAddress = file.Bind("Server", "BindAddress", "127.0.0.1", "127.0.0.1 allows this computer only. Use 0.0.0.0 for home-network access. Restart after file changes.");
        enabled = file.Bind("Visuals", "Enabled", true, "Enable the clarity filter. False restores the game's effects while keeping individual choices.");
        foreach (var effect in Effects.All)
        {
            string key = char.ToUpperInvariant(effect.Id[0]) + effect.Id[1..];
            var entry = file.Bind("Visuals.RemoveEffects", key, effect.DefaultRemove, "True removes this effect; false allows it. " + effect.Description);
            if (legacy != null && !existing.Contains(new("Visuals.RemoveEffects", key))) entry.Value = legacy.Effects[effect.Id];
            effects.Add(effect.Id, entry);
        }
        if (legacy != null)
        {
            if (!existing.Contains(new("Visuals", "Enabled"))) enabled.Value = legacy.Enabled;
            // An existing v0.1 installation always ran the web server. Preserve
            // that choice on upgrade; fresh installations still default to off.
            if (!existing.Contains(new("Server", "Enabled")) &&
                (existing.Contains(new("Server", "BindAddress")) || existing.Contains(new("Server", "Port")))) webEnabled.Value = true;
        }
    }

    public SettingsSnapshot Read() => new(enabled.Value, effects.ToDictionary(e => e.Key, e => e.Value.Value), 1, webEnabled.Value);

    public void Save(SettingsSnapshot snapshot)
    {
        enabled.Value = snapshot.Enabled;
        webEnabled.Value = snapshot.WebEnabled;
        foreach (var entry in effects) entry.Value.Value = snapshot.Effects[entry.Key];
        file.Save(); // Throws on failure so the caller can keep the controls running.
        if (legacyToArchive == null) return;
        try
        {
            string backup = legacyToArchive + ".migrated.bak";
            if (File.Exists(backup)) backup += "." + Guid.NewGuid().ToString("N");
            File.Move(legacyToArchive, backup);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { warn("The .cfg is saved, but the old JSON could not be archived: " + e.Message); }
        legacyToArchive = null;
    }

    private static HashSet<ConfigDefinition> ExistingKeys(string path)
    {
        var keys = new HashSet<ConfigDefinition>();
        if (!File.Exists(path)) return keys;
        string section = "";
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; }
            int equals = line.IndexOf('=');
            if (equals > 0) keys.Add(new(section, line[..equals].Trim()));
        }
        return keys;
    }
}
