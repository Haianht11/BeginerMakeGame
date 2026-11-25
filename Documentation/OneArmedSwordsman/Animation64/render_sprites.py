"""Draw both views of the original one-armed swordsman on the final 64 px grid."""

from dataclasses import dataclass
from hashlib import sha256
import json
from math import atan2, cos, hypot, radians, sin, sqrt
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


SIZE = 64
ANCHOR = (32, 57)  # Ground boundary, immediately below the sole pixels.
GROUND = ANCHOR[1] - 1
ROOT = Path(__file__).parent
COLORS = {
    'ink': '#14131a', 'black': '#211e27', 'deep': '#302a32', 'shadow': '#3b343b',
    'plate': '#514b53', 'light': '#69636a', 'rim': '#807c81', 'scarfd': '#514b50',
    'scarf': '#696366', 'scarfh': '#8a8182', 'hair': '#27242e', 'hairh': '#403946',
    'hairl': '#554a56', 'skin': '#eab184', 'skind': '#bd805f', 'scar': '#93595a',
    'eye': '#f6e4cf', 'wrapd': '#80675c', 'wrap': '#b69c86', 'wraph': '#d1b69a',
    'belt': '#60463c', 'belth': '#89664e', 'bronze': '#b18a60', 'brass': '#d0ad7e',
    'steel0': '#383741', 'steel1': '#666675', 'steel2': '#9598a6', 'steel3': '#cbd0d7',
}


def add(a, b):
    return (a[0] + b[0], a[1] + b[1])


def turn(p, angle):
    c, s = cos(angle), sin(angle)
    return (p[0] * c - p[1] * s, p[0] * s + p[1] * c)


def pixel(p):
    return tuple(round(value, 7) for value in p)


class Canvas:
    def __init__(self):
        self.image = Image.new('RGBA', (SIZE, SIZE))
        self.draw = ImageDraw.Draw(self.image)

    def shape(self, origin, points, color, angle=0):
        world = [pixel(add(origin, turn(p, angle))) for p in points]
        if any(not (0 <= x < SIZE and 0 <= y < SIZE) for x, y in world):
            raise ValueError(f'{color} polygon extends off canvas at {world}')
        self.draw.polygon(world, fill=COLORS[color])

    def line(self, origin, points, color, angle=0):
        self.draw.line([pixel(add(origin, turn(p, angle))) for p in points], fill=COLORS[color])

    def dot(self, origin, point, color):
        self.draw.point(add(origin, point), fill=COLORS[color])


# Each view has its own shapes and screen coordinates. The visible left arm is
# the near arm in LEFT. The right stump is the near shoulder in RIGHT.
def head_left(c, o, angry):
    c.shape(o, [(-9,7),(-7,4),(-10,4),(-6,2),(-7,0),(-2,1),(0,-2),(2,0),(6,-2),
                (5,1),(9,1),(7,4),(9,6),(7,10),(5,12),(3,13),(-4,13),(-7,11),(-9,10)], 'ink')
    c.shape(o, [(-8,5),(-4,3),(-6,1),(-2,2),(0,0),(2,2),(5,0),(5,2),(7,2),(6,4),
                (8,5),(6,9),(4,10),(-4,9),(-7,8)], 'hair')
    c.shape(o, [(-7,5),(-5,3),(-4,3),(-5,5),(-2,4),(0,2),(1,3),(0,5),(-3,6)], 'hairh')
    c.line(o, [(1,5),(4,3),(6,3)], 'hairh')
    c.shape(o, [(-6,8),(-3,7),(0,6),(1,8),(4,7),(6,7),(6,9),(4,11),(2,12),(-4,12),(-6,11),(-8,10)], 'skind')
    c.shape(o, [(-6,8),(-3,8),(-1,7),(0,9),(3,8),(5,7),(5,9),(3,10),(2,11),(-4,11),(-6,10),(-8,10)], 'skin')
    c.line(o, [(-6,8),(-4,8),(-3,7 if angry else 8)], 'ink')
    c.dot(o, (-5,9), 'eye')
    c.dot(o, (-6,9), 'ink')
    c.dot(o, (-6,10), 'scar')
    c.line(o, [(-5,11),(-3,11)], 'ink')
    c.dot(o, (5,8), 'skind')


