import math
from px import Canvas, ramp, limb, preview, recolor, hexc
from hum import Pose, Rig, walk_pose, idle_pose, draw_leg, draw_arm, TAU

ramp('cloak', ['#17112b', '#241d45', '#342f66', '#4a4a8c', '#6668ad', '#8c90cf'])
ramp('cloak_in', ['#08050f', '#0f0a1c', '#171129'])
ramp('pants', ['#140f1d', '#211a2e', '#30283f', '#433a55'])
ramp('boot', ['#1a1018', '#2c1b26', '#43293a', '#5e3b4f', '#7a5068'])
ramp('glove', ['#1d1424', '#33253d', '#4c3a57', '#6a5577'])
ramp('scarf', ['#0b3a48', '#0f5d70', '#14879a', '#22b3c2', '#5fe0e4', '#bff8f4'])
ramp('eye', ['#2fb7c9', '#7defff', '#e8ffff'], emissive=True)
ramp('blade', ['#252c3d', '#46536d', '#7b8ba9', '#b9c7de', '#f1f6ff'])
ramp('lining', ['#3b0f2a', '#5c1a3c', '#7d2a4e'])
ramp('belt', ['#2a1a14', '#4a2f22', '#6d4632', '#946347'])
ramp('buckle', ['#6b4a1d', '#a8792d', '#e0b354', '#fff0b0'])

W, H = 64, 56
GY = 53
CX = 30
DIMS = dict(thigh=7.5, shin=7.0, torso=12.5, head=7.5, shoulder_w=10, hip_w=5, upper=5.5, fore=5.5)


def scarf_tail(cv, x, y, phase, length=6, amp=1.3, drift=0.0, lift=0.0, z=0.5, straight=0.0):
    pts = [(x, y)]
    for k in range(1, length + 1):
        wave = amp * math.sin(phase - k * 0.95) * (1 - straight)
        pts.append((x - k * 3.1 - drift * k * 0.45, y + k * (0.7 - lift) + wave))
    widths = [3.6 - k * 0.32 for k in range(length + 1)]
    cv.ribbon(pts, widths, 'scarf', z=z, grp=9, fold=0.1)
    tx, ty = pts[-1]
    # frayed split tip
    cv.capsule(tx, ty - 0.6, tx - 2.4, ty - 1.0 + math.sin(phase) * 0.6, 0.7, 0.5, 'scarf', z=z, grp=9, bias=-0.2)
    cv.capsule(tx, ty + 0.6, tx - 2.0, ty + 1.4 + math.sin(phase + 1) * 0.6, 0.7, 0.5, 'scarf', z=z, grp=9, bias=-0.4)


