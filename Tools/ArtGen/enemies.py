import math
from px import Canvas, ramp, limb, preview, hexc
from hum import TAU

# shared materials
ramp('iron', ['#151820', '#262b38', '#3c4456', '#5a667c', '#8593a8', '#c3cddb'])
ramp('steel', ['#141c2c', '#22314a', '#34507a', '#4c73a3', '#76a0cc', '#b7d6f0'])
ramp('leather', ['#1c110c', '#2f1d14', '#48301f', '#664530', '#8a6045'])
ramp('bone', ['#4a3b2c', '#7a6750', '#ad9a7b', '#ddcfb1', '#fff7e2'])
ramp('glow_w', ['#c9c2ff', '#ffffff'], emissive=True)
ramp('glow_o', ['#ff7a1a', '#ffc04a', '#fff3c0'], emissive=True)
ramp('glow_r', ['#ff3b3b', '#ff9a7a', '#ffe0d0'], emissive=True)
ramp('glow_g', ['#9dff5a', '#eaffb0'], emissive=True)
ramp('glow_p', ['#ff5ad7', '#ffc2f2'], emissive=True)
ramp('glow_c', ['#5ae8ff', '#d8ffff'], emissive=True)
ramp('glow_y', ['#ffc23a', '#ffe98a', '#fffbe0'], emissive=True)
ramp('dark', ['#07040c', '#0d0816', '#150d22'])

ramp('brute', ['#26080f', '#44121c', '#661c28', '#8c2a34', '#b34043', '#d6675c', '#efa085'])
ramp('stone_o', ['#2a1408', '#4a240e', '#6e3816', '#965020', '#bf6d2e', '#e0954a', '#f7c27a'])
ramp('wood', ['#22140b', '#3a2414', '#573720', '#77502f', '#9a6c42', '#be8d5c'])
ramp('straw', ['#3e2c10', '#6a4c1c', '#987028', '#c29a3c', '#e6c466', '#fbe8a4'])
ramp('burlap', ['#2e2016', '#4c3624', '#6c5034', '#8f6e4a', '#b39168', '#d6b88e'])
ramp('goblin', ['#10200c', '#1d3814', '#2e561c', '#447a26', '#62a033', '#8ac650', '#c0e88a'])
ramp('pack', ['#1e140c', '#33231a', '#4e3828', '#6e5038', '#93704e'])
ramp('bomb', ['#0c0c14', '#1a1a26', '#2c2c3e', '#45455c', '#6c6c88'])
ramp('imp', ['#2a0828', '#4a1046', '#701a68', '#992a8c', '#c445b0', '#e47ad0', '#f8b8ea'])
ramp('wing', ['#1e0620', '#360c36', '#541850', '#77276c', '#9a3a86'])
ramp('plume', ['#4a0c14', '#7a1420', '#a8202c', '#d23a3c', '#f07060'])
ramp('cloth_b', ['#0e1424', '#1a2540', '#283a5e', '#38517e'])


class P(dict):
    def __getattr__(self, k):
        return self.get(k, 0.0)


def legs2(cv, cx, gy, hip_y, spread, phase, stride, r, ramp_leg, ramp_foot, z_back, z_front, lift=2.0, foot=2.6, knee_out=1.0):
    """Short stubby legs: two capsules with feet; phase animates a walk."""
    for side, z in ((-1, z_back), (1, z_front)):
        s = math.sin(phase + (0 if side > 0 else math.pi))
        fx = cx + side * spread * 0.5 + s * stride
        fy = gy - max(0.0, math.cos(phase + (0 if side > 0 else math.pi))) * lift
        hx = cx + side * spread * 0.35
        kx = (hx + fx) / 2 + knee_out
        ky = (hip_y + fy) / 2
        cv.capsule(hx, hip_y, kx, ky, r, r * 0.9, ramp_leg, z=z, grp=20 + side)
        cv.capsule(kx, ky, fx, fy - 1.2, r * 0.9, r * 0.85, ramp_leg, z=z + 0.05, grp=20 + side)
        cv.ellipse(fx + foot * 0.35, fy - 1.2, foot, 1.8, ramp_foot, z=z + 0.1, grp=20 + side)


