"""Compose the two reference-drawn views and export the game PNG frames."""

from dataclasses import dataclass
from hashlib import sha256
import json
from math import atan2, cos, hypot, pi, radians, sin, sqrt
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).parent
SIZE = 128
ANCHOR = (64, 116)
GROUND = ANCHOR[1] - 1
PADDING = 48
STAGE = SIZE + 2 * PADDING
WEAPON_LENGTH = 47
PART_NAMES = ('head', 'chest', 'waist', 'cape', 'upper_arm', 'forearm', 'stump', 'fist',
              'near_thigh', 'near_shin', 'near_boot', 'far_thigh', 'far_shin', 'far_boot',
              'sword', 'attack_head')


def silhouette(image):
    return image.getchannel('A').getbbox()


def isolate_part(cell):
    mask = cell.getchannel('A').point(lambda value: 255 if value >= 148 else 0)
    components = []
    while (data := np.asarray(mask)).max() == 255:
        y, x = np.argwhere(data == 255)[0]
        ImageDraw.floodfill(mask, (int(x), int(y)), 127)
        island = np.asarray(mask) == 127
        components.append((int(island.sum()), island))
        mask = mask.point(lambda value: 0 if value == 127 else value)
        if len(components) == 1 and components[0][0] > (np.asarray(mask) == 255).sum():
            break
    if not components:
        raise ValueError('Empty cutout cell')
    largest = max(components, key=lambda item: item[0])[1]
    clean = cell.copy()
    clean.putalpha(Image.fromarray(largest.astype('uint8') * 255))
    return clean.crop(silhouette(clean))


def atlas(face):
    with Image.open(ROOT / 'Source' / f'{face}.png') as source:
        art = source.convert('RGBA')
    xs = [round(i * art.width / 4) for i in range(5)]
    ys = [round(i * art.height / 4) for i in range(5)]
    return {name: isolate_part(art.crop((xs[x], ys[y], xs[x+1], ys[y+1])))
            for i, name in enumerate(PART_NAMES) for y, x in [divmod(i, 4)]}