def draw_player(p, blade=0, cloak_wind=0.0):
    cv = Canvas(W, H)
    r = Rig(CX, GY, DIMS, p)
    hx, hy = r.hip
    nx, ny = r.neck
    # scarf tail behind everything
    scarf_tail(cv, nx - 2, ny + 0.5, p.phase * 1.0 + p.get('scarf_phase', 0), length=p.get('scarf_len', 6),
               amp=p.get('scarf_amp', 1.3), drift=cloak_wind, lift=p.get('scarf_lift', 0.0), z=0.2, straight=p.get('scarf_straight', 0.0))
    # back arm and leg
    draw_arm(cv, r.shB, r.elbB, r.handB, 1.7, 1.5, 'cloak', 'glove', z=1, grp=1)
    draw_leg(cv, r.hipB, r.kneeB, r.footB, 2.1, 1.8, 'pants', 'boot', z=1.5, grp=2, boot_len=2.6, boot_r=1.9)
    # cloak body
    lean = p.lean
    sway = p.get('cloak_sway', 0.0)
    hem_y = hy + 6.5 + p.get('hem_drop', 0)
    back = -8.5 - cloak_wind * 3.5 + sway
    front = 7.0 + sway * 0.4 - cloak_wind * 0.5
    pts = [
        (nx - 3.5, ny - 0.5), (nx + 3.8, ny - 0.5),
        (nx + 6.0, ny + 3.5),
        (hx + front, hem_y - 1.0),
        (hx + front - 2.0, hem_y + 0.8),
        (hx + 1.0, hem_y - 0.2 + math.sin(p.phase * 2) * 0.5),
        (hx - 3.5, hem_y + 1.0),
        (hx + back, hem_y - 0.5 + cloak_wind * -2.0),
        (nx - 6.5 - cloak_wind * 1.0, ny + 4.0),
    ]
    cv.poly(pts, 'cloak', z=3, grp=3, bevel=2, flat=0.12, edge=0.42, vgrad=-0.55, hgrad=-0.2)
    # inner lining visible at the trailing hem
    if cloak_wind > 0.3:
        cv.poly([(hx + back + 0.5, hem_y - 0.5 - cloak_wind * 2.0), (hx - 3.0, hem_y + 0.5), (hx - 2.0, hem_y - 2.0), (hx + back + 3.0, hem_y - 3.0 - cloak_wind)],
                'lining', z=3.05, grp=3, bevel=0, flat=0.0)
    # shoulder mantle
    m = [(nx - 5.5 - cloak_wind * 0.8, ny + 1.0), (nx - 2.0, ny - 1.2), (nx + 3.5, ny - 1.0), (nx + 6.5, ny + 2.5),
         (nx + 5.5, ny + 5.8), (nx + 2.5, ny + 4.6), (nx - 0.5, ny + 6.4), (nx - 3.5, ny + 5.0), (nx - 6.0 - cloak_wind, ny + 5.6)]
    cv.poly(m, 'cloak', z=3.3, grp=11, bevel=1, flat=0.3, edge=0.4, vgrad=-0.3)
    # cloak fold lines
    cv.line(hx - 1.5, ny + 6, hx - 2.5 + sway * 0.3, hem_y - 1, hexc('#241d45'))
    cv.line(hx + 3.2, ny + 7, hx + 3.6, hem_y - 2, hexc('#342f66'))
    # belt
    cv.capsule(hx - 5.5, hy - 2.0, hx + 6.0, hy - 2.4, 1.1, 1.1, 'belt', z=3.5, grp=3)
    cv.ellipse(hx + 3.5, hy - 2.3, 1.4, 1.3, 'buckle', z=3.6, grp=3)
    # front leg
    draw_leg(cv, r.hipF, r.kneeF, r.footF, 2.2, 1.9, 'pants', 'boot', z=2.5, grp=4, boot_len=2.8, boot_r=2.0)
    # head / hood
    hdx, hdy = r.head
    tip_sway = p.get('hood_tip', 0.0)
    cv.poly([(hdx - 6.0, hdy - 3.0), (hdx - 11.5 - tip_sway - cloak_wind * 2, hdy + 1.5 + tip_sway * 0.3), (hdx - 5.0, hdy + 4.5)],
            'cloak', z=4.9, grp=5, bevel=1, flat=-0.15)
    cv.ellipse(hdx, hdy, 7.6, 7.2, 'cloak', z=5, grp=5, bias=0.12)
    # face opening
    cv.ellipse(hdx + 3.0, hdy + 1.3, 4.6, 4.4, 'cloak_in', z=5.2, grp=6, flat=0.6, bias=-0.3, ao=False)
    # hood rim highlight
    for a in range(-60, 70, 12):
        ang = math.radians(a)
        cv.px(hdx + 3.0 + math.cos(ang) * 5.0, hdy + 1.3 - math.sin(ang) * 4.9, hexc('#6668ad'))
    # eyes
    blink = p.get('blink', 0)
    ey = hdy + 1.0
    if blink:
        cv.px(hdx + 2.5, ey + 0.8, hexc('#7defff'), top=True)
        cv.px(hdx + 3.5, ey + 0.8, hexc('#7defff'), top=True)
        cv.px(hdx + 5.5, ey + 0.8, hexc('#7defff'), top=True)
        cv.px(hdx + 6.5, ey + 0.8, hexc('#7defff'), top=True)
    else:
        for ex in (hdx + 3.0, hdx + 6.0):
            cv.px(ex, ey, hexc('#e8ffff'), top=True)
            cv.px(ex, ey + 1, hexc('#7defff'), top=True)
            cv.px(ex - 1, ey + 1, hexc('#2fb7c9'), top=True)
    # scarf wrap at the neck
    cv.ellipse(nx + 0.6, ny + 0.6, 5.6, 2.4, 'scarf', z=6, grp=7, bias=0.05)
    cv.capsule(nx + 3.2, ny + 1.2, nx + 4.5 + p.get('knot', 0), ny + 4.8, 1.4, 1.0, 'scarf', z=6.2, grp=7)
    # front arm
    draw_arm(cv, r.shF, r.elbF, r.handF, 1.8, 1.6, 'cloak', 'glove', z=7, grp=8, hand_r=1.7)
    if blade:
        hxp, hyp = r.handF
        ang = p.get('blade_ang', 0.0)
        L = p.get('blade_len', 9.0) * 1.25
        tipx = hxp + math.cos(ang) * L
        tipy = hyp + math.sin(ang) * L
        nxv, nyv = -math.sin(ang), math.cos(ang)
        pts = [(hxp - nxv * 1.3, hyp - nyv * 1.3), (hxp + nxv * 1.9, hyp + nyv * 1.9),
               (tipx + nxv * 0.9, tipy + nyv * 0.9), (tipx + math.cos(ang) * 2.5, tipy + math.sin(ang) * 2.5)]
        cv.poly(pts, 'blade', z=7.5, grp=10, bevel=1, flat=0.35, edge=0.5)
        cv.capsule(hxp - math.cos(ang) * 1.0, hyp - math.sin(ang) * 1.0, hxp - math.cos(ang) * 2.5, hyp - math.sin(ang) * 2.5, 0.9, 0.9, 'belt', z=7.6, grp=10)
        cv.line(hxp + nxv * 0.6 + math.cos(ang) * 2, hyp + nyv * 0.6 + math.sin(ang) * 2, tipx, tipy, hexc('#f1f6ff'), top=False)
    return cv.render()