# ------------------------------------------------------------------ Brute
def brute(p):
    W, H, gy, cx = 60, 48, 45, 28
    cv = Canvas(W, H)
    lean = p.lean
    bob = p.bob
    hip_y = gy - 9 + bob
    legs2(cv, cx, gy, hip_y, 8, p.phase, p.stride, 2.6, 'brute', 'leather', 1, 4, lift=2.5, foot=3.0)
    # torso: big hunched ellipse
    tx = cx + 1 + lean * 9
    ty = hip_y - 10 + abs(lean) * 2
    # back arm
    shb = (tx - 4 + lean * 3, ty - 5)
    eb = limb(*shb, 7, p.armB)
    hb = limb(*eb, 7, p.armB + 0.4)
    cv.capsule(*shb, *eb, 3.2, 2.8, 'brute', z=1.5, grp=2, bias=-0.15)
    cv.capsule(*eb, *hb, 2.8, 2.6, 'brute', z=1.6, grp=2, bias=-0.15)
    cv.ellipse(hb[0], hb[1] + 1, 3.4, 3.0, 'brute', z=1.7, grp=2, bias=-0.1)
    # loincloth
    cv.poly([(cx - 3, hip_y - 2), (cx + 6, hip_y - 2), (cx + 5, hip_y + 6), (cx + 2, hip_y + 4.5), (cx - 1.5, hip_y + 6)], 'leather', z=3.2, grp=3, bevel=1)
    cv.ellipse(tx, ty, 11.5, 10.0, 'brute', z=3, grp=3, rot=lean * 0.6, bias=0.05)
    # belly highlight and chest scar
    cv.ellipse(tx + 3, ty + 3, 6.0, 5.5, 'brute', z=3.1, grp=3, bias=0.18, ao=False)
    cv.line(tx - 3, ty - 5, tx + 2, ty - 1, hexc('#44121c'))
    # belt
    cv.capsule(cx - 8, hip_y - 2.5, cx + 9, hip_y - 2.5, 1.6, 1.6, 'leather', z=3.3, grp=3)
    cv.rect(cx + 2, hip_y - 4, cx + 6, hip_y - 1, 'iron', z=3.4, grp=3)
    # head: low, forward
    hx = tx + 9 + lean * 5
    hy = ty - 3 + lean * 6 + p.headdy
    # horns (back horn first)
    cv.capsule(hx - 3, hy - 3, hx - 7, hy - 9, 1.8, 0.8, 'bone', z=2.8, grp=4, bias=-0.2)
    cv.capsule(hx - 7, hy - 9, hx - 5, hy - 13, 0.9, 0.4, 'bone', z=2.8, grp=4, bias=-0.2)
    cv.ellipse(hx, hy, 6.2, 5.6, 'brute', z=7, grp=5, bias=0.1)
    # brow ridge
    cv.capsule(hx - 1, hy - 2.6, hx + 5, hy - 1.8, 1.4, 1.2, 'brute', z=7.1, grp=5, bias=-0.1)
    cv.capsule(hx + 1, hy - 3, hx + 1, hy - 9, 1.9, 0.9, 'bone', z=7.2, grp=6)
    cv.capsule(hx + 1, hy - 9, hx + 4, hy - 13, 0.9, 0.4, 'bone', z=7.2, grp=6)
    # eyes
    cv.px(hx + 2.5, hy - 1, hexc('#ffffff'), top=True)
    cv.px(hx + 3.5, hy - 1, hexc('#c9c2ff'), top=True)
    # jaw and tusks
    cv.ellipse(hx + 2.5, hy + 2.5, 3.6, 2.4, 'brute', z=7.3, grp=5, bias=-0.05)
    cv.px(hx + 4.5, hy + 1, hexc('#fff7e2'), top=True)
    cv.px(hx + 4.5, hy + 0, hexc('#ddcfb1'), top=True)
    cv.px(hx + 2.0, hy + 1, hexc('#ddcfb1'), top=True)
    if p.mouth:
        cv.px(hx + 3.5, hy + 3, hexc('#26080f'), top=True)
        cv.px(hx + 4.5, hy + 3, hexc('#26080f'), top=True)
    # iron pauldron
    shf = (tx + 1 + lean * 4, ty - 4)
    cv.ellipse(shf[0] - 2, shf[1] - 1.5, 5.0, 3.6, 'iron', z=6.5, grp=7, bias=0.1)
    cv.px(shf[0] - 3, shf[1] - 3, hexc('#c3cddb'), top=True)
    # front arm
    ef = limb(*shf, 7.5, p.armF)
    hf = limb(*ef, 7.5, p.armF + p.elb)
    cv.capsule(*shf, *ef, 3.4, 3.0, 'brute', z=6, grp=8)
    cv.capsule(*ef, *hf, 3.0, 2.8, 'brute', z=6.1, grp=8)
    cv.ellipse(hf[0], hf[1] + 0.5, 3.8, 3.4, 'brute', z=6.2, grp=8, bias=0.05)
    cv.capsule(ef[0] - 1, ef[1] + 1, ef[0] + 1.5, ef[1] + 3, 1.2, 1.2, 'leather', z=6.15, grp=8)
    if p.steam:
        for k in range(3):
            cv.px(hx + 6 + k, hy + 1 - k * 2, hexc('#ddd6ee'), top=True)
    return cv.render()


