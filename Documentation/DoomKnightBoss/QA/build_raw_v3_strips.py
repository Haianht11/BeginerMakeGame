import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
RAW_DIR = ROOT / "Documentation/DoomKnightBoss/Raw_v3"
OUT_DIR = ROOT / "Documentation/DoomKnightBoss/QA/raw_v3_strips"

CLIPS = [
    "RIGHT_IDLE",
    "LEFT_IDLE",
    "RIGHT_WALK",
    "LEFT_WALK",
    "RIGHT_ATTACK",
    "LEFT_ATTACK",
    "RIGHT_HURT",
    "LEFT_HURT",
    "RIGHT_DEATH",
    "LEFT_DEATH",
]


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    summary = {}
    for clip in CLIPS:
        clip_dir = RAW_DIR / clip
        frames = sorted(clip_dir.glob("*.png"))
        assert frames, f"No frames found in {clip_dir}"
        imgs = [Image.open(f).convert("RGBA") for f in frames]
        w, h = imgs[0].size
        n = len(imgs)
        scale = 2
        pad_top = 24
        cw, ch = w * scale, h * scale
        strip = Image.new("RGBA", (cw * n, ch + pad_top), (42, 46, 57, 255))
        draw = ImageDraw.Draw(strip)

        frame_stats = []
        for i, (fp, im) in enumerate(zip(frames, imgs)):
            arr = np.array(im)
            a = arr[:, :, 3]
            opaque = a > 0
            ys, xs = np.where(opaque)
            edge_touch = int(
                np.sum(opaque[:, 0]) + np.sum(opaque[:, w - 1]) + np.sum(opaque[0, :]) + np.sum(opaque[h - 1, :])
            )
            bbox = [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())] if len(xs) else None
            frame_stats.append({
                "index": i,
                "file": fp.name,
                "size": list(im.size),
                "bbox": bbox,
                "edge_touch_px": edge_touch,
            })

            # Alternating subtle checkerboard/dark slate behind each frame cell
            bg_col = (44, 48, 60, 255) if i % 2 == 0 else (36, 40, 50, 255)
            cell = Image.new("RGBA", (cw, ch), bg_col)
            zoomed = im.resize((cw, ch), Image.Resampling.NEAREST)
            cell.alpha_composite(zoomed)
            strip.paste(cell, (i * cw, pad_top))
            # Ground line at y=167*scale
            gy = pad_top + 167 * scale
            draw.line([(i * cw, gy), ((i + 1) * cw - 1, gy)], fill=(210, 180, 70, 160), width=1)
            # Frame border
            draw.rectangle([(i * cw, pad_top), ((i + 1) * cw - 1, pad_top + ch - 1)], outline=(90, 95, 110, 255))
            draw.text((i * cw + 8, 6), f"{clip} #{i} ({fp.name})", fill=(235, 240, 245, 255))

        out_path = OUT_DIR / f"{clip}_2x.png"
        strip.save(out_path)
        summary[clip] = {"frames": n, "strip": str(out_path.relative_to(ROOT)).replace("\\", "/"), "stats": frame_stats}

    (OUT_DIR / "raw_v3_stats.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    for k, v in summary.items():
        et = [s["edge_touch_px"] for s in v["stats"]]
        bbs = [s["bbox"] for s in v["stats"]]
        print(f"{k}: {v['frames']}f, edge_touch={et}, y_max={[b[3] for b in bbs]}, x_range={[(b[0], b[2]) for b in bbs]}")


if __name__ == "__main__":
    main()
