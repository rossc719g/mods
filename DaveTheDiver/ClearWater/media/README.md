# Clear Waters release images

[Comparison image](clear-waters-comparison.png) — 2560 × 1600 PNG with full
views and close-ups of both screen edges. Use this for the mod page. The two
3840 × 2160 originals are also ready to upload individually:

- [Clarity filter off](screenshots/clear-waters-off.jpg)
- [Clarity filter on](screenshots/clear-waters-on.jpg)

The screenshots were captured in-game on October 4, 2026, three seconds apart,
using Clear Waters 0.1.0. They show the combined clarity filter, including color
fringing, blur, and edge darkening. They are not a test of chromatic aberration
alone. The camera moved slightly between captures; animation and input prompts
also differ. The release candidate still needs its own in-game validation.

The source JPEGs are byte-for-byte copies of the captures. The comparison adds
labels and crop outlines to reduced full views, followed by native-resolution
576 × 432 crops. There is no sharpening, brightness adjustment, color
correction, or generated replacement of gameplay pixels. The crops use different
vertical positions to follow the same scenery after the camera moved; neither
image is warped to align it.

| View | Original capture       | Left crop (x, y) | Right crop (x, y) |
| ---- | ---------------------- | ---------------- | ----------------- |
| Off  | `20261004181552_1.jpg` | 0, 975           | 3264, 1250        |
| On   | `20261004181555_1.jpg` | 0, 928           | 3264, 1192        |

Source SHA-256 checksums:

```text
41a1ee6b8d8905cb3c1b8333da74e51a493a1323fe97272a7123d3291b33f1ac  clear-waters-off.jpg
211ad177f2e5335064399d2784dc5e0f7ab764d53ad3e04b45bc880ca8f9eed5  clear-waters-on.jpg
```

The editable layout is [comparison.html](comparison.html). Open it locally in a
browser, or run `npm ci` and `npm run media:render` from the mod directory to
rebuild the PNG. The renderer uses Playwright with an installed Chrome; set
`CHROME_PATH` if it is elsewhere. Rendering uses a device scale of 1 to preserve
the detail crops' source pixels.

The water-drop mark is [web/favicon.svg](../web/favicon.svg), also used by the
web controls. It is embedded in the plugin DLL. The screenshots, comparison
layout, and export tools are release-page materials and are not part of the DLL
or mod ZIP.