def brute_frames():
    out = {'idle': [], 'walk': [], 'windup': [], 'attack': [], 'recover': []}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(brute(P(phase=0, stride=0, bob=-0.6 - 0.6 * math.sin(ph), lean=0.05, armF=0.1 + 0.05 * math.sin(ph), armB=-0.05, elb=0.3, headdy=0.4 * math.sin(ph))))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(brute(P(phase=ph, stride=3.0, bob=-1.0 * abs(math.sin(ph)), lean=0.12, armF=-0.45 * math.sin(ph), armB=0.45 * math.sin(ph), elb=0.4)))
    for i in range(2):
        out['windup'].append(brute(P(phase=0, stride=0, bob=-1 - i, lean=-0.18, armF=-2.4 - i * 0.2, armB=-1.6, elb=-0.8, headdy=-1, mouth=1, steam=1)))
    for i in range(2):
        out['attack'].append(brute(P(phase=i * 3.1, stride=4.5, bob=0.5, lean=0.55, armF=1.1, armB=-0.9, elb=0.4, headdy=2, mouth=1)))
    for i in range(2):
        out['recover'].append(brute(P(phase=0, stride=0, bob=1.5 + i * 0.5, lean=0.32, armF=0.05, armB=0.0, elb=0.1, headdy=3.0 + i * 0.5)))
    return out


# ------------------------------------------------------------------ Shooter (eye idol)
def shooter(p):
    W, H, gy, cx = 48, 44, 41, 23
    cv = Canvas(W, H)
    bob = p.bob
    hip_y = gy - 6 + bob
    legs2(cv, cx, gy, hip_y, 9, p.phase, p.stride, 2.2, 'stone_o', 'stone_o', 1, 4, lift=1.0, foot=2.4)
    by = hip_y - 10 + p.recoil * 0.5
    bx = cx - p.recoil
    # body: rounded block
    cv.ellipse(bx, by, 12.0, 11.0, 'stone_o', z=3, grp=3, bias=0.05)
    cv.ellipse(bx, by + 6, 10.5, 5.0, 'stone_o', z=3.05, grp=3, bias=-0.2, ao=False)
    # crown spikes
    for k, (dx, hgt) in enumerate(((-7, 5), (-2.5, 7), (2.5, 7), (7, 5))):
        cv.poly([(bx + dx - 2.2, by - 8.5), (bx + dx + 2.2, by - 8.5), (bx + dx + 0.3, by - 9 - hgt)], 'stone_o', z=2.5, grp=2, bevel=1, flat=0.0)
    # runes
    for (rx, ry) in ((-8, 2), (-9, -2), (8, 3)):
        cv.px(bx + rx, by + ry, hexc('#ffc04a'), top=True)
    # eye socket
    cv.ellipse(bx + 2.0, by - 0.5, 6.8, 6.2, 'dark', z=3.3, grp=4, flat=0.8, ao=False)
    lid = p.lid
    er = 4.2 + p.eyegrow
    cv.ellipse(bx + 2.5, by - 0.5 + lid * 0.5, er, max(0.8, er - lid), 'glow_o', z=3.4, grp=5, flat=1.0, bias=0.55)
    cv.ellipse(bx + 3.2, by - 0.8 + lid * 0.5, er * 0.45, max(0.5, er * 0.5 - lid * 0.5), 'glow_o', z=3.5, grp=5, flat=1.0, bias=1.0)
    cv.px(bx + 4.0, by - 2.5, hexc('#ffffff'), top=True)
    if lid > 2:
        cv.line(bx - 3, by - 0.5, bx + 7, by - 0.5, hexc('#2a1408'), top=True)
    if p.crack:
        cv.line(bx - 6, by - 6, bx - 3, by - 2, hexc('#2a1408'))
        cv.line(bx - 3, by - 2, bx - 4, by + 2, hexc('#2a1408'))
    # charging sparks
    if p.charge:
        for k in range(6):
            a = k / 6 * TAU + p.charge
            rr = 9 - p.charge * 1.5
            cv.px(bx + 2.5 + math.cos(a) * rr, by - 0.5 + math.sin(a) * rr, hexc('#fff3c0' if k % 2 else '#ffc04a'), top=True)
    return cv.render()


