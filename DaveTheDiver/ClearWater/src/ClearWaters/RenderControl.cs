extern alias Game;
extern alias UnityCore;
using KawaseBlur = Game::KawaseBlur;
using VerticalBlur = Game::VerticalBlur;
using Material = UnityCore::UnityEngine.Material;
using Time = UnityCore::UnityEngine.Time;
#if RENDER_DIAGNOSTICS
using Camera = UnityCore::UnityEngine.Camera;
#endif
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClearWaters;

#if RENDER_DIAGNOSTICS
public sealed class EffectStatus
{
    public bool VolumeFound { get; set; }
    public long ValuesSuppressed { get; set; }
    public long PassesSuppressed { get; set; }
    public int LastObservedFrame { get; set; }
    public string? LastCamera { get; set; }
    public string? OriginalValue { get; set; }
    public string? EffectiveValue { get; set; }
    public string? Error { get; set; }
}
#endif

public sealed class RenderControl : IDisposable
{
    private readonly SettingsStore settings;
    private readonly ManualLogSource log;
    private readonly Harmony harmony = new("local.dave.clearwaters");
    private readonly Dictionary<IntPtr, StackBindings> stacks = new();
    private readonly Dictionary<IntPtr, string?> passTypes = new();
    private StackBindings? lastStack;
    private double nextErrorLog;
    public static RenderControl? Instance { get; private set; }
#if RENDER_DIAGNOSTICS
    private string cameraName = "unknown";
    public readonly Dictionary<string, EffectStatus> Status = Effects.All.ToDictionary(e => e.Id, _ => new EffectStatus());
    public readonly List<string> HookErrors = new();
    public bool VolumeHook { get; private set; }
    public bool PassHook { get; private set; }
    public bool ChromaticHook { get; private set; }
    public long CamerasProcessed { get; private set; }
    public long PassesSeen { get; private set; }
    public int LastRenderFrame { get; private set; }
    public long RenderedRevision { get; private set; }
    public string? LastError { get; private set; }
#endif

    // Only named screen passes: never lights, particles, water, or object shaders.
    private static readonly Dictionary<string, string> PassEffects = new() {
        ["SCPE.ColorSplitRenderer+ColorSplitRenderPass"] = "colorSplit",
        ["SCPE.DoubleVisionRenderer+DoubleVisionRenderPass"] = "doubleVision",
        ["SCPE.RefractionRenderer+RefractionRenderPass"] = "refraction",
        ["SCPE.RipplesRenderer+RipplesRenderPass"] = "ripples",
        ["SCPE.TiltShiftRenderer+TiltShiftRenderPass"] = "tiltShift",
        ["SCPE.RadialBlurRenderer+RadialBlurRenderPass"] = "radialBlur",
        ["SCPE.BlurRenderer+BlurRenderPass"] = "blur",
        ["SCPE.PixelizeRenderer+PixelizeRenderPass"] = "pixelize",
        ["SCPE.SpeedLinesRenderer+SpeedLinesRenderPass"] = "speedLines",
        ["SCPE.DangerRenderer+DangerRenderPass"] = "danger",
        ["DR.Rendering.RadialBlurRenderPass"] = "radialBlur",
        ["DR.Rendering.RipplesRenderPass"] = "ripples",
        ["VerticalBlurRenderer+CustomRenderPass"] = "verticalBlur",
        ["KawaseBlur+CustomRenderPass"] = "kawaseBlur",
        ["GlitchRenderPass"] = "glitch",
    };

    private interface IBinding { void Apply(SettingsSnapshot configuration); void Restore(); }
    private sealed class Binding<T> : IBinding where T : IEquatable<T>
    {
        private readonly string id;
        private readonly TransientOverride<T> value;
#if RENDER_DIAGNOSTICS
        private readonly RenderControl owner;
        private readonly EffectStatus status;
        private readonly T neutral;
#endif
        public Binding(RenderControl owner, string id, Func<T> read, Action<T> write, T neutral)
        {
            this.id = id; value = new(read, write, neutral);
#if RENDER_DIAGNOSTICS
            this.owner = owner; status = owner.Status[id]; this.neutral = neutral;
#endif
        }
        public void Apply(SettingsSnapshot configuration)
        {
#if RENDER_DIAGNOSTICS
            if (value.Apply(configuration.Removes(id))) status.ValuesSuppressed++;
            status.LastObservedFrame = Time.frameCount;
            status.LastCamera = owner.cameraName;
            status.OriginalValue = value.Original.ToString();
            status.EffectiveValue = (configuration.Removes(id) ? neutral : value.Original).ToString();
#else
            value.Apply(configuration.Removes(id));
#endif
        }
        public void Restore() => value.Restore();
    }
    private sealed class StackBindings
    {
        public readonly VolumeStack Stack;
        public readonly List<IBinding> Bindings = new();
        public StackBindings(VolumeStack stack) => Stack = stack;
    }