def head_right(c, o, angry):
    c.shape(o, [(-9,5),(-7,3),(-10,2),(-5,2),(-6,-1),(-2,0),(1,-2),(3,0),(7,-1),
                (6,2),(9,3),(8,6),(9,8),(10,9),(8,11),(6,13),(-1,13),(-5,11),(-7,10)], 'ink')
    c.shape(o, [(-8,5),(-6,3),(-4,3),(-5,0),(-2,1),(1,0),(3,1),(6,0),(5,3),(8,4),
                (7,6),(5,8),(2,10),(-4,9),(-6,8)], 'hair')
    c.shape(o, [(-7,4),(-4,3),(-3,4),(0,3),(2,1),(3,2),(2,4),(-1,5),(-5,6)], 'hairh')
    c.line(o, [(-4,4),(-2,4),(0,3)], 'hairl')
    c.line(o, [(1,6),(4,5),(6,3)], 'hairh')
    c.shape(o, [(-4,8),(-2,7),(0,8),(3,7),(5,6),(7,7),(7,8),(9,9),(7,10),(6,12),(0,12),(-3,10)], 'skind')
    c.shape(o, [(-3,8),(-2,8),(-1,9),(2,8),(5,7),(7,7),(7,8),(9,9),(7,9),(6,11),(1,11),(0,10),(-2,10)], 'skin')
    c.line(o, [(4,7 if angry else 8),(6,8),(7,8)], 'ink')
    c.dot(o, (5,9), 'eye')
    c.dot(o, (6,9), 'ink')
    c.dot(o, (8,9), 'scar')
    c.line(o, [(5,11),(7,11)], 'ink')
    c.dot(o, (-2,9), 'skind')


def cape_left(c, chest, flare):
    c.shape(chest, [(1,-4),(7,-5),(10,-2),(12+flare,3),(14+flare,11),(12+flare,10),
                    (12+flare,14),(9+flare,12),(8+flare,15),(6,12),(4,13),(2,8),(-1,4)], 'ink')
    c.shape(chest, [(3,-3),(7,-4),(9,-1),(11+flare,4),(12+flare,10),(10+flare,9),(10+flare,12),
                    (8,10),(7,12),(5,9),(3,8),(2,4)], 'deep')
    c.shape(chest, [(5,-3),(8,-3),(9,1),(11+flare,8),(8,5),(7,1),(5,0)], 'shadow')


def cape_right(c, chest, flare):
    c.shape(chest, [(-1,-5),(-7,-4),(-10,-2),(-12-flare,3),(-16-flare,11),(-13-flare,10),
                    (-13-flare,14),(-10-flare,12),(-10,14),(-7,11),(-6,13),(-4,8),(0,3)], 'ink')
    c.shape(chest, [(-3,-4),(-7,-3),(-9,-1),(-11-flare,3),(-14-flare,10),(-11-flare,8),
                    (-11-flare,12),(-9,10),(-8,11),(-6,8),(-5,6),(-1,1)], 'deep')
    c.shape(chest, [(-7,-3),(-4,-4),(-5,1),(-8,4),(-11-flare,7),(-9,2),(-8,-1)], 'shadow')


def torso_left(c, chest, waist):
    c.shape(waist, [(-8,-3),(-6,-6),(6,-6),(8,-3),(7,2),(3,4),(-1,2),(-5,4),(-8,1)], 'ink')
    c.shape(waist, [(-7,-3),(6,-4),(7,-2),(6,1),(3,2),(1,0),(-2,2),(-6,3),(-7,0)], 'plate')
    c.line(waist, [(-5,2),(-2,1),(0,0)], 'light')
    c.shape(chest, [(-7,-4),(-3,-5),(6,-5),(8,-2),(7,3),(5,7),(5,9),(-6,9),(-7,5),(-9,0)], 'ink')
    c.shape(chest, [(-7,-3),(-3,-4),(5,-4),(7,-2),(6,2),(4,6),(4,8),(-5,8),(-6,4),(-8,0)], 'plate')
    c.shape(chest, [(0,-2),(4,0),(5,2),(3,6),(-3,7),(-5,5),(-2,4)], 'shadow')
    c.line(chest, [(-6,1),(-3,3),(0,3)], 'light')
    c.shape(waist, [(-7,-3),(-4,-2),(7,-3),(7,-1),(-3,0),(-7,-1)], 'belt')
    c.line(waist, [(-6,-3),(-3,-2),(5,-3)], 'belth')
    c.shape(waist, [(-6,-2),(-3,-2),(-3,0),(-6,0)], 'bronze')
    c.dot(waist, (-4,-1), 'ink')