def shooter_frames():
    out = {'idle': [], 'walk': [], 'windup': [], 'attack': [], 'recover': []}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(shooter(P(bob=-0.5 - 0.5 * math.sin(ph), lid=1.5 if i == 2 else 0.0)))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(shooter(P(phase=ph, stride=2.0, bob=-0.8 * abs(math.sin(ph)))))
    for i in range(2):
        out['windup'].append(shooter(P(bob=-1, eyegrow=0.8 + i * 0.6, charge=0.5 + i * 1.2)))
    for i in range(2):
        out['attack'].append(shooter(P(bob=0, eyegrow=1.0 - i * 0.6, recoil=2.5 - i)))
    for i in range(2):
        out['recover'].append(shooter(P(bob=1, lid=3.0 + i, crack=1)))
    return out


# ------------------------------------------------------------------ Shield knight
def shieldman(p):
    W, H, gy, cx = 60, 56, 53, 27
    cv = Canvas(W, H)
    bob = p.bob
    hip_y = gy - 14 + bob
    # legs (armoured)
    for side, z in ((-1, 1), (1, 3.5)):
        s = math.sin(p.phase + (0 if side > 0 else math.pi))
        fx = cx + side * 3 + s * p.stride
        fy = gy - max(0.0, math.cos(p.phase + (0 if side > 0 else math.pi))) * 2.0
        hx = cx + side * 2.2
        kx = (hx + fx) / 2 + 1.0
        ky = (hip_y + fy) / 2
        cv.capsule(hx, hip_y, kx, ky, 2.6, 2.3, 'steel', z=z, grp=20 + side, bias=-0.1 if side < 0 else 0)
        cv.ellipse(kx + 0.5, ky, 2.2, 2.0, 'iron', z=z + 0.05, grp=20 + side)
        cv.capsule(kx, ky, fx, fy - 1.3, 2.3, 2.2, 'steel', z=z + 0.04, grp=20 + side, bias=-0.1 if side < 0 else 0)
        cv.ellipse(fx + 1.0, fy - 1.4, 3.0, 1.9, 'iron', z=z + 0.1, grp=20 + side)
    lean = p.lean
    tx = cx + lean * 8
    ty = hip_y - 8
    # sword arm (back) holding a blade over the shoulder
    shb = (tx - 3, ty - 5)
    eb = limb(*shb, 6, p.armB)
    hb = limb(*eb, 6, p.armB + p.elbB)
    cv.capsule(*shb, *eb, 2.4, 2.2, 'steel', z=1.8, grp=2, bias=-0.15)
    cv.capsule(*eb, *hb, 2.2, 2.0, 'steel', z=1.9, grp=2, bias=-0.15)
    sa = p.sword
    sx, sy = hb[0] + math.cos(sa) * 13, hb[1] + math.sin(sa) * 13
    nxv, nyv = -math.sin(sa), math.cos(sa)
    cv.poly([(hb[0] + nxv * 1.3, hb[1] + nyv * 1.3), (sx + nxv * 0.8, sy + nyv * 0.8), (sx + math.cos(sa) * 2.0, sy + math.sin(sa) * 2.0), (sx - nxv * 0.8, sy - nyv * 0.8), (hb[0] - nxv * 1.3, hb[1] - nyv * 1.3)],
            'iron', z=1.7, grp=2, bevel=1, flat=0.4)
    cv.capsule(hb[0] - nxv * 2.5, hb[1] - nyv * 2.5, hb[0] + nxv * 2.5, hb[1] + nyv * 2.5, 0.9, 0.9, 'leather', z=1.95, grp=2)
    # tabard + torso
    cv.poly([(tx - 6, ty - 7), (tx + 6, ty - 7), (tx + 7, ty + 6), (tx - 7, ty + 6)], 'steel', z=3, grp=3, bevel=2, flat=0.2, vgrad=-0.4)
    cv.poly([(tx - 4, ty - 2), (tx + 4, ty - 2), (tx + 4.5, ty + 10), (tx, ty + 8), (tx - 4.5, ty + 10)], 'cloth_b', z=3.2, grp=4, bevel=1)
    cv.line(tx, ty - 1, tx, ty + 6, hexc('#b7d6f0'))
    cv.line(tx - 2, ty + 1, tx + 2, ty + 1, hexc('#b7d6f0'))
    cv.capsule(tx - 7, ty + 6, tx + 7, ty + 6, 1.3, 1.3, 'leather', z=3.3, grp=4)
    # helmet
    hx, hy = tx + 1 + lean * 4, ty - 13 + p.headdy
    cv.capsule(hx - 2, hy - 4, hx - 8 - p.plume, hy - 6 + p.plume * 0.5, 2.0, 1.2, 'plume', z=4.8, grp=5)
    cv.capsule(hx - 8 - p.plume, hy - 6 + p.plume * 0.5, hx - 11 - p.plume * 1.5, hy - 2, 1.2, 0.8, 'plume', z=4.8, grp=5)
    cv.ellipse(hx, hy, 6.0, 6.4, 'steel', z=5, grp=6, bias=0.1)
    cv.rect(hx - 0.5, hy - 1.5, hx + 6.5, hy + 0.5, 'dark', z=5.2, grp=7, bevel=0)
    vis = '#b7d6f0' if not p.dim else '#4c73a3'
    cv.px(hx + 3, hy - 0.8, hexc(vis), top=True)
    cv.px(hx + 4, hy - 0.8, hexc(vis), top=True)
    cv.line(hx - 4, hy - 5.5, hx + 3, hy - 6.2, hexc('#b7d6f0'))
    # pauldron
    cv.ellipse(tx + 4, ty - 6, 4.0, 3.0, 'iron', z=5.5, grp=8, bias=0.1)
    # shield arm + tower shield in front
    shx = tx + 9 + p.shieldx
    shy = ty + 1 + p.shieldy
    sh_h = 17
    sh_w = 6
    if p.lowered:
        pts = [(shx - 2, shy + 2), (shx + 6, shy + 6), (shx + 3, shy + 20), (shx - 5, shy + 16)]
    else:
        pts = [(shx - sh_w * 0.5, shy - sh_h * 0.55), (shx + sh_w * 0.5, shy - sh_h * 0.6), (shx + sh_w * 0.55, shy + sh_h * 0.35),
               (shx, shy + sh_h * 0.55), (shx - sh_w * 0.55, shy + sh_h * 0.35)]
    cv.poly(pts, 'steel', z=7, grp=9, bevel=2, flat=0.25, edge=0.4, vgrad=-0.3)
    # shield boss + rim
    if not p.lowered:
        cv.ellipse(shx + 0.3, shy - 1, 2.0, 2.4, 'iron', z=7.2, grp=9, bias=0.15)
        cv.line(shx - 1, shy - sh_h * 0.45, shx - 1, shy + sh_h * 0.4, hexc('#76a0cc'))
    return cv.render()