    public RenderControl(SettingsStore settings, ManualLogSource log) { this.settings = settings; this.log = log; Instance = this; }
    public void Install()
    {
#if RENDER_DIAGNOSTICS
        VolumeHook =
#endif
        Patch(typeof(UniversalRenderPipeline), "UpdateVolumeFramework", nameof(BeforeVolume), nameof(AfterVolume));
#if RENDER_DIAGNOSTICS
        PassHook =
#endif
        Patch(typeof(ScriptableRenderer), "EnqueuePass", nameof(BeforeEnqueue), null);
        // Separate protection for the most troublesome URP effect, also covering
        // camera paths that don't update volumes every frame.
#if RENDER_DIAGNOSTICS
        ChromaticHook =
#endif
        Patch(typeof(PostProcessPass), "SetupChromaticAberration", null, nameof(AfterChromatic));
    }
    private bool Patch(Type type, string method, string? prefix, string? postfix)
    {
        try
        {
            var original = AccessTools.DeclaredMethod(type, method) ?? throw new MissingMethodException(type.FullName, method);
            harmony.Patch(original,
                prefix == null ? null : new HarmonyMethod(typeof(RenderControl), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(RenderControl), postfix));
            log.LogInfo("Hook installed: " + type.FullName + "." + method);
            return true;
        }
        catch (Exception e)
        {
#if RENDER_DIAGNOSTICS
            HookErrors.Add(type.Name + "." + method + ": " + e.Message);
#endif
            log.LogError("Could not hook " + type.FullName + "." + method + ": " + e);
            return false;
        }
    }

    private static void BeforeVolume() => Instance?.Restore();
#if RENDER_DIAGNOSTICS
    private static void AfterVolume(Camera __0)
    {
        var self = Instance;
        if (self == null) return;
        try { self.cameraName = __0 ? __0.name : "unknown"; }
        catch (Exception e) { self.Error(e); }
        self.ApplyVolume();
    }
#else
    private static void AfterVolume() => Instance?.ApplyVolume();
#endif
    private static bool BeforeEnqueue(ScriptableRenderPass __0) => Instance?.AllowPass(__0) ?? true;
    private static void AfterChromatic(Material __0)
    {
        var self = Instance;
        if (self == null || !self.settings.Current.Removes("chromaticAberration")) return;
        try { __0.DisableKeyword("_CHROMATIC_ABERRATION"); __0.SetFloat("_Chroma_Params", 0f); }
        catch (Exception e) { self.Error(e); }
    }

    public void Restore()
    {
        var old = lastStack;
        lastStack = null;
        if (old == null) return;
        try
        {
            if (!old.Stack.isValid) return;
            foreach (var binding in old.Bindings) binding.Restore();
        }
        catch (Exception e) { Error(e); }
    }

    private void ApplyVolume()
    {
        try
        {
            var stack = VolumeManager.instance.stack;
            if (stack == null || !stack.isValid) return;
            if (!stacks.TryGetValue(stack.Pointer, out var bindings))
            {
                bindings = BuildBindings(stack);
                stacks[stack.Pointer] = bindings;
                foreach (var stale in stacks.Where(pair => !pair.Value.Stack.isValid).Select(pair => pair.Key).ToArray()) stacks.Remove(stale);
            }
            var configuration = settings.Current;
            lastStack = bindings;
            foreach (var binding in bindings.Bindings) binding.Apply(configuration);
#if RENDER_DIAGNOSTICS
            CamerasProcessed++;
            LastRenderFrame = Time.frameCount;
            RenderedRevision = configuration.Revision;
#endif
        }
        catch (Exception e) { Error(e); Restore(); }
    }

