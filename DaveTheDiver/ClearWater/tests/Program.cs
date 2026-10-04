using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ClearWaters;
using BepInEx.Configuration;

int checkedCount = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Interlocked.Increment(ref checkedCount); }
byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
var store = new SettingsStore();
Check(store.Current.Removes("chromaticAberration") && store.Current.Removes("refraction"), "Clarity defaults");
Check(!store.Current.Removes("bloom") && !store.Current.Removes("danger"), "Preserve glow and warning by default");
store.Change(Bytes("{\"enabled\":false,\"effects\":{\"depthOfField\":false}}"));
Check(!store.Current.Removes("chromaticAberration") && !store.Current.Effects["depthOfField"], "Master and individual changes independent");
store.Change(Bytes("{\"enabled\":true}"));
Check(store.Current.Removes("chromaticAberration") && !store.Current.Removes("depthOfField"), "Master restores saved individual choices");
foreach (string invalid in new[] { "{}", "null", "{\"enabled\":0}", "{\"effects\":null}", "{\"enabled\":false,\"unknown\":true}", "{\"effects\":{\"typo\":true}}", "{\"enabled\":true,\"enabled\":false}", "{\"effects\":{\"blur\":true,\"blur\":false}}" })
{
    var before = store.Current;
    try { store.Change(Bytes(invalid)); throw new Exception("Invalid settings accepted: " + invalid); }
    catch (ArgumentException) { }
    Check(ReferenceEquals(before, store.Current), "Bad updates are atomic");
}
await Task.WhenAll(Effects.All.Select(effect => Task.Run(() => store.Change(Bytes(JsonSerializer.Serialize(new { effects = new Dictionary<string, bool> { [effect.Id] = false } }))))));
Check(store.Current.Effects.Values.All(v => !v), "Concurrent disjoint changes do not lose each other");
ConfigurationChecks.Run(Check);

float live = 0.3f;
var effectOverride = new TransientOverride<float>(() => live, v => live = v, 0);
Check(effectOverride.Apply(true) && live == 0, "Remove effect");
effectOverride.Restore(); Check(live == 0.3f, "Original value returns for next camera");
effectOverride.Apply(true); live = 0.8f; effectOverride.Restore();
Check(live == 0.8f, "An intervening game update is preserved");
Check(!effectOverride.Apply(false) && live == 0.8f, "Master off does not alter the game value");
effectOverride.Restore(); Check(live == 0.8f, "Restoring unmodified value is inert");
Check(LocalSubnets.InSubnet(new byte[] {192,168,40,3}, new byte[] {192,168,1,2}, new byte[] {255,255,192,0}), "Actual /18 home subnet");
Check(!LocalSubnets.InSubnet(new byte[] {8,8,8,8}, new byte[] {192,168,1,2}, new byte[] {255,255,192,0}), "Public peer excluded");

var root = Directory.GetCurrentDirectory();
var pluginAssembly = System.Reflection.Assembly.LoadFile(Path.Combine(root, "src/ClearWaters/bin/Release/net6.0/ClearWaters.dll"));
Check(!(bool)pluginAssembly.GetType("ClearWaters.ControlServer")!.GetProperty("SupportsWebFileOverrides")!.GetValue(null)!, "Public release DLL compiles out file override support");
Check(!(bool)pluginAssembly.GetType("ClearWaters.ControlServer")!.GetProperty("SupportsRenderDiagnostics")!.GetValue(null)! &&
    pluginAssembly.GetType("ClearWaters.ControlServer")!.GetMethod("PublishDiagnostics") == null && pluginAssembly.GetType("ClearWaters.EffectStatus") == null,
    "Public DLL compiles out statistics types and snapshot publication");