def torso_right(c, chest, waist):
    c.shape(waist, [(-8,-3),(-7,-6),(5,-6),(7,-3),(7,1),(5,3),(1,1),(-3,4),(-7,2)], 'ink')
    c.shape(waist, [(-7,-3),(-5,-4),(6,-4),(6,1),(4,2),(1,0),(-3,3),(-6,1)], 'plate')
    c.line(waist, [(-5,2),(-3,2),(0,0)], 'light')
    c.shape(chest, [(-7,-3),(-4,-5),(4,-5),(7,-3),(9,-1),(8,3),(6,5),(5,9),(-6,9),(-7,6),(-9,0)], 'ink')
    c.shape(chest, [(-7,-2),(-4,-4),(4,-4),(6,-2),(8,-1),(7,2),(5,5),(4,8),(-5,8),(-6,5),(-8,0)], 'plate')
    c.shape(chest, [(-6,-2),(-3,-3),(1,0),(4,2),(6,2),(3,6),(-2,7),(-4,4)], 'shadow')
    c.line(chest, [(2,0),(5,1),(7,-1)], 'light')
    c.shape(waist, [(-7,-3),(1,-2),(7,-3),(7,-1),(1,0),(-7,-1)], 'belt')
    c.line(waist, [(-5,-3),(1,-2),(7,-3)], 'belth')
    c.shape(waist, [(3,-2),(6,-2),(6,0),(3,0)], 'bronze')
    c.dot(waist, (5,-1), 'ink')


def scarf_left(c, o, flutter):
    c.shape(o, [(-7,-4),(-3,-3),(3,-4),(7,-6),(9,-5),(9,-2),(11,1+flutter),(7,0),
                (7,2+flutter),(3,1),(-2,0),(-6,-1)], 'ink')
    c.shape(o, [(-6,-4),(-2,-3),(3,-4),(7,-5),(8,-4),(7,-2),(9,0+flutter),(6,-1),
                (6,1+flutter),(2,0),(-1,-1),(-5,-2)], 'scarf')
    c.line(o, [(-5,-3),(0,-2),(3,-3),(6,-4)], 'scarfh')


def scarf_right(c, o, flutter):
    c.shape(o, [(-9,-5),(-6,-6),(-3,-4),(3,-3),(7,-5),(8,-3),(6,-1),(2,0),(-3,1),
                (-5,3+flutter),(-7,1),(-10,2+flutter),(-9,-1)], 'ink')
    c.shape(o, [(-8,-5),(-6,-5),(-3,-3),(2,-2),(7,-4),(6,-2),(2,-1),(-2,0),(-5,1+flutter),
                (-6,0),(-9,1+flutter),(-8,-1)], 'scarf')
    c.line(o, [(-6,-4),(-3,-2),(2,-2),(5,-3)], 'scarfh')


def stub_left(c, shoulder):
    c.shape(shoulder, [(2,-2),(-1,-2),(-4,0),(-5,3),(-4,5),(-1,4),(1,2)], 'ink')
    c.shape(shoulder, [(1,-1),(-1,-1),(-3,0),(-4,2),(-2,3),(0,2)], 'shadow')
    c.shape(shoulder, [(-4,1),(-2,2),(-3,4),(-4,4),(-5,3)], 'wrapd')
    c.line(shoulder, [(-4,2),(-3,3),(-4,3)], 'wraph')


def stub_right(c, shoulder):
    c.shape(shoulder, [(-3,-3),(1,-3),(3,-1),(2,2),(-1,6),(-4,7),(-6,5),(-6,2),(-5,-1)], 'ink')
    c.shape(shoulder, [(-3,-2),(1,-2),(2,-1),(1,2),(-1,3),(-5,2),(-5,0)], 'plate')
    c.line(shoulder, [(-4,0),(-2,-2),(0,-2)], 'rim')
    c.shape(shoulder, [(-5,2),(-3,3),(0,2),(0,4),(-2,6),(-4,6),(-5,5)], 'wrapd')
    c.line(shoulder, [(-5,3),(-3,4),(-1,3)], 'wraph')
    c.line(shoulder, [(-4,5),(-2,5),(-1,4)], 'wrap')