def frames():
    out = {}
    # idle
    idle = []
    for i in range(4):
        p = idle_pose(i, 4, breath=1.0)
        p['scarf_amp'] = 0.7
        p['scarf_lift'] = 0.35
        p['scarf_len'] = 5
        p['cloak_sway'] = 0.4 * math.sin(p.phase)
        p['hood_tip'] = 0.5 * math.sin(p.phase + 1)
        p['blink'] = 1 if i == 3 else 0
        idle.append(draw_player(p))
    out['idle'] = idle
    # run
    run = []
    for i in range(6):
        p = walk_pose(i, 6, stride=0.85, bob=1.6, arm=0.75, lean=0.16)
        p['bendF'] = 0.2 + 1.1 * max(0.0, -math.sin(p.phase + 0.6))
        p['bendB'] = 0.2 + 1.1 * max(0.0, math.sin(p.phase + 0.6))
        p['scarf_amp'] = 1.5
        p['scarf_phase'] = p.phase
        p['cloak_sway'] = -0.6
        p['hood_tip'] = 1.0
        p['elbF'] = 0.9
        p['elbB'] = 0.9
        run.append(draw_player(p, cloak_wind=0.8))
    out['run'] = run
    # jump (rising) and fall
    jumps = []
    for i, t in enumerate((0.0, 1.0)):
        p = Pose(legF=0.7, legB=-0.2, bendF=1.3, bendB=0.9 + t * 0.2, armF=-0.6 - t * 0.3, armB=0.9, elbF=0.7, elbB=0.6,
                 bob=2.5, lean=0.05, phase=1.2 + t, scarf_amp=0.6, scarf_lift=-0.5, hem_drop=1.5, hood_tip=-0.5, cloak_sway=1.0)
        jumps.append(draw_player(p, cloak_wind=0.2))
    out['jump'] = jumps
    falls = []
    for i in range(2):
        p = Pose(legF=0.35, legB=-0.35, bendF=0.5, bendB=0.7, armF=-1.6 + i * 0.2, armB=1.8, elbF=-0.4, elbB=0.5,
                 bob=1.0, lean=-0.05, phase=2.5 + i * 1.5, scarf_amp=0.9, scarf_lift=1.4, hem_drop=-1.5, hood_tip=1.5, cloak_sway=-1.2)
        falls.append(draw_player(p, cloak_wind=-0.4))
    out['fall'] = falls
    # dash
    dash = []
    for i in range(2):
        p = Pose(legF=1.1, legB=-1.0, bendF=0.9, bendB=0.4, armF=1.3, armB=-1.5, elbF=0.2, elbB=0.2,
                 bob=1.5, lean=0.55, phase=i * 3.0, scarf_amp=0.4, scarf_straight=0.7, scarf_lift=0.9, scarf_len=7,
                 hood_tip=3.0, cloak_sway=-2.0, crouch=2.0)
        dash.append(draw_player(p, cloak_wind=2.0))
    out['dash'] = dash
    # attack: anticipation, swing, follow-through, settle
    att = []
    specs = [
        dict(armF=-2.3, elbF=-0.6, lean=-0.12, legF=0.3, legB=-0.3, bendF=0.3, bendB=0.4, blade_ang=-2.2, blade_len=8, crouch=1),
        dict(armF=1.75, elbF=0.15, lean=0.25, legF=0.65, legB=-0.55, bendF=0.4, bendB=0.3, blade_ang=0.05, blade_len=10, crouch=2),
        dict(armF=1.0, elbF=0.9, lean=0.2, legF=0.6, legB=-0.5, bendF=0.4, bendB=0.3, blade_ang=1.3, blade_len=9, crouch=1.5),
        dict(armF=0.4, elbF=0.7, lean=0.08, legF=0.3, legB=-0.3, bendF=0.2, bendB=0.3, blade_ang=1.75, blade_len=8, crouch=0.5),
    ]
    for i, s in enumerate(specs):
        p = Pose(armB=-0.6, elbB=0.4, bob=0, phase=i * 1.2, scarf_amp=1.0, scarf_phase=i, hood_tip=0.5 + i * 0.3, cloak_sway=-0.5 + i * 0.3)
        p.update(s)
        att.append(draw_player(p, blade=1, cloak_wind=0.5 if i in (1, 2) else 0.1))
    out['attack'] = att
    # hurt
    p = Pose(legF=-0.2, legB=0.3, bendF=0.5, bendB=0.4, armF=-1.2, armB=1.0, elbF=0.9, elbB=0.6, bob=0.5,
             lean=-0.3, headtilt=-0.2, phase=0.5, scarf_amp=1.8, hood_tip=-1.5, cloak_sway=1.5, blink=1)
    out['hurt'] = [draw_player(p, cloak_wind=-0.6)]
    return out