def shield_frames():
    out = {'idle': [], 'walk': [], 'windup': [], 'attack': [], 'recover': []}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(shieldman(P(bob=-0.5 - 0.5 * math.sin(ph), armB=-0.4, elbB=-1.6, sword=-1.9, plume=0.5 * math.sin(ph))))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(shieldman(P(phase=ph, stride=3.0, bob=-0.9 * abs(math.sin(ph)), lean=0.06, armB=-0.4, elbB=-1.6, sword=-1.9, plume=1.0, shieldy=0.6 * math.sin(ph))))
    for i in range(2):
        out['windup'].append(shieldman(P(bob=-1, lean=-0.12, armB=-2.6, elbB=-0.5, sword=-2.9 - i * 0.2, shieldx=-2, shieldy=-1 - i, plume=-0.5)))
    for i in range(2):
        out['attack'].append(shieldman(P(phase=1.5, stride=3, bob=0, lean=0.3, armB=-0.6, elbB=-0.2, sword=-0.6, shieldx=4 - i, shieldy=-1, plume=2)))
    for i in range(2):
        out['recover'].append(shieldman(P(bob=1.0 + i * 0.5, lean=-0.1, armB=0.3, elbB=0.2, sword=1.2, lowered=1, headdy=1.5, dim=1)))
    return out


