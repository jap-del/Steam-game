import math
from px import Canvas, ramp, limb, preview, hexc
from hum import TAU
import enemies  # noqa: F401  (shared ramps)
from enemies import P

ramp('gstone', ['#17131f', '#29232f', '#3c3445', '#53495d', '#6c6177', '#8a7f96', '#ada3ba'])
ramp('glow_m', ['#c42a9c', '#ff6ad8', '#ffd6f4'], emissive=True)
ramp('crimson', ['#1e050b', '#370a14', '#56121e', '#781c29', '#9c2c35', '#c2464a', '#e07068'])
ramp('gold', ['#341f06', '#5f3c0e', '#916116', '#c38b24', '#e9b645', '#fde893'])
ramp('sunsteel', ['#3a1604', '#68300c', '#9c4c16', '#cc7022', '#ee9c3a', '#ffcd72', '#fff0c0'])
ramp('flame', ['#ff4a12', '#ff9228', '#ffd56a', '#fff8d8'], emissive=True)
ramp('moonrobe', ['#161430', '#25224c', '#373570', '#504f96', '#7072b8', '#989dd8', '#c8cdf4'])
ramp('silver', ['#252838', '#43485e', '#6a7290', '#97a0bd', '#c6cde4', '#f4f7ff'])
ramp('glow_moon', ['#8eaaff', '#d2deff', '#ffffff'], emissive=True)
ramp('lightarmor', ['#463820', '#766440', '#a6966a', '#d2c69c', '#efe7cc', '#fffdf4'])
ramp('glow_light', ['#ffd970', '#fff3c4', '#ffffff'], emissive=True)
ramp('brass', ['#271806', '#472d0e', '#70501a', '#9c7226', '#c69838', '#eac660', '#fff0a6'])
ramp('cogsteel', ['#1a1c22', '#2e323c', '#474e5c', '#656f82', '#8b95a8', '#bec7d4'])
ramp('coat', ['#1b100a', '#2e1c12', '#462b1c', '#603d28', '#7c5236', '#9a6a48'])
ramp('clockface', ['#5a4c36', '#958566', '#c9bb97', '#ece2c8', '#fffaee'], emissive=False)
ramp('skin_d', ['#2a1a22', '#46303a', '#664852', '#8a646c'])


def shade_line(cv, pts, color):
    for a, b in zip(pts, pts[1:]):
        cv.line(a[0], a[1], b[0], b[1], hexc(color))


# ---------------------------------------------------------------- 1-3 Citadel gatekeeper
def gatekeeper(p):
    W, H, gy, cx = 100, 84, 80, 46
    cv = Canvas(W, H)
    bob = p.bob
    lean = p.lean
    hip_y = gy - 16 + bob
    # legs: stone pillars
    for side, z in ((-1, 1), (1, 4)):
        s = math.sin(p.phase + (0 if side > 0 else math.pi))
        fx = cx + side * 8 + s * p.stride
        fy = gy - max(0.0, math.cos(p.phase + (0 if side > 0 else math.pi))) * 3.0
        hx = cx + side * 6
        b = -0.15 if side < 0 else 0.0
        cv.poly([(hx - 5, hip_y), (hx + 5, hip_y), (fx + 5.5, fy - 5), (fx - 5.5, fy - 5)], 'gstone', z=z, grp=20 + side, bevel=2, bias=b)
        cv.rect(fx - 7, fy - 6, fx + 8, fy, 'iron', z=z + 0.1, grp=20 + side, bevel=1, bias=b)
        cv.rect(hx - 5.5, (hip_y + fy) / 2 - 2, hx + 5.5 + (fx - hx) * 0.5, (hip_y + fy) / 2 + 1, 'iron', z=z + 0.05, grp=20 + side, bevel=1, bias=b)
    tx = cx + lean * 14
    ty = hip_y - 18
    # back arm
    shb = (tx - 15, ty - 12)
    eb = limb(*shb, 12, p.armB)
    hb = limb(*eb, 11, p.armB + p.elbB)
    cv.capsule(*shb, *eb, 6, 5, 'gstone', z=1.5, grp=2, bias=-0.2)
    cv.capsule(*eb, *hb, 5, 5.5, 'gstone', z=1.6, grp=2, bias=-0.2)
    cv.rect(hb[0] - 7, hb[1] - 5, hb[0] + 7, hb[1] + 6, 'iron', z=1.7, grp=2, bevel=2, bias=-0.15)
    # torso: gatehouse
    top = ty - 16
    bot = ty + 14
    cv.poly([(tx - 18, top + 4), (tx + 18, top + 4), (tx + 16, bot), (tx - 16, bot)], 'gstone', z=3, grp=3, bevel=3, flat=0.2, vgrad=-0.35)
    # battlements
    for k in range(5):
        bx = tx - 16 + k * 8
        cv.rect(bx, top - 2, bx + 5, top + 5, 'gstone', z=3.05, grp=3, bevel=2, flat=0.35)
    # brick lines
    for row in range(3):
        yy = top + 10 + row * 6
        cv.line(tx - 16, yy, tx + 15, yy, hexc('#29232f'))
        for k in range(4):
            xx = tx - 14 + k * 8 + (4 if row % 2 else 0)
            cv.line(xx, yy, xx, yy + 5, hexc('#29232f'))
    # gate arch with core and portcullis
    gw = 9
    gtop = ty - 4
    glow = 0.25 + p.glow * 0.75
    cv.ellipse(tx + 2, gtop + 2, gw, 7, 'dark', z=3.2, grp=4, flat=1, ao=False)
    cv.rect(tx + 2 - gw, gtop + 2, tx + 2 + gw, bot - 1, 'dark', z=3.2, grp=4, bevel=0)
    cv.ellipse(tx + 2, gtop + 8, 5 + p.glow * 1.5, 6 + p.glow * 1.5, 'glow_m', z=3.25, grp=4, flat=1, bias=glow * 0.7)
    cv.ellipse(tx + 2, gtop + 8, 2.5, 3, 'glow_m', z=3.26, grp=4, flat=1, bias=0.4 + glow)
    gate_drop = p.gate
    for k in range(5):
        xx = tx + 2 - gw + 2 + k * 3.6
        cv.capsule(xx, gtop - 3 + gate_drop, xx, bot - 1, 0.9, 0.9, 'iron', z=3.4, grp=5)
    for yy in (gtop + 3 + gate_drop * 0.5, gtop + 10 + gate_drop * 0.5):
        cv.capsule(tx + 2 - gw + 1, yy, tx + 2 + gw - 1, yy, 0.8, 0.8, 'iron', z=3.45, grp=5)
    # head: small helm in the middle of battlements
    hx, hy = tx + 1 + lean * 6, top - 6 + p.headdy
    cv.ellipse(hx, hy, 7, 6.5, 'iron', z=5, grp=6, bias=0.1)
    cv.rect(hx - 6, hy - 1, hx + 7.5, hy + 1.5, 'dark', z=5.1, grp=7, bevel=0)
    for k in range(3):
        cv.px(hx + 1 + k * 2, hy, hexc('#ff6ad8' if p.glow > 0.2 else '#c42a9c'), top=True)
    cv.px(hx + 3, hy - 1, hexc('#ffd6f4'), top=True)
    cv.poly([(hx - 1, hy - 6), (hx + 1, hy - 13), (hx + 3, hy - 6)], 'iron', z=5.05, grp=6, bevel=1)
    # front arm: stacked blocks + ram fist
    shf = (tx + 15, ty - 12)
    cv.rect(shf[0] - 5, shf[1] - 2, shf[0] + 7, shf[1] + 7, 'iron', z=6.5, grp=8, bevel=2, flat=0.3)
    ef = limb(*shf, 12, p.armF)
    hf = limb(*ef, 11, p.armF + p.elbF)
    cv.capsule(*shf, *ef, 6, 5, 'gstone', z=6, grp=9)
    cv.ellipse(ef[0], ef[1], 4.5, 4.5, 'iron', z=6.05, grp=9)
    cv.capsule(*ef, *hf, 5, 5.5, 'gstone', z=6.1, grp=9)
    cv.rect(hf[0] - 7, hf[1] - 5, hf[0] + 8, hf[1] + 7, 'iron', z=6.2, grp=9, bevel=2, flat=0.2)
    for k in range(3):
        cv.px(hf[0] - 4 + k * 4, hf[1] + 1, hexc('#c3cddb'), top=True)
    if p.dust:
        for k in range(9):
            dx = (k - 4) * 5
            cv.ellipse(cx + dx * 1.4, gy - 2 - abs(dx) * 0.1, 2.5, 1.8, 'gstone', z=8, grp=30, bias=0.3)
    return cv.render()