SHADOW_MAP = {}


def shadow_palette():
    """Player colours -> shadow clone colours (deep violet body, glowing lavender eyes and scarf)."""
    from px import RAMPS
    def mk(src, dst):
        for a, b in zip(RAMPS[src], dst):
            SHADOW_MAP[a] = hexc(b)
    mk('cloak', ['#0c0614', '#160a26', '#21103a', '#2f1852', '#40226b', '#553088'])
    mk('cloak_in', ['#030106', '#06020c', '#0a0414'])
    mk('pants', ['#0a0510', '#120a1c', '#1b1028', '#261838'])
    mk('boot', ['#0a0510', '#140b1e', '#1f1230', '#2c1a42', '#3a2454'])
    mk('glove', ['#0f0718', '#1a0e28', '#27163a', '#36204f'])
    mk('scarf', ['#3a1466', '#5a1f96', '#7c32c4', '#a052e6', '#c88cff', '#efd9ff'])
    mk('eye', ['#b07cff', '#e2c8ff', '#ffffff'])
    mk('blade', ['#1e1430', '#3a2a5c', '#64509a', '#a08ad0', '#e6dcff'])
    mk('belt', ['#120a1c', '#1f1230', '#2c1a42', '#3a2454'])
    mk('buckle', ['#4a2a7a', '#7a4cc0', '#b98cff', '#f0e0ff'])
    # decal / outline colours that are not ramp entries
    extra = {'#241d45': '#160a26', '#342f66': '#21103a', '#6668ad': '#553088', '#7defff': '#e2c8ff', '#e8ffff': '#ffffff',
             '#2fb7c9': '#b07cff', '#f1f6ff': '#e6dcff'}
    for a, b in extra.items():
        SHADOW_MAP[hexc(a)] = hexc(b)
    return SHADOW_MAP


if __name__ == '__main__':
    fr = frames()
    allf = []
    for k in ('idle', 'run', 'jump', 'fall', 'dash', 'attack', 'hurt'):
        allf += fr[k]
    preview(allf, scale=5, cols=8, path='prev_player.png')
    sm = shadow_palette()
    preview([recolor(f, sm) for f in allf[:10]], scale=5, cols=10, path='prev_shadow.png')
    print(len(allf))