# ------------------------------------------------------------------ Bomber (goblin sapper)
def bomber(p):
    W, H, gy, cx = 56, 48, 45, 26
    cv = Canvas(W, H)
    bob = p.bob
    hip_y = gy - 7 + bob
    legs2(cv, cx, gy, hip_y, 6, p.phase, p.stride, 1.8, 'goblin', 'leather', 1, 4, lift=2.0, foot=2.4)
    lean = p.lean
    tx = cx + lean * 6
    ty = hip_y - 6
    # backpack with bombs
    px_, py_ = tx - 6.5, ty - 4
    cv.rect(px_ - 3, py_ - 5, px_ + 3, py_ + 4, 'pack', z=1.5, grp=2, bevel=1)
    cv.ellipse(px_ - 1, py_ - 8, 3.0, 3.0, 'bomb', z=1.4, grp=2)
    cv.ellipse(px_ - 4.5, py_ - 6.5, 2.4, 2.4, 'bomb', z=1.3, grp=2)
    cv.line(px_ - 1, py_ - 11, px_ + 0.5, py_ - 12.5, hexc('#8a6045'))
    # back arm
    shb = (tx - 2, ty - 4)
    eb = limb(*shb, 4.5, p.armB)
    hb = limb(*eb, 4.5, p.armB + 0.5)
    cv.capsule(*shb, *eb, 1.6, 1.4, 'goblin', z=1.8, grp=3, bias=-0.15)
    cv.capsule(*eb, *hb, 1.4, 1.3, 'goblin', z=1.9, grp=3, bias=-0.15)
    # body
    cv.ellipse(tx, ty, 6.5, 7.5, 'goblin', z=3, grp=4, bias=0.05)
    cv.poly([(tx - 6, ty - 2), (tx + 6, ty - 2), (tx + 6.5, ty + 7), (tx - 6.5, ty + 7)], 'leather', z=3.1, grp=5, bevel=1, vgrad=-0.3)
    cv.capsule(tx - 6, ty - 1, tx + 5, ty + 5, 1.1, 1.1, 'pack', z=3.2, grp=5)
    for k in range(3):
        cv.ellipse(tx - 3.5 + k * 3.2, ty + 0.5 + k * 1.9, 1.3, 1.3, 'bomb', z=3.3, grp=5)
    # head: big ears, goggles
    hx, hy = tx + 3 + lean * 4, ty - 10 + p.headdy
    cv.poly([(hx - 3, hy - 1), (hx - 12, hy - 4 + p.ear), (hx - 3, hy + 2)], 'goblin', z=4.8, grp=6, bevel=1, flat=0.0)
    cv.ellipse(hx, hy, 5.8, 5.2, 'goblin', z=5, grp=7, bias=0.1)
    cv.poly([(hx + 1, hy - 1), (hx + 9, hy - 6 + p.ear * 0.6), (hx + 3, hy + 2)], 'goblin', z=5.05, grp=7, bevel=1, flat=0.05)
    # nose
    cv.ellipse(hx + 5.5, hy + 1.5, 2.2, 1.6, 'goblin', z=5.3, grp=7, bias=0.15)
    # goggles strap + lenses
    cv.capsule(hx - 5, hy - 2.5, hx + 4, hy - 2.5, 1.1, 1.1, 'leather', z=5.2, grp=8)
    cv.ellipse(hx + 2.5, hy - 2.2, 2.2, 2.0, 'iron', z=5.4, grp=8)
    cv.px(hx + 2.5, hy - 2.5, hexc('#eaffb0'), top=True)
    cv.px(hx + 3.5, hy - 2.5, hexc('#9dff5a'), top=True)
    # grin
    cv.line(hx + 1.5, hy + 3, hx + 4.5, hy + 3.5, hexc('#10200c'))
    cv.px(hx + 3, hy + 3, hexc('#fff7e2'), top=True)
    # front arm + held bomb
    shf = (tx + 2, ty - 4)
    ef = limb(*shf, 4.5, p.armF)
    hf = limb(*ef, 4.5, p.armF + p.elb)
    cv.capsule(*shf, *ef, 1.7, 1.5, 'goblin', z=6, grp=9)
    cv.capsule(*ef, *hf, 1.5, 1.4, 'goblin', z=6.05, grp=9)
    if p.hold:
        bx, by = hf[0] + 0.5, hf[1] - 2.5
        cv.ellipse(bx, by, 3.2, 3.2, 'bomb', z=6.2, grp=10, bias=0.05)
        cv.px(bx - 1, by - 1.5, hexc('#6c6c88'), top=True)
        cv.line(bx + 1, by - 3, bx + 2.5, by - 5, hexc('#be8d5c'), top=True)
        if p.fuse:
            cv.px(bx + 3, by - 6, hexc('#fff3c0'), top=True)
            cv.px(bx + 2, by - 6.5, hexc('#ffc04a'), top=True)
            cv.px(bx + 4, by - 6, hexc('#ff7a1a'), top=True)
            cv.px(bx + 3, by - 7.2, hexc('#ff7a1a'), top=True)
    cv.ellipse(hf[0], hf[1], 1.7, 1.7, 'goblin', z=6.3, grp=9, bias=0.05)
    return cv.render()