    private StackBindings BuildBindings(VolumeStack stack)
    {
        var result = new StackBindings(stack);
        void Float<T>(string id, Func<T, VolumeParameter<float>> select) where T : VolumeComponent
        {
            try
            {
                var component = stack.GetComponent<T>();
                if (component == null) return;
                var parameter = select(component);
                if (parameter == null) return;
                result.Bindings.Add(new Binding<float>(this, id, () => parameter.value, v => parameter.value = v, 0));
#if RENDER_DIAGNOSTICS
                Status[id].VolumeFound = true;
#endif
            }
            catch (Exception e)
            {
#if RENDER_DIAGNOSTICS
                Status[id].Error = e.Message;
#endif
                Error(e);
            }
        }
        Float<ChromaticAberration>("chromaticAberration", c => c.intensity);
        Float<MotionBlur>("motionBlur", c => c.intensity);
        Float<LensDistortion>("lensDistortion", c => c.intensity);
        Float<PaniniProjection>("paniniProjection", c => c.distance);
        Float<FilmGrain>("filmGrain", c => c.intensity);
        Float<Vignette>("vignette", c => c.intensity);
        Float<Bloom>("bloom", c => c.intensity);
        Float<SCPE.ColorSplit>("colorSplit", c => c.offset);
        Float<SCPE.DoubleVision>("doubleVision", c => c.intensity);
        Float<SCPE.Refraction>("refraction", c => c.amount);
        Float<SCPE.Ripples>("ripples", c => c.strength);
        Float<SCPE.TiltShift>("tiltShift", c => c.amount);
        Float<SCPE.RadialBlur>("radialBlur", c => c.amount);
        Float<SCPE.Blur>("blur", c => c.amount);
        Float<SCPE.Pixelize>("pixelize", c => c.amount);
        Float<SCPE.SpeedLines>("speedLines", c => c.intensity);
        Float<SCPE.Danger>("danger", c => c.intensity);
        Float<VerticalBlur>("verticalBlur", c => c.amount);
        try
        {
            var dof = stack.GetComponent<DepthOfField>();
            if (dof != null)
            {
                result.Bindings.Add(new Binding<int>(this, "depthOfField",
                    () => (int)dof.mode.value, value => dof.mode.value = (DepthOfFieldMode)value, 0));
#if RENDER_DIAGNOSTICS
                Status["depthOfField"].VolumeFound = true;
#endif
            }
        }
        catch (Exception e)
        {
#if RENDER_DIAGNOSTICS
            Status["depthOfField"].Error = e.Message;
#endif
            Error(e);
        }
        return result;
    }

    private bool AllowPass(ScriptableRenderPass pass)
    {
        try
        {
#if RENDER_DIAGNOSTICS
            PassesSeen++;
#endif
            // Cache by native CLASS, not object address (objects can be recycled).
            var type = pass.GetIl2CppType();
            if (!passTypes.TryGetValue(type.Pointer, out var id))
            {
                PassEffects.TryGetValue(type.FullName.Replace('/', '+'), out id);
                passTypes[type.Pointer] = id;
            }
            if (id == null || !settings.Current.Removes(id)) return true;
            // A texture-producing blur may feed a particular UI object. Removing
            // it would leave that object's texture stale. Only block direct screen blur.
            if (id == "kawaseBlur" && !pass.Cast<KawaseBlur.CustomRenderPass>().copyToFramebuffer) return true;
#if RENDER_DIAGNOSTICS
            Status[id].PassesSuppressed++;
            Status[id].LastObservedFrame = Time.frameCount;
#endif
            return false;
        }
        catch (Exception e) { Error(e); return true; }
    }

    private void Error(Exception error)
    {
#if RENDER_DIAGNOSTICS
        LastError = error.Message;
#endif
        if (Time.realtimeSinceStartupAsDouble < nextErrorLog) return;
        nextErrorLog = Time.realtimeSinceStartupAsDouble + 10;
        log.LogError("Effect control: " + error);
    }
    public void Dispose() { Restore(); harmony.UnpatchSelf(); Instance = null; }
}
