"""Render the left-handed idle and walk cycles from the two existing cutout atlases."""

from dataclasses import dataclass, replace
from hashlib import sha256
import json
from math import acos, cos, degrees, floor, hypot, radians, sin
from pathlib import Path
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT.parent / 'Animation128'
sys.dont_write_bytecode = True
sys.path.insert(0, str(SOURCE))
import render_sprites as rig


CLIPS = (('LEFT', 'IDLE'), ('LEFT', 'WALK'), ('RIGHT', 'IDLE'), ('RIGHT', 'WALK'))
FRAME_MS = {'IDLE': 200, 'WALK': 100}
FRAME_NAMES = {('LEFT', 'IDLE'): 'IdleLeft', ('LEFT', 'WALK'): 'WalkLeft',
               ('RIGHT', 'IDLE'): 'IdleRight', ('RIGHT', 'WALK'): 'WalkRight'}
IDLE_MOTION = {
    'LEFT': [(0, 0, 3), (1, -1, 4), (2, -2, 5), (2, -1, 3), (1, 0, 1), (0, 0, 2)],
    'RIGHT': [(0, 0, -3), (1, 1, -4), (2, 2, -5), (2, 1, -3), (1, 0, -1), (0, 0, -2)],
}
WALK_CARRY = {
    'LEFT': [((44, 60), -127), ((45, 61), -125), ((46, 59), -122), ((45, 58), -124),
             ((44, 60), -127), ((45, 61), -125), ((46, 59), -123), ((45, 59), -125)],
    'RIGHT': [((86, 59), -53), ((85, 61), -55), ((84, 59), -58), ((85, 58), -56),
              ((86, 60), -53), ((85, 61), -55), ((84, 59), -57), ((85, 59), -55)],
}
WALK_FEET = {
    'LEFT': [((56, 115, 12), (72, 115, -12)), ((59, 115, 0), (74, 112, -17)),
             ((62, 115, 0), (65, 108, -7)), ((68, 115, -7), (54, 111, 9)),
             ((72, 115, -11), (53, 115, 12)), ((74, 112, -17), (57, 115, 0)),
             ((64, 108, -6), (62, 115, 0)), ((55, 111, 10), (68, 115, -7))],
    'RIGHT': [((56, 115, 11), (76, 115, -12)), ((54, 112, 17), (71, 115, 0)),
              ((63, 108, 6), (66, 115, 0)), ((73, 111, -10), (60, 115, 7)),
              ((73, 115, -12), (55, 115, 11)), ((70, 115, 0), (53, 112, 17)),
              ((66, 115, 0), (62, 108, 7)), ((60, 115, 7), (73, 111, -9))],
}
WALK_HIPS_Y = {'LEFT': (76, 76, 74, 75, 75, 75, 73, 74), 'RIGHT': (75, 75, 73, 74, 76, 76, 74, 75)}
IDLE_STANCE = {'LEFT': (75, 58, 70), 'RIGHT': (73, 54, 70)}
JOINTS = (
    ('head', 'chest'), ('cape', 'chest'), ('chest', 'waist'),
    ('chest', 'stump'), ('chest', 'upper_arm'), ('upper_arm', 'forearm'),
    ('forearm', 'fist'), ('fist', 'sword'),
    ('waist', 'near_thigh'), ('near_thigh', 'near_shin'), ('near_shin', 'near_boot'),
    ('waist', 'far_thigh'), ('far_thigh', 'far_shin'), ('far_shin', 'far_boot'),
)


@dataclass(frozen=True, kw_only=True)
class TracedPart(rig.Part):
    name: str
    masks: dict

    def draw(self, stage, point, turn=0):
        layer = super().draw(stage, point, turn)
        self.masks[self.name] = np.asarray(layer.getchannel('A')) == 255
        return layer


def trace_part(part, name, masks):
    return TracedPart(part.image, part.origin, part.natural_axis, name=name, masks=masks)


def attach_grip(pose, direction):
    # The upright grip rotates the fist by half a turn relative to the point-down sword pose.
    fist_turn = pose.blade + 90 if pose.blade < 0 else pose.blade - 90
    angle = radians(fist_turn)
    x = 4 if direction == 'LEFT' else -4
    y = 2
    wrist = rig.offset(pose.grip, x * cos(angle) - y * sin(angle), x * sin(angle) + y * cos(angle))
    return replace(pose, wrist=wrist, fist_turn=fist_turn)