def bomber_frames():
    out = {'idle': [], 'walk': [], 'windup': [], 'attack': [], 'recover': []}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(bomber(P(bob=-0.4 - 0.5 * math.sin(ph), armF=0.3, elb=-1.2, armB=-0.2, hold=1, ear=0.8 * math.sin(ph))))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(bomber(P(phase=ph, stride=2.5, bob=-1.0 * abs(math.sin(ph)), lean=0.1, armF=0.3, elb=-1.2, armB=0.5 * math.sin(ph), hold=1, ear=1.5)))
    for i in range(2):
        out['windup'].append(bomber(P(bob=-1, lean=-0.15, armF=-2.6, elb=-0.3, armB=0.6, hold=1, fuse=1, ear=-1 - i, headdy=-0.5)))
    for i in range(2):
        out['attack'].append(bomber(P(bob=0, lean=0.25, armF=1.4 - i * 0.3, elb=0.2, armB=-0.8, hold=0, ear=2)))
    for i in range(2):
        out['recover'].append(bomber(P(bob=0.5, lean=0.0, armF=-2.2, elb=-1.8, armB=0.1, hold=0, ear=2.5, headdy=1)))
    return out


# ------------------------------------------------------------------ Flyer (bat imp)
def flyer(p):
    W, H, gy, cx = 60, 40, 30, 30
    cv = Canvas(W, H)
    by = gy - 10 + p.bob
    bx = cx + p.dx
    tilt = p.tilt
    flap = p.flap  # -1 up .. 1 down
    up = -flap
    for side, z in ((-1, 1), (1, 5)):
        sp = p.span
        sx, sy = bx + side * 3, by - 2
        wrist = (bx + side * 8 * sp, by - 6 - 5 * up)
        tip = (bx + side * 15 * sp, by - 3 - 6 * up)
        s1 = (bx + side * 12.5 * sp, by + 2.5 - up * 2.5)
        f2 = (bx + side * 10 * sp, by + 0.5 - up * 3)
        s2 = (bx + side * 7.5 * sp, by + 4.5 - up * 1.5)
        f3 = (bx + side * 5.5 * sp, by + 2.5 - up * 1.5)
        body = (bx + side * 3, by + 4)
        pts = [(sx, sy), wrist, tip, s1, f2, s2, f3, body]
        cv.poly(pts, 'wing', z=z, grp=10 + side, bevel=1, flat=0.05 if side > 0 else -0.25, vgrad=-0.35)
        bone = hexc('#9a3a86' if side > 0 else '#77276c')
        cv.line(sx, sy, wrist[0], wrist[1], bone)
        cv.line(wrist[0], wrist[1], tip[0], tip[1], bone)
        cv.line(wrist[0], wrist[1], f2[0], f2[1], bone)
        cv.line(wrist[0], wrist[1], f3[0], f3[1], bone)
    # body
    cv.ellipse(bx, by, 6.0, 6.5, 'imp', z=3, grp=3, rot=tilt, bias=0.05)

    # ears
    cv.poly([(bx - 3, by - 4), (bx - 5 - p.ear, by - 11), (bx - 0.5, by - 5.5)], 'imp', z=2.9, grp=2, bevel=1, flat=-0.1)
    cv.poly([(bx + 1, by - 5.5), (bx + 4 + p.ear, by - 12), (bx + 4, by - 4)], 'imp', z=3.2, grp=4, bevel=1, flat=0.1)
    # eyes
    ex = bx + 2.5
    ey = by - 1.5
    if p.dizzy:
        cv.line(ex - 1, ey - 1, ex + 1, ey + 1, hexc('#ffc2f2'), top=True)
        cv.line(ex - 1, ey + 1, ex + 1, ey - 1, hexc('#ffc2f2'), top=True)
    else:
        for exx in (ex - 2.5, ex + 0.5):
            cv.px(exx, ey, hexc('#ffffff'), top=True)
            cv.px(exx + 1, ey, hexc('#ffc2f2'), top=True)
            cv.px(exx, ey + 1, hexc('#ff5ad7'), top=True)
    # mouth + fangs
    cv.line(ex - 2, ey + 3, ex + 2, ey + 3, hexc('#2a0828'))
    cv.px(ex - 1, ey + 4, hexc('#fff7e2'), top=True)
    cv.px(ex + 1, ey + 4, hexc('#fff7e2'), top=True)
    # little feet / tail
    cv.capsule(bx - 3, by + 4, bx - 7, by + 7 + p.tail, 1.0, 0.6, 'imp', z=2.5, grp=2)
    cv.capsule(bx, by + 5.5, bx + 0.5, by + 8, 0.9, 0.9, 'imp', z=3.3, grp=3)
    cv.capsule(bx + 3, by + 5, bx + 3.5, by + 7.5, 0.9, 0.9, 'imp', z=3.3, grp=3)
    return cv.render()