def joint(a, end, upper, lower, bend):
    """Two fixed-length bones with the specified screen-space bend side."""
    dx, dy = end[0] - a[0], end[1] - a[1]
    distance = hypot(dx, dy)
    if not abs(upper - lower) < distance < upper + lower:
        raise ValueError(f'Unreachable pose: {a} to {end}, reach {upper + lower}')
    ux, uy = dx / distance, dy / distance
    along = (upper * upper - lower * lower + distance * distance) / (2 * distance)
    across = bend * sqrt(max(0, upper * upper - along * along))
    return (a[0] + ux * along - uy * across, a[1] + uy * along + ux * across)


def direction(a, b):
    return atan2(b[1] - a[1], b[0] - a[0])


def upper_arm(c, shoulder, elbow, near):
    angle = direction(shoulder, elbow)
    c.shape(shoulder, [(-3,-2),(0,-4),(4,-4),(7,-3),(9,-2),(9,2),(6,3),(1,4),(-2,3)], 'ink', angle)
    c.shape(shoulder, [(-2,-1),(0,-3),(4,-3),(6,-2),(6,2),(1,3),(-1,2)], 'plate' if near else 'shadow', angle)
    c.line(shoulder, [(-1,-1),(0,-2),(3,-2)], 'rim' if near else 'light', angle)
    c.line(shoulder, [(5,-2),(5,2)], 'belth' if near else 'belt', angle)
    c.shape(shoulder, [(7,-2),(8,-1),(8,1),(7,2)], 'shadow', angle)


def forearm(c, elbow, fist, near):
    angle = direction(elbow, fist)
    c.shape(elbow, [(-1,-2),(2,-3),(8,-2),(9,-1),(9,2),(6,3),(1,3),(-1,2)], 'ink', angle)
    c.shape(elbow, [(1,-2),(4,-2),(6,-1),(6,2),(2,2),(0,1)], 'skind' if near else 'wrapd', angle)
    c.line(elbow, [(1,-1),(4,-1),(5,0)], 'skin' if near else 'skind', angle)
    c.shape(elbow, [(5,-2),(8,-1),(8,1),(6,2),(5,1)], 'belt', angle)
    c.line(elbow, [(6,-1),(6,1)], 'belth', angle)


# One rigid weapon. Angle and grip position are its only animated parameters.
# The hilt centre is the origin; the tip is 25 px out and the pommel is 5 px back.
SWORD_OUTLINE = [(-5,-1),(-4,-2),(2,-2),(3,-4),(5,-5),(6,-5),(6,-4),(10,-4),
                 (22,-3),(25,0),(22,3),(10,4),(6,4),(6,5),(4,4),(2,2),(-4,2),(-5,1)]


def sword(c, grip, angle):
    c.shape(grip, SWORD_OUTLINE, 'ink', angle)
    c.shape(grip, [(5,-2),(9,-3),(22,-2),(24,0),(22,2),(9,3),(6,2)], 'steel2', angle)
    c.shape(grip, [(7,-1),(11,-2),(21,-1),(23,0),(21,1),(11,2),(7,1)], 'steel0', angle)
    c.line(grip, [(8,0),(20,0),(22,0)], 'steel1', angle)
    c.line(grip, [(8,-3),(11,-3),(21,-2),(23,-1)], 'steel3', angle)
    c.line(grip, [(-4,0),(3,0)], 'belth', angle)
    c.shape(grip, [(3,-3),(5,-4),(5,-2),(4,0),(5,3),(4,3),(2,1),(2,-1)], 'bronze', angle)
    c.line(grip, [(3,-2),(4,-3)], 'brass', angle)
    c.line(grip, [(-4,-1),(-4,1)], 'bronze', angle)


def hand(c, grip, angle):
    c.shape(grip, [(-2,-2),(1,-2),(2,-1),(2,1),(1,2),(-2,2),(-3,1),(-3,-1)], 'ink', angle)
    c.shape(grip, [(-2,-1),(0,-1),(1,0),(1,1),(-2,1)], 'skind', angle)
    c.line(grip, [(-2,-1),(-1,-1),(0,0)], 'skin', angle)


BOOT_LEFT = [(-3,-2),(2,-2),(3,0),(3,2),(2,3),(-6,3),(-7,2),(-6,1),(-4,0)]
BOOT_RIGHT = [(-3,-2),(2,-2),(3,0),(6,1),(7,2),(6,3),(-2,3),(-3,2)]


@dataclass(frozen=True)
class Foot:
    x: int
    bottom: int = GROUND
    roll: int = 0


