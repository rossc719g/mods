using BepInEx;
using BepInEx.Configuration;
using ClearWaters;
using System.Net;
using System.Text;

static class ConfigurationChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "clearwaters-config-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        Paths.SetExecutablePath(Path.Combine(root, "Fixture.exe"));
        var warnings = new List<string>();
        ModConfiguration Open(string name) => new(new ConfigFile(Path.Combine(root, name, "local.dave.clearwaters.cfg"), false), warnings.Add);
        string ConfigPath(string name) => Path.Combine(root, name, "local.dave.clearwaters.cfg");
        void Write(string name, string cfg, string? legacy = null)
        {
            Directory.CreateDirectory(Path.Combine(root, name));
            File.WriteAllText(ConfigPath(name), cfg);
            if (legacy != null) File.WriteAllText(Path.Combine(root, name, "local.dave.clearwaters.settings.json"), legacy);
        }
        try
        {
            var fresh = Open("fresh");
            var store = new SettingsStore(fresh.Read());
            check(!store.Current.WebEnabled && store.Current.Enabled && store.Current.Removes("chromaticAberration"), "Fresh install works with no web controls");
            using (var disabled = new ControlServer(IPAddress.Loopback, 0, store, root, fresh.Save))
            {
                bool rejected = false;
                try { disabled.Start(); } catch (InvalidOperationException) { rejected = true; }
                check(rejected, "Disabled web service refuses to open a listener");
            }
            store.SaveCurrent(fresh.Save);
            check(Directory.GetFiles(Path.Combine(root, "fresh")).Length == 1 && File.ReadAllText(ConfigPath("fresh")).Contains("[Visuals.RemoveEffects]"), "One standard .cfg contains all preferences");
            check(!Open("fresh").Read().WebEnabled, "Web service stays off on next launch");

            Write("minimal", "[Server]\nEnabled = true\n");
            var minimal = Open("minimal");
            var minimalStore = new SettingsStore(minimal.Read());
            minimalStore.SaveCurrent(minimal.Save);
            check(minimalStore.Current.WebEnabled && minimalStore.Current.Enabled && minimalStore.Current.Effects.All(e => e.Value == Effects.ById[e.Key].DefaultRemove), "Enabling only the server uses every visual default");
            var saved = new ConfigFile(ConfigPath("minimal"), false) { SaveOnConfigSet = false };
            foreach (var effect in Effects.All)
            {
                string key = char.ToUpperInvariant(effect.Id[0]) + effect.Id[1..];
                check(saved.Bind("Visuals.RemoveEffects", key, !effect.DefaultRemove).Value == effect.DefaultRemove, "Defaults are actually written for " + key);
            }
            string fullText = File.ReadAllText(ConfigPath("minimal"));
            check(fullText.IndexOf("[Server]") < fullText.IndexOf("[Visuals]") && fullText.IndexOf("Enabled = true") < fullText.IndexOf("Port = 18780") && fullText.IndexOf("Port = 18780") < fullText.IndexOf("BindAddress = 127.0.0.1"), "Server section and its enable, port and address come first");

            Write("manual", "[Visuals]\nEnabled = false\n[Visuals.RemoveEffects]\nChromaticAberration = false\nBlur = false\n[Server]\nEnabled = true\nBindAddress = 0.0.0.0\nPort = 18801\n[ZExtra]\nKeepMe = yes\n");
            var manual = Open("manual");
            store = new SettingsStore(manual.Read());
            check(!store.Current.Enabled && store.Current.WebEnabled && !store.Current.Effects["chromaticAberration"] && !store.Current.Effects["blur"], "Manual .cfg controls master, effects and server");
            store.Change(Encoding.UTF8.GetBytes("{\"enabled\":true,\"effects\":{\"colorSplit\":false,\"bloom\":true}}"));
            store.SaveCurrent(manual.Save);
            var reopened = Open("manual");
            check(reopened.Read().Enabled && !reopened.Read().Effects["colorSplit"] && reopened.Read().Effects["bloom"], "Browser choices survive .cfg reload");
            check(reopened.BindAddress.Value == "0.0.0.0" && reopened.Port.Value == 18801 && File.ReadAllText(ConfigPath("manual")).Contains("KeepMe = yes"), "Visual autosave preserves server and unrelated entries");
            var before = store.Current;
            File.Move(ConfigPath("manual"), ConfigPath("manual") + ".saved");
            Directory.CreateDirectory(ConfigPath("manual"));
            bool failed = false;
            try { store.DisableWeb(manual.Save); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed = true; }
            check(failed && ReferenceEquals(before, store.Current), "Failed .cfg save leaves web service and visual choices unchanged");
            Directory.Delete(ConfigPath("manual"));
            File.Move(ConfigPath("manual") + ".saved", ConfigPath("manual"));
            store.DisableWeb(manual.Save);
            var stopped = Open("manual").Read();
            check(!stopped.WebEnabled && stopped.Enabled && stopped.Effects["bloom"] && !stopped.Effects["colorSplit"], "Stopping web controls persists off without disabling the filter");
            check(store.PersistedRevision == store.Current.Revision, "Stop acknowledges the revision actually saved");
            bool lateRejected = false;
            try { store.Change(Encoding.UTF8.GetBytes("{\"enabled\":false}"), fromWeb: true); }
            catch (InvalidOperationException) { lateRejected = true; }
            check(lateRejected && store.Current.Enabled, "An in-flight web mutation cannot change settings after shutdown");

            const string oldJson = "{\"enabled\":false,\"effects\":{\"chromaticAberration\":false,\"bloom\":true}}";
            Write("legacy", "[Server]\nBindAddress = 0.0.0.0\nPort = 18780\n", oldJson);
            var legacy = Open("legacy");
            store = new SettingsStore(legacy.Read());
            check(!store.Current.Enabled && !store.Current.Effects["chromaticAberration"] && store.Current.Effects["bloom"] && store.Current.WebEnabled, "Upgrade preserves legacy visual settings and running web controls");
            check(File.Exists(Path.Combine(root, "legacy", "local.dave.clearwaters.settings.json")), "Legacy file retained until .cfg saves successfully");
            store.SaveCurrent(legacy.Save);
            check(!File.Exists(Path.Combine(root, "legacy", "local.dave.clearwaters.settings.json")) && File.Exists(Path.Combine(root, "legacy", "local.dave.clearwaters.settings.json.migrated.bak")), "Successful migration archives legacy JSON");
            store.DisableWeb(legacy.Save);
            check(!Open("legacy").Read().WebEnabled, "Legacy backup cannot re-enable web controls on restart");

            Write("partial", "[Visuals]\nEnabled = true\n[Visuals.RemoveEffects]\nChromaticAberration = true\n[Server]\nEnabled = false\nBindAddress = 0.0.0.0\n", oldJson);
            var partial = Open("partial").Read();
            check(partial.Enabled && partial.Effects["chromaticAberration"] && !partial.WebEnabled && partial.Effects["bloom"], "Explicit .cfg values win; migration fills only missing entries");
            Write("bad-json", "[Server]\nBindAddress = 0.0.0.0\n", "{broken");
            var broken = Open("bad-json");
            new SettingsStore(broken.Read()).SaveCurrent(broken.Save);
            check(!broken.Read().WebEnabled && warnings.Count == 1 && File.Exists(Path.Combine(root, "bad-json", "local.dave.clearwaters.settings.json")), "Invalid legacy JSON is preserved and does not activate the server");
        }
        finally { Directory.Delete(root, true); }
    }
}