def flyer_frames():
    out = {'idle': [], 'walk': [], 'windup': [], 'attack': [], 'recover': []}
    flaps = [-1.0, -0.2, 0.9, 0.2]
    for i in range(4):
        out['idle'].append(flyer(P(flap=flaps[i], span=1.0, bob=[-1, 0, 1, 0][i], ear=0.5 * math.sin(i * 1.6), tail=[0, 1, 1, 0][i])))
    out['walk'] = list(out['idle'])
    for i in range(2):
        out['windup'].append(flyer(P(flap=-1.25, span=1.1, bob=-2 - i, tilt=0.3, ear=-1, tail=-1)))
    for i in range(2):
        out['attack'].append(flyer(P(flap=0.6, span=0.65, bob=1, tilt=0.6, dx=2, ear=2, tail=2)))
    for i in range(2):
        out['recover'].append(flyer(P(flap=1.0, span=0.85, bob=3 + i, tilt=-0.2, ear=1.5, dizzy=1, tail=1)))
    return out


# ------------------------------------------------------------------ Dummy
def dummy(p):
    W, H, gy, cx = 48, 64, 61, 24
    cv = Canvas(W, H)
    sway = p.sway
    cv.rect(cx - 7, gy - 3, cx + 7, gy, 'wood', z=0.5, grp=1, bevel=1)
    cv.rect(cx - 1.6, gy - 40, cx + 1.6, gy - 2, 'wood', z=1, grp=1, bevel=1)
    top = gy - 54
    # crossbar arms
    ax, ay = cx + sway * 2, gy - 38
    cv.capsule(ax - 13, ay + sway, ax + 13, ay - sway, 1.5, 1.5, 'wood', z=1.5, grp=2)
    for e in (-13, 13):
        cv.ellipse(ax + e, ay + (sway if e < 0 else -sway) + 1, 2.2, 2.6, 'straw', z=1.6, grp=2)
    # sack body
    bx = cx + sway * 3
    cv.ellipse(bx, gy - 30, 9.5, 12.0, 'burlap', z=3, grp=3, bias=0.05, rot=sway * 0.1)
    cv.capsule(bx - 9, gy - 21, bx + 9, gy - 21, 1.2, 1.2, 'leather', z=3.2, grp=3)
    # target on chest
    cv.ellipse(bx + 1, gy - 31, 5.0, 5.0, 'plume', z=3.3, grp=4, flat=0.6)
    cv.ellipse(bx + 1, gy - 31, 3.2, 3.2, 'bone', z=3.4, grp=4, flat=0.6)
    cv.ellipse(bx + 1, gy - 31, 1.4, 1.4, 'plume', z=3.5, grp=4, flat=0.6)
    # head sack
    hx, hy = cx + sway * 4, top + 6
    cv.ellipse(hx, hy, 7.0, 6.5, 'burlap', z=4, grp=5, bias=0.1)
    cv.capsule(hx - 5, hy + 5, hx + 5, hy + 5, 1.0, 1.0, 'straw', z=4.1, grp=5)
    for k in range(4):
        cv.px(hx - 4 + k * 2.6, hy + 7.2, hexc('#e6c466'), top=True)
    # stitched face
    cv.line(hx + 1, hy - 2, hx + 3, hy, hexc('#2e2016'))
    cv.line(hx + 1, hy, hx + 3, hy - 2, hexc('#2e2016'))
    cv.line(hx + 4.5, hy - 2, hx + 6, hy, hexc('#2e2016'))
    cv.line(hx + 4.5, hy, hx + 6, hy - 2, hexc('#2e2016'))
    cv.line(hx + 1, hy + 2.5, hx + 6, hy + 2.5, hexc('#2e2016'))
    # straw tuft on top
    for k in range(5):
        cv.line(hx - 3 + k * 1.5, hy - 6, hx - 4 + k * 2, hy - 9 - (k % 2), hexc('#e6c466'))
    return cv.render()


def dummy_frames():
    out = {'idle': [dummy(P(sway=0)), dummy(P(sway=0.3)), dummy(P(sway=0)), dummy(P(sway=-0.3))]}
    return out


ALL = {
    'Brute': (brute_frames, (28, 45)),
    'Shooter': (shooter_frames, (23, 41)),
    'Shield': (shield_frames, (27, 53)),
    'Bomber': (bomber_frames, (26, 45)),
    'Flyer': (flyer_frames, (30, 30)),
    'Dummy': (dummy_frames, (24, 61)),
}

if __name__ == '__main__':
    import sys
    names = sys.argv[1:] or list(ALL)
    for n in names:
        fr = ALL[n][0]()
        allf = []
        for k in ('idle', 'walk', 'windup', 'attack', 'recover'):
            allf += fr.get(k, [])
        preview(allf, scale=5, cols=7, path='prev_%s.png' % n)
        print(n, len(allf))