def leg(c, hip, foot, face, near):
    boot = BOOT_LEFT if face == 'LEFT' else BOOT_RIGHT
    roll = radians(foot.roll)
    ankle = (foot.x, foot.bottom - max(turn(v, roll)[1] for v in boot))
    knee = joint(hip, ankle, 8.0, 7.6, 1 if face == 'LEFT' else -1)
    thigh = direction(hip, knee)
    shin = direction(knee, ankle)
    c.shape(hip, [(-1,-3),(1,-4),(6,-3),(9,-2),(9,2),(7,3),(1,4),(-1,3)], 'ink', thigh)
    c.shape(hip, [(0,-2),(2,-3),(6,-2),(7,-1),(7,2),(4,2),(1,3),(0,2)], 'plate' if near else 'deep', thigh)
    c.line(hip, [(1,-2),(4,-2),(5,-1)], 'light' if near else 'shadow', thigh)
    c.shape(knee, [(-2,-2),(0,-3),(3,-3),(6,-2),(7.8,-1),(7.8,1),(5,2),(0,3),(-2,2)], 'ink', shin)
    c.shape(knee, [(-1,-1),(0,-2),(2,-2),(3,-1),(2,1),(0,2),(-1,1)], 'light' if near else 'shadow', shin)
    c.shape(knee, [(3,-2),(6,-1),(7,-1),(7,1),(6,1),(4,2),(3,1)], 'plate' if near else 'deep', shin)
    c.line(knee, [(4,1),(6,0),(7,0)], 'light' if near else 'shadow', shin)
    c.shape(ankle, boot, 'ink', roll)
    if face == 'LEFT':
        c.shape(ankle, [(-2,-1),(1,-1),(2,1),(1,2),(-6,2),(-4,1),(-2,0)], 'plate' if near else 'deep', roll)
        c.line(ankle, [(-5,1),(-3,1),(-2,0)], 'light' if near else 'shadow', roll)
    else:
        c.shape(ankle, [(-2,-1),(1,-1),(3,0),(5,1),(6,2),(-2,2)], 'plate' if near else 'deep', roll)
        c.line(ankle, [(2,1),(4,1),(5,2)], 'light' if near else 'shadow', roll)
    return ankle, knee


@dataclass(frozen=True)
class Pose:
    chest: tuple
    waist: tuple
    head: tuple
    near: Foot
    far: Foot
    grip: tuple
    angle: int
    flare: int = 0
    bend: int = 1


def frame(face, action, p):
    c = Canvas()
    leftward = face == 'LEFT'
    cape = cape_left if leftward else cape_right
    cape(c, p.chest, p.flare)
    hip_far = add(p.waist, (-2, 0) if leftward else (2, 0))
    hip_near = add(p.waist, (2, 0) if leftward else (-2, 0))
    _, knee_far = leg(c, hip_far, p.far, face, False)
    _, knee_near = leg(c, hip_near, p.near, face, True)
    shoulder_left = add(p.chest, (2,-1) if leftward else (5,-2))
    shoulder_right = add(p.chest, (-6,-1) if leftward else (-4,0))
    elbow_left = joint(shoulder_left, p.grip, 8.6, 8.7, p.bend)
    angle = radians(p.angle)

    if not leftward:
        upper_arm(c, shoulder_left, elbow_left, False)
    torso = torso_left if leftward else torso_right
    torso(c, p.chest, p.waist)
    scarf = scarf_left if leftward else scarf_right
    scarf(c, p.chest, min(1, max(-1, p.flare)))
    if leftward:
        stub_left(c, shoulder_right)
        upper_arm(c, shoulder_left, elbow_left, True)
    else:
        stub_right(c, shoulder_right)
    if action == 'WALK':
        sword(c, p.grip, angle)
    forearm(c, elbow_left, p.grip, leftward)
    head = head_left if leftward else head_right
    head(c, p.head, action == 'ATTACK')
    if action != 'WALK':
        sword(c, p.grip, angle)
    hand(c, p.grip, angle)

    return c.image, {
        'leftShoulder': shoulder_left, 'rightShoulder': shoulder_right,
        'leftHand': p.grip, 'leftElbow': pixel(elbow_left), 'rightHand': None,
        'swordAngle': p.angle, 'swordTip': pixel(add(p.grip, turn((25, 0), angle))),
        'nearKnee': pixel(knee_near), 'farKnee': pixel(knee_far),
    }


