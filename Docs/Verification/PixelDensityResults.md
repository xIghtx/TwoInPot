# Pixel density correction — 2026-10-28

- Replaced the atlas claw hammer with a simple steel mallet and brown handle, using the built-in image generation tool. Source: Assets/TwoInPot/Art/Pixel/Source/Hammer.png.
- Final generation prompt: one horizontal 48x16 logical pixel hammer, navy outline, 2–3 flat shades per material, rectangular gray head, brown/orange handle; no claw, grain, bolts, gradients, gloss, or extra objects; transparent background. Character atlas supplied as style reference.
- Unity importer reduces the hammer to a fixed seven-color palette and imports separate head/handle sprites at 24 pixels/unit. The head follows the existing contact-head transform without stretch; only the handle length changes.
- Scenery, switches, gates, clues and markers are baked to their displayed dimensions at 24 pixels/unit, preserving original colliders and placement. Background sampling now uses 640x360 at 24 pixels/unit. Dust and impact sprites are reduced to their final displayed pixel sizes.
- Intentional character squash/stretch and tool rotation remain animated.
- Build passed (225,602,281 bytes). Hidden two-process regression passed: host 1,019 checks, guest 620 checks; both processes exited 0. All 12 practice/objective transitions, hammer contact/push/grip/release, both endings, network synchronization and sound checks passed.
- Inspected native runtime captures of stages 1, 6 and 12, plus the character close-up. Head-shape and visual/contact-head alignment assertions passed.
- Measured averages: host 58.8 fps, guest 58.9 fps. 1% lows: 26.1 / 27.6 fps; worst frames 94.4 / 78.1 ms. This run does not establish a steady 30 fps minimum. Logs: PixelDensityBuild.log, PixelDensityHost.log, PixelDensityGuest.log. Existing exit-time ComputeBuffer cleanup warning remains; guest also logged a Curl output error during startup, without failing connection or regression checks.
- Final imported tool parts: Assets/TwoInPot/Art/Pixel/Density/HammerHead.png and HammerHandle.png. Shared density outputs total 46 small PNGs (85,569 bytes). The original generated image is preserved under Source/Hammer.png.