if (ControlServer.SupportsWebFileOverrides) {
    var developmentAssembly = System.Reflection.Assembly.LoadFile(Path.Combine(root, ".deps/dev-plugin/ClearWaters.dll"));
    Check((bool)developmentAssembly.GetType("ClearWaters.ControlServer")!.GetProperty("SupportsWebFileOverrides")!.GetValue(null)!, "Development DLL includes file override support");
    Check(!(bool)developmentAssembly.GetType("ClearWaters.ControlServer")!.GetProperty("SupportsRenderDiagnostics")!.GetValue(null)! && developmentAssembly.GetType("ClearWaters.EffectStatus") == null,
        "Live web overrides do not implicitly enable rendering statistics");
}
if (ControlServer.SupportsRenderDiagnostics) {
    string variant = ControlServer.SupportsWebFileOverrides ? "diagnostic-dev-plugin" : "diagnostic-plugin";
    var diagnosticAssembly = System.Reflection.Assembly.LoadFile(Path.Combine(root, ".deps", variant, "ClearWaters.dll"));
    Check((bool)diagnosticAssembly.GetType("ClearWaters.ControlServer")!.GetProperty("SupportsRenderDiagnostics")!.GetValue(null)! && diagnosticAssembly.GetType("ClearWaters.EffectStatus") != null,
        "Explicit diagnostic build includes rendering statistics");
    Check((bool)diagnosticAssembly.GetType("ClearWaters.ControlServer")!.GetProperty("SupportsWebFileOverrides")!.GetValue(null)! == ControlServer.SupportsWebFileOverrides,
        "Rendering statistics and live web overrides are independent flags");
}
foreach (string filename in new[] { "index.html", "app.js", "style.css" }) {
    using var embedded = pluginAssembly.GetManifestResourceStream("ClearWaters.Web." + filename);
    Check(embedded != null, "Release DLL contains " + filename);
    using var bytes = new MemoryStream();
    embedded!.CopyTo(bytes);
    Check(bytes.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(root, "web", filename))), "Release DLL embeds the exact source bytes of " + filename);
}
var overrides = Path.Combine(Path.GetTempPath(), "clearwaters-web-" + Guid.NewGuid());
var serverStore = new SettingsStore(new SettingsStore().Current with { WebEnabled = true });
bool failSave = false;
SettingsSnapshot? savedSettings = null;
using var server = new ControlServer(IPAddress.Loopback, 0, serverStore, overrides, snapshot => {
    if (failSave) throw new IOException("Simulated read-only config");
    savedSettings = snapshot;
});
server.Start();
int serverPort = server.Port;
#if RENDER_DIAGNOSTICS
server.PublishDiagnostics(Bytes("{\"frame\":42}"));
#endif
async Task<(int Code, string Body)> Request(string request, bool split = false) {
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, server.Port);
    using var stream = client.GetStream();
    var bytes = Bytes(request);
    if (split) { await stream.WriteAsync(bytes.AsMemory(0, bytes.Length - 2)); await Task.Delay(20); await stream.WriteAsync(bytes.AsMemory(bytes.Length - 2)); }
    else await stream.WriteAsync(bytes);
    using var reader = new StreamReader(stream); var answer = await reader.ReadToEndAsync();
    int end = answer.IndexOf("\r\n\r\n", StringComparison.Ordinal);
    return (int.Parse(answer.Split(' ')[1]), answer[(end + 4)..]);
}
string Get(string path, string method = "GET") => $"{method} {path} HTTP/1.1\r\nHost: localhost:{server.Port}\r\n\r\n";
string Patch(string json, string marker = "1", string? origin = null) => $"PATCH /api/settings HTTP/1.1\r\nHost: localhost:{server.Port}\r\nX-ClearWaters: {marker}\r\nContent-Type: application/json\r\nContent-Length: {Bytes(json).Length}\r\n{(origin == null ? "" : "Origin: " + origin + "\r\n")}\r\n{json}";
string Stop(string json = "{}", string marker = "1", string? origin = null) => Patch(json, marker, origin).Replace("PATCH /api/settings", "POST /api/server/stop");
var initialState = JsonDocument.Parse((await Request(Get("/api/state"))).Body).RootElement;
Check(initialState.GetProperty("settings").GetProperty("enabled").GetBoolean() && initialState.GetProperty("effects").GetArrayLength() == Effects.All.Length,
    "Controls read settings directly without a game frame or snapshot publisher");
Check(!initialState.TryGetProperty("hooks", out _) && !initialState.TryGetProperty("scene", out _) && !initialState.TryGetProperty("frame", out _) &&
    !initialState.TryGetProperty("renderedRevision", out _) && initialState.GetProperty("effects").EnumerateArray().All(e => !e.TryGetProperty("status", out _)),
    "Normal control state is independent of rendering diagnostics");
