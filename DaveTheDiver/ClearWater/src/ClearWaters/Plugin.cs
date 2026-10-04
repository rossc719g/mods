extern alias UnityCore;
using MonoBehaviour = UnityCore::UnityEngine.MonoBehaviour;
using Time = UnityCore::UnityEngine.Time;
#if RENDER_DIAGNOSTICS
using SceneManager = UnityCore::UnityEngine.SceneManagement.SceneManager;
using System.Text.Json;
#endif
using System.Net;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace ClearWaters;

[BepInPlugin("local.dave.clearwaters", "Clear Waters", "0.2.0")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin? Instance;
    private SettingsStore settings = new();
    private ModConfiguration? configuration;
    private RenderControl? render;
    private ControlServer? server;
    private double nextSaveAttempt;
#if RENDER_DIAGNOSTICS
    private double nextSnapshot;
#endif

    public override void Load()
    {
        Instance = this;
        configuration = new ModConfiguration(Config, message => Log.LogWarning(message));
        settings = new SettingsStore(configuration.Read());
        try { settings.SaveCurrent(configuration.Save); }
        catch (Exception e) { Log.LogError("Could not save preferences: " + e.Message); }
        render = new RenderControl(settings, Log);
        render.Install();
        AddComponent<ControlLoop>();
        if (!settings.Current.WebEnabled)
        {
            Log.LogInfo("Clarity filter loaded. Web controls are disabled in local.dave.clearwaters.cfg.");
            return;
        }
        try
        {
            server = new ControlServer(IPAddress.Parse(configuration.BindAddress.Value), configuration.Port.Value, settings,
                Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "web"), configuration.Save);
            server.Start();
            var host = IPAddress.IsLoopback(IPAddress.Parse(configuration.BindAddress.Value)) ? "localhost" : Environment.MachineName;
            Log.LogInfo($"Clear Waters controls: http://{host}:{server.Port}");
        }
        catch (Exception e) { Log.LogError("The visual mod is loaded, but the web controls failed to start: " + e); }
    }

    internal void Tick()
    {
        if (settings.Current.Revision != settings.PersistedRevision)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextSaveAttempt)
            {
                try { settings.SaveCurrent(configuration!.Save); }
                catch (Exception e) { nextSaveAttempt = now + 5; Log.LogError("Saving switches: " + e.Message); }
            }
        }
#if RENDER_DIAGNOSTICS
        PublishDiagnostics();
#endif
    }

#if RENDER_DIAGNOSTICS
    private void PublishDiagnostics()
    {
        if (server == null || server.IsStopped || render == null || Time.realtimeSinceStartupAsDouble < nextSnapshot) return;
        try
        {
            nextSnapshot = Time.realtimeSinceStartupAsDouble + 0.5;
            var snapshot = new {
                capturedAtUtc = DateTime.UtcNow, frame = Time.frameCount,
                scene = SceneManager.GetActiveScene().name,
                renderedRevision = render.RenderedRevision,
                hooks = new { volume = render.VolumeHook, renderPass = render.PassHook, chromatic = render.ChromaticHook,
                    camerasProcessed = render.CamerasProcessed, passesSeen = render.PassesSeen, lastRenderFrame = render.LastRenderFrame,
                    lastError = render.LastError, errors = render.HookErrors.ToArray() },
                effects = render.Status
            };
            server.PublishDiagnostics(JsonSerializer.SerializeToUtf8Bytes(snapshot, SettingsStore.JsonOptions));
        }
        catch (Exception e) { Log.LogError("Rendering diagnostics: " + e); nextSnapshot = Time.realtimeSinceStartupAsDouble + 5; }
    }
#endif

    public override bool Unload()
    {
        server?.Dispose();
        if (configuration != null && settings.Current.Revision != settings.PersistedRevision)
        {
            try { settings.SaveCurrent(configuration.Save); }
            catch (Exception e) { Log.LogError("Saving switches on exit: " + e.Message); }
        }
        render?.Dispose();
        Instance = null;
        return true;
    }
}

public sealed class ControlLoop : MonoBehaviour
{
    public ControlLoop(IntPtr pointer) : base(pointer) { }
    public void Update() => Plugin.Instance?.Tick();
    public void OnApplicationQuit() => Plugin.Instance?.Unload();
}
