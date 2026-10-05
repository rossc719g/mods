# Clear Waters

A Dave the Diver mod that removes selected screen effects for a clearer view. It
includes a master switch and 21 individual controls for chromatic aberration,
color separation, blur, distortion, pixelation, and overlays.

The mod works immediately with its defaults. Browser controls are optional and
**disabled by default** on new installations. All preferences live in one
commented configuration file.

[Download on Nexus Mods](https://www.nexusmods.com/davethediver/mods/66)

![Clear Waters promotional cover artwork](media/clear-waters-cover.png)

[In-game comparisons and original screenshots](media/README.md)

See the filter in action:
[hand and hanging ice](media/clear-waters-mirror-comparison.png),
[coral and glowing rocks](media/clear-waters-comparison.png), and
[seaweed and rock ledges](media/clear-waters-seaweed-comparison.png).

## Requirements

- Dave the Diver for Windows, 64-bit. The current target is Steam build
  **25315876**, using Unity **6000.0.52f1**.
- BepInEx **6.0.0-be.788+5b766a3**, **Unity IL2CPP Windows x64**.
  [Download the matching loader](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip).

Compatibility with other game builds and platforms has not been verified.

## Install

1. Close the game and open the folder containing `DaveTheDiver.exe`.
2. If the matching BepInEx loader is not already installed, extract its archive
   into that folder. See the
   [official IL2CPP installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html)
   for the loader setup.
3. Extract the Clear Waters release ZIP into the same game folder. The plugin
   should end up at `BepInEx/plugins/ClearWaters/ClearWaters.dll`.
4. Start the game. The first BepInEx launch takes longer while it generates the
   files needed to load mods. Clear Waters creates its configuration and enables
   the clarity filter automatically.

The plugin needs only `ClearWaters.dll`; its browser page is embedded in the
DLL. No separate HTML, JavaScript, CSS, or prewritten configuration is needed.
Close the game before replacing the DLL with an update.

## Choose which effects to remove

The defaults remove blur, color separation, distortion, noise, and several
screen overlays. **Bloom and the danger overlay are kept by default**, with
optional removal switches. Each effect can be allowed individually, and the
master switch restores the original effects while retaining those choices.

Controls apply wherever the game uses the selected screen effects, including
transitions. Some effects appear only in specific scenes or situations, so a
switch may make no visible difference in the current view.

The mod does not modify object lights, particle systems, or water materials.
Blur textures feeding individual UI objects are preserved. Camera shake,
environmental fog, color grading, and transition fades are outside its scope.

## Configuration file

All settings are in `BepInEx/config/local.dave.clearwaters.cfg`, under the game
folder. With no existing configuration, the mod writes a complete file with all
defaults and the web server disabled. Every save writes all settings, including
unchanged defaults.

The `[Server]` section appears first, with `Enabled`, `Port`, and `BindAddress`.
Under `[Visuals]`, `Enabled` controls the master clarity filter. Under
`[Visuals.RemoveEffects]`, **`true` removes the named effect; `false` allows
it**. Every setting has an explanation in the file.

Edit the file with the game closed, then restart to read the changes. To reset
all settings, close the game and delete this file; the next launch recreates the
defaults with the web server disabled.

## Optional browser controls

To enable browser controls with every visual default, create the configuration
file before launching with just:

```ini
[Server]
Enabled = true
```

If the file already exists, change `Enabled` under `[Server]` to `true`. The mod
fills in and saves any omitted settings. Start the game and open
**<http://localhost:18780>** on the gaming computer.

Keep **Clarity filter** on and use **All switches off** to clear the individual
selections. Then check effects one at a time to compare them. **Restore
defaults** restores the default individual selections. Both buttons preserve the
master switch.

Browser changes are saved automatically and take effect as the game renders. If
the game is paused or unfocused, resume play to see the changes. The page
confirms when settings have been saved and reports save failures.

**Save and stop web controls** saves your choices, disables the server in the
configuration, and closes the listener. The clarity filter continues using those
choices. To reopen the controls, enable the server in the file and restart the
game. If saving fails, the server remains available so you can retry.

### Access from another device

The default bind address, `127.0.0.1`, permits connections only from the gaming
computer. To allow another device on a trusted local network, use:

```ini
[Server]
Enabled = true
Port = 18780
BindAddress = 0.0.0.0
```

Open `http://<gaming-computer-name>:18780` on that device. You may also need to
allow that port for the game through the local firewall. Use the configured port
in the URL if you change it.

**LAN access has no password.** Any reachable device on an allowed subnet can
read and change these visual settings or stop the web controls. The server
checks subnets associated with active IPv4 network adapters, which can include
VPNs and other connected networks. It also checks request hosts and rejects
cross-origin changes from other websites. These checks do not authenticate a
person or device. Use LAN mode only on networks you trust.

The mod makes no outgoing telemetry or update requests. When browser controls
are disabled, it starts no listener or HTTP worker. BepInEx is a separate loader
and may download dependencies during its first-run setup.

## Troubleshooting and removal

- **The mod does not load:** check the DLL location and that the loader is the
  matching Unity IL2CPP Windows x64 build. Startup and error messages are in
  `BepInEx/LogOutput.log`.
- **A particular effect still appears:** confirm that `[Visuals] Enabled` and
  that effect's removal switch are both `true`. File edits require a restart.
- **The page does not open:** enable `[Server] Enabled`, restart the game, and
  check the bind address, port, and firewall. Another program using the same
  port can prevent the server from starting; the visual filter still works.
- **Settings do not save:** check that the configuration file and directory are
  writable. Browser controls report save failures and can retry.
- **Uninstall:** close the game and remove `BepInEx/plugins/ClearWaters`. You
  can also remove `BepInEx/config/local.dave.clearwaters.cfg` and any firewall
  rule you created for the controls. BepInEx can remain installed for other
  mods.

## Upgrading from 0.1.0

The old `local.dave.clearwaters.settings.json` is imported for entries missing
from the `.cfg`; explicit `.cfg` values win. Previously active browser controls
stay enabled unless explicitly disabled in the new configuration. After a
successful save, the old JSON is archived with a `.migrated.bak` suffix and is
no longer used for settings.

## Source and builds

The plugin is C#, targeting .NET 6 with the .NET 8 SDK. The project embeds the
page, script, stylesheet, and favicon in `web/` as assembly resources. Game
assemblies and generated bindings are build dependencies and are not included in
the source or mod ZIP.

Public builds use only embedded web resources. Development builds can enable
live file overrides with `-p:EnableWebFileOverrides=true`, which defines
`WEB_FILE_OVERRIDES`. This permits optional files under the plugin's `web/`
folder to override the embedded page during development. Public builds ignore
loose web files.

Rendering statistics have a separate compile-time option:
`-p:EnableRenderDiagnostics=true` defines `RENDER_DIAGNOSTICS`. It enables
camera/effect counters and a **Rendering stats** panel in the browser controls.
Both build options default to off and can be enabled independently. Normal
builds compile out statistics collection and periodic rendering snapshots.

The rendering code changes blended camera values and selected screen passes at
runtime. It restores original values before the next camera update, without
changing source profiles or skipping gameplay callbacks. Runtime rendering
diagnostics are collected only in builds with that option enabled. Ordinary
startup and error logging remains in all builds.

## Nexus page text

[NEXUS_DESCRIPTION.bbcode](NEXUS_DESCRIPTION.bbcode) contains the full
description in Nexus's BBCode format. Open the file's **Raw** view on GitHub,
copy all of its contents, and paste into the description editor's **Source**
view. Switch back to the formatted view to check it, then save.

[NEXUS_SUMMARY.txt](NEXUS_SUMMARY.txt) is the separate plain-text short summary.
Paste it into the Summary field. The description file contains only the full
description, so it can be copied in its entirety.

Keep BBCode paragraphs on single source lines, with blank lines between them;
line breaks inside the configuration example are intentional. Nexus's
[BBCode documentation](https://www.nexusmods.com/news/14897) explains the
formatting and tag-nesting requirements.