Check(initialState.GetProperty("capabilities").GetProperty("renderDiagnostics").GetBoolean() == ControlServer.SupportsRenderDiagnostics,
    "Controls expose the compiled statistics capability");
#if RENDER_DIAGNOSTICS
Check(initialState.GetProperty("diagnostics").GetProperty("frame").GetInt32() == 42, "Diagnostic builds serve explicitly published snapshots");
#else
Check(!initialState.TryGetProperty("diagnostics", out _), "Normal builds publish no diagnostic snapshots");
#endif
Check((await Request(Get("/", "HEAD"))).Body == "", "HEAD omits body");
Check((await Request(Get("/"))).Body.Contains("Clear Waters"), "Control page is served");
foreach (string filename in new[] { "index.html", "app.js", "style.css" }) {
    var response = await Request(Get("/" + filename));
    Check(response.Code == 200 && response.Body == File.ReadAllText(Path.Combine(root, "web", filename)), "Embedded " + filename + " served without any loose web files");
}
Directory.CreateDirectory(overrides);
try {
    string overrideFile = Path.Combine(overrides, "index.html");
    File.WriteAllText(overrideFile, "A temporary development page");
    if (ControlServer.SupportsWebFileOverrides) {
        Check((await Request(Get("/"))).Body == "A temporary development page", "Optional loose file overrides its embedded copy");
        File.WriteAllText(overrideFile, "An updated development page");
        Check((await Request(Get("/"))).Body == "An updated development page", "Override changes work without restarting the server");
    } else {
        Check((await Request(Get("/"))).Body == File.ReadAllText(Path.Combine(root, "web", "index.html")), "Public build ignores external HTML files");
        File.WriteAllText(Path.Combine(overrides, "app.js"), "broken JavaScript");
        File.WriteAllText(Path.Combine(overrides, "style.css"), "broken CSS");
        foreach (string filename in new[] { "app.js", "style.css" })
            Check((await Request(Get("/" + filename))).Body == File.ReadAllText(Path.Combine(root, "web", filename)), "Public build ignores external " + filename);
    }
    Check((await Request(Get("/app.js"))).Body == File.ReadAllText(Path.Combine(root, "web", "app.js")), "Partial overrides retain embedded files for other assets");
    File.Delete(overrideFile);
    Check((await Request(Get("/"))).Body == File.ReadAllText(Path.Combine(root, "web", "index.html")), "Removing an override restores the embedded page");
} finally { Directory.Delete(overrides, true); }
Check((await Request(Get("/../../settings.json"))).Code == 404, "No arbitrary file serving");
Check((await Request(Get("/api/settings", "POST"))).Code == 405, "Unsupported method rejected");
Check((await Request(Patch("{\"enabled\":false}"))).Code == 200, "Remote master works");
Check(!serverStore.Current.Enabled, "HTTP updates managed state");
var pendingState = JsonDocument.Parse((await Request(Get("/api/state"))).Body).RootElement;
Check(!pendingState.GetProperty("settings").GetProperty("enabled").GetBoolean() && pendingState.GetProperty("persistedRevision").GetInt64() < serverStore.Current.Revision,
    "Control state immediately reflects changes without claiming they are saved");
try { serverStore.SaveCurrent(_ => throw new IOException("Simulated save failure")); } catch (IOException) { }
Check(JsonDocument.Parse((await Request(Get("/api/state"))).Body).RootElement.GetProperty("persistenceError").GetString() == "Simulated save failure",
    "Save failure reaches the browser without rendering diagnostics");
serverStore.SaveCurrent(snapshot => savedSettings = snapshot);
var savedState = JsonDocument.Parse((await Request(Get("/api/state"))).Body).RootElement;
Check(savedState.GetProperty("persistenceError").ValueKind == JsonValueKind.Null && savedState.GetProperty("persistedRevision").GetInt64() == serverStore.Current.Revision,
    "Successful retry clears the error and confirms saved settings");
