# Clear Waters

## Short description

Remove chromatic aberration, blur, color splitting, and other screen effects in
Dave the Diver. Includes 21 individual switches and optional browser controls.

## Full description

Clear Waters lets you choose which screen effects to remove from Dave the Diver,
for a clearer picture with fewer visual distortions. It can remove colored
fringes around the screen edges, double vision, blur, and the color splitting
and refraction used by magic-mirror transitions.

There are **21 individual switches** and one master switch. Use the defaults, or
adjust each effect to suit your preferences. Turning the master off allows the
game's original effects while remembering your choices.

### Effects you can remove

- **Color separation and distortion:** chromatic aberration, ColorSplit, double
  vision, screen refraction, ripples, lens distortion, Panini projection, and
  screen glitch.
- **Blur:** depth of field, motion blur, tilt shift, radial blur, full-screen
  blur, vertical blur, and camera blur.
- **Overlays:** extra pixelation, film grain, vignette, speed lines, the danger
  overlay, and bloom.

**Bloom and the danger overlay are kept by default.** Object lights, particles,
and water materials are not modified. Some switches only matter in scenes that
use those effects. Camera shake, environmental fog, color grading, and
transition fades are outside the mod's scope.

### Installation

Requires the Windows 64-bit game and **BepInEx 6 for Unity IL2CPP, Windows
x64**. The current target is Steam build **25315876** with
[BepInEx 6.0.0-be.788+5b766a3](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip).

1. Close the game. Install the matching BepInEx loader in the folder containing
   `DaveTheDiver.exe` if it is not already installed.
2. Extract the mod ZIP into that folder. The plugin should be at
   `BepInEx/plugins/ClearWaters/ClearWaters.dll`.
3. Start the game. The filter uses its defaults and creates a complete,
   commented configuration file. The first BepInEx startup can take longer.

### Settings and optional browser controls

All settings are in `BepInEx/config/local.dave.clearwaters.cfg`. Edit it with
the game closed. Under `[Visuals]`, `Enabled` controls the master switch. Under
`[Visuals.RemoveEffects]`, **`true` removes an effect; `false` allows it**.
Missing settings use their defaults, and every save writes the complete file.

**Browser controls are disabled by default.** To experiment during play, enable
this setting before starting the game:

```ini
[Server]
Enabled = true
```

You can create a new configuration containing just those two lines. The mod
fills in everything else. Open **<http://localhost:18780>** on the gaming
computer while the game is running.

Keep the master on and use **All switches off** to compare removals one at a
time. **Restore defaults** returns to the default selections. Changes save
automatically. Resume a paused or unfocused game to see them.

**Save and stop web controls** shuts down the service while keeping your chosen
settings active. Reopening it requires enabling the server in the file and
restarting the game.

Access defaults to the gaming computer only. Optional LAN access has no password
and is intended for trusted networks. Clear Waters makes no outgoing telemetry
or update requests.

### Removal and source

To uninstall, close the game and remove `BepInEx/plugins/ClearWaters`. BepInEx
can remain for other mods.

[Source code and full instructions](https://github.com/rossc719g/mods/tree/main/DaveTheDiver/ClearWater)
are available on GitHub. The mod's code, browser interface, and promotional
artwork were created with AI. The comparison images use actual gameplay
screenshots with unaltered detail crops.
