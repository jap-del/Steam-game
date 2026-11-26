"""Tintable grayscale props and tiles. The game multiplies these by a colour, so keep them neutral."""
import math
import numpy as np
from px import Canvas, ramp, preview, hexc

ramp('g', ['#121212', '#242424', '#3a3a3a', '#535353', '#6e6e6e', '#8c8c8c', '#adadad', '#d0d0d0', '#f2f2f2'])
ramp('gw', ['#5a5a5a', '#9a9a9a', '#d8d8d8', '#ffffff'])
ramp('ge', ['#bdbdbd', '#ffffff'], emissive=True)
G = ['#121212', '#242424', '#3a3a3a', '#535353', '#6e6e6e', '#8c8c8c', '#adadad', '#d0d0d0', '#f2f2f2']


def door():
    cv = Canvas(52, 84)
    # stone arch frame
    cv.rect(2, 10, 50, 84, 'g', z=1, bevel=2, flat=0.15, vgrad=-0.2)
    cv.ellipse(26, 22, 24, 20, 'g', z=1, flat=0.6, bias=0.1)
    # blocks of the arch
    for k in range(9):
        a = math.pi + k / 8 * math.pi
        x0, y0 = 26 + math.cos(a) * 15, 22 + math.sin(a) * 13
        x1, y1 = 26 + math.cos(a) * 24, 22 + math.sin(a) * 20
        cv.line(x0, y0, x1, y1, hexc(G[2]))
    for y in (34, 46, 58, 70):
        cv.line(2, y, 9, y, hexc(G[2]))
        cv.line(43, y, 50, y, hexc(G[2]))
    # keystone
    cv.poly([(22, 0), (30, 0), (29, 9), (23, 9)], 'g', z=1.5, bevel=1, flat=0.4)
    cv.ellipse(26, 4, 1.6, 1.6, 'ge', z=1.6, flat=1, bias=0.6)
    # inner opening: bright swirling portal (takes the tint)
    cv.ellipse(26, 24, 15, 14, 'gw', z=2, flat=0.3, bias=0.2, ao=False)
    cv.rect(11, 24, 41, 84, 'gw', z=2, bevel=0, flat=0.3, ao=False)
    for k in range(5):
        r = 3 + k * 2.6
        for a in range(0, 360, 14):
            ang = math.radians(a + k * 25)
            x = 26 + math.cos(ang) * r
            y = 44 + math.sin(ang) * r * 1.5
            if 11 < x < 41 and 12 < y < 84:
                cv.px(x, y, hexc('#ffffff' if (a // 14 + k) % 3 == 0 else '#d8d8d8'))
    # runes on pillars
    for y in (40, 52, 64):
        cv.px(5, y, hexc(G[7]))
        cv.px(6, y + 1, hexc(G[7]))
        cv.px(46, y, hexc(G[7]))
        cv.px(45, y + 1, hexc(G[7]))
    # steps
    cv.rect(0, 80, 52, 84, 'g', z=3, bevel=1, flat=0.3)
    return cv.render()


def potion():
    cv = Canvas(32, 32)
    cv.rect(13, 3, 19, 8, 'g', z=1, bevel=1, flat=0.0)        # cork
    cv.rect(12, 8, 20, 12, 'gw', z=1.2, bevel=1, flat=0.1)    # neck
    cv.ellipse(16, 21, 10, 9.5, 'gw', z=1, flat=0.0, bias=0.0)  # glass
    cv.ellipse(16, 23, 8.2, 7, 'g', z=1.1, flat=0.0, bias=0.35, ao=False)  # liquid
    cv.rect(8, 17, 24, 19, 'gw', z=1.15, bevel=0, flat=0.6, ao=False)
    cv.erase_ellipse(0, 0, 0.1, 0.1)
    for (x, y) in ((12, 24), (18, 26), (15, 21), (20, 22)):
        cv.px(x, y, hexc('#ffffff'))
    cv.line(10, 16, 10, 22, hexc('#ffffff'), top=True)
    cv.px(11, 15, hexc('#ffffff'), top=True)
    return cv.render()


def crystal():
    cv = Canvas(32, 32)
    pts = [(16, 1), (24, 10), (21, 26), (16, 30), (11, 26), (8, 10)]
    cv.poly(pts, 'gw', z=1, bevel=2, flat=0.2, edge=0.5)
    cv.poly([(16, 1), (16, 30), (11, 26), (8, 10)], 'gw', z=1.1, bevel=0, flat=-0.4)
    cv.line(16, 2, 16, 29, hexc('#ffffff'))
    cv.line(9, 10, 23, 10, hexc('#d8d8d8'))
    cv.px(12, 7, hexc('#ffffff'), top=True)
    cv.px(13, 6, hexc('#ffffff'), top=True)
    for (x, y) in ((4, 6), (27, 16), (5, 22)):
        cv.px(x, y, hexc('#ffffff'), top=True)
    return cv.render()


def altar():
    cv = Canvas(40, 40)
    cv.rect(4, 32, 36, 40, 'g', z=1, bevel=1, flat=0.2)
    cv.poly([(9, 16), (31, 16), (28, 33), (12, 33)], 'g', z=1.2, bevel=2, flat=0.2, vgrad=-0.3)
    cv.rect(5, 12, 35, 17, 'g', z=1.4, bevel=1, flat=0.4)
    cv.ellipse(20, 12, 9, 3, 'g', z=1.5, flat=0.0, bias=-0.3)
    # floating shard above the bowl
    cv.poly([(20, 0), (24, 6), (20, 11), (16, 6)], 'gw', z=2, bevel=1, flat=0.3, edge=0.5)
    cv.line(20, 1, 20, 10, hexc('#ffffff'))
    # carved runes
    for x in (14, 20, 26):
        cv.line(x, 21, x, 27, hexc(G[7]))
        cv.px(x - 1, 23, hexc(G[7]))
        cv.px(x + 1, 25, hexc(G[7]))
    return cv.render()


def orb():
    cv = Canvas(16, 16)
    cv.ellipse(8, 8, 7, 7, 'gw', z=1, flat=0.5, bias=0.1)
    cv.ellipse(7, 7, 4, 4, 'gw', z=1.1, flat=0.8, bias=0.6, ao=False)
    cv.px(6, 5, hexc('#ffffff'), top=True)
    cv.px(5, 6, hexc('#ffffff'), top=True)
    return cv.render(outline=None)


def gear():
    cv = Canvas(24, 24)
    cv.ellipse(12, 12, 8.5, 8.5, 'g', z=1, bias=0.3)
    for k in range(8):
        a = k / 8 * math.tau
        cx, cy = 12 + math.cos(a) * 9.5, 12 + math.sin(a) * 9.5
        c, s = math.cos(a), math.sin(a)
        pts = [(cx + (-s) * 2 + c * -2, cy + c * 2 + s * -2), (cx + (-s) * 1.5 + c * 2, cy + c * 1.5 + s * 2),
               (cx - (-s) * 1.5 + c * 2, cy - c * 1.5 + s * 2), (cx - (-s) * 2 + c * -2, cy - c * 2 + s * -2)]
        cv.poly(pts, 'g', z=0.9, bevel=1, flat=0.4)
    cv.ellipse(12, 12, 5.5, 5.5, 'g', z=1.1, bias=-0.2, flat=0.4)
    cv.erase_ellipse(12, 12, 2.4, 2.4)
    for k in range(4):
        a = k / 4 * math.tau + 0.4
        cv.px(12 + math.cos(a) * 4, 12 + math.sin(a) * 4, hexc(G[2]))
    return cv.render()


def bomb():
    cv = Canvas(16, 16)
    cv.ellipse(8, 9.5, 6, 6, 'g', z=1, bias=0.2)
    cv.rect(6, 2.5, 10, 5, 'g', z=1.1, bevel=1, flat=0.5)
    cv.line(9, 2, 11, 0, hexc(G[6]))
    cv.px(5, 7, hexc('#ffffff'), top=True)
    cv.px(6, 6, hexc(G[8]), top=True)
    return cv.render()


def seal():
    cv = Canvas(24, 36)
    cv.poly([(5, 6), (12, 0), (19, 6), (20, 32), (4, 32)], 'g', z=1, bevel=2, flat=0.25, vgrad=-0.3)
    cv.rect(1, 31, 23, 36, 'g', z=1.2, bevel=1, flat=0.3)
    cv.ellipse(12, 16, 5, 5, 'gw', z=1.3, flat=0.6, bias=0.1, ao=False)
    cv.ellipse(12, 16, 3, 3, 'g', z=1.35, flat=0.6, bias=-0.2, ao=False)
    cv.ellipse(12, 16, 1.4, 1.4, 'gw', z=1.4, flat=1, bias=0.6, ao=False)
    for y in (8, 24):
        cv.line(9, y, 15, y, hexc('#d8d8d8'))
    return cv.render()


def slash():
    """Crescent swoosh, brightest at the leading edge, fading towards the inside."""
    W, H = 54, 42
    img = np.zeros((H, W, 4), dtype=np.uint8)
    cx, cy = 12, 21
    for y in range(H):
        for x in range(W):
            dx, dy = (x + 0.5 - cx) / 40.0, (y + 0.5 - cy) / 20.0
            r = math.hypot(dx, dy)
            dx2, dy2 = (x + 0.5 - (cx - 9)) / 40.0, (y + 0.5 - cy) / 17.0
            r2 = math.hypot(dx2, dy2)
            if r <= 1.0 and r2 > 1.0:
                ang = abs(math.atan2(dy, dx))
                if ang > 1.45:
                    continue
                edge = 1.0 - min(1.0, (r2 - 1.0) * 6.0)
                fade = 1.0 - ang / 1.45
                v = 0.55 + 0.45 * (1 - edge)
                a = min(1.0, fade * 1.3) * (0.55 + 0.45 * (r > 0.93))
                if r > 0.93:
                    v = 1.0
                lv = int(255 * v)
                img[y, x] = (lv, lv, lv, int(255 * a))
    # speed streaks
    for k, yy in enumerate((9, 15, 27, 33)):
        for x in range(30 - k * 3, 44 - k * 2):
            if 0 <= x < W:
                img[yy, x] = (255, 255, 255, 120)
    return img


def stone_tile():
    """Seamless 32x32 brick tile."""
    S = 32
    rng = np.random.RandomState(7)
    v = np.zeros((S, S))
    rows = [(0, 8), (8, 16), (16, 24), (24, 32)]
    for ri, (y0, y1) in enumerate(rows):
        off = 0 if ri % 2 == 0 else 8
        for bi in range(2):
            x0 = (off + bi * 16) % S
            base = 0.52 + rng.uniform(-0.08, 0.08)
            for y in range(y0, y1):
                for xx in range(16):
                    x = (x0 + xx) % S
                    lv = base
                    if y == y0 or xx == 0:
                        lv = 0.22  # mortar
                    elif y == y0 + 1 or xx == 1:
                        lv = base + 0.18  # lit top-left edge
                    elif y == y1 - 1 or xx == 15:
                        lv = base - 0.14
                    lv += rng.uniform(-0.035, 0.035)
                    v[y, x] = lv
    # cracks and chips
    for (x, y, dx, dy, n) in ((5, 3, 1, 1, 3), (22, 18, -1, 1, 4), (13, 27, 1, 0, 3)):
        for k in range(n):
            v[(y + k * dy) % S, (x + k * dx) % S] = 0.26
    for (x, y) in ((27, 11), (9, 20), (3, 29)):
        v[y, x] = 0.7
    lv = np.clip(v, 0, 1)
    out = np.zeros((S, S, 4), dtype=np.uint8)
    q = (np.round(lv * 12) / 12 * 255).astype(np.uint8)
    out[:, :, 0] = out[:, :, 1] = out[:, :, 2] = q
    out[:, :, 3] = 255
    return out


def cap_tile():
    S, Hh = 32, 8
    out = np.zeros((Hh, S, 4), dtype=np.uint8)
    rng = np.random.RandomState(3)
    for x in range(S):
        drip = 5 + int(2 * (math.sin(x * 0.9) > 0.6)) + (1 if x % 7 == 3 else 0)
        for y in range(Hh):
            if y > drip:
                continue
            if y == 0:
                lv = 0.98
            elif y == 1:
                lv = 0.85
            elif y == drip:
                lv = 0.4
            else:
                lv = 0.66 + rng.uniform(-0.06, 0.06)
            q = int(round(lv * 12) / 12 * 255)
            out[y, x] = (q, q, q, 255)
    for x in (4, 13, 21, 29):
        out[2, x] = (255, 255, 255, 255)
    return out


def glass_tile():
    S, Hh = 32, 16
    out = np.zeros((Hh, S, 4), dtype=np.uint8)
    for y in range(Hh):
        for x in range(S):
            lv = 0.62
            if y == 0:
                lv = 1.0
            elif y == Hh - 1:
                lv = 0.35
            elif (x - y * 1.2) % 32 < 3:
                lv = 0.92
            elif (x - y * 1.2) % 32 < 5:
                lv = 0.78
            if x == 0:
                lv -= 0.15
            q = int(round(lv * 12) / 12 * 255)
            out[y, x] = (q, q, q, 255)
    return out


def spikes_tile():
    cv = Canvas(32, 16)
    for k in range(4):
        x = 4 + k * 8
        cv.poly([(x - 4, 16), (x, 1), (x + 4, 16)], 'g', z=1, bevel=1, flat=0.3, edge=0.45, hgrad=-0.4)
        cv.px(x - 1, 4, hexc('#f2f2f2'), top=True)
    cv.rect(0, 13, 32, 16, 'g', z=2, bevel=0, flat=-0.2)
    return cv.render(outline=None)


ALL = {
    'Door': door, 'Potion': potion, 'Crystal': crystal, 'Altar': altar, 'Orb': orb, 'Gear': gear, 'Bomb': bomb,
    'Seal': seal, 'Slash': slash, 'Stone': stone_tile, 'Cap': cap_tile, 'Glass': glass_tile, 'Spikes': spikes_tile,
}

if __name__ == '__main__':
    from PIL import Image
    ims = []
    for name, fn in ALL.items():
        a = fn()
        ims.append(a)
        print(name, a.shape)
    # tint previews
    tinted = []
    tints = [(0.3, 0.9, 0.5), (0.9, 0.35, 0.4), (0.5, 0.75, 1), (0.76, 0.64, 1), (1, 0.8, 0.2), (0.95, 0.68, 0.32), (0.5, 0.5, 0.56), (0.35, 0.45, 0.6), (1, 1, 1), (0.32, 0.34, 0.42), (0.36, 0.58, 0.55), (0.6, 0.85, 1), (0.6, 0.6, 0.65)]
    for a, t in zip(ims, tints):
        b = a.astype(float)
        b[:, :, 0] *= t[0]
        b[:, :, 1] *= t[1]
        b[:, :, 2] *= t[2]
        b = b.astype(np.uint8)
        if a.shape[0] <= 16 and a.shape[1] >= 32:
            b = np.tile(b, (2, 3, 1))
        elif a.shape == (32, 32, 4) and t[0] < 0.4:
            b = np.tile(b, (2, 3, 1))
        tinted.append(b)
    mh = max(i.shape[0] for i in tinted)
    padded = []
    for b in tinted:
        p = np.zeros((mh, max(b.shape[1], 1), 4), dtype=np.uint8)
        p[:b.shape[0], :b.shape[1]] = b
        padded.append(p)
    W = sum(p.shape[1] + 4 for p in padded)
    canvas = Image.new('RGBA', (W, mh), (40, 36, 58, 255))
    x = 0
    for p in padded:
        canvas.alpha_composite(Image.fromarray(p, 'RGBA'), (x, 0))
        x += p.shape[1] + 4
    canvas.resize((W * 4, mh * 4), Image.NEAREST).save('prev_statics.png')