Check((await Request(Patch("{\"effects\":{\"colorSplit\":false}}"), split: true)).Code == 200, "Body across TCP packets");
Check(!serverStore.Current.Effects["colorSplit"], "Remote individual switch works");
Check((await Request(Patch("{\"enabled\":true}", marker: ""))).Code == 403, "Cross-site form cannot mutate");
Check((await Request(Patch("{\"enabled\":true}", origin: "http://evil.invalid"))).Code == 403, "Foreign origin rejected");
Check((await Request(Get("/api/state").Replace("localhost", "evil.invalid"))).Code == 403, "DNS rebinding host rejected");
Check((await Request(Patch("{\"effects\":{\"blur\":5}}"))).Code == 400, "HTTP invalid switches rejected");
Check((await Request(Patch("{\"enabled\":true}").Replace("Content-Length: 16", "Content-Length: 9000"))).Code == 413, "Oversized request rejected");
await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => Check((await Request(Get("/api/health"))).Code == 200, "Parallel requests")));
Check((await Request(Stop(marker: ""))).Code == 403, "Shutdown requires the API marker");
Check((await Request(Stop(origin: "http://evil.invalid"))).Code == 403, "Foreign page cannot shut down controls");
Check((await Request(Stop("{\"enabled\":false}"))).Code == 400, "Shutdown cannot smuggle visual setting changes");
failSave = true;
Check((await Request(Stop())).Code == 503 && !server.IsStopped && serverStore.Current.WebEnabled, "Failed persistence leaves HTTP service running");
Check((await Request(Get("/api/health"))).Code == 200, "Service remains usable after failed shutdown");
failSave = false;
var beforeStop = serverStore.Current;
var stopResult = await Request(Stop());
Check(stopResult.Code == 200 && JsonDocument.Parse(stopResult.Body).RootElement.GetProperty("stopped").GetBoolean(), "Successful shutdown acknowledges a completed save");
Check(savedSettings is { WebEnabled: false } && savedSettings.Enabled == beforeStop.Enabled && savedSettings.Effects.SequenceEqual(beforeStop.Effects), "Shutdown saves all visual choices and disables web service");
for (int i = 0; i < 20 && !server.IsStopped; i++) await Task.Delay(10);
using (var probe = new TcpClient()) {
    bool closed = false;
    try { await probe.ConnectAsync(IPAddress.Loopback, serverPort); } catch (SocketException) { closed = true; }
    Check(closed, "Listener closes after the shutdown response");
}
Console.WriteLine($"Passed {checkedCount} checks: reversible values, persistence, atomic/concurrent switches, subnet rules, HTTP limits and routes.");

if (args.Contains("--serve")) {
    Directory.CreateDirectory(Path.Combine(root, "test-results"));
    string fixturePath = Path.Combine(root, "test-results", "browser-fixture.cfg");
    File.WriteAllText(fixturePath, "[Server]\nEnabled = true\n");
    var fixtureConfig = new ModConfiguration(new ConfigFile(fixturePath, false), Console.WriteLine);
    var fixtureStore = new SettingsStore(fixtureConfig.Read());
    // Exercise the same embedded-only layout as a one-DLL release.
    using var fixture = new ControlServer(IPAddress.Loopback, 18781, fixtureStore, overrides, fixtureConfig.Save);
    fixture.Start();
#if RENDER_DIAGNOSTICS
    fixture.PublishDiagnostics(JsonSerializer.SerializeToUtf8Bytes(new {
        capturedAtUtc = DateTime.UtcNow, frame = 42, scene = "UI test fixture", renderedRevision = 1,
        hooks = new { camerasProcessed = 42, passesSeen = 504, errors = Array.Empty<string>() },
        effects = Effects.All.ToDictionary(e => e.Id, e => new { volumeFound = true, originalValue = "0.2", effectiveValue = "0", valuesSuppressed = 42, passesSuppressed = 0 })
    }, SettingsStore.JsonOptions));
#endif
    Console.WriteLine("UI fixture: http://localhost:18781");
    while (!fixture.IsStopped) {
        if (fixtureStore.Current.Revision != fixtureStore.PersistedRevision) fixtureStore.SaveCurrent(fixtureConfig.Save);
        await Task.Delay(500);
    }
}
