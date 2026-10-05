# Clear Waters page header

[Header image](clear-waters-header.png) is the 1300 x 372 PNG banner for the top
of the Nexus Mods page. It was created with the built-in imagegen tool on
October 5, 2026 UTC (October 4 local), using the
[existing promotional cover](clear-waters-cover.png) as its visual reference.
This is illustrated promotional artwork, not a gameplay capture.

The generated illustration is preserved at
[artwork/clear-waters-header-generated.png](artwork/clear-waters-header-generated.png)
(2140 x 735). The final export resizes it proportionally and crops equal amounts
from the top and bottom to fit the requested banner. The title, water-drop mark,
tagline, Dave, and mirror remain visible.

To reproduce the 1300 x 372 export with ImageMagick, run from this directory:

```sh
convert artwork/clear-waters-header-generated.png \
  -resize '1300x372^' -gravity center -extent 1300x372 +repage -strip \
  clear-waters-header.png
```

## Exact generation prompt

```text
Use case: ads-marketing.
Asset type: Nexus Mods page header/banner for the Clear Waters mod for Dave the Diver.
Input image 1 is the existing promotional cover: use it as the visual identity and illustrated-scene reference. Create a separate wide banner, preserving the recognizable title lettering, turquoise water-drop emblem with a white wave, deep-blue ice cavern, circular cyan mirror, and Dave's dark diving suit and yellow goggles/fins.
Output: a finished opaque PNG banner, exactly 1300 pixels wide by 372 pixels high if possible, aspect ratio 1300:372 (approximately 3.495:1). Compose specifically for this very wide, short canvas. Fill the entire banner; no letterboxing, borders, or mockup.
Composition: compact title group across the left 60 percent, Dave and the glowing mirror on the right. Fit the typography comfortably within the middle 75 percent of the banner height, leaving breathing room at the top and bottom. Make the mod name dominant and very readable. Recompose the artwork for the banner instead of squashing or clipping the original cover. Keep all text and the water-drop emblem fully inside the frame, with generous side margins.
Text, exactly and only:
"DAVE THE DIVER" as a small identifier above the title.
"CLEAR WATERS" as the dominant title, preferably on one horizontal line, with CLEAR warm ivory and WATERS pale mint/turquoise, in the same bold clean pixel-style lettering as the supplied cover.
"A calmer, clearer view." as one short supporting line beneath.
Style: polished, crisp, pixel-influenced game illustration matching the reference. Calm deep ocean navy and turquoise, sharp silhouettes and restrained light. Let dark quiet space behind the lettering provide contrast. Dave and the mirror stay recognizable at this short header height.
Constraints: promotional illustration, not a gameplay comparison. No HUD, meters, button prompts, website addresses, feature lists, health promises, before/after labels, additional characters, watermarks, blur, chromatic aberration, smeared edges, or rainbow glitches. Do not add text other than the three exact lines above.
```
