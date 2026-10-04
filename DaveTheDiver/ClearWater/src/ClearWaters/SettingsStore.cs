using System.Text.Json;

namespace ClearWaters;

// Detached managed state: HTTP workers never call Unity or IL2CPP.
public sealed record SettingsSnapshot(bool Enabled, IReadOnlyDictionary<string, bool> Effects, long Revision, bool WebEnabled = false)
{
    public bool Removes(string id) => Enabled && Effects.TryGetValue(id, out var remove) && remove;
}

public sealed class SettingsStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly object gate = new();
    private SettingsSnapshot current;
    private long persistedRevision;
    private string? persistenceError;
    public SettingsStore(SettingsSnapshot? initial = null) => current = initial ?? new(true, Effects.All.ToDictionary(e => e.Id, e => e.DefaultRemove), 1);
    public SettingsSnapshot Current => Volatile.Read(ref current);
    public long PersistedRevision => Interlocked.Read(ref persistedRevision);

    public (SettingsSnapshot Settings, long PersistedRevision, string? PersistenceError) ReadForControls()
    {
        lock (gate) return (current, persistedRevision, persistenceError);
    }

    public SettingsSnapshot Change(ReadOnlyMemory<byte> json, bool fromWeb = false)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("Settings must be an object.");
        lock (gate)
        {
            if (fromWeb && !current.WebEnabled) throw new InvalidOperationException("Web controls are stopped.");
            bool enabled = current.Enabled;
            var effects = new Dictionary<string, bool>(current.Effects, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new ArgumentException("Duplicate setting.");
                if (property.Name == "enabled") enabled = Boolean(property.Value);
                else if (property.Name == "effects")
                {
                    if (property.Value.ValueKind != JsonValueKind.Object) throw new ArgumentException("effects must be an object.");
                    var effectNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var effect in property.Value.EnumerateObject())
                    {
                        if (!Effects.ById.ContainsKey(effect.Name) || !effectNames.Add(effect.Name))
                            throw new ArgumentException("Unknown or duplicate effect: " + effect.Name);
                        effects[effect.Name] = Boolean(effect.Value);
                    }
                }
                else throw new ArgumentException("Unknown setting: " + property.Name);
            }
            if (seen.Count == 0) throw new ArgumentException("No settings supplied.");
            var next = new SettingsSnapshot(enabled, effects, current.Revision + 1, current.WebEnabled);
            Volatile.Write(ref current, next);
            return next;
        }
    }

    private static bool Boolean(JsonElement value) => value.ValueKind switch {
        JsonValueKind.True => true, JsonValueKind.False => false,
        _ => throw new ArgumentException("Switches must be true or false.")
    };

    public void SaveCurrent(Action<SettingsSnapshot> save)
    {
        lock (gate)
        {
            Save(current, save);
        }
    }

    // Save under the same lock as all updates. A failed save keeps the server on;
    // a successful stop includes every previously accepted visual change.
    public SettingsSnapshot DisableWeb(Action<SettingsSnapshot> save)
    {
        lock (gate)
        {
            var next = current with { WebEnabled = false, Revision = current.Revision + 1 };
            Save(next, save);
            Volatile.Write(ref current, next);
            return next;
        }
    }

    // Called under gate. Save failures stay visible to the controls until a
    // successful retry, without any rendering diagnostics or Unity calls.
    private void Save(SettingsSnapshot snapshot, Action<SettingsSnapshot> save)
    {
        try
        {
            save(snapshot);
            Interlocked.Exchange(ref persistedRevision, snapshot.Revision);
            persistenceError = null;
        }
        catch (Exception e) { persistenceError = e.Message; throw; }
    }
}