def gatekeeper_frames():
    out = {k: [] for k in ('idle', 'walk', 'windup', 'attack', 'recover')}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(gatekeeper(P(bob=-1 - math.sin(ph), armF=0.15, elbF=0.2, armB=-0.1, elbB=0.2, glow=0.3 + 0.2 * math.sin(ph), headdy=0.5 * math.sin(ph))))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(gatekeeper(P(phase=ph, stride=5, bob=-1.5 * abs(math.sin(ph)), lean=0.06, armF=-0.35 * math.sin(ph), elbF=0.3, armB=0.35 * math.sin(ph), elbB=0.3, glow=0.4)))
    for i in range(2):
        out['windup'].append(gatekeeper(P(bob=-2 - i, lean=-0.1, armF=-2.7, elbF=-0.2, armB=-2.4, elbB=-0.3, glow=0.8 + i * 0.2, gate=-3, headdy=-1)))
    for i in range(2):
        out['attack'].append(gatekeeper(P(bob=3, lean=0.25, armF=0.9 + i * 0.4, elbF=0.2, armB=0.6, elbB=0.3, glow=1.0, gate=0, dust=i, headdy=2)))
    for i in range(2):
        out['recover'].append(gatekeeper(P(bob=4 + i, lean=0.15, armF=0.2, elbF=0.1, armB=0.1, elbB=0.1, glow=0.0, gate=6, headdy=4)))
    return out


