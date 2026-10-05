# Clear Waters release images

## Promotional cover

[Clear Waters cover](clear-waters-cover.png) — 1672 × 941 PNG. Use this as the
first image and the mod's main listing image. It is AI-generated promotional
artwork based on the mirror chamber, with the mod name and existing tagline.
[Generation prompt and provenance](COVER_PROMPT.md).

Suggested gallery order:

1. Promotional cover.
2. Mirror chamber comparison.
3. Seaweed comparison.
4. Cave comparison.
5. Original screenshots, if desired.

## In-game comparisons

Three 2560 × 1600 PNG comparisons include full views and close-ups of the screen
edges. Each has a pair of untouched 3840 × 2160 originals for individual
uploads.

## Mirror chamber: hand and hanging ice

[Mirror chamber comparison](clear-waters-mirror-comparison.png) shows pronounced
colored outlines and doubling around the lower-left hand and upper-right hanging
ice. Captured four seconds apart. This is a view beside the mirror, not a
capture of the teleport transition.

- [Clarity filter off](screenshots/clear-waters-mirror-off.jpg)
- [Clarity filter on](screenshots/clear-waters-mirror-on.jpg)

## Cave: coral and glowing rocks

[Comparison image](clear-waters-comparison.png) shows color fringing and edge
darkening in a darker scene. Captured three seconds apart.

- [Clarity filter off](screenshots/clear-waters-off.jpg)
- [Clarity filter on](screenshots/clear-waters-on.jpg)

## Seaweed: thin stems and rock ledges

[Seaweed comparison](clear-waters-seaweed-comparison.png) shows separated
colored outlines around the lower-right seaweed, coral, and left rock ledges in
a brighter scene. Captured one second apart. The fish-capture notification
disappears naturally between shots; the mod does not remove that notification.

- [Clarity filter off](screenshots/clear-waters-seaweed-off.jpg)
- [Clarity filter on](screenshots/clear-waters-seaweed-on.jpg)

## Capture and composition details

All three pairs were captured in-game on October 4, 2026, using Clear Waters
0.1.0. They show the combined clarity filter, including color fringing, blur,
and edge darkening. They are not tests of chromatic aberration alone. The camera
moved slightly between captures; animation and input prompts also differ. The
release candidate still needs its own in-game validation.

The source JPEGs are byte-for-byte copies of the captures. The comparison adds
labels and crop outlines to reduced full views, followed by native-resolution
576 × 432 crops. There is no sharpening, brightness adjustment, color
correction, or generated replacement of gameplay pixels. The crops use different
vertical positions to follow the same scenery after the camera moved; neither
image is warped to align it.

| Scene   | View | Original capture       | Left crop (x, y) | Right crop (x, y) |
| ------- | ---- | ---------------------- | ---------------- | ----------------- |
| Cave    | Off  | `20261004181552_1.jpg` | 0, 975           | 3264, 1250        |
| Cave    | On   | `20261004181555_1.jpg` | 0, 928           | 3264, 1192        |
| Seaweed | Off  | `20261004183416_1.jpg` | 264, 1040        | 3264, 1400        |
| Seaweed | On   | `20261004183417_1.jpg` | 264, 1025        | 3264, 1385        |
| Mirror  | Off  | `20261004185328_1.jpg` | 0, 1340          | 3264, 40          |
| Mirror  | On   | `20261004185332_1.jpg` | 0, 1358          | 3264, 72          |

Source SHA-256 checksums:

```text
41a1ee6b8d8905cb3c1b8333da74e51a493a1323fe97272a7123d3291b33f1ac  clear-waters-off.jpg
211ad177f2e5335064399d2784dc5e0f7ab764d53ad3e04b45bc880ca8f9eed5  clear-waters-on.jpg
22e98c217b31c6f2975b4b349925050d9682837dd423617ca69df4a3f93ffad6  clear-waters-seaweed-off.jpg
a0c1fd44a398b1204303bb6d07bf1b55fc04cfcb866845bd9994e2a0acdfead1  clear-waters-seaweed-on.jpg
a82f2f079ae46b8d429e411f1885ba5100c6dd180cc439552b449f0ff29a7c82  clear-waters-mirror-off.jpg
b30405bf4f6229e15086505935f73e00cc42fa57e29189d252145ee2e21cc451  clear-waters-mirror-on.jpg
```

The shared editable layout is [comparison.html](comparison.html). Open it
locally in a browser, adding `?scene=seaweed` or `?scene=mirror` for those
pairs. The default view shows the cave. Run `npm ci` and `npm run media:render`
from the mod directory to rebuild all three PNGs. The renderer uses Playwright
with an installed Chrome; set `CHROME_PATH` if it is elsewhere. Rendering uses a
device scale of 1 to preserve the detail crops' source pixels.

The water-drop mark is [web/favicon.svg](../web/favicon.svg), also used by the
web controls. It is embedded in the plugin DLL. The screenshots, comparison
layout, promotional cover, and export tools are release-page materials and are
not part of the DLL or mod ZIP.