# Four unique breathing drawings, with boots and sword fixed in screen space.
L_IDLE = [
    Pose((33,34),(33,43),(31,22),Foot(33),Foot(38),(19,31),90,0,-1),
    Pose((33,33),(33,43),(31,21),Foot(33),Foot(38),(19,31),90,-1,-1),
    Pose((32,32),(33,42),(30,20),Foot(33),Foot(38),(19,31),90,0,-1),
    Pose((32,33),(33,43),(30,21),Foot(33),Foot(38),(19,31),90,1,-1),
]
R_IDLE = [
    Pose((31,34),(31,43),(33,22),Foot(29),Foot(35),(46,31),90,0,1),
    Pose((31,33),(31,42),(33,21),Foot(29),Foot(35),(46,31),90,-1,1),
    Pose((32,32),(31,42),(34,20),Foot(29),Foot(35),(46,31),90,0,1),
    Pose((32,33),(31,43),(34,21),Foot(29),Foot(35),(46,31),90,1,1),
]

# Contact, rear toe-off, passing, front swing, then the other leg leads.
L_WALK = [
    Pose((32,34),(32,44),(29,22),Foot(23),Foot(37),(22,36),-4,1,-1),
    Pose((31,35),(32,44),(29,23),Foot(26),Foot(39,54,-36),(21,37),-2,0,-1),
    Pose((32,33),(32,42),(30,21),Foot(30),Foot(32,50,-19),(22,35),-6,1,-1),
    Pose((31,32),(32,42),(29,20),Foot(33),Foot(25,52,20),(21,34),-5,2,-1),
    Pose((32,34),(32,44),(30,22),Foot(37),Foot(22),(22,36),-4,1,-1),
    Pose((32,35),(32,44),(29,23),Foot(39,54,-34),Foot(25),(22,37),-1,0,-1),
    Pose((31,33),(32,42),(29,21),Foot(32,50,-22),Foot(29),(21,35),-5,2,-1),
    Pose((32,32),(32,42),(30,20),Foot(25,52,22),Foot(33),(22,34),-6,1,-1),
]
R_WALK = [
    Pose((32,34),(32,44),(35,22),Foot(27),Foot(41),(42,35),188,1,1),
    Pose((33,35),(32,44),(35,23),Foot(24,54,34),Foot(38),(43,36),186,2,1),
    Pose((33,33),(32,42),(36,21),Foot(30,50,18),Foot(34),(42,34),190,1,1),
    Pose((32,32),(32,42),(35,20),Foot(38,52,-21),Foot(30),(42,33),189,0,1),
    Pose((33,34),(32,44),(36,22),Foot(42),Foot(26),(43,35),187,1,1),
    Pose((32,35),(32,44),(35,23),Foot(38),Foot(24,54,35),(42,36),185,2,1),
    Pose((33,33),(32,42),(35,21),Foot(34),Foot(31,50,21),(42,34),189,1,1),
    Pose((32,32),(32,42),(34,20),Foot(30),Foot(39,53,-23),(41,33),191,0,1),
]

# Wind-up (00–02), descending cut (03–04), and recovery (05–07).
L_ATTACK = [
    Pose((33,34),(33,43),(31,22),Foot(31),Foot(38),(24,34),137,0,-1),
    Pose((34,34),(34,43),(32,22),Foot(30),Foot(39),(31,27),-10,1,-1),
    Pose((35,33),(34,43),(32,21),Foot(29),Foot(40),(34,22),-52,2,-1),
    Pose((32,34),(33,44),(29,22),Foot(26),Foot(40),(26,27),-115,1,1),
    Pose((28,38),(32,46),(25,26),Foot(22),Foot(41),(27,39),153,3,-1),
    Pose((29,39),(32,46),(25,27),Foot(22),Foot(41),(26,42),147,2,-1),
    Pose((31,36),(33,44),(29,24),Foot(26),Foot(40),(25,37),139,1,-1),
    Pose((32,34),(33,43),(30,22),Foot(30),Foot(38),(22,33),125,0,-1),
]
R_ATTACK = [
    Pose((31,34),(31,43),(33,22),Foot(28),Foot(36),(42,34),42,0,1),
    Pose((30,34),(31,43),(33,22),Foot(27),Foot(36),(36,27),191,1,1),
    Pose((29,32),(31,43),(32,20),Foot(26),Foot(37),(33,22),229,2,1),
    Pose((32,34),(32,44),(35,22),Foot(25),Foot(39),(38,27),302,1,-1),
    Pose((36,37),(33,46),(39,25),Foot(23),Foot(43),(36,39),31,3,1),
    Pose((35,39),(33,46),(38,27),Foot(23),Foot(43),(37,42),32,2,1),
    Pose((33,36),(32,44),(36,24),Foot(25),Foot(40),(40,37),39,1,1),
    Pose((32,34),(31,43),(34,22),Foot(28),Foot(37),(43,33),56,0,1),
]

