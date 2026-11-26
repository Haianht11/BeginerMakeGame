import json
import math
from pathlib import Path
import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
PALETTE_PATH = ROOT / "Documentation/DoomKnightBoss/Palette/palette.json"
RIGHT_PATH = ROOT / "Documentation/DoomKnightBoss/Masters/master_right_256x192.png"
LEFT_PATH = ROOT / "Documentation/DoomKnightBoss/Masters/master_left_256x192.png"
OUT_JSON = ROOT / "Documentation/DoomKnightBoss/QA/masters_v3_qa.json"


def load_palette(path: Path) -> set[tuple[int, int, int]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    out = set()
    for c in data["colors"]:
        h = c["hex"].lstrip("#")
        out.add((int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)))
    return out


def connected_components(mask: np.ndarray, connectivity: int = 8):
    h, w = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    comps = []
    offsets = (
        [(-1, 0), (1, 0), (0, -1), (0, 1)]
        if connectivity == 4
        else [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
    )
    ys, xs = np.where(mask)
    for sy, sx in zip(ys.tolist(), xs.tolist()):
        if visited[sy, sx]:
            continue
        stack = [(sy, sx)]
        visited[sy, sx] = True
        pts = []
        while stack:
            cy, cx = stack.pop()
            pts.append((cx, cy))
            for dy, dx in offsets:
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    stack.append((ny, nx))
        comps.append(pts)
    return comps


def point_to_line_dist(px: float, py: float, p0: tuple[float, float], p1: tuple[float, float]) -> float:
    x0, y0 = p0
    x1, y1 = p1
    dx, dy = x1 - x0, y1 - y0
    denom = math.hypot(dx, dy)
    if denom == 0:
        return 0.0
    return abs(dy * px - dx * py + x1 * y0 - y1 * x0) / denom


def fit_line_tls(points: list[tuple[float, float]]):
    pts = np.array(points, dtype=np.float64)
    mean = pts.mean(axis=0)
    centered = pts - mean
    _, _, vt = np.linalg.svd(centered)
    direction = vt[0]
    p0 = (float(mean[0]), float(mean[1]))
    p1 = (float(mean[0] + direction[0]), float(mean[1] + direction[1]))
    return p0, p1, direction


def is_silver_bevel(r: int, g: int, b: int) -> bool:
    # Silver-gray bevel shades: #413744 (lum 59.5) .. #8e7a89 (lum 129.7)
    # Exclude dark fuller (#1c1822..#2f2937, lum <= 44.4) and crimson runes (r - g > 22)
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    return lum >= 55.0 and (r - g) <= 22


def is_outline_or_dark(r: int, g: int, b: int) -> bool:
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    return lum <= 22.0 and (r - g) <= 15


def find_inner_gem_centroid(arr: np.ndarray, x0: int, x1: int, y0: int, y1: int) -> tuple[float, float]:
    # Locate the isolated bright crimson center of the diamond gem inside the crossguard or pommel
    # by finding 4-connected components of R >= 195, G < 45 (pure bright crimson core)
    # and selecting the central gem cluster closest to the bounding box center of the crossguard/pommel.
    sub = arr[y0:y1, x0:x1]
    bright = (sub[:, :, 3] > 0) & (sub[:, :, 0] >= 190) & (sub[:, :, 1] < 45) & (sub[:, :, 2] < 50)
    comps = connected_components(bright, connectivity=4)
    cx_box = (x1 - x0) / 2.0
    cy_box = (y1 - y0) / 2.0
    # Score clusters by closeness to center of ROI and intensity
    best = None
    best_dist = 1e9
    for c in comps:
        cx = sum(p[0] for p in c) / len(c)
        cy = sum(p[1] for p in c) / len(c)
        d = math.hypot(cx - cx_box, cy - cy_box)
        if d < best_dist:
            best_dist = d
            best = (x0 + cx, y0 + cy)
    assert best is not None
    return best


def analyze_master(path: Path, side: str, pal_set: set[tuple[int, int, int]]) -> dict:
    im = Image.open(path)
    arr = np.array(im)
    h, w = arr.shape[:2]
    a = arr[:, :, 3]
    alpha_vals = sorted(int(v) for v in np.unique(a))
    binary_alpha = set(alpha_vals).issubset({0, 255})

    opaque = a > 0
    ys, xs = np.where(opaque)
    x_min, x_max = int(xs.min()), int(xs.max())
    y_min, y_max = int(ys.min()), int(ys.max())
    height_px = y_max - y_min + 1
    margins = {
        "left": x_min,
        "right": (w - 1) - x_max,
        "top": y_min,
        "bottom": (h - 1) - y_max,
    }
    edge_touch_px = int(
        np.sum(opaque[:, 0]) + np.sum(opaque[:, w - 1]) + np.sum(opaque[0, :]) + np.sum(opaque[h - 1, :])
    )

    rgb_opaque = arr[opaque][:, :3]
    uniq_rgb = {tuple(map(int, p)) for p in rgb_opaque}
    off_palette = [f"#{r:02x}{g:02x}{b:02x}" for (r, g, b) in sorted(uniq_rgb - pal_set)]

    comps_8 = connected_components(opaque, connectivity=8)
    orphan_1px = sum(1 for c in comps_8 if len(c) == 1)

    boot_y_max = int(ys[(xs >= 75) & (xs <= 185)].max()) if side == "right" else int(ys[(xs >= 95) & (xs <= 215)].max())

    if side == "right":
        tip_mask = opaque & (np.arange(w)[None, :] >= x_max - 1) & (np.arange(h)[:, None] >= 155)
        tys, txs = np.where(tip_mask)
        idx = int(np.argmax(txs))
        tip = (float(txs[idx]), float(tys[idx]))
        pommel_c = find_inner_gem_centroid(arr, 91, 99, 109, 117)
        gem_c = find_inner_gem_centroid(arr, 116, 123, 122, 129)
    else:
        tip_mask = opaque & (np.arange(w)[None, :] <= x_min + 1) & (np.arange(h)[:, None] >= 155)
        tys, txs = np.where(tip_mask)
        idx = int(np.argmin(txs))
        tip = (float(txs[idx]), float(tys[idx]))
        pommel_c = find_inner_gem_centroid(arr, 121, 128, 109, 117)
        gem_c = find_inner_gem_centroid(arr, 96, 103, 121, 129)

    # Measure exposed grip pixels (dark-reddish wrapped handle segments just outside the gauntlet fingers)
    pgx, pgy = gem_c[0] - pommel_c[0], gem_c[1] - pommel_c[1]
    pg_len = math.hypot(pgx, pgy)
    ux, uy = pgx / pg_len, pgy / pg_len
    nx, ny = -uy, ux

    grip_pts = []
    for yy in range(108, 132):
        for xx in range(92, 128):
            if not opaque[yy, xx]:
                continue
            dx, dy = xx - pommel_c[0], yy - pommel_c[1]
            t = (dx * ux + dy * uy) / pg_len
            d_perp = abs(dx * nx + dy * ny)
            if (0.14 <= t <= 0.30 or 0.70 <= t <= 0.88) and d_perp <= 2.0:
                r, g, b = int(arr[yy, xx, 0]), int(arr[yy, xx, 1]), int(arr[yy, xx, 2])
                if d_perp <= 1.3 or (r > b + 6):
                    grip_pts.append((xx, yy))
    grip_c = (
        sum(p[0] for p in grip_pts) / len(grip_pts),
        sum(p[1] for p in grip_pts) / len(grip_pts),
    )

    # Scan blade cross-sections from crossguard toward tip along (gem -> tip)
    bx, by = tip[0] - gem_c[0], tip[1] - gem_c[1]
    b_len = math.hypot(bx, by)
    bux, buy = bx / b_len, by / b_len
    bnx, bny = -buy, bux
    if bny < 0:
        bnx, bny = -bnx, -bny

    bevel_stations = []
    blade_stations = []
    mid_centers = []

    # Blade starts past crossguard quillons at s=12 px and converges to 1px tip outline at s=b_len-6 px
    s_start = 12.0
    s_end = b_len - 6.0
    n_steps = int(round((s_end - s_start) * 2))
    for i in range(n_steps + 1):
        s = s_start + i * 0.5
        cx = gem_c[0] + bux * s
        cy = gem_c[1] + buy * s

        samples = []
        for k in range(-48, 49):
            v = k * 0.25
            px = int(round(cx + bnx * v))
            py = int(round(cy + bny * v))
            if 0 <= px < w and 0 <= py < h and opaque[py, px]:
                r, g, b = int(arr[py, px, 0]), int(arr[py, px, 1]), int(arr[py, px, 2])
                samples.append((v, r, g, b))
            else:
                samples.append((v, -1, -1, -1))

        # Walk outward from fuller core (v ~ -3..+3 in parallel body, -0.5..+0.5 in tip taper)
        # to find each bevel band and stop at the blade outline!
        v_inner = 3.0 if s <= b_len - 18.0 else 0.5
        upper_slv = []
        in_u = False
        for v, r, g, b in reversed([it for it in samples if -11.0 <= it[0] <= -v_inner]):
            if r >= 0 and is_silver_bevel(r, g, b):
                upper_slv.append(v)
                in_u = True
            elif in_u:
                break

        lower_slv = []
        in_l = False
        for v, r, g, b in [it for it in samples if v_inner <= it[0] <= 11.0]:
            if r >= 0 and is_silver_bevel(r, g, b):
                lower_slv.append(v)
                in_l = True
            elif in_l:
                break

        bevel_stations.append({
            "s": s,
            "has_upper": len(upper_slv) > 0,
            "has_lower": len(lower_slv) > 0,
            "w_upper": (max(upper_slv) - min(upper_slv) + 0.25) if upper_slv else 0.0,
            "w_lower": (max(lower_slv) - min(lower_slv) + 0.25) if lower_slv else 0.0,
        })

        # Parallel-sided main blade region: s in [18, b_len - 18]
        if 18.0 <= s <= b_len - 18.0 and upper_slv and lower_slv:
            # Find outer outline pixels directly outside upper_slv and lower_slv
            u_out = min(upper_slv) - 1.0
            l_out = max(lower_slv) + 1.0
            v_mid = 0.5 * (u_out + l_out)
            total_w = l_out - u_out
            blade_stations.append({
                "s": s,
                "cx": cx + bnx * v_mid,
                "cy": cy + bny * v_mid,
                "w_upper": max(upper_slv) - min(upper_slv) + 0.25,
                "w_lower": max(lower_slv) - min(lower_slv) + 0.25,
                "blade_width": total_w,
            })
            if 0.40 * b_len <= s <= 0.60 * b_len:
                mid_centers.append((cx + bnx * v_mid, cy + bny * v_mid))

    mid_blade_c = (
        sum(p[0] for p in mid_centers) / len(mid_centers),
        sum(p[1] for p in mid_centers) / len(mid_centers),
    )

    landmarks = {
        "pommel_centroid": pommel_c,
        "grip_centroid": grip_c,
        "crossguard_gem_centroid": gem_c,
        "mid_blade_centroid": mid_blade_c,
        "blade_tip": tip,
    }
    p0_tls, p1_tls, _ = fit_line_tls(list(landmarks.values()))
    ortho_devs_tls = {
        k: round(point_to_line_dist(v[0], v[1], p0_tls, p1_tls), 4)
        for k, v in landmarks.items()
    }
    ortho_devs_pt = {
        k: round(point_to_line_dist(v[0], v[1], pommel_c, tip), 4)
        for k, v in landmarks.items()
    }

    handle_angle = math.degrees(math.atan2(gem_c[1] - pommel_c[1], abs(gem_c[0] - pommel_c[0])))
    blade_angle = math.degrees(math.atan2(tip[1] - gem_c[1], abs(tip[0] - gem_c[0])))
    full_angle = math.degrees(math.atan2(tip[1] - pommel_c[1], abs(tip[0] - pommel_c[0])))
    kink_deg = abs(blade_angle - handle_angle)

    upper_present_pct = 100.0 * sum(1 for b in bevel_stations if b["has_upper"]) / len(bevel_stations)
    lower_present_pct = 100.0 * sum(1 for b in bevel_stations if b["has_lower"]) / len(bevel_stations)
    w_upper_mean = sum(b["w_upper"] for b in blade_stations) / len(blade_stations)
    w_lower_mean = sum(b["w_lower"] for b in blade_stations) / len(blade_stations)
    blade_width_mean = sum(b["blade_width"] for b in blade_stations) / len(blade_stations)
    sword_length = math.hypot(tip[0] - pommel_c[0], tip[1] - pommel_c[1])

    boot_xs = xs[(ys >= 155) & (ys <= 167)]
    char_cx = float(np.median(boot_xs))

    return {
        "file": str(path.relative_to(ROOT)).replace("\\", "/"),
        "width": w,
        "height_canvas": h,
        "mode": im.mode,
        "alpha_values": alpha_vals,
        "binary_alpha": binary_alpha,
        "off_palette_count": len(off_palette),
        "off_palette_hex": off_palette,
        "bbox": {"x_min": x_min, "y_min": y_min, "x_max": x_max, "y_max": y_max},
        "character_height_px": height_px,
        "grounded_y": y_max,
        "boot_grounded_y": boot_y_max,
        "margins_px": margins,
        "min_margin_px": min(margins.values()),
        "edge_touch_px": edge_touch_px,
        "orphan_1px_count": orphan_1px,
        "landmarks": {k: [round(v[0], 3), round(v[1], 3)] for k, v in landmarks.items()},
        "ortho_dev_tls_px": ortho_devs_tls,
        "max_ortho_dev_tls_px": round(max(ortho_devs_tls.values()), 4),
        "ortho_dev_pommel_to_tip_px": ortho_devs_pt,
        "max_ortho_dev_pommel_to_tip_px": round(max(ortho_devs_pt.values()), 4),
        "angles_deg": {
            "handle": round(handle_angle, 3),
            "blade": round(blade_angle, 3),
            "full_sword": round(full_angle, 3),
            "kink_deg": round(kink_deg, 3),
        },
        "sword_length_px": round(sword_length, 3),
        "blade_length_px": round(b_len, 3),
        "bevel_audit": {
            "stations_checked": len(bevel_stations),
            "upper_bevel_present_pct": round(upper_present_pct, 2),
            "lower_bevel_present_pct": round(lower_present_pct, 2),
            "upper_bevel_mean_px": round(w_upper_mean, 3),
            "lower_bevel_mean_px": round(w_lower_mean, 3),
            "bevel_width_diff_px": round(abs(w_upper_mean - w_lower_mean), 3),
            "blade_width_mean_px": round(blade_width_mean, 3),
        },
        "asymmetry_anchors": {
            "character_center_x": round(char_cx, 2),
            "right_hand_behind_crossguard": (gem_c[0] > pommel_c[0]) if side == "right" else (gem_c[0] < pommel_c[0]),
            "sword_points_right": tip[0] > gem_c[0],
        },
    }


def main():
    pal_set = load_palette(PALETTE_PATH)
    right_res = analyze_master(RIGHT_PATH, "right", pal_set)
    left_res = analyze_master(LEFT_PATH, "left", pal_set)

    r_arr = np.array(Image.open(RIGHT_PATH))
    l_arr = np.array(Image.open(LEFT_PATH))
    r_flip = np.fliplr(r_arr)
    mirror_diff_ratio = float(np.mean(np.any(l_arr != r_flip, axis=-1)))

    len_diff = abs(left_res["sword_length_px"] - right_res["sword_length_px"])
    width_diff = abs(
        left_res["bevel_audit"]["blade_width_mean_px"] - right_res["bevel_audit"]["blade_width_mean_px"]
    )
    angle_diff = abs(left_res["angles_deg"]["full_sword"] - right_res["angles_deg"]["full_sword"])

    checks = {
        "right_coaxial_ortho_le_0_6": right_res["max_ortho_dev_tls_px"] <= 0.6,
        "left_coaxial_ortho_le_0_6": left_res["max_ortho_dev_tls_px"] <= 0.6,
        "right_kink_le_1_5": right_res["angles_deg"]["kink_deg"] <= 1.5,
        "left_kink_le_1_5": left_res["angles_deg"]["kink_deg"] <= 1.5,
        "right_bevels_100pct": right_res["bevel_audit"]["upper_bevel_present_pct"] == 100.0
        and right_res["bevel_audit"]["lower_bevel_present_pct"] == 100.0,
        "left_bevels_100pct": left_res["bevel_audit"]["upper_bevel_present_pct"] == 100.0
        and left_res["bevel_audit"]["lower_bevel_present_pct"] == 100.0,
        "right_bevel_sym_le_0_5": right_res["bevel_audit"]["bevel_width_diff_px"] <= 0.5,
        "left_bevel_sym_le_0_5": left_res["bevel_audit"]["bevel_width_diff_px"] <= 0.5,
        "sword_length_diff_le_1_5": len_diff <= 1.5,
        "blade_width_diff_le_0_5": width_diff <= 0.5,
        "angle_diff_le_1_0": angle_diff <= 1.0,
        "canvas_and_format_ok": (
            right_res["width"] == 256
            and right_res["height_canvas"] == 192
            and right_res["mode"] == "RGBA"
            and left_res["width"] == 256
            and left_res["height_canvas"] == 192
            and left_res["mode"] == "RGBA"
        ),
        "grounded_167_height_132": (
            right_res["grounded_y"] == 167
            and left_res["grounded_y"] == 167
            and right_res["character_height_px"] == 132
            and left_res["character_height_px"] == 132
        ),
        "margins_ge_10_zero_edge_touch": (
            right_res["min_margin_px"] >= 10
            and left_res["min_margin_px"] >= 10
            and right_res["edge_touch_px"] == 0
            and left_res["edge_touch_px"] == 0
        ),
        "binary_alpha_zero_off_palette": (
            right_res["binary_alpha"]
            and left_res["binary_alpha"]
            and right_res["off_palette_count"] == 0
            and left_res["off_palette_count"] == 0
            and right_res["orphan_1px_count"] == 0
            and left_res["orphan_1px_count"] == 0
        ),
        "non_mirrored_asymmetric": mirror_diff_ratio > 0.05,
    }

    report = {
        "gate": "Gate 1 - Masters v3 QA",
        "overall_pass": all(checks.values()),
        "checks": checks,
        "cross_side_comparison": {
            "sword_length_diff_px": round(len_diff, 3),
            "blade_width_diff_px": round(width_diff, 3),
            "angle_diff_deg": round(angle_diff, 3),
            "mirror_diff_ratio": round(mirror_diff_ratio, 4),
        },
        "master_right": right_res,
        "master_left": left_res,
    }
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