def poses(direction, action):
    if action == 'WALK':
        frames = []
        for original, (grip, blade), (near, far), hips_y in zip(
            rig.POSES[direction][action], WALK_CARRY[direction], WALK_FEET[direction], WALK_HIPS_Y[direction], strict=True,
        ):
            back_x = original.hips[0]
            chest_y = hips_y - 15
            head_x = back_x - 3 if direction == 'LEFT' else back_x + 3
            pose = replace(original, hips=(back_x, hips_y), chest=(back_x, chest_y),
                           neck=(head_x, chest_y - 13), grip=grip, blade=blade,
                           near=rig.Foot(*near), far=rig.Foot(*far))
            frames.append(attach_grip(pose, direction))
        return frames

    rest = rig.POSES[direction]['IDLE'][0]
    hips_y, near_x, far_x = IDLE_STANCE[direction]
    rise = rest.hips[1] - hips_y
    stance = replace(rest, hips=(rest.hips[0], hips_y), near=rig.Foot(near_x), far=rig.Foot(far_x))
    return [attach_grip(replace(stance, chest=(rest.hips[0], rest.chest[1] - rise - lift),
                                neck=rig.offset(rest.neck, 0, -rise - lift), head=head, cape=cape), direction)
            for lift, head, cape in IDLE_MOTION[direction]]


def pixel_changes(first, second):
    return int(np.any(np.asarray(first) != np.asarray(second), axis=2).sum())


def check_joints(image, masks, label):
    overlaps = {f'{first}/{second}': int(np.count_nonzero(masks[first] & masks[second]))
                for first, second in JOINTS}
    for pair, pixels in overlaps.items():
        if pixels < 3:
            raise ValueError(f'{label}: {pair} has only {pixels} overlapping pixels')
    alpha = image.getchannel('A')
    x, y = np.argwhere(np.asarray(alpha) == 255)[0][::-1]
    ImageDraw.floodfill(alpha, (int(x), int(y)), 0)
    if alpha.getbbox() is not None:
        raise ValueError(f'{label}: opaque pixels detached at {alpha.getbbox()}')
    return overlaps


def check_grip(pose, masks, label):
    axis = radians(pose.blade)
    hold = masks['sword'] & masks['fist']
    covered = 0
    for step in range(-3, 4):
        x = floor(pose.grip[0] + rig.PADDING + step * cos(axis))
        y = floor(pose.grip[1] + rig.PADDING + step * sin(axis))
        covered += int(hold[y, x])
    if covered < 5:
        raise ValueError(f'{label}: fist covers {covered}/7 handle centerline samples')
    return covered


def knee_angle(leg):
    knee_x, knee_y = leg['knee']
    hip_x, hip_y = leg['hip']
    ankle_x, ankle_y = leg['ankle']
    thigh_x, thigh_y = hip_x - knee_x, hip_y - knee_y
    shin_x, shin_y = ankle_x - knee_x, ankle_y - knee_y
    cosine = (thigh_x * shin_x + thigh_y * shin_y) / (hypot(thigh_x, thigh_y) * hypot(shin_x, shin_y))
    return round(degrees(acos(max(-1, min(1, cosine)))), 1)


def render_clips():
    atlases = {direction: rig.atlas(direction) for direction in ('LEFT', 'RIGHT')}
    pieces = {direction: rig.assemble_parts(direction, art) for direction, art in atlases.items()}
    sword = rig.weapon(atlases['LEFT']['sword'])
    rig.clamp_palette(pieces, sword)
    masks = {}
    traced = {direction: {name: trace_part(part, name, masks) for name, part in parts.items()}
              for direction, parts in pieces.items()}
    traced_sword = trace_part(sword, 'sword', masks)
    clips = {}
    report = {}
    for direction, action in CLIPS:
        name = FRAME_NAMES[direction, action]
        images, records = [], []
        for index, pose in enumerate(poses(direction, action)):
            label = f'{name}/{index:02}'
            masks.clear()
            try:
                image, record = rig.make_frame(direction, action, index, pose, traced[direction], traced_sword)
            except ValueError as exc:
                raise ValueError(f'{label}: {exc}') from exc
            record['back'] = {'hips': pose.hips, 'chest': pose.chest}
            record['jointOverlapPixels'] = check_joints(image, masks, label)
            record['coveredHandleSamples'] = check_grip(pose, masks, label)
            for leg in record['feet'].values():
                leg['kneeAngleDegrees'] = knee_angle(leg)
            assert pose.hips[0] == pose.chest[0], label
            assert set(image.getchannel('A').tobytes()) == {0, 255}, label
            assert record['leftHand'] == pose.grip and record['rightHand'] is None, label
            if action == 'IDLE':
                assert all(foot['grounded'] for foot in record['feet'].values()), name
                assert all(foot['kneeAngleDegrees'] >= 145 for foot in record['feet'].values()), label
                assert record['swordTip'] == [pose.grip[0], rig.GROUND], name
            else:
                foot = 'left' if index < 4 else 'right'
                other = 'right' if foot == 'left' else 'left'
                assert record['feet'][foot]['grounded'], name
                assert record['feet'][other]['grounded'] == (index % 4 == 0), name
                record['phase'] = rig.phase_for(index)
                record['plantedFoot'] = foot
                tip_x, tip_y = record['swordTip']
                forward_sign = -1 if direction == 'LEFT' else 1
                assert tip_y < pose.grip[1] - 28 and forward_sign * (tip_x - pose.grip[0]) > 18, label
                if record['phase'] == 'passing':
                    assert record['feet'][foot]['kneeAngleDegrees'] >= 145, f'{label}: support knee too bent'
            images.append(image)
            records.append(record)

        if action == 'WALK':
            for foot in ('left', 'right'):
                swing = max(rig.GROUND - record['feet'][foot]['bottomY'] for record in records)
                assert 6 <= swing <= 8, f'{name}: {foot} foot lifts {swing}px'
            assert max(frame['back']['hips'][1] for frame in records) - min(
                frame['back']['hips'][1] for frame in records) == 3, name

        signatures = [rig.signature(image) for image in images]
        assert len(set(signatures)) == len(images), f'{name} contains a repeated or translated frame'
        pairs = []
        for index, image in enumerate(images):
            following = (index + 1) % len(images)
            changed = pixel_changes(image, images[following])
            assert changed > 0, f'{name} repeats at {index} to {following}'
            pairs.append({'from': index, 'to': following, 'differentPixels': changed})
        clips[name] = images
        report[name] = {'frames': records, 'adjacentPairs': pairs}

    left = {rig.signature(image) for name, images in clips.items() if name.endswith('Left') for image in images}
    for name, images in clips.items():
        if name.endswith('Right'):
            for image in images:
                assert rig.signature(image) not in left, f'{name} uses a copied frame'
                assert rig.signature(image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)) not in left, (
                    f'{name} uses a mirrored frame')
    return clips, report


