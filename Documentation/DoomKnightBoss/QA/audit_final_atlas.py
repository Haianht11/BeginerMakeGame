import json, re, subprocess, hashlib, glob, os
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
GIT = r"C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe"
ATLAS = ROOT / "Assets/project/art/Sprites/Boss/DoomKnightBoss.png"
META = ATLAS.with_name(ATLAS.name + ".meta")
PAL = ROOT / "Documentation/DoomKnightBoss/Palette/palette.json"
OUT = ROOT / "Documentation/DoomKnightBoss/QA/final_atlas_qa.json"


def git(*a):
    return subprocess.run([GIT, *a], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout


pal = {tuple(int(c["hex"][i:i + 2], 16) for i in (1, 3, 5)) for c in json.loads(PAL.read_text(encoding="utf-8"))["colors"]}

# meta parse
txt = META.read_text(encoding="utf-8")
guid = re.search(r"^guid: (\w+)", txt, re.M).group(1)
def kv(k):
    return re.search(rf"^\s*{k}: (\S+)", txt, re.M).group(1)
meta_flags = {k: kv(k) for k in ["spritePixelsToUnits", "enableMipMap", "filterMode"]}
# textureCompression in platformSettings
meta_flags["textureCompression_all"] = sorted(set(re.findall(r"^\s*textureCompression: (\S+)", txt, re.M)))
sprites = re.findall(
    r"- serializedVersion: 2\s+name: (\S+)\s+rect:\s+serializedVersion: 2\s+x: (\d+)\s+y: (\d+)\s+width: (\d+)\s+height: (\d+).*?internalID: (-?\d+)",
    txt, re.S)
sprite_ids = re.findall(r"spriteID: (\w+)", txt)
id_table = re.findall(r"213: (-?\d+)\s+second: (\S+)", txt)

# compare with HEAD meta
head_meta = git("show", "HEAD:Assets/project/art/Sprites/Boss/DoomKnightBoss.png.meta")
head_ids = re.findall(r"213: (-?\d+)\s+second: (\S+)", head_meta)
head_sids = re.findall(r"spriteID: (\w+)", head_meta)
meta_diff = git("diff", "--stat", "--", "Assets/project/art/Sprites/Boss/DoomKnightBoss.png.meta").strip()

im = Image.open(ATLAS)
arr = np.array(im.convert("RGBA"))
H, W = arr.shape[:2]

frame_report = []
tot_off = tot_part = tot_edge = 0
for name, x, y, w, h, iid in sprites:
    x, y, w, h = int(x), int(y), int(w), int(h)
    top = H - (y + h)  # unity rect y is from bottom
    cell = arr[top:top + h, x:x + w]
    a = cell[:, :, 3]
    op = a > 0
    part = int(np.sum((a != 0) & (a != 255)))
    rgb = cell[op][:, :3]
    off = int(sum(1 for p in {tuple(map(int, r)) for r in rgb} if p not in pal))
    off_px = int(sum(1 for r in rgb if tuple(map(int, r)) not in pal)) if off else 0
    edge = int(op[:, 0].sum() + op[:, -1].sum() + op[0, :].sum() + op[-1, :].sum())
    ys, xs = np.where(op)
    tot_off += off_px; tot_part += part; tot_edge += edge
    frame_report.append({"name": name, "rect": [x, y, w, h], "opaque_px": int(op.sum()), "off_palette_px": off_px,
                         "partial_alpha_px": part, "edge_touch_px": edge,
                         "bbox": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())] if len(xs) else None,
                         "empty": not len(xs)})

# animations: compare atlas cell vs Animations/<SIDE>/<ACT>/ frames
mismatch = []
act_map = {}
for name, x, y, w, h, iid in sprites:
    m = re.match(r"DoomKnight_(Idle|Walk|Attack|Hurt|Death)(Right|Left)_(\d+)", name)
    act, side, idx = m.group(1), m.group(2), int(m.group(3))
    act_map.setdefault((side, act), []).append((idx, int(x), int(y), int(w), int(h)))