def resize(art, scale):
    quantized = art.resize((round(scale * art.width), round(scale * art.height)), Image.Resampling.BOX)
    quantized.putalpha(quantized.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    return quantized


@dataclass(frozen=True)
class Part:
    image: Image.Image
    origin: tuple
    natural_axis: float = pi / 2

    def raster(self, point, turn=0):
        x, y = point[0] + PADDING, point[1] + PADDING
        u, v = self.origin
        c, s = cos(turn), sin(turn)
        reverse = (c, s, u - c*x - s*y, -s, c, v + s*x - c*y)
        return self.image.transform((STAGE, STAGE), Image.Transform.AFFINE, reverse, Image.Resampling.NEAREST)

    def draw(self, stage, point, turn=0):
        layer = self.raster(point, turn)
        stage.alpha_composite(layer)
        return layer

    def aim(self, stage, start, end):
        rotation = atan2(end[1]-start[1], end[0]-start[0]) - self.natural_axis
        return self.draw(stage, start, rotation)


def fitted(art, height, pivot):
    image = resize(art, height / art.height)
    return Part(image, (image.width*pivot[0], image.height*pivot[1]))


def bone(art, proximal, distal, length):
    vx = (distal[0] - proximal[0]) * art.width
    vy = (distal[1] - proximal[1]) * art.height
    image = resize(art, length / hypot(vx, vy))
    pivot = (image.width*proximal[0], image.height*proximal[1])
    axis = atan2((distal[1]-proximal[1])*image.height, (distal[0]-proximal[0])*image.width)
    return Part(image, pivot, axis)


def weapon(art):
    image = resize(art, 59 / art.height)
    _, _, _, bottom = silhouette(image)
    tip_xs = np.flatnonzero(np.asarray(image.getchannel('A'))[bottom-1])
    tip_x = int(tip_xs[len(tip_xs)//2]) + 0.5
    tip_y = bottom - 0.5
    return Part(image, (tip_x, tip_y - WEAPON_LENGTH))


FEMUR = 17.5
SHIN = 16.5
HUMERUS = 18.0
FOREARM = 18.0


def assemble_parts(face, source):
    l = face == 'LEFT'
    look = (0.57, 0.86) if l else (0.44, 0.86)
    cape_pin = (0.30, 0.10) if l else (0.73, 0.10)
    boot_pin = (0.73, 0.31) if l else (0.27, 0.31)
    return {
        'head': fitted(source['head'], 30, look),
        'attack_head': fitted(source['attack_head'], 30, look),
        'chest': fitted(source['chest'], 32 if l else 33, (0.51, 0.46)),
        'waist': fitted(source['waist'], 25, (0.49, 0.30)),
        'cape': fitted(source['cape'], 52, cape_pin),
        'upper_arm': bone(source['upper_arm'], (0.55, 0.18), (0.35 if l else 0.43, 0.88), HUMERUS),
        'forearm': bone(source['forearm'], (0.55, 0.19), (0.27 if l else 0.39, 0.86), FOREARM),
        'stump': fitted(source['stump'], 19, (0.51, 0.25)),
        'fist': fitted(source['fist'], 12, (0.50, 0.52)),
        'near_thigh': bone(source['near_thigh'], (0.68 if l else 0.56, 0.12),
                           (0.37 if l else 0.62, 0.81), FEMUR),
        'far_thigh': bone(source['far_thigh'], (0.66 if l else 0.48, 0.12),
                          (0.36 if l else 0.62, 0.81), FEMUR),
        'near_shin': bone(source['near_shin'], (0.51, 0.12), (0.46 if l else 0.43, 0.90), SHIN),
        'far_shin': bone(source['far_shin'], (0.54, 0.12), (0.43, 0.89), SHIN),
        'near_boot': fitted(source['near_boot'], 13, boot_pin),
        'far_boot': fitted(source['far_boot'], 13, boot_pin),
    }


def clamp_palette(parts, sword):
    palette_parts = [p for view in parts.values() for p in view.values()] + [sword]
    body_colors = np.concatenate([np.asarray(p.image)[np.asarray(p.image)[:,:,3] == 255][:,:3]
                                  for p in palette_parts], axis=0)
    rgb = Image.fromarray(body_colors.reshape(1, -1, 3))
    palette = rgb.quantize(colors=96, method=Image.Quantize.MEDIANCUT)
    for part in palette_parts:
        alpha = part.image.getchannel('A')
        colors = part.image.convert('RGB').quantize(palette=palette, dither=Image.Dither.NONE)
        part.image.paste(colors.convert('RGB'), mask=alpha)


def offset(point, dx, dy):
    return point[0] + dx, point[1] + dy


def joint(root, tip, length1, length2, bend):
    dx, dy = tip[0] - root[0], tip[1] - root[1]
    d = hypot(dx, dy)
    if not abs(length1 - length2) < d < length1 + length2:
        raise ValueError(f'Unreachable joint {root} → {tip} (distance {d:.1f})')
    ax = (length1*length1 - length2*length2 + d*d) / (2*d)
    ay = bend * sqrt(length1*length1 - ax*ax)
    return (root[0] + (ax*dx - ay*dy)/d, root[1] + (ax*dy + ay*dx)/d)


@dataclass(frozen=True)
class Foot:
    x: int
    bottom: int = GROUND
    roll: int = 0


@dataclass(frozen=True)
class Pose:
    chest: tuple
    hips: tuple
    neck: tuple
    near: Foot
    far: Foot
    grip: tuple
    blade: int
    cape: int = 0
    head: int = 0
    elbow: int | None = None
    wrist: tuple | None = None
    fist_turn: float | None = None


LEFT = {
    'IDLE': [
        Pose((64,68),(64,83),(62,55),Foot(59),Foot(75),(39,68),90),
        Pose((64,67),(64,83),(62,54),Foot(59),Foot(75),(39,68),90,1,-1),
        Pose((63,66),(64,82),(61,53),Foot(59),Foot(75),(39,68),90,-1,-2),
        Pose((63,67),(64,83),(61,54),Foot(59),Foot(75),(39,68),90,0,-1),
    ],
    'WALK': [
        Pose((63,69),(64,84),(60,56),Foot(48),Foot(75),(42,63),5,1),
        Pose((62,70),(64,85),(59,57),Foot(54),Foot(77,111,-24),(42,64),6,3,-1),
        Pose((63,67),(64,83),(60,54),Foot(61),Foot(65,103,-14),(43,62),2,-1),
        Pose((63,66),(64,82),(60,53),Foot(68),Foot(52,107,16),(43,61),3,1),
        Pose((62,69),(64,84),(59,56),Foot(73),Foot(49),(42,63),5,2),
        Pose((62,70),(64,85),(59,57),Foot(78,111,-24),Foot(54),(42,64),6,4,-1),
        Pose((63,67),(64,83),(60,54),Foot(64,103,-17),Foot(61),(42,62),2,0),
        Pose((63,66),(64,82),(60,53),Foot(53,107,18),Foot(68),(43,61),3,-2),
    ],
    'ATTACK': [
        Pose((64,68),(64,83),(61,55),Foot(58),Foot(75),(49,68),139,0,1),
        Pose((66,68),(65,83),(64,54),Foot(57),Foot(77),(60,51),-14,-3,4),
        Pose((67,66),(65,83),(64,52),Foot(55),Foot(77),(65,42),-48,-4,6),
        Pose((62,67),(64,84),(60,54),Foot(49),Foot(78),(55,54),-119,2,-3,1),
        Pose((59,72),(64,87),(57,59),Foot(47),Foot(80),(51,64),177,-7,-5),
        Pose((55,76),(63,91),(52,64),Foot(44),Foot(81),(48,75),150,-12,-9),
        Pose((56,78),(64,91),(53,65),Foot(44),Foot(81),(48,78),135,-9,-7),
        Pose((60,72),(64,87),(57,58),Foot(50),Foot(78),(45,73),130,-5,-4),
        Pose((64,68),(64,83),(62,55),Foot(57),Foot(75),(39,68),90,0,0),
    ],
}

RIGHT = {
    'IDLE': [
        Pose((63,68),(64,83),(66,55),Foot(54),Foot(69),(92,68),90),
        Pose((63,67),(64,82),(66,54),Foot(54),Foot(69),(92,68),90,-1,1),
        Pose((64,66),(64,82),(67,53),Foot(54),Foot(69),(92,68),90,1,2),
        Pose((64,67),(64,83),(67,54),Foot(54),Foot(69),(92,68),90,0,1),
    ],
    'WALK': [
        Pose((65,69),(64,84),(68,56),Foot(52),Foot(80),(85,63),174,-1),
        Pose((66,70),(64,85),(69,57),Foot(49,111,25),Foot(74),(86,64),173,-4,1),
        Pose((65,67),(64,83),(68,54),Foot(62,102,15),Foot(67),(85,61),177,0),
        Pose((65,66),(64,82),(68,53),Foot(75,107,-16),Foot(60),(84,60),176,-2),
        Pose((66,69),(64,84),(69,56),Foot(81),Foot(51),(86,63),175,-2),
        Pose((65,70),(64,85),(69,57),Foot(74),Foot(49,111,26),(85,64),173,-5,1),
        Pose((66,67),(64,83),(70,54),Foot(68),Foot(61,103,12),(85,61),177,-1),
        Pose((65,66),(64,82),(69,53),Foot(60),Foot(75,106,-18),(84,61),176,2),
    ],
    'ATTACK': [
        Pose((63,68),(64,83),(66,55),Foot(54),Foot(70),(84,68),40,0,-1),
        Pose((61,68),(63,83),(64,54),Foot(52),Foot(71),(67,51),196,4,-3),
        Pose((60,65),(63,82),(63,51),Foot(50),Foot(72),(64,42),230,5,-6),
        Pose((65,67),(64,84),(68,53),Foot(48),Foot(76),(76,53),302,-1,4,-1),
        Pose((69,71),(65,86),(72,57),Foot(47),Foot(80),(78,64),-7,7,6),
        Pose((73,75),(65,90),(76,63),Foot(46),Foot(83),(84,74),34,11,10),
        Pose((72,78),(65,91),(75,65),Foot(46),Foot(83),(84,78),47,8,8),
        Pose((68,72),(64,86),(72,58),Foot(49),Foot(77),(86,73),48,4,3),
        Pose((63,68),(64,83),(66,55),Foot(53),Foot(71),(92,68),90,0,0),
    ],
}

POSES = {'LEFT': LEFT, 'RIGHT': RIGHT}
TIMING = {'IDLE': (310, 260, 320, 250), 'WALK': (100,)*8, 'ATTACK': (80, 100, 130, 50, 45, 85, 90, 95, 125)}


def bounds(layer):
    return tuple(value - PADDING for value in silhouette(layer))


def require_margin(box, item):
    if min(box[:2]) < 1 or max(box[2:]) > SIZE - 1:
        raise ValueError(f'{item} reaches a frame edge: {box}')


def render_leg(stage, art, side, hip, foot, facing):
    layer = Image.new('RGBA', stage.size)
    boot = art[side + '_boot']
    rotation = radians(foot.roll)
    probe = boot.raster((foot.x, 64), rotation)
    bottom = bounds(probe)[3] - 1
    ankle = (foot.x, 64 + foot.bottom - bottom)
    knee = joint(hip, ankle, FEMUR, SHIN, 1 if facing == 'LEFT' else -1)
    art[side + '_thigh'].aim(layer, hip, knee)
    art[side + '_shin'].aim(layer, knee, ankle)
    boot_box = bounds(boot.draw(layer, ankle, rotation))
    if boot_box[3] - 1 != foot.bottom or bounds(layer)[3] - 1 > GROUND:
        raise ValueError(f'{side} foot does not respect its intended contact: {boot_box}, {bounds(layer)}')
    stage.alpha_composite(layer)
    return {'hip': hip, 'ankle': ankle, 'knee': [round(v, 3) for v in knee],
            'bottomY': foot.bottom, 'grounded': foot.bottom == GROUND}


def make_frame(face, action, number, pose, art, sword):
    stage = Image.new('RGBA', (STAGE, STAGE))
    left = face == 'LEFT'
    cape_root = offset(pose.chest, 8 if left else -9, -11)
    art['cape'].draw(stage, cape_root, radians(pose.cape))
    far = render_leg(stage, art, 'far', offset(pose.hips, -6 if left else 6, 1), pose.far, face)
    near = render_leg(stage, art, 'near', offset(pose.hips, 6 if left else -6, 1), pose.near, face)
    shoulder = offset(pose.chest, 8 if left else 6, -6)
    cuff = offset(pose.chest, -10 if left else -10, -4 if left else -3)
    bend = pose.elbow if pose.elbow is not None else (-1 if left else 1)
    wrist = pose.wrist if pose.wrist is not None else pose.grip
    elbow = joint(shoulder, wrist, HUMERUS, FOREARM, bend)
    if not left:
        art['upper_arm'].aim(stage, shoulder, elbow)
    art['chest'].draw(stage, pose.chest, radians(pose.head / 2))
    art['waist'].draw(stage, pose.hips, radians(pose.head / 4))
    art['stump'].draw(stage, cuff, radians(18 if left else 10))
    if left:
        art['upper_arm'].aim(stage, shoulder, elbow)
    blade_turn = radians(pose.blade - 90)
    legacy_walk = action == 'WALK' and pose.wrist is None
    if legacy_walk:
        blade = sword.draw(stage, pose.grip, blade_turn)
    head = 'attack_head' if action == 'ATTACK' and 0 < number < len(TIMING['ATTACK']) - 1 else 'head'
    art[head].draw(stage, pose.neck, radians(pose.head))
    if not legacy_walk:
        blade = sword.draw(stage, pose.grip, blade_turn)
    art['forearm'].aim(stage, elbow, wrist)
    fist_turn = radians(pose.fist_turn) if pose.fist_turn is not None else blade_turn
    art['fist'].draw(stage, pose.grip, fist_turn)
    sword_box = bounds(blade)
    require_margin(sword_box, 'sword')
    require_margin(bounds(stage), 'sprite')
    if sword_box[3] > ANCHOR[1] or bounds(stage)[3] != ANCHOR[1]:
        raise ValueError(f'Ground line is crossed or missed: {sword_box}, {bounds(stage)}')
    blade_tip = offset(pose.grip, WEAPON_LENGTH * cos(radians(pose.blade)), WEAPON_LENGTH * sin(radians(pose.blade)))
    record = {'opaqueBounds': bounds(stage), 'swordBounds': sword_box,
              'swordTip': [round(v, 3) for v in blade_tip], 'swordAngle': pose.blade,
              'leftHand': pose.grip, 'leftWrist': wrist, 'rightHand': None, 'rightStump': cuff,
              'feet': {'left': near if left else far, 'right': far if left else near}}
    return stage.crop((PADDING, PADDING, PADDING+SIZE, PADDING+SIZE)), record


def signature(image):
    crop = image.crop(silhouette(image))
    pixels = np.asarray(crop).copy()
    pixels[pixels[:,:,3] == 0] = 0
    return (crop.size, pixels.tobytes())


def phase_for(index):
    return ('contact', 'toe-off', 'passing', 'forward swing')[index % 4]


def write_previews(images):
    font = ImageFont.load_default(size=14)
    columns = max(map(len, images.values()))
    board = Image.new('RGB', (96 + columns*SIZE, 48 + len(images)*SIZE), '#24212a')
    draw = ImageDraw.Draw(board)
    draw.text((14, 16), 'ONE-ARMED SWORDSMAN  /  128 x 128', fill='#ede2d8', font=font)
    for row, ((face, action), seq) in enumerate(images.items()):
        y = 43 + row * SIZE
        draw.text((14,y+46), face, fill='#e7af83', font=font)
        draw.text((14,y+66), action, fill='#ede2d8', font=font)
        for index, frame in enumerate(seq):
            x = 92 + index * SIZE
            draw.rectangle((x,y,x+126,y+126), fill='#514b57' if index % 2 else '#45404b')
            draw.line((x,y+ANCHOR[1],x+126,y+ANCHOR[1]), fill='#6a626e')
            board.paste(frame, (x,y), frame)
    board.save(ROOT / 'Preview.png')

    walk = []
    for index in range(8):
        card = Image.new('RGB', (256, 169), '#4c4551')
        draw = ImageDraw.Draw(card)
        for x, face in ((0,'LEFT'),(128,'RIGHT')):
            frame = images[face, 'WALK'][index]
            card.paste(frame, (x,21), frame)
            draw.text((x+9, 5), face, fill='#ede2d8', font=font)
        draw.line((0,21+ANCHOR[1],255,21+ANCHOR[1]), fill='#867887')
        lead = 'LEFT FOOT' if index < 4 else 'RIGHT FOOT'
        draw.text((9,149), f'{lead}  /  {phase_for(index)}', fill='#ede2d8', font=font)
        walk.append(card)
    walk[0].save(ROOT / 'WalkSlow.gif', save_all=True, append_images=walk[1:],
                 duration=330, loop=0, disposal=2, optimize=False)


def render():
    sources = {face: atlas(face) for face in POSES}
    pieces = {face: assemble_parts(face, source) for face, source in sources.items()}
    sword = weapon(sources['LEFT']['sword'])
    clamp_palette(pieces, sword)
    images = {}
    audit = {'frames': {}, 'adjacentPairs': {}, 'weaponArt': 'Source/LEFT.png, row 4 column 3',
             'gripToTipPx': WEAPON_LENGTH, 'swordArtRaster': list(sword.image.size)}
    clips = {'frameSize': [SIZE,SIZE], 'anchor': ANCHOR, 'groundPixelY': GROUND,
             'sampling': 'nearest', 'directions': {}}
    for face, actions in POSES.items():
        clips['directions'][face] = {}
        for action, poses in actions.items():
            loop = action != 'ATTACK'
            path = f'{face}/{action}'
            frames = images[face, action] = []
            clip = {'loop': loop, 'frames': []}
            clips['directions'][face][action] = clip
            for i, pose in enumerate(poses):
                try:
                    image, geometry = make_frame(face, action, i, pose, pieces[face], sword)
                except ValueError as exc:
                    raise ValueError(f'{path}/{i:02}: {exc}') from exc
                if set(image.getchannel('A').tobytes()) != {0,255}:
                    raise ValueError(f'{path}/{i:02} is not binary alpha')
                file = f'{path}/{i:02}.png'
                audit['frames'][file] = geometry
                entry = {'file': file, 'durationMs': TIMING[action][i]}
                if action == 'IDLE':
                    if geometry['swordTip'] != [poses[0].grip[0], GROUND]:
                        raise ValueError(f'{file} has a moving sword point')
                elif action == 'WALK':
                    lead, other = ('left','right') if i < 4 else ('right','left')
                    step = phase_for(i)
                    entry.update({'plantedFoot': lead, 'otherFoot': other, 'phase': step})
                    if not geometry['feet'][lead]['grounded'] or geometry['feet'][other]['grounded'] != (step == 'contact'):
                        raise ValueError(f'{file} has the wrong footfall for {step}')
                else:
                    entry['phase'] = 'wind-up' if i < 3 else 'descending cut' if i < 6 else 'recovery'
                clip['frames'].append(entry)
                frames.append(image)
            sequence = [(i-1,i) for i in range(1,len(frames))] + ([(len(frames)-1,0)] if loop else [])
            comparison = audit['adjacentPairs'][path] = []
            for a, b in sequence:
                if signature(frames[a]) == signature(frames[b]):
                    raise ValueError(f'{path} {a} → {b} only translates or repeats')
                changes = np.any(np.asarray(frames[a]) != np.asarray(frames[b]), axis=2).sum()
                comparison.append({'pair': [a,b], 'differentPixels': int(changes)})
        tip_y = [audit['frames'][f'{face}/ATTACK/{i:02}.png']['swordTip'][1] for i in (2,3,4,5)]
        if any(previous >= current for previous, current in zip(tip_y, tip_y[1:])) or tip_y[0] >= actions['ATTACK'][2].neck[1] - 24:
            raise ValueError(f'{face} cut did not start overhead and descend: {tip_y}')

    left = {signature(frame) for (face, _), seq in images.items() if face == 'LEFT' for frame in seq}
    for (face, action), frames in images.items():
        if face == 'RIGHT' and any(signature(frame) in left or signature(frame.transpose(Image.Transpose.FLIP_LEFT_RIGHT)) in left
                                   for frame in frames):
            raise ValueError(f'{action} contains a copied or mirrored pose')
        folder = ROOT / face / action
        folder.mkdir(parents=True, exist_ok=True)
        for i, frame in enumerate(frames):
            frame.save(folder / f'{i:02}.png')
    write_previews(images)
    for name, data in {'Animations': clips, 'QA': audit, 'Source/Provenance': {
        'artSource': 'host_image', 'generation': 'Codex ImageGen cutout atlases drawn from the original reference',
        'animation': 'independent key poses and rigid cutout composition at native game resolution',
        'reference': '../../SourceSheet.png', 'partsSha256': {face: sha256((ROOT / 'Source' / f'{face}.png').read_bytes()).hexdigest()
                                                             for face in POSES},
    }}.items():
        (ROOT / f'{name}.json').write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    print(f'{sum(map(len, images.values()))} PNG frames / {len(images)} clips / '
          f'{sum(map(len, audit["adjacentPairs"].values()))} adjacent pairs')


if __name__ == '__main__':
    render()