def write_previews(output, clips):
    font = ImageFont.load_default(size=14)
    width = 92 + 8 * rig.SIZE
    board = Image.new('RGB', (width, 24 + len(clips) * rig.SIZE), '#24212a')
    draw = ImageDraw.Draw(board)
    draw.text((12, 5), 'ONE-ARMED SWORDSMAN', fill='#ded5ce', font=font)
    for row, (name, images) in enumerate(clips.items()):
        y = 24 + row * rig.SIZE
        draw.text((12, y + 53), name, fill='#ded5ce', font=font)
        for column, image in enumerate(images):
            x = 92 + column * rig.SIZE
            draw.rectangle((x, y, x + 126, y + 126), fill='#e2d8ca' if column % 2 else '#4c4551')
            draw.line((x, y + rig.ANCHOR[1], x + 126, y + rig.ANCHOR[1]), fill='#857884')
            board.paste(image, (x, y), image)
    board.save(output / 'FramesPreview.png')

    playback = []
    for time_ms in range(0, 2400, 100):
        card = Image.new('RGB', (512, 508), '#302a33')
        draw = ImageDraw.Draw(card)
        for name, images in clips.items():
            idle = name.startswith('Idle')
            x, y = (0 if name.endswith('Left') else 256), (0 if idle else 254)
            draw.rectangle((x + 1, y + 1, x + 254, y + 252), fill='#4c4551')
            duration = FRAME_MS['IDLE' if idle else 'WALK']
            image = images[(time_ms // duration) % len(images)].resize((256, 256), Image.Resampling.NEAREST)
            card.paste(image, (x, y + 8), image)
            draw.text((x + 12, y + 9), name, fill='#f4e6db', font=font)
        playback.append(card)
    playback[0].save(output / 'Locomotion.gif', save_all=True, append_images=playback[1:],
                     duration=100, loop=0, disposal=2, optimize=False)


def export(output):
    clips, reports = render_clips()
    output.mkdir(parents=True, exist_ok=True)
    sheet = Image.new('RGBA', (rig.SIZE * 8, rig.SIZE * len(clips)))
    manifest = {'frameSize': rig.SIZE, 'anchor': list(rig.ANCHOR), 'clips': []}
    for row, ((_, action), (name, images)) in enumerate(zip(CLIPS, clips.items(), strict=True)):
        manifest['clips'].append({'name': name, 'row': row, 'count': len(images), 'frameMs': FRAME_MS[action]})
        for column, image in enumerate(images):
            sheet.paste(image, (column * rig.SIZE, row * rig.SIZE))
    sheet.save(output / 'OneArmedSwordsmanLocomotion.png')
    write_previews(output, clips)
    manifest['artSource'] = 'Codex ImageGen cutout atlases, one independently drawn atlas per direction'
    manifest['animationSource'] = 'Rigged cutout animation composed in Python'
    manifest['sourceSha256'] = {d: sha256((SOURCE / 'Source' / f'{d}.png').read_bytes()).hexdigest()
                                for d in ('LEFT', 'RIGHT')}
    manifest['quality'] = {'status': 'pass', 'duplicateAdjacentPairs': 0, 'mirroredFrames': 0, 'clips': reports}
    (output / 'Locomotion.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    export(Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT)