anim_root = ROOT / "Documentation/DoomKnightBoss/Animations"
anim_check = {}
for (side, act), lst in act_map.items():
    d = anim_root / side.upper() / act.upper()
    if not d.exists():
        d = anim_root / side / act
    files = sorted(d.glob("*.png")) if d.exists() else []
    anim_check[f"{side}_{act}"] = {"dir": str(d.relative_to(ROOT)).replace("\\", "/") if d.exists() else None, "files": len(files), "slices": len(lst)}
    for (idx, x, y, w, h), f in zip(sorted(lst), files):
        top = H - (y + h)
        c = arr[top:top + h, x:x + w]
        s = np.array(Image.open(f).convert("RGBA"))
        if s.shape != c.shape or not np.array_equal(np.where(c[:, :, 3:4] > 0, c, 0), np.where(s[:, :, 3:4] > 0, s, 0)):
            mismatch.append(f"{side}_{act}_{idx:02d} vs {f.name}")

# anim events
events = {}
for side in ["Right", "Left"]:
    p = next(ROOT.glob(f"Assets/**/DoomKnight_Attack{side}.anim"))
    raw = p.read_bytes()
    t = raw.decode("utf-8")
    ev = re.findall(r"time: ([\d.]+)\s+functionName: (\w+)", t)
    events[side] = {"path": str(p.relative_to(ROOT)).replace("\\", "/"), "events": ev, "crlf_count": raw.count(b"\r\n"),
                    "git_diff_stat": git("diff", "--stat", "--", str(p.relative_to(ROOT))).strip()}

# duplicate meta guids
guids = {}
for mp in ROOT.glob("Assets/**/*.meta"):
    try:
        g = re.search(r"^guid: (\w+)", mp.read_text(encoding="utf-8", errors="ignore"), re.M)
    except Exception:
        continue
    if g:
        guids.setdefault(g.group(1), []).append(str(mp.relative_to(ROOT)))
dups = {g: v for g, v in guids.items() if len(v) > 1}

scene_diff = git("diff", "--stat", "--", "Assets/Scenes/DungeonDemo.unity", "Assets/Scenes/SampleScene.unity").strip()
scene_status = git("status", "--short", "--", "Assets/Scenes/DungeonDemo.unity", "Assets/Scenes/SampleScene.unity").strip()

checks = {
    "atlas_2304x1920": [W, H] == [2304, 1920],
    "slices_70": len(sprites) == 70 and len(id_table) == 70,
    "guid_ok": guid == "3960fb48e22f4a47b0a15f6c1cffee7b",
    "ppu_80": meta_flags["spritePixelsToUnits"] == "80",
    "filter_point": meta_flags["filterMode"] == "0",
    "mip_off": meta_flags["enableMipMap"] == "0",
    "compression_none": meta_flags["textureCompression_all"] == ["0"],
    "internal_ids_consistent_unique_70": len(set(i for i,_ in id_table)) == 70 and {n: int(i) for i, n in id_table} == {n: int(i) for n, *_r, i in sprites} and len(sprite_ids) == 71,
    "all_70_frames_nonempty": all(not f["empty"] for f in frame_report),
    "zero_off_palette": tot_off == 0,
    "zero_partial_alpha": tot_part == 0,
    "zero_edge_touch": tot_edge == 0,
    "atlas_matches_animation_pngs": not mismatch,
    "attack_right_event_0_38": any(abs(float(t) - 0.38) < 1e-6 and fn == "OnBossAttackContact" for t, fn in events["Right"]["events"]),
    "attack_left_event_0_285": any(abs(float(t) - 0.285) < 1e-6 and fn == "OnBossAttackContact" for t, fn in events["Left"]["events"]),
    "anim_lf_only": events["Right"]["crlf_count"] == 0 and events["Left"]["crlf_count"] == 0,
    "unique_meta_guids": not dups,
    "scenes_zero_edits": scene_diff == "" and scene_status == "",
}
rep = {"overall_pass": all(checks.values()), "checks": checks, "atlas": {"size": [W, H], "mode": im.mode}, "meta": {"guid": guid, **meta_flags, "meta_git_diff": meta_diff},
       "totals": {"off_palette_px": tot_off, "partial_alpha_px": tot_part, "edge_touch_px": tot_edge, "frames": len(frame_report)},
       "mismatch": mismatch, "anim_check": anim_check, "events": events, "duplicate_guids": dups, "scene_diff": scene_diff, "scene_status": scene_status,
       "frames": frame_report}
OUT.write_text(json.dumps(rep, indent=2), encoding="utf-8")
print(json.dumps({k: rep[k] for k in ["overall_pass", "checks", "totals", "mismatch", "events", "scene_diff", "scene_status", "meta"]}, indent=1))
print({k: v for k, v in anim_check.items()})
print("dups:", list(dups.items())[:3])

