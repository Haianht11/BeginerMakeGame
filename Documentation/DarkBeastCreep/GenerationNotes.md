# DarkBeastCreep Generation & QA Notes

## Asset Spec
- **Character**: `DarkBeastCreep` (quadrupedal dark-armored predator beast)
- **Canvas**: `192x160` RGBA, binary alpha (`0` or `255`), facing **RIGHT**
- **Unity PPU**: `80` (`2.4 x 2.0` world units per frame)
- **Pivot / Grounding**: `{x: 0.5, y: 0.1}` (`16 px` bottom margin, paws grounded around `y=143`)
- **Locked Palette**: `Palette/palette.json` (`32` colors; `#f09028` reserved amber eye)

## Palette QA (`Palette/palette-qa.json` & `Pipeline/03-palette-lock/palette-lock.json`)
- Palette build status: `pass`
- Master lock fit (`master_right_192x160.png`): `measured_px=7606`, `off_palette_px=0`, `partial_alpha_px=0`, `palette_colors_used=32`, `mean_delta_e=0.0`, `max_delta_e=0.0`

## Pre-Quantization Cleaning & Deduplication
- `IDLE`: `5 -> 4` frames (`frame_00..03`, dropped loop-wrap `frame_04`)
- `RUN`: `9 -> 8` frames (`frame_00..07`, dropped loop-wrap `frame_08`)
- `ATTACK`: `9` frames (`frame_00..08`, retake `seed=206`, `hitFrame=4` -> `0.36s`, `OnAttackContact`)
- `HURT`: `5` frames (`frame_00..04`); removed 1 disconnected `5 px` stray speck (`x=155..158, y=104..106`) right of snout in staged `HURT/frame_01.png` without mutating `Raw/`
- `DEATH`: `9` frames (`frame_00..08`, collapse onto ground line)
- All 35 final frames verified `192x160` RGBA with `0` edge-touching pixels.

## Temporal Quantization QA (`palette_tool.py quantize-seq`)
Flags: `--alpha-threshold 128 --orphans 0 --margin 4e-4` (`--loop-policy cycle` for `IDLE`/`RUN`; `oneshot` for `ATTACK`/`HURT`/`DEATH`).

| Clip | Frames | Policy | Timing (ms/f) | Status | `off_palette_px` | `output_partial_alpha_px` | `max_delta_e` | `idx_flip` | `noise_flip` | `alpha_flip` |
| --- | ---: | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `IDLE` | 4 | `cycle` | 190 | `pass` | 0 | 0 | 0.0 | 0.789214 | 0.0 | 0.128645 |
| `RUN` | 8 | `cycle` | 90 | `pass` | 0 | 0 | 0.0 | 0.871039 | 0.0 | 0.307203 |
| `ATTACK` | 9 | `oneshot` | 90 | `pass` | 0 | 0 | 0.0 | 0.876516 | 0.0 | 0.384897 |
| `HURT` | 5 | `oneshot` | 85 | `pass` | 0 | 0 | 0.0 | 0.868264 | 0.0 | 0.277742 |
| `DEATH` | 9 | `oneshot` | 105 | `pass` | 0 | 0 | 0.0 | 0.574572 | 0.0 | 0.254195 |

## Per-Frame Despeckle & Alpha Check
- `IDLE` (`Animations/IDLE/quantize-qa.json`): `despeckled_px=[27, 28, 27, 23]`, `held_index_px=[0, 0, 0, 0]`
- `RUN` (`Animations/RUN/quantize-qa.json`): `despeckled_px=[27, 22, 32, 26, 26, 29, 26, 26]`, `held_index_px=[0, 0, 0, 0, 0, 0, 0, 0]`
- `ATTACK` (`Animations/ATTACK/quantize-qa.json`): `despeckled_px=[27, 35, 32, 30, 27, 38, 33, 38, 26]`, `held_index_px=[0, 0, 0, 0, 0, 0, 0, 0, 0]`
- `HURT` (`Animations/HURT/quantize-qa.json`): `despeckled_px=[27, 34, 36, 28, 29]`, `held_index_px=[0, 0, 0, 0, 0]`
- `DEATH` (`Animations/DEATH/quantize-qa.json`): `despeckled_px=[27, 26, 27, 24, 15, 17, 21, 22, 21]`, `held_index_px=[0, 0, 0, 0, 0, 0, 0, 0, 0]`