CLIPS = {'LEFT': {'IDLE': L_IDLE, 'WALK': L_WALK, 'ATTACK': L_ATTACK},
         'RIGHT': {'IDLE': R_IDLE, 'WALK': R_WALK, 'ATTACK': R_ATTACK}}
TIMING = {'IDLE': [330, 250, 340, 260], 'WALK': [100] * 8,
          'ATTACK': [80, 105, 140, 55, 90, 95, 100, 125]}
WALK_PHASES = ('contact', 'toe-off', 'passing', 'forward swing')


def walk_labels(index):
    lead = 'left' if index < 4 else 'right'
    trail = 'right' if index < 4 else 'left'
    phase = WALK_PHASES[index % 4]
    return {'planted': lead, 'otherFoot': trail, 'phase': phase}


def leg_bottoms(face, pose):
    near_hip_x, far_hip_x = (2, -2) if face == 'LEFT' else (-2, 2)
    bottom = {}
    for name, foot, hip_x, near in (('near', pose.near, near_hip_x, True),
                                    ('far', pose.far, far_hip_x, False)):
        layer = Canvas()
        leg(layer, add(pose.waist, (hip_x, 0)), foot, face, near)
        bottom[name] = silhouette(layer.image)[3] - 1
    return bottom


def silhouette(image):
    return image.getchannel('A').getbbox()


def normalized(image):
    trimmed = image.crop(silhouette(image))
    return (trimmed.size, trimmed.tobytes())


def check_pair(previous, current, label):
    if normalized(previous) == normalized(current):
        raise ValueError(f'Duplicate or whole-sprite translation: {label}')
    a, b = previous.tobytes(), current.tobytes()
    changed = sum(a[i:i+4] != b[i:i+4] for i in range(0, len(a), 4))
    return changed


def docs_preview(frames):
    board = Image.new('RGB', (8 * 64 + 114, 6 * 64 + 31), '#26242e')
    d = ImageDraw.Draw(board)
    d.text((12, 9), 'ONE-ARMED SWORDSMAN  /  64 PX', fill='#ebded2', font=ImageFont.load_default())
    for row, ((face, action), images) in enumerate(frames.items()):
        y = 27 + row * 64
        d.text((12, y + 18), face, fill='#dcad83')
        d.text((12, y + 31), action, fill='#e1d3cd')
        for i, image in enumerate(images):
            x = 103 + i * 64
            d.rectangle((x, y, x + 62, y + 62), fill='#46404a' if i % 2 else '#55505b')
            d.line((x, y + ANCHOR[1], x + 62, y + ANCHOR[1]), fill='#6b626b')
            board.paste(image, (x, y), image)
    board.resize((board.width * 2, board.height * 2), Image.Resampling.NEAREST).save(ROOT / 'Preview.png')


def slow_walk(frames):
    preview = []
    for index in range(8):
        display = Image.new('RGB', (128, 90), '#4f4753')
        d = ImageDraw.Draw(display)
        d.line((0, 10 + ANCHOR[1], 127, 10 + ANCHOR[1]), fill='#796c74')
        d.text((4, 0), 'LEFT', fill='#ede0d3')
        d.text((69, 0), 'RIGHT', fill='#ede0d3')
        for x, face in ((0,'LEFT'), (64,'RIGHT')):
            image = frames[(face,'WALK')][index]
            display.paste(image, (x, 10), image)
        phase = walk_labels(index)
        label = phase['planted'].upper() + ': ' + phase['phase']
        d.text((4, 75), label, fill='#ede0d3')
        preview.append(display.resize((384, 270), Image.Resampling.NEAREST))
    preview[0].save(ROOT / 'WalkSlow.gif', save_all=True, append_images=preview[1:],
                    duration=350, loop=0, disposal=2, optimize=False)


