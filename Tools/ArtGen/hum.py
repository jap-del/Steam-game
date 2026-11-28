"""Generic humanoid rig on top of px.Canvas. Pose angles in radians, 0 = limb pointing straight down,
positive = rotated towards the facing direction (right)."""
import math
from px import Canvas, limb

TAU = math.pi * 2


class Pose(dict):
    def __getattr__(self, k):
        return self.get(k, 0.0)


def walk_pose(i, n, stride=0.6, bob=1.2, arm=0.5, lean=0.06):
    p = i / n * TAU
    s = math.sin(p)
    return Pose(
        legF=stride * s, legB=-stride * s,
        bendF=0.15 + 0.75 * max(0.0, -math.cos(p)) * (1 if s < 0.3 else 0.6),
        bendB=0.15 + 0.75 * max(0.0, math.cos(p)) * (1 if s > -0.3 else 0.6),
        armF=-arm * s, armB=arm * s, elbF=0.35, elbB=0.35,
        bob=bob * abs(math.cos(p)) - bob * 0.5, lean=lean, phase=p,
    )


def idle_pose(i, n, breath=1.0):
    p = i / n * TAU
    return Pose(legF=0.06, legB=-0.08, bendF=0.08, bendB=0.1, armF=0.08, armB=-0.06, elbF=0.25, elbB=0.25,
                bob=-breath * (0.5 + 0.5 * math.sin(p)) * 0.8, lean=0.0, phase=p, breath=math.sin(p))


class Rig:
    """Computes joints. cx, gy: foot centre and ground line. dims: thigh, shin, torso, neck, head,
    shoulder_w (half width between shoulders), hip_w."""

    def __init__(self, cx, gy, dims, pose, facing=1):
        self.cx, self.gy, self.d, self.p = cx, gy, dims, pose
        d = dims
        leg = d['thigh'] + d['shin']
        self.hip = (cx + pose.hipx, gy - leg * math.cos(min(abs(pose.legF), abs(pose.legB)) * 0.5) - pose.bob + pose.crouch)
        lean = pose.lean
        hx, hy = self.hip
        self.neck = (hx + math.sin(lean) * d['torso'], hy - math.cos(lean) * d['torso'])
        nx, ny = self.neck
        hl = lean + pose.headtilt
        self.head = (nx + math.sin(hl) * d['head'] * 0.85 + pose.headx, ny - math.cos(hl) * d['head'] * 0.85 + pose.heady)
        sw = d.get('shoulder_w', 3)
        self.shF = (nx + sw * 0.35 + math.sin(lean) * 1.5, ny + 1.5)
        self.shB = (nx - sw * 0.35 + math.sin(lean) * 1.5, ny + 1.5)
        hw = d.get('hip_w', 2)
        self.hipF = (hx + hw * 0.4, hy)
        self.hipB = (hx - hw * 0.4, hy)
        self.kneeF = limb(*self.hipF, d['thigh'], pose.legF)
        self.footF = limb(*self.kneeF, d['shin'], pose.legF - pose.bendF)
        self.kneeB = limb(*self.hipB, d['thigh'], pose.legB)
        self.footB = limb(*self.kneeB, d['shin'], pose.legB - pose.bendB)
        ua = d.get('upper', 6)
        fa = d.get('fore', 6)
        self.elbF = limb(*self.shF, ua, pose.armF)
        self.handF = limb(*self.elbF, fa, pose.armF + pose.elbF)
        self.elbB = limb(*self.shB, ua, pose.armB)
        self.handB = limb(*self.elbB, fa, pose.armB + pose.elbB)


def draw_leg(cv, hipj, knee, foot, r_thigh, r_shin, ramp_leg, ramp_boot, z, grp, boot_len=3.0, boot_r=2.0, facing=1):
    cv.capsule(*hipj, *knee, r_thigh, r_shin * 1.05, ramp_leg, z=z, grp=grp)
    cv.capsule(*knee, foot[0], foot[1] - 1, r_shin, r_shin * 0.95, ramp_boot, z=z + 0.1, grp=grp)
    cv.ellipse(foot[0] + boot_len * 0.45 * facing, foot[1] - boot_r * 0.55, boot_len, boot_r, ramp_boot, z=z + 0.2, grp=grp)


def draw_arm(cv, sh, elb, hand, r_up, r_fore, ramp_arm, ramp_hand, z, grp, hand_r=1.6):
    cv.capsule(*sh, *elb, r_up, r_fore, ramp_arm, z=z, grp=grp)
    cv.capsule(*elb, *hand, r_fore, r_fore * 0.9, ramp_arm, z=z + 0.05, grp=grp)
    cv.ellipse(hand[0], hand[1], hand_r, hand_r, ramp_hand, z=z + 0.1, grp=grp)
