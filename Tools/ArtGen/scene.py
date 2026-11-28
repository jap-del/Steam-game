"""Usage: python3 scene.py [citadel|sanctum|clock] [mobs|Boss,Names,...] [out.png]

Compose an in-game-like scene from the exported sheets, using the catalog's slicing and pivots (mirrors the C# code)."""
import os, re, sys
import numpy as np
from PIL import Image
ASSETS = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets'))
ART = os.path.join(ASSETS, 'Resources', 'Art') + os.sep
cat = open(os.path.join(ASSETS, 'Scripts', 'Prototype', 'ArtCatalog.cs')).read()
INFO = {}
for m in re.finditer(r'new ArtSheetInfo\("(\w+)", (\d+), (\d+), (\d+), (\d+), (\d+), (\w+), ([\d.]+)f, ([\d.]+)f,\s*new\[\] \{(.*?)\}\);', cat, re.S):
    clips = {c[0]: (int(c[1]), int(c[2])) for c in re.findall(r'new ArtClip\("(\w+)", (\d+), (\d+)', m.group(10))}
    INFO[m.group(1)] = dict(w=int(m.group(2)), h=int(m.group(3)), pad=int(m.group(4)), cols=int(m.group(5)), count=int(m.group(6)), px=float(m.group(8)), py=float(m.group(9)), clips=clips)
TEX = {}
def frame(name, clip='idle', k=0, white=False):
    i = INFO[name]; t = TEX.setdefault(name, Image.open(ART + name + '.png').convert('RGBA'))
    s, c = i['clips'].get(clip, i['clips']['idle']); idx = s + min(k, c - 1) + (i['count'] if white else 0)
    col, row = idx % i['cols'], idx // i['cols']
    x = col * (i['w'] + 2 * i['pad']) + i['pad']; y = row * (i['h'] + 2 * i['pad']) + i['pad']
    return t.crop((x, y, x + i['w'], y + i['h']))
PPU = 32
VW, VH = 740, 416
img = Image.new('RGBA', (VW, VH), (14, 14, 22, 255))
def tint(im, c):
    a = np.asarray(im).astype(float); a[..., 0] *= c[0]; a[..., 1] *= c[1]; a[..., 2] *= c[2]; a[..., 3] *= c[3] if len(c) > 3 else 1
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA')
def tiled(name, x0, y0, w, h, color, ppu=32):
    t = Image.open(ART + name + '.png').convert('RGBA')
    sc = ppu / 32.0
    if sc != 1: t = t.resize((int(t.width / sc), int(t.height / sc)), Image.NEAREST)
    t = tint(t, color)
    for yy in range(y0, y0 + h, t.height):
        for xx in range(x0, x0 + w, t.width):
            crop = t.crop((0, 0, min(t.width, x0 + w - xx), min(t.height, y0 + h - yy)))
            img.alpha_composite(crop, (xx, yy))
def put(name, wx, feet_y, clip='idle', k=0, flip=False, flash=None, mul=None):
    """wx = world px of the character centre line, feet_y = screen px of the ground."""
    f = frame(name, clip, k); i = INFO[name]
    if mul: f = tint(f, mul)
    if flash:
        wf = tint(frame(name, clip, k, True), flash)
        f = Image.alpha_composite(f, wf)
    px = i['px']
    if flip: f = f.transpose(Image.FLIP_LEFT_RIGHT); px = i['w'] - px
    img.alpha_composite(f, (int(round(wx - px)), int(round(feet_y - (i['h'] - i['py'])))))
def stretch(name, cx, cy, w, h, color):
    f = Image.open(ART + name + '.png').convert('RGBA').resize((int(w * PPU), int(h * PPU)), Image.NEAREST)
    f = tint(f, color); img.alpha_composite(f, (int(cx - f.width / 2), int(cy - f.height / 2)))
theme = sys.argv[1] if len(sys.argv) > 1 else 'citadel'
TH = {'citadel': dict(solid=(0.32, 0.34, 0.42), back=(0.13, 0.14, 0.2), cap=(0.36, 0.58, 0.55), far=(0.105, 0.115, 0.165)),
      'clock': dict(solid=(0.46, 0.38, 0.3), back=(0.2, 0.15, 0.12), cap=(0.8, 0.55, 0.28), far=(0.12, 0.095, 0.085)),
      'sanctum': dict(solid=(0.55, 0.52, 0.6), back=(0.24, 0.22, 0.3), cap=(0.85, 0.75, 0.45), far=(0.16, 0.15, 0.21))}[theme]
G = 330
tiled('Stone', 0, 0, VW, VH, TH['far'], ppu=16)
for x, hgt in ((60, 200), (300, 260), (560, 150)):
    tiled('Stone', x, G - hgt, 48, hgt, TH['back'])
tiled('Stone', 0, G, VW, VH - G, TH['solid'])
tiled('Cap', 0, G, VW, 8, TH['cap'])
tiled('Stone', 420, 230, 160, 32, TH['solid']); tiled('Cap', 420, 230, 160, 8, TH['cap'])
tiled('Glass', 200, 250, 96, 16, (0.7, 0.92, 1, 0.6)); tiled('Cap', 200, 250, 96, 4, (0.85, 1, 1, 0.9))
tiled('Spikes', 620, G - 16, 96, 16, (0.72, 0.74, 0.8))
stretch('Door', 40, G - 1.3 * PPU, 1.6, 2.6, (0.3, 0.9, 0.5))
scene = sys.argv[2] if len(sys.argv) > 2 else 'mobs'
if scene == 'mobs':
    put('PlayerShadow', 130, G, 'run', 2, mul=(1, 1, 1, 0.75))
    put('Player', 175, G, 'attack', 0)
    stretch('Slash', 175 + 32, G - 0.7 * PPU, 1.7, 1.3, (1, 1, 1, 0.7))
    put('Brute', 250, G, 'windup', 1, flip=True, flash=(1, 0.95, 0.3, 0.75))
    put('Shield', 320, G, 'walk', 1, flip=True)
    put('Shooter', 500, 230, 'idle', 0, flip=True)
    put('Bomber', 380, G, 'recover', 0, flip=True, mul=(0.55, 0.55, 0.55, 1))
    put('Flyer', 260, 170, 'idle', 1, flip=True)
    put('Dummy', 690, G - 16, 'idle', 0, flip=True)
    stretch('Orb', 450, 210, 0.35, 0.35, (1, 0.8, 0.2)); stretch('Bomb', 410, 300, 0.5, 0.5, (0.5, 0.5, 0.56))
    stretch('Potion', 560, 214, 1, 1, (0.9, 0.35, 0.4)); stretch('Crystal', 520, 214, 1, 1, (0.5, 0.75, 1))
else:
    put('Player', 150, G, 'run', 3)
    put('PlayerShadow', 100, G, 'idle', 0, mul=(1, 1, 1, 0.9), flash=(0.75, 0.55, 1, 0.3))
    bosses = scene.split(',')
    xs = np.linspace(300, 680, len(bosses))
    for b, x in zip(bosses, xs):
        put(b, x, G, 'idle', 0, flip=True)
out = sys.argv[3] if len(sys.argv) > 3 else 'scene.png'
img.resize((VW * 2, VH * 2), Image.NEAREST).save(out)
print('ok', out)