def render():
    frames = {}
    manifest = {'width': SIZE, 'height': SIZE, 'anchor': ANCHOR, 'groundPixelY': GROUND,
                'sampling': 'nearest', 'directions': {}}
    audit = {'adjacentPairs': {}, 'frames': {}, 'weaponLengthGripToTip': 25,
             'bladeWidth': 8, 'totalWeaponLength': 30}
    for face, actions in CLIPS.items():
        direction_data = manifest['directions'][face] = {}
        for action, poses in actions.items():
            key = f'{face}/{action}'
            loop = action != 'ATTACK'
            images = frames[face, action] = []
            records = []
            for i, pose in enumerate(poses):
                try:
                    image, geometry = frame(face, action, pose)
                except ValueError as exc:
                    raise ValueError(f'{key} {i:02}: {exc}') from exc
                bounds = silhouette(image)
                if image.size != (SIZE, SIZE) or set(image.getchannel('A').tobytes()) != {0, 255}:
                    raise ValueError(f'{key} {i:02} has invalid size or alpha')
                if min(bounds[:2]) < 1 or max(bounds[2:]) >= SIZE or bounds[3] != ANCHOR[1]:
                    raise ValueError(f'{key} {i:02} has invalid margin or ground: {bounds}')
                sword_tip = geometry['swordTip']
                if abs(hypot(sword_tip[0] - pose.grip[0], sword_tip[1] - pose.grip[1]) - 25) > 1e-5:
                    raise ValueError('Sword geometry length drift')
                record = {'file': f'{key}/{i:02}.png', 'durationMs': TIMING[action][i]}
                if action == 'IDLE' and sword_tip != (poses[0].grip[0], GROUND):
                    raise ValueError(f'{key} sword tip moved')
                rendered_bottom = leg_bottoms(face, pose)
                expected_contact = (pose.near.bottom == GROUND, pose.far.bottom == GROUND)
                actual_contact = tuple(rendered_bottom[side] == GROUND for side in ('near', 'far'))
                if actual_contact != expected_contact or max(rendered_bottom.values()) > GROUND:
                    raise ValueError(f'{key} {i:02} rendered footfall differs: {rendered_bottom}')
                if action == 'WALK':
                    record.update(walk_labels(i))
                    plant = pose.near if (face == 'LEFT') == (record['planted'] == 'left') else pose.far
                    swing = pose.far if plant is pose.near else pose.near
                    if plant.bottom != GROUND or (swing.bottom == GROUND) != (record['phase'] == 'contact'):
                        raise ValueError(f'{key} {i:02} footfall mismatch')
                if action == 'ATTACK':
                    record['phase'] = 'wind-up' if i < 3 else 'cut' if i < 5 else 'recovery'
                audit['frames'][record['file']] = {'opaqueBoundsExclusive': bounds,
                                                   'renderedLegBottomY': rendered_bottom, **geometry}
                records.append(record)
                images.append(image)
            direction_data[action] = {'loop': loop, 'frames': records}
            pairs = [(i - 1, i) for i in range(1, len(images))]
            if loop:
                pairs.append((len(images) - 1, 0))
            audit['adjacentPairs'][key] = [
                {'pair': [a, b], 'differentPixels': check_pair(images[a], images[b], (key, a, b))}
                for a, b in pairs
            ]
        tips = [audit['frames'][f'{face}/ATTACK/{i:02}.png']['swordTip'][1] for i in (2, 3, 4)]
        if tips[0] >= min(p.head[1] - 2 for p in actions['ATTACK']) or not tips[0] < tips[1] < tips[2]:
            raise ValueError(f'{face} cut should go from overhead to low: {tips}')

    for action in CLIPS['LEFT']:
        opposite = {normalized(img.transpose(Image.Transpose.FLIP_LEFT_RIGHT)) for img in frames['RIGHT',action]}
        if any(normalized(img) in opposite for img in frames['LEFT',action]):
            raise ValueError(f'{action} contains an exact mirror')

    for (face, action), images in frames.items():
        folder = ROOT / face / action
        folder.mkdir(parents=True, exist_ok=True)
        for index, image in enumerate(images):
            image.save(folder / f'{index:02}.png')
    docs_preview(frames)
    slow_walk(frames)
    for name, data in {'Animations': manifest, 'QA': audit, 'codeart-meta': {
        'art_source': 'code', 'placeholder': False,
        'disclosure': 'Code-drawn at 1x, based on the prior original outfit reference.',
        'sourceSha256': sha256(Path(__file__).read_bytes()).hexdigest(),
    }}.items():
        (ROOT / (name + '.json')).write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    print(f'{sum(map(len, frames.values()))} PNG frames across {len(frames)} clips; '
          f'{sum(map(len, audit["adjacentPairs"].values()))} adjacent pairs checked.')


if __name__ == '__main__':
    render()