# ---------------------------------------------------------------- 1-6 Seal guardian
def guardian(p):
    W, H, gy, cx = 120, 104, 100, 56
    cv = Canvas(W, H)
    bob = p.bob
    lean = p.lean
    hem = gy
    waist_y = gy - 34 + bob
    tx = cx + lean * 12
    ty = waist_y - 16
    # chains hanging from back arm
    shb = (tx - 14, ty - 10)
    eb = limb(*shb, 13, p.armB)
    hb = limb(*eb, 12, p.armB + p.elbB)
    # robe (behind arms front)
    sway = p.sway
    robe = [(tx - 13, ty - 4), (tx + 12, ty - 4), (cx + 20 + sway, hem - 2), (cx + 13 + sway, hem), (cx + 4, hem - 2), (cx - 5, hem),
            (cx - 14 + sway * 0.5, hem - 1), (cx - 22 + sway * 1.4, hem - 3)]
    cv.capsule(*shb, *eb, 5, 4.5, 'crimson', z=1.5, grp=2, bias=-0.25)
    cv.capsule(*eb, *hb, 4.5, 4, 'crimson', z=1.6, grp=2, bias=-0.25)
    cv.ellipse(hb[0], hb[1], 3.5, 3.5, 'skin_d', z=1.7, grp=2)
    cv.poly(robe, 'crimson', z=3, grp=3, bevel=2, flat=0.15, vgrad=-0.5, edge=0.35)
    # robe folds + gold hem
    for k, fx in enumerate((-12, -4, 5, 13)):
        shade_line(cv, [(tx + fx * 0.5, ty + 6), (cx + fx * 1.2 + sway * 0.6, hem - 4)], '#370a14' if k % 2 == 0 else '#781c29')
    for k in range(-20, 20, 1):
        x = cx + k
        cv.px(x, hem - 3 - (1 if (k // 3) % 2 == 0 else 0), hexc('#e9b645'))
    # tabard with seal
    cv.poly([(tx - 8, ty - 2), (tx + 8, ty - 2), (tx + 9, waist_y + 22), (tx, waist_y + 26), (tx - 9, waist_y + 22)], 'crimson', z=3.3, grp=4, bevel=1, flat=0.35, vgrad=-0.3)
    for (a, b) in (((tx - 8, ty - 1), (tx - 9, waist_y + 22)), ((tx + 8, ty - 1), (tx + 9, waist_y + 22))):
        shade_line(cv, [a, b], '#c38b24')
    # belt
    cv.capsule(tx - 13, waist_y, tx + 13, waist_y, 2.0, 2.0, 'gold', z=3.5, grp=5)
    # chest plate
    cv.poly([(tx - 12, ty - 8), (tx + 12, ty - 8), (tx + 10, ty + 8), (tx, ty + 12), (tx - 10, ty + 8)], 'gold', z=3.6, grp=6, bevel=2, flat=0.15)
    # seal on chest
    sg = 0.3 + 0.7 * p.glow
    cv.ellipse(tx + 1, ty + 1, 6.5, 6.5, 'dark', z=3.7, grp=7, flat=1, ao=False)
    cv.ellipse(tx + 1, ty + 1, 5.5, 5.5, 'glow_y', z=3.75, grp=7, flat=1, bias=sg * 0.6)
    cv.ellipse(tx + 1, ty + 1, 3.6, 3.6, 'dark', z=3.8, grp=7, flat=1, ao=False)
    cv.ellipse(tx + 1, ty + 1, 2.2, 2.2, 'glow_y', z=3.85, grp=7, flat=1, bias=0.3 + sg)
    for k in range(8):
        a = k / 8 * TAU + p.spin
        cv.px(tx + 1 + math.cos(a) * 4.5, ty + 1 + math.sin(a) * 4.5, hexc('#fffbe0'), top=True)
    # pauldrons
    for side, z in ((-1, 3.9), (1, 6.5)):
        px_ = tx + side * 13
        cv.ellipse(px_, ty - 8, 8, 5.5, 'gold', z=z, grp=8 + (side > 0), bias=0.05 if side > 0 else -0.15)
        cv.poly([(px_ - 4, ty - 12), (px_ + side * 10, ty - 20), (px_ + 3, ty - 10)], 'gold', z=z - 0.01, grp=8 + (side > 0), bevel=1)
    # head: horned crown helm
    hx, hy = tx + 3 + lean * 6, ty - 20 + p.headdy
    for side, z in ((-1, 4.6), (1, 5.4)):
        base = (hx + side * 5, hy - 4)
        mid = (hx + side * 12, hy - 12)
        tipp = (hx + side * 9, hy - 22)
        cv.capsule(*base, *mid, 2.4, 1.6, 'bone', z=z, grp=10, bias=-0.1 if side < 0 else 0.05)
        cv.capsule(*mid, *tipp, 1.6, 0.6, 'bone', z=z, grp=10, bias=-0.1 if side < 0 else 0.05)
    cv.ellipse(hx, hy, 8, 8.5, 'gold', z=5, grp=11, bias=0.1)
    cv.rect(hx - 4, hy - 1, hx + 8.5, hy + 4, 'dark', z=5.1, grp=12, bevel=0)
    eye = '#ffe98a' if p.glow > 0.15 else '#916116'
    cv.px(hx + 3, hy + 1, hexc(eye), top=True)
    cv.px(hx + 6, hy + 1, hexc(eye), top=True)
    cv.px(hx + 4, hy + 1, hexc('#fffbe0' if p.glow > 0.5 else eye), top=True)
    for k in range(3):
        cv.poly([(hx - 5 + k * 5, hy - 7), (hx - 3.5 + k * 5, hy - 12 - (2 if k == 1 else 0)), (hx - 2 + k * 5, hy - 7)], 'gold', z=5.05, grp=11, bevel=0, flat=0.5)
    # front arm with chain
    shf = (tx + 13, ty - 7)
    ef = limb(*shf, 13, p.armF)
    hf = limb(*ef, 12, p.armF + p.elbF)
    cv.capsule(*shf, *ef, 5, 4.5, 'crimson', z=6, grp=13)
    cv.capsule(*ef, *hf, 4.5, 4.0, 'crimson', z=6.1, grp=13)
    cv.rect(ef[0] - 4, ef[1] + 1, ef[0] + 4, ef[1] + 6, 'gold', z=6.15, grp=13, bevel=1)
    cv.ellipse(hf[0], hf[1], 3.6, 3.6, 'skin_d', z=6.2, grp=13)
    # chain from front hand
    cl = p.chain
    ca = p.chain_ang
    for k in range(int(cl)):
        lx = hf[0] + math.cos(ca) * k * 3.0
        ly = hf[1] + math.sin(ca) * k * 3.0 + (0 if p.taut else (k * k) * 0.06)
        if k % 2 == 0:
            cv.ellipse(lx, ly, 1.8, 1.2, 'iron', z=6.3, grp=14, rot=ca)
        else:
            cv.ellipse(lx, ly, 1.2, 1.8, 'iron', z=6.3, grp=14, rot=ca)
    if cl >= 1:
        ex = hf[0] + math.cos(ca) * cl * 3.0
        ey = hf[1] + math.sin(ca) * cl * 3.0 + (0 if p.taut else cl * cl * 0.06)
        cv.ellipse(ex, ey, 3.2, 3.2, 'iron', z=6.35, grp=14, bias=0.1)
        cv.ellipse(ex, ey, 1.6, 1.6, 'dark', z=6.36, grp=14, flat=1, ao=False)
    return cv.render()


def guardian_frames():
    out = {k: [] for k in ('idle', 'walk', 'windup', 'attack', 'recover')}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(guardian(P(bob=-1 - math.sin(ph), armF=0.25, elbF=0.4, armB=-0.2, elbB=0.3, glow=0.4 + 0.2 * math.sin(ph), sway=math.sin(ph), spin=ph / 4, chain=6, chain_ang=1.9)))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(guardian(P(bob=-1.5 * abs(math.sin(ph)), lean=0.05, armF=0.1 - 0.2 * math.sin(ph), elbF=0.4, armB=0.2 * math.sin(ph), elbB=0.3, glow=0.4, sway=-2 + math.sin(ph) * 1.5, spin=ph / 4, chain=6, chain_ang=2.0)))
    for i in range(2):
        out['windup'].append(guardian(P(bob=-3 - i, lean=-0.1, armF=-2.6, elbF=-0.2, armB=-2.3, elbB=-0.2, glow=0.8 + 0.2 * i, sway=2, spin=i, chain=7, chain_ang=-2.4, headdy=-1)))
    for i in range(2):
        out['attack'].append(guardian(P(bob=0, lean=0.2, armF=1.45, elbF=0.0, armB=0.9, elbB=0.1, glow=1.0, sway=-3, spin=2 + i, chain=10 + i * 2, chain_ang=-0.05, taut=1, headdy=1)))
    for i in range(2):
        out['recover'].append(guardian(P(bob=3 + i, lean=0.12, armF=0.05, elbF=0.1, armB=0.0, elbB=0.1, glow=0.0, sway=0, chain=6, chain_ang=1.6, headdy=4)))
    return out


# ---------------------------------------------------------------- 2-3 Twins
def twin(p, moon=False):
    W, H, gy, cx = 92, 84, 80, 42
    cv = Canvas(W, H)
    bob = p.bob
    lean = p.lean
    armor = 'moonrobe' if moon else 'sunsteel'
    trim = 'silver' if moon else 'gold'
    hip_y = gy - 26 + bob + p.crouch
    down = p.down
    # legs
    thigh, shin = 12, 12
    for side, z in ((-1, 1), (1, 4)):
        s = math.sin(p.phase + (0 if side > 0 else math.pi))
        la = p.stride * s + (p.legF if side > 0 else p.legB)
        bend = p.bend + max(0.0, -math.cos(p.phase + (0 if side > 0 else math.pi))) * p.stride * 0.9
        hx = cx + side * 3
        k = limb(hx, hip_y, thigh, la)
        f = limb(*k, shin, la - bend)
        bias = -0.2 if side < 0 else 0
        cv.capsule(hx, hip_y, *k, 3.2, 2.8, 'cloth_b' if not moon else 'moonrobe', z=z, grp=20 + side, bias=bias)
        cv.capsule(*k, *f, 2.8, 2.6, trim, z=z + 0.05, grp=20 + side, bias=bias)
        cv.ellipse(f[0] + 2, f[1] - 1.5, 3.6, 2.2, trim, z=z + 0.1, grp=20 + side, bias=bias)
    tx = cx + lean * 16
    ty = hip_y - 13
    # back arm
    shb = (tx - 6, ty - 9)
    eb = limb(*shb, 9, p.armB)
    hb = limb(*eb, 9, p.armB + p.elbB)
    cv.capsule(*shb, *eb, 2.8, 2.5, armor if moon else 'cloth_b', z=1.5, grp=2, bias=-0.25)
    cv.capsule(*eb, *hb, 2.5, 2.3, trim, z=1.6, grp=2, bias=-0.25)
    if moon:
        # long hair + robe skirt
        hx0, hy0 = tx + 2 + lean * 6, ty - 18 + p.headdy
        cv.poly([(hx0 - 6, hy0 - 3), (hx0 + 2, hy0 - 6), (hx0 - 4 - p.hair, ty + 10), (hx0 - 14 - p.hair * 2, ty + 14), (hx0 - 12, hy0 + 4)], 'silver', z=0.8, grp=1, bevel=1, flat=0.2, vgrad=-0.4)
        cv.poly([(tx - 9, hip_y - 4), (tx + 9, hip_y - 4), (cx + 14 + p.sway, gy - 8), (cx - 12 + p.sway, gy - 7)], 'moonrobe', z=3, grp=3, bevel=2, vgrad=-0.5)
    else:
        cv.poly([(tx - 8, hip_y - 3), (tx + 8, hip_y - 3), (cx + 11 + p.sway, hip_y + 9), (cx + 3, hip_y + 7), (cx - 10 + p.sway, hip_y + 9)], 'crimson', z=3, grp=3, bevel=1, vgrad=-0.3)
    # torso
    cv.poly([(tx - 8, ty - 10), (tx + 8, ty - 10), (tx + 7, ty + 6), (tx - 7, ty + 6)], armor, z=3.2, grp=4, bevel=2, flat=0.2, vgrad=-0.3)
    cv.capsule(tx - 8, ty + 6, tx + 8, ty + 6, 1.6, 1.6, trim, z=3.3, grp=4)
    # emblem
    if moon:
        cv.ellipse(tx + 1, ty - 3, 3.5, 3.5, 'glow_moon', z=3.4, grp=5, flat=1, bias=0.4 + 0.5 * p.glow)
        cv.ellipse(tx + 2.6, ty - 4, 3.0, 3.0, armor, z=3.45, grp=5, bias=0.1)
    else:
        cv.ellipse(tx + 1, ty - 3, 3.0, 3.0, 'flame', z=3.4, grp=5, flat=1, bias=0.4 + 0.5 * p.glow)
        for k in range(8):
            a = k / 8 * TAU
            cv.px(tx + 1 + math.cos(a) * 4.6, ty - 3 + math.sin(a) * 4.6, hexc('#ffd56a'), top=True)
    # pauldrons
    cv.ellipse(tx - 7, ty - 9, 4.5, 3.5, trim, z=3.5, grp=6, bias=-0.1)
    # head
    hx, hy = tx + 2 + lean * 6, ty - 18 + p.headdy
    if moon:
        # crescent halo behind head
        cv.ellipse(hx - 3, hy - 1, 12, 12, 'glow_moon', z=0.5, grp=0, flat=1, bias=0.2 + 0.3 * p.glow)
        cv.erase_ellipse(hx + 1, hy - 4, 10, 10, zmax=0.55)
    cv.ellipse(hx, hy, 6.2, 6.8, 'skin_d' if not moon else 'silver', z=5, grp=7, bias=0.05)
    if moon:
        cv.ellipse(hx + 1.5, hy + 1, 4.5, 5, 'moonrobe', z=5.05, grp=7, bias=0.4)
        cv.px(hx + 3, hy + 0.5, hexc('#ffffff'), top=True)
        cv.px(hx + 5, hy + 0.5, hexc('#d2deff'), top=True)
        cv.poly([(hx - 7, hy - 3), (hx + 6, hy - 6), (hx + 7, hy - 2), (hx - 2, hy - 1)], 'silver', z=5.1, grp=8, bevel=1, flat=0.3)
    else:
        # helm with visor and flame crest
        cv.rect(hx - 1, hy - 0.5, hx + 6.5, hy + 2, 'dark', z=5.1, grp=8, bevel=0)
        cv.px(hx + 3, hy + 0.5, hexc('#fff8d8'), top=True)
        cv.px(hx + 5, hy + 0.5, hexc('#ffd56a'), top=True)
        cv.ellipse(hx, hy - 1, 6.6, 6.0, 'sunsteel', z=5.05, grp=8, bias=0.15)
        cv.rect(hx - 1, hy - 0.5, hx + 6.5, hy + 2, 'dark', z=5.15, grp=8, bevel=0)
        cv.px(hx + 3, hy + 0.5, hexc('#fff8d8'), top=True)
        cv.px(hx + 5, hy + 0.5, hexc('#ffd56a'), top=True)
        if not down:
            fl = p.flicker
            for k in range(5):
                bx = hx - 4 + k * 2.2
                hgt = 6 + (3 if (k + int(fl)) % 2 == 0 else 0) + (2 if k == 2 else 0)
                cv.poly([(bx - 1.6, hy - 5), (bx - 3 - k * 0.6 - lean * 8, hy - 5 - hgt), (bx + 1.6, hy - 5)], 'flame', z=4.9, grp=9, bevel=0, flat=0.0, bias=0.6 - k * 0.08)
    # front arm and weapon
    shf = (tx + 6, ty - 9)
    ef = limb(*shf, 9, p.armF)
    hf = limb(*ef, 9, p.armF + p.elbF)
    cv.ellipse(shf[0], shf[1], 4.5, 3.5, trim, z=6.5, grp=10, bias=0.1)
    cv.capsule(*shf, *ef, 2.8, 2.5, armor if moon else 'cloth_b', z=6, grp=11)
    cv.capsule(*ef, *hf, 2.5, 2.3, trim, z=6.05, grp=11)
    wa = p.weapon
    if moon:
        # staff with crescent head
        L = 26
        sx0, sy0 = hf[0] - math.cos(wa) * 8, hf[1] - math.sin(wa) * 8
        sx1, sy1 = hf[0] + math.cos(wa) * (L - 8), hf[1] + math.sin(wa) * (L - 8)
        cv.capsule(sx0, sy0, sx1, sy1, 1.0, 1.0, 'silver', z=6.2, grp=12)
        cv.ellipse(sx1, sy1, 5.0, 5.0, 'glow_moon', z=6.25, grp=12, flat=1, bias=0.3 + 0.6 * p.glow)
        cv.erase_ellipse(sx1 + math.cos(wa) * 2 + 1.5, sy1 + math.sin(wa) * 2 - 1, 4.2, 4.2, zmax=6.26)
    else:
        # curved sun blade
        L = 18
        nxv, nyv = -math.sin(wa), math.cos(wa)
        tipx, tipy = hf[0] + math.cos(wa) * L, hf[1] + math.sin(wa) * L
        mid = (hf[0] + math.cos(wa) * L * 0.55 - nxv * 2.5, hf[1] + math.sin(wa) * L * 0.55 - nyv * 2.5)
        cv.poly([(hf[0] + nxv * 1.8, hf[1] + nyv * 1.8), (mid[0] + nxv * 2.2, mid[1] + nyv * 2.2), (tipx, tipy), (mid[0] - nxv * 0.5, mid[1] - nyv * 0.5), (hf[0] - nxv * 1.4, hf[1] - nyv * 1.4)],
                'sunsteel', z=6.2, grp=12, bevel=1, flat=0.45, edge=0.5)
        cv.line(hf[0] + nxv * 1.2, hf[1] + nyv * 1.2, tipx, tipy, hexc('#fff0c0'))
        if p.glow > 0.6:
            for k in range(4):
                t = 0.3 + k * 0.2
                cv.px(hf[0] + math.cos(wa) * L * t + nxv * 3, hf[1] + math.sin(wa) * L * t + nyv * 3, hexc('#ffd56a'), top=True)
    cv.ellipse(hf[0], hf[1], 2.3, 2.3, trim, z=6.4, grp=11, bias=0.1)
    return cv.render()


def twin_frames(moon):
    out = {k: [] for k in ('idle', 'walk', 'windup', 'attack', 'recover', 'down')}
    w0 = -1.1 if not moon else -1.55
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(twin(P(bob=-0.8 - 0.8 * math.sin(ph), legF=0.1, legB=-0.12, bend=0.15, armF=0.4, elbF=-0.9, armB=-0.2, elbB=0.4, weapon=w0, glow=0.3, flicker=i, hair=math.sin(ph), sway=math.sin(ph)), moon))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(twin(P(phase=ph, stride=0.7, bob=-1.5 * abs(math.cos(ph)), lean=0.15, bend=0.3, armF=0.5, elbF=-0.8, armB=0.6 * math.sin(ph), elbB=0.5, weapon=w0 + 0.3, glow=0.4, flicker=i, hair=2, sway=-2), moon))
    for i in range(2):
        out['windup'].append(twin(P(bob=-1, crouch=3 + i, lean=-0.1, legF=0.5, legB=-0.5, bend=0.7, armF=-2.5, elbF=-0.4, armB=-1.0, elbB=0.3, weapon=-2.6 if not moon else -1.8, glow=0.9, flicker=i, hair=-1, sway=1, headdy=1), moon))
    for i in range(2):
        out['attack'].append(twin(P(bob=0, lean=0.35, legF=0.9, legB=-0.8, bend=0.3, armF=1.55, elbF=0.05, armB=-0.9, elbB=0.2, weapon=0.1 if not moon else -0.2, glow=1.0, flicker=i, hair=3, sway=-3), moon))
    for i in range(2):
        out['recover'].append(twin(P(bob=1, crouch=2, lean=0.2, legF=0.3, legB=-0.3, bend=0.5, armF=0.3, elbF=0.3, armB=0.1, elbB=0.2, weapon=1.2, glow=0.0, flicker=i, headdy=3, sway=0), moon))
    out['down'].append(twin(P(bob=0, crouch=12, lean=0.35, legF=1.4, legB=-1.2, bend=2.4, armF=0.6, elbF=0.4, armB=0.4, elbB=0.3, weapon=1.6, glow=0.0, down=1, headdy=6, sway=0), moon))
    return out


# ---------------------------------------------------------------- 2-6 Lord of white light
def lightlord(p):
    W, H, gy, cx = 132, 116, 112, 64
    cv = Canvas(W, H)
    bob = p.bob
    lean = p.lean
    float_y = gy - 16 + bob
    tx = cx + lean * 12
    ty = float_y - 52
    g = 0.3 + 0.7 * p.glow
    # halo
    hx, hy = tx + 2 + lean * 6, ty - 22 + p.headdy
    cv.ellipse(hx - 3, hy - 2, 17, 17, 'glow_light', z=0.1, grp=0, flat=1, bias=0.1 + 0.35 * g)
    cv.erase_ellipse(hx - 3, hy - 2, 14, 14, zmax=0.15)
    for k in range(12):
        a = k / 12 * TAU + p.spin
        cv.px(hx - 3 + math.cos(a) * 15.5, hy - 2 + math.sin(a) * 15.5, hexc('#ffffff'), top=False)
    # wings of light (behind)
    for k, (ang, ln) in enumerate(((-2.3, 44), (-2.75, 38), (-1.85, 34), (-3.1, 28))):
        a = ang - p.wing * 0.25 * (k + 1)
        bx, by = tx - 6, ty - 6
        ex, ey = bx + math.cos(a) * ln, by + math.sin(a) * ln
        nxv, nyv = -math.sin(a), math.cos(a)
        cv.poly([(bx, by - 2), (ex + nxv * 2, ey + nyv * 2), (ex + math.cos(a) * 4, ey + math.sin(a) * 4), (ex - nxv * 2, ey - nyv * 2), (bx, by + 4)],
                'glow_light', z=0.5, grp=1, bevel=1, flat=0.0, bias=0.15 + 0.3 * g - k * 0.08, edge=0.3)
    # back arm
    shb = (tx - 13, ty - 4)
    eb = limb(*shb, 13, p.armB)
    hb = limb(*eb, 12, p.armB + p.elbB)
    cv.capsule(*shb, *eb, 4.5, 4.0, 'lightarmor', z=1.5, grp=2, bias=-0.3)
    cv.capsule(*eb, *hb, 4.0, 3.6, 'lightarmor', z=1.6, grp=2, bias=-0.3)
    # robe body tapering into light trails
    trail = [(tx - 14, ty + 6), (tx + 14, ty + 6), (tx + 16, float_y - 6), (tx + 8 + p.trail, float_y + 6), (tx + 2, float_y + 2), (tx - 4 - p.trail, float_y + 8),
             (tx - 12, float_y), (tx - 18, float_y - 8)]
    cv.poly(trail, 'lightarmor', z=3, grp=3, bevel=2, flat=0.15, vgrad=-0.7, hgrad=-0.35, edge=0.35)
    shade_line(cv, [(tx - 14, ty + 7), (tx - 18, float_y - 8)], '#c38b24')
    shade_line(cv, [(tx + 14, ty + 7), (tx + 16, float_y - 6)], '#c38b24')
    for k in range(-2, 3):
        shade_line(cv, [(tx + k * 5, ty + 10), (tx + k * 6.5 + p.trail * 0.3, float_y - 2)], '#a6966a')
    # glowing hem drips
    for k in range(-3, 4):
        x = tx + k * 4
        y0 = float_y - 4 - abs(k)
        cv.capsule(x, y0, x - p.trail * 0.2, y0 + 6 + (k % 2) * 3, 1.2, 0.5, 'glow_light', z=2.9, grp=3, bias=0.3 + 0.3 * g)
    # chest armour
    cv.poly([(tx - 14, ty - 10), (tx + 14, ty - 10), (tx + 12, ty + 10), (tx, ty + 16), (tx - 12, ty + 10)], 'lightarmor', z=3.5, grp=4, bevel=2, flat=0.35, edge=0.35)
    cv.poly([(tx - 3, ty - 6), (tx + 4, ty - 6), (tx + 1, ty + 10)], 'gold', z=3.6, grp=4, bevel=1)
    cv.ellipse(tx + 0.5, ty - 2, 3.4, 3.4, 'glow_light', z=3.7, grp=5, flat=1, bias=0.4 + 0.5 * g)
    # pauldrons
    for side, z in ((-1, 3.8), (1, 6.5)):
        px_ = tx + side * 14
        cv.ellipse(px_, ty - 9, 8.5, 6, 'lightarmor', z=z, grp=6 + (side > 0), bias=0.15 if side > 0 else -0.1)
        cv.poly([(px_ - 6, ty - 12), (px_ + side * 4, ty - 22), (px_ + 4, ty - 12)], 'gold', z=z + 0.01, grp=6 + (side > 0), bevel=1)
    # head: featureless mask with tall crown
    cv.ellipse(hx, hy, 7.5, 8.5, 'lightarmor', z=5, grp=8, bias=0.0)
    cv.capsule(hx + 1, hy + 1, hx + 6, hy + 1, 0.8, 0.8, 'glow_light', z=5.1, grp=9, bias=0.6 + 0.4 * g)
    for k in range(5):
        cv.poly([(hx - 7 + k * 3.2, hy - 6), (hx - 6 + k * 3.2, hy - 14 - (6 if k == 2 else (3 if k in (1, 3) else 0))), (hx - 4.5 + k * 3.2, hy - 6)], 'gold', z=5.05, grp=8, bevel=0, flat=0.45)
    cv.capsule(hx - 7, hy - 6, hx + 7, hy - 6, 1.2, 1.2, 'gold', z=5.06, grp=8)
    # front arm + scepter
    shf = (tx + 14, ty - 4)
    ef = limb(*shf, 13, p.armF)
    hf = limb(*ef, 12, p.armF + p.elbF)
    cv.capsule(*shf, *ef, 4.5, 4.0, 'lightarmor', z=6, grp=10)
    cv.capsule(*ef, *hf, 4.0, 3.6, 'lightarmor', z=6.05, grp=10)
    cv.rect(ef[0] - 3.5, ef[1], ef[0] + 3.5, ef[1] + 5, 'gold', z=6.1, grp=10, bevel=1)
    wa = p.weapon
    L = 30
    s0 = (hf[0] - math.cos(wa) * 8, hf[1] - math.sin(wa) * 8)
    s1 = (hf[0] + math.cos(wa) * (L - 8), hf[1] + math.sin(wa) * (L - 8))
    cv.capsule(*s0, *s1, 1.2, 1.2, 'gold', z=6.2, grp=11)
    cv.ellipse(s1[0], s1[1], 3.8 + p.glow * 1.5, 3.8 + p.glow * 1.5, 'glow_light', z=6.3, grp=11, flat=1, bias=0.4 + 0.6 * g)
    for k in range(4):
        a = k / 4 * TAU + p.spin * 2 + 0.4
        cv.line(s1[0] + math.cos(a) * 5, s1[1] + math.sin(a) * 5, s1[0] + math.cos(a) * (7 + p.glow * 3), s1[1] + math.sin(a) * (7 + p.glow * 3), hexc('#fff3c4'), top=True)
    cv.ellipse(hf[0], hf[1], 3.2, 3.2, 'lightarmor', z=6.4, grp=10, bias=0.2)
    return cv.render()


def lightlord_frames():
    out = {k: [] for k in ('idle', 'walk', 'windup', 'attack', 'recover')}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(lightlord(P(bob=-2 * math.sin(ph), armF=0.4, elbF=-0.6, armB=-0.2, elbB=0.4, weapon=-1.4, glow=0.4 + 0.2 * math.sin(ph), wing=math.sin(ph), spin=ph / 6, trail=2 * math.sin(ph))))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(lightlord(P(bob=-2 * math.sin(ph), lean=0.1, armF=0.5, elbF=-0.6, armB=0.0, elbB=0.4, weapon=-1.2, glow=0.45, wing=0.5 + math.sin(ph), spin=ph / 6, trail=-3 + math.sin(ph))))
    for i in range(2):
        out['windup'].append(lightlord(P(bob=-4 - i, lean=-0.12, armF=-2.8, elbF=-0.1, armB=-1.5, elbB=-0.4, weapon=-1.7, glow=0.9 + 0.1 * i, wing=-1 - i, spin=1 + i, trail=3, headdy=-1)))
    for i in range(2):
        out['attack'].append(lightlord(P(bob=0, lean=0.25, armF=1.5, elbF=0.0, armB=-0.8, elbB=0.3, weapon=0.0, glow=1.0, wing=1.5, spin=2 + i, trail=-5)))
    for i in range(2):
        out['recover'].append(lightlord(P(bob=5 + i, lean=0.12, armF=0.1, elbF=0.2, armB=0.05, elbB=0.2, weapon=1.3, glow=0.0, wing=2.0, spin=0, trail=0, headdy=4)))
    return out


# ---------------------------------------------------------------- 3-3 Clockwork knight
def clockknight(p):
    W, H, gy, cx = 100, 88, 84, 46
    cv = Canvas(W, H)
    bob = p.bob
    lean = p.lean
    hip_y = gy - 30 + bob + p.crouch
    for side, z in ((-1, 1), (1, 4)):
        s = math.sin(p.phase + (0 if side > 0 else math.pi))
        la = p.stride * s + (p.legF if side > 0 else p.legB)
        bend = p.bend + max(0.0, -math.cos(p.phase + (0 if side > 0 else math.pi))) * p.stride * 0.9
        hx = cx + side * 4
        k = limb(hx, hip_y, 14, la)
        f = limb(*k, 14, la - bend)
        bias = -0.2 if side < 0 else 0
        cv.capsule(hx, hip_y, *k, 3.8, 3.2, 'cogsteel', z=z, grp=20 + side, bias=bias)
        cv.ellipse(k[0] + 1, k[1], 3.4, 3.4, 'brass', z=z + 0.05, grp=20 + side, bias=bias)
        cv.capsule(*k, *f, 3.2, 3.0, 'cogsteel', z=z + 0.04, grp=20 + side, bias=bias)
        cv.ellipse(f[0] + 2.5, f[1] - 2, 4.5, 2.6, 'brass', z=z + 0.1, grp=20 + side, bias=bias)
    tx = cx + lean * 16
    ty = hip_y - 15
    # wind-up key on the back (rotates)
    kx, ky = tx - 14, ty - 2
    cv.capsule(tx - 8, ty - 2, kx, ky, 1.5, 1.5, 'brass', z=0.8, grp=1)
    kr = p.key
    for side in (-1, 1):
        a = kr + (0 if side > 0 else math.pi)
        lx, ly = kx - 3 + math.cos(a) * 0.5, ky + math.sin(a) * 7
        cv.ellipse(lx - 1.5, ly, 3.0 + abs(math.cos(a)) * 0.0, 3.5 * abs(math.sin(a)) + 1.2, 'brass', z=0.85, grp=1, bias=0.1 * side)
    # back arm
    shb = (tx - 8, ty - 11)
    eb = limb(*shb, 11, p.armB)
    hb = limb(*eb, 10, p.armB + p.elbB)
    cv.capsule(*shb, *eb, 3.4, 3.0, 'cogsteel', z=1.5, grp=2, bias=-0.25)
    cv.capsule(*eb, *hb, 3.0, 2.8, 'cogsteel', z=1.6, grp=2, bias=-0.25)
    # torso
    cv.poly([(tx - 11, ty - 13), (tx + 11, ty - 13), (tx + 9, ty + 8), (tx - 9, ty + 8)], 'cogsteel', z=3, grp=3, bevel=2, flat=0.25, vgrad=-0.3)
    cv.poly([(tx - 9, ty + 7), (tx + 9, ty + 7), (tx + 11, hip_y + 6), (tx - 11, hip_y + 6)], 'brass', z=3.1, grp=3, bevel=1, vgrad=-0.3)
    # chest gear with spinning teeth
    gx, gy2 = tx + 1, ty - 3
    cv.ellipse(gx, gy2, 6, 6, 'brass', z=3.3, grp=4, bias=0.1)
    for k in range(8):
        a = k / 8 * TAU + p.key * 0.5
        cv.ellipse(gx + math.cos(a) * 6.3, gy2 + math.sin(a) * 6.3, 1.4, 1.4, 'brass', z=3.29, grp=4)
    cv.ellipse(gx, gy2, 2.4, 2.4, 'glow_c', z=3.4, grp=4, flat=1, bias=0.3 + 0.6 * p.glow)
    # pauldrons
    for side, z in ((-1, 3.5), (1, 6.5)):
        px_ = tx + side * 11
        cv.ellipse(px_, ty - 12, 6.5, 4.8, 'brass', z=z, grp=5 + (side > 0), bias=0.1 if side > 0 else -0.15)
        for k in range(3):
            cv.px(px_ - 3 + k * 3, ty - 15, hexc('#fff0a6'), top=False)
    # head: helm with gear crest and visor glow
    hx, hy = tx + 3 + lean * 6, ty - 21 + p.headdy
    cv.ellipse(hx - 2, hy - 7, 5, 5, 'brass', z=4.9, grp=7)
    for k in range(6):
        a = k / 6 * TAU + p.key
        cv.ellipse(hx - 2 + math.cos(a) * 5.2, hy - 7 + math.sin(a) * 5.2, 1.2, 1.2, 'brass', z=4.89, grp=7)
    cv.ellipse(hx, hy, 6.8, 7.2, 'cogsteel', z=5, grp=8, bias=0.15)
    cv.rect(hx, hy - 1, hx + 7.5, hy + 1.5, 'dark', z=5.1, grp=9, bevel=0)
    eye = '#d8ffff' if p.glow > 0.3 else '#5ae8ff'
    cv.px(hx + 3, hy, hexc(eye), top=True)
    cv.px(hx + 4, hy, hexc('#5ae8ff'), top=True)
    cv.px(hx + 6, hy, hexc(eye), top=True)
    # front arm + clock-hand sword
    shf = (tx + 9, ty - 11)
    ef = limb(*shf, 11, p.armF)
    hf = limb(*ef, 10, p.armF + p.elbF)
    cv.capsule(*shf, *ef, 3.4, 3.0, 'cogsteel', z=6, grp=10)
    cv.ellipse(ef[0], ef[1], 2.8, 2.8, 'brass', z=6.05, grp=10)
    cv.capsule(*ef, *hf, 3.0, 2.8, 'cogsteel', z=6.06, grp=10)
    wa = p.weapon
    L = 30
    nxv, nyv = -math.sin(wa), math.cos(wa)
    tip = (hf[0] + math.cos(wa) * L, hf[1] + math.sin(wa) * L)
    diamond = (hf[0] + math.cos(wa) * L * 0.72, hf[1] + math.sin(wa) * L * 0.72)
    cv.poly([(hf[0] + nxv * 1.4, hf[1] + nyv * 1.4), (diamond[0] + nxv * 3.0, diamond[1] + nyv * 3.0), (tip[0], tip[1]), (diamond[0] - nxv * 3.0, diamond[1] - nyv * 3.0), (hf[0] - nxv * 1.4, hf[1] - nyv * 1.4)],
            'cogsteel', z=6.2, grp=11, bevel=1, flat=0.45, edge=0.5)
    cv.ellipse(diamond[0], diamond[1], 1.5, 1.5, 'glow_c', z=6.25, grp=11, flat=1, bias=0.5)
    cv.ellipse(hf[0], hf[1], 3.4, 3.4, 'brass', z=6.3, grp=11, bias=0.1)
    if p.blur:
        for k in range(3):
            off = (k + 1) * 4
            for t in range(6):
                q = t / 5
                cv.px(hf[0] + math.cos(wa) * L * q - nxv * off, hf[1] + math.sin(wa) * L * q - nyv * off, hexc('#5ae8ff' if k == 0 else '#2e323c'), top=False)
    return cv.render()


def clockknight_frames():
    out = {k: [] for k in ('idle', 'walk', 'windup', 'attack', 'recover')}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(clockknight(P(bob=-0.8 - 0.8 * math.sin(ph), legF=0.12, legB=-0.12, bend=0.15, armF=0.5, elbF=-0.6, armB=-0.1, elbB=0.3, weapon=-1.3, glow=0.4, key=ph)))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(clockknight(P(phase=ph, stride=0.55, bob=-1.4 * abs(math.cos(ph)), lean=0.08, bend=0.25, armF=0.6, elbF=-0.6, armB=0.4 * math.sin(ph), elbB=0.4, weapon=-1.1, glow=0.4, key=ph * 1.5)))
    for i in range(2):
        out['windup'].append(clockknight(P(bob=-1, crouch=3, lean=-0.12, legF=0.5, legB=-0.5, bend=0.6, armF=-2.6, elbF=-0.6, armB=-1.2, elbB=0.4, weapon=-2.8 - i * 0.2, glow=0.9 + 0.1 * i, key=i * 2.0)))
    for i in range(2):
        out['attack'].append(clockknight(P(bob=0, crouch=2, lean=0.35, legF=1.0, legB=-0.9, bend=0.3, armF=1.5, elbF=0.0, armB=-1.0, elbB=0.2, weapon=0.05 + i * 0.5, glow=1.0, key=3 + i, blur=1)))
    for i in range(2):
        out['recover'].append(clockknight(P(bob=1, crouch=4, lean=0.2, legF=0.3, legB=-0.3, bend=0.6, armF=0.2, elbF=0.3, armB=0.1, elbB=0.2, weapon=1.4, glow=0.0, key=0.4, headdy=3)))
    return out


# ---------------------------------------------------------------- 3-6 Master of the clock tower
def clockmaster(p):
    W, H, gy, cx = 132, 124, 120, 62
    cv = Canvas(W, H)
    bob = p.bob
    lean = p.lean
    hem = gy - 2
    tx = cx + lean * 12
    ty = gy - 66 + bob
    # back arm
    shb = (tx - 15, ty - 10)
    eb = limb(*shb, 14, p.armB)
    hb = limb(*eb, 13, p.armB + p.elbB)
    cv.capsule(*shb, *eb, 4.6, 4.2, 'coat', z=1.5, grp=2, bias=-0.25)
    cv.capsule(*eb, *hb, 4.2, 3.8, 'coat', z=1.6, grp=2, bias=-0.25)
    cv.ellipse(hb[0], hb[1], 3.4, 3.4, 'brass', z=1.7, grp=2)
    # pendulum (behind coat, swinging)
    pa = p.pend
    pvx, pvy = cx, ty + 22
    pex, pey = pvx + math.sin(pa) * 34, pvy + math.cos(pa) * 34
    cv.capsule(pvx, pvy, pex, pey, 1.0, 1.0, 'brass', z=2.0, grp=3)
    cv.ellipse(pex, pey, 6, 6, 'brass', z=2.05, grp=3, bias=0.1)
    cv.ellipse(pex, pey, 2.5, 2.5, 'glow_c', z=2.06, grp=3, flat=1, bias=0.3 + 0.5 * p.glow)
    # long coat: split tails so the pendulum shows
    sway = p.sway
    cv.poly([(tx - 14, ty - 8), (tx + 13, ty - 8), (tx + 15, ty + 22), (cx + 6 + sway, hem - 8), (cx + 2, ty + 26), (cx - 6 + sway, hem - 8), (tx - 17, ty + 22)],
            'coat', z=3, grp=4, bevel=2, flat=0.15, vgrad=-0.45)
    cv.poly([(tx - 17, ty + 20), (cx - 5, ty + 24), (cx - 7 + sway, hem), (cx - 22 + sway * 1.5, hem - 3)], 'coat', z=2.9, grp=4, bevel=2, flat=0.0, vgrad=-0.4)
    cv.poly([(tx + 15, ty + 20), (cx + 5, ty + 24), (cx + 8 + sway, hem), (cx + 22 + sway * 1.2, hem - 3)], 'coat', z=4.5, grp=5, bevel=2, flat=0.1, vgrad=-0.4)
    # legs under coat (thin, dark)
    for side, z in ((-1, 2.5), (1, 2.6)):
        x = cx + side * 5 + math.sin(p.phase + (0 if side > 0 else math.pi)) * p.stride
        cv.capsule(cx + side * 4, ty + 24, x, gy - 3, 2.6, 2.2, 'dark', z=z, grp=6)
        cv.ellipse(x + 2, gy - 2.5, 4, 2.2, 'brass', z=z + 0.1, grp=6)
    # vest + gold buttons
    cv.poly([(tx - 7, ty - 8), (tx + 8, ty - 8), (tx + 7, ty + 16), (tx - 6, ty + 16)], 'brass', z=3.3, grp=7, bevel=1, flat=0.0, vgrad=-0.3)
    for k in range(4):
        cv.px(tx + 1, ty - 4 + k * 5, hexc('#fff0a6'), top=False)
    # gears on shoulders
    for side, z in ((-1, 3.4), (1, 6.4)):
        gx, gyy = tx + side * 14, ty - 10
        cv.ellipse(gx, gyy, 7, 7, 'brass', z=z, grp=8 + (side > 0), bias=0.1 if side > 0 else -0.15)
        for k in range(10):
            a = k / 10 * TAU + p.spin * side
            cv.ellipse(gx + math.cos(a) * 7.3, gyy + math.sin(a) * 7.3, 1.5, 1.5, 'brass', z=z - 0.01, grp=8 + (side > 0), bias=0.0 if side > 0 else -0.15)
        cv.ellipse(gx, gyy, 2.5, 2.5, 'dark', z=z + 0.01, grp=8 + (side > 0), flat=1, ao=False)
    # head: clock face
    hx, hy = tx + 2 + lean * 6, ty - 24 + p.headdy
    cv.ellipse(hx, hy, 12, 12, 'brass', z=5, grp=10, bias=0.1)
    cv.ellipse(hx + 0.5, hy, 9.5, 9.5, 'clockface', z=5.1, grp=11, flat=0.4, bias=0.25)
    for k in range(12):
        a = k / 12 * TAU
        c = '#462b1c' if k % 3 else '#1b100a'
        cv.px(hx + 0.5 + math.cos(a) * 8, hy + math.sin(a) * 8, hexc(c), top=True)
    ha = p.hands
    cv.line(hx + 0.5, hy, hx + 0.5 + math.cos(ha) * 4.5, hy + math.sin(ha) * 4.5, hexc('#1b100a'), top=True)
    cv.line(hx + 0.5, hy, hx + 0.5 + math.cos(ha * 12) * 7, hy + math.sin(ha * 12) * 7, hexc('#1b100a'), top=True)
    cv.px(hx + 0.5, hy, hexc('#5ae8ff' if p.glow < 0.6 else '#d8ffff'), top=True)
    # eye glints on the face
    cv.px(hx + 4, hy - 3, hexc('#5ae8ff'), top=True)
    cv.px(hx - 3, hy - 3, hexc('#5ae8ff'), top=True)
    # top hat brim
    cv.rect(hx - 9, hy - 15, hx + 10, hy - 12, 'coat', z=5.2, grp=12, bevel=1)
    cv.rect(hx - 6, hy - 25, hx + 7, hy - 14, 'coat', z=5.15, grp=12, bevel=1, flat=0.2)
    cv.rect(hx - 6, hy - 18, hx + 7, hy - 16, 'brass', z=5.16, grp=12, bevel=0)
    # front arm holding clock shield
    shf = (tx + 13, ty - 9)
    ef = limb(*shf, 14, p.armF)
    hf = limb(*ef, 13, p.armF + p.elbF)
    cv.capsule(*shf, *ef, 4.6, 4.2, 'coat', z=6, grp=13)
    cv.capsule(*ef, *hf, 4.2, 3.8, 'coat', z=6.05, grp=13)
    sx, sy = hf[0] + 4 + p.shieldx, hf[1] + p.shieldy
    sr = 14
    if p.lowered:
        cv.ellipse(sx - 2, sy + 10, sr * 0.55, sr, 'brass', z=7, grp=14, bias=-0.1, rot=0.6)
    else:
        cv.ellipse(sx, sy, sr * 0.6, sr, 'brass', z=7, grp=14, bias=0.05)
        cv.ellipse(sx + 1.2, sy, sr * 0.45, sr * 0.8, 'clockface', z=7.1, grp=15, flat=0.4, bias=0.15)
        for k in range(12):
            a = k / 12 * TAU
            cv.px(sx + 1.2 + math.cos(a) * sr * 0.38, sy + math.sin(a) * sr * 0.7, hexc('#462b1c'), top=True)
        sa = p.hands * 3
        cv.line(sx + 1.2, sy, sx + 1.2 + math.cos(sa) * 4, sy + math.sin(sa) * 8, hexc('#1b100a'), top=True)
        cv.ellipse(sx + 1.2, sy, 1.5, 1.5, 'glow_c', z=7.2, grp=15, flat=1, bias=0.4 + 0.5 * p.glow)
    return cv.render()


def clockmaster_frames():
    out = {k: [] for k in ('idle', 'walk', 'windup', 'attack', 'recover')}
    for i in range(4):
        ph = i / 4 * TAU
        out['idle'].append(clockmaster(P(bob=-1 - math.sin(ph), armF=0.6, elbF=-0.9, armB=-0.15, elbB=0.3, glow=0.4, pend=0.35 * math.sin(ph), sway=math.sin(ph), spin=ph / 3, hands=i * 0.5)))
    for i in range(4):
        ph = i / 4 * TAU
        out['walk'].append(clockmaster(P(phase=ph, stride=4, bob=-1.5 * abs(math.sin(ph)), lean=0.05, armF=0.6, elbF=-0.9, armB=0.3 * math.sin(ph), elbB=0.3, glow=0.4, pend=0.45 * math.sin(ph), sway=-2 + math.sin(ph), spin=ph / 2, hands=i * 0.5)))
    for i in range(2):
        out['windup'].append(clockmaster(P(bob=-3 - i, lean=-0.1, armF=-0.3, elbF=-1.6, armB=-2.6, elbB=-0.2, glow=0.9 + 0.1 * i, pend=-0.6, sway=2, spin=2 + i, hands=3 + i, shieldy=-4, headdy=-1)))
    for i in range(2):
        out['attack'].append(clockmaster(P(bob=0, lean=0.22, armF=1.2, elbF=-0.4, armB=1.0, elbB=0.1, glow=1.0, pend=0.7, sway=-3, spin=4 + i, hands=5 + i, shieldx=4)))
    for i in range(2):
        out['recover'].append(clockmaster(P(bob=4 + i, lean=0.12, armF=0.2, elbF=0.2, armB=0.05, elbB=0.1, glow=0.0, pend=0.1, sway=0, spin=0, hands=0.3, lowered=1, headdy=4)))
    return out


ALL = {
    'MidBoss': (gatekeeper_frames, (46, 80)),
    'Boss': (guardian_frames, (56, 100)),
    'TwinSun': (lambda: twin_frames(False), (42, 80)),
    'TwinMoon': (lambda: twin_frames(True), (42, 80)),
    'LightLord': (lightlord_frames, (64, 112)),
    'ClockKnight': (clockknight_frames, (46, 84)),
    'ClockMaster': (clockmaster_frames, (62, 120)),
}

if __name__ == '__main__':
    import sys
    names = sys.argv[1:] or list(ALL)
    for n in names:
        fr = ALL[n][0]()
        allf = []
        for k in ('idle', 'walk', 'windup', 'attack', 'recover', 'down'):
            allf += fr.get(k, [])
        preview(allf, scale=3, cols=8, path='prev_%s.png' % n)
        print(n, len(allf))
