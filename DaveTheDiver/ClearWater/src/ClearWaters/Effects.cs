namespace ClearWaters;

public sealed record EffectDefinition(string Id, string Label, string Description, string Group, bool DefaultRemove = true);

public static class Effects
{
    public static readonly EffectDefinition[] All = {
        new("chromaticAberration", "Chromatic aberration", "Removes colored fringes, especially near the edges.", "Color and distortion"),
        new("colorSplit", "ColorSplit", "Stops separated color channels, including the magic mirror flare.", "Color and distortion"),
        new("doubleVision", "Double vision", "Removes offset copies of the scene and edge ghosting.", "Color and distortion"),
        new("refraction", "Screen refraction", "Stops the full-screen glass-like warp used by magic mirrors.", "Color and distortion"),
        new("ripples", "Screen ripples", "Stops full-screen waves and portal ripples.", "Color and distortion"),
        new("lensDistortion", "Lens distortion", "Removes camera-lens warping.", "Color and distortion"),
        new("paniniProjection", "Panini projection", "Removes this additional wide-angle screen distortion.", "Color and distortion"),
        new("glitch", "Screen glitch", "Stops the game's full-screen glitch pass.", "Color and distortion"),
        new("depthOfField", "Depth of field", "Keeps near and distant parts of the image in focus.", "Blur"),
        new("motionBlur", "Motion blur", "Removes blur added to camera or object movement.", "Blur"),
        new("tiltShift", "Tilt shift / edge blur", "Removes the soft-focus band or ring around the scene.", "Blur"),
        new("radialBlur", "Radial blur", "Removes streaking and blur radiating toward screen edges.", "Blur"),
        new("blur", "Full-screen blur", "Removes the SCPE blur effect.", "Blur"),
        new("verticalBlur", "Vertical blur", "Removes the game's zone-based vertical blur.", "Blur"),
        new("kawaseBlur", "Camera blur (Kawase)", "Removes Kawase blur applied directly to the screen. Keeps textures used by individual UI objects.", "Blur"),
        new("pixelize", "Extra pixelation", "Stops post-processing that reduces the scene's resolution; keeps the game's pixel art.", "Overlays"),
        new("filmGrain", "Film grain", "Removes added noise over the image.", "Overlays"),
        new("vignette", "Vignette", "Removes the darkened border around the screen.", "Overlays"),
        new("speedLines", "Speed lines", "Removes the full-screen streak overlay.", "Overlays"),
        new("danger", "Danger overlay", "Removes the red screen overlay. Other danger indicators remain.", "Overlays", false),
        new("bloom", "Bloom", "Removes glow spreading from bright objects. Leave off to keep their glow.", "Overlays", false),
    };
    public static readonly IReadOnlyDictionary<string, EffectDefinition> ById = All.ToDictionary(e => e.Id, StringComparer.Ordinal);
}
