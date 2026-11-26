"""Small procedural pixel-art toolkit: shaded primitives, AO lines, outlines, sheets."""
import math
import numpy as np
from PIL import Image

LIGHT = np.array([-0.5, -0.62, 0.6])
LIGHT = LIGHT / np.linalg.norm(LIGHT)

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.47


def hexc(h, a=255):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


RAMPS = {}
EMISSIVE = set()


def ramp(name, colors, emissive=False):
    RAMPS[name] = [hexc(c) for c in colors]
    if emissive:
        EMISSIVE.add(name)


def lerp_hex(a, b, t):
    ca, cb = hexc(a), hexc(b)
    return '#%02x%02x%02x' % tuple(int(round(ca[i] + (cb[i] - ca[i]) * t)) for i in range(3))


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.ramp = np.full((h, w), '', dtype=object)
        self.shade = np.zeros((h, w))
        self.z = np.full((h, w), -1e9)
        self.grp = np.full((h, w), -1, dtype=int)
        self.dith = np.zeros((h, w), dtype=bool)
        self.decal = {}
        self.top = {}
        self.no_ao = np.zeros((h, w), dtype=bool)
        yy, xx = np.mgrid[0:h, 0:w]
        self.xx = xx + 0.5
        self.yy = yy + 0.5

    # ---- core writer ----
    def _put(self, mask, shade, rname, z, grp, dither, ao=True):
        if rname not in RAMPS:
            raise KeyError(rname)
        m = mask & (z >= self.z)
        self.ramp[m] = rname
        self.shade[m] = shade[m] if isinstance(shade, np.ndarray) else shade
        self.z[m] = z
        self.grp[m] = grp
        self.dith[m] = dither
        self.no_ao[m] = not ao

    # ---- primitives ----
    def ellipse(self, cx, cy, rx, ry, rname, z=0, grp=0, bias=0.0, dither=False, rot=0.0, flat=0.0, ao=True):
        dx = self.xx - cx
        dy = self.yy - cy
        if rot:
            c, s = math.cos(rot), math.sin(rot)
            dx, dy = dx * c + dy * s, -dx * s + dy * c
        nx = dx / max(rx, 0.01)
        ny = dy / max(ry, 0.01)
        d2 = nx * nx + ny * ny
        mask = d2 <= 1.0
        nz = np.sqrt(np.clip(1 - d2, 0, 1))
        if rot:
            c, s = math.cos(rot), math.sin(rot)
            nx, ny = nx * c - ny * s, nx * s + ny * c
        sh = (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) * (1 - flat) + bias
        self._put(mask, sh, rname, z, grp, dither, ao)

    def capsule(self, x0, y0, x1, y1, r0, r1, rname, z=0, grp=0, bias=0.0, dither=False, ao=True):
        vx, vy = x1 - x0, y1 - y0
        L2 = vx * vx + vy * vy
        px = self.xx - x0
        py = self.yy - y0
        t = np.clip((px * vx + py * vy) / L2, 0, 1) if L2 > 0 else np.zeros_like(px)
        cx = x0 + vx * t
        cy = y0 + vy * t
        r = r0 + (r1 - r0) * t
        ox = self.xx - cx
        oy = self.yy - cy
        d = np.sqrt(ox * ox + oy * oy)
        mask = d <= r
        rr = np.maximum(r, 0.01)
        nx = ox / rr
        ny = oy / rr
        nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
        sh = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2] + bias
        self._put(mask, sh, rname, z, grp, dither, ao)

    def poly(self, pts, rname, z=0, grp=0, bias=0.0, bevel=1, flat=0.15, dither=False, edge=0.38, ao=True, vgrad=0.0, hgrad=0.0):
        mask = poly_mask(pts, self.w, self.h)
        sh = np.full((self.h, self.w), flat + bias)
        if (vgrad or hgrad) and mask.any():
            ys, xs = np.nonzero(mask)
            y0, y1 = ys.min(), ys.max() + 1
            x0, x1 = xs.min(), xs.max() + 1
            if vgrad:
                sh = sh + vgrad * ((self.yy - y0) / max(1, y1 - y0) - 0.5)
            if hgrad:
                sh = sh + hgrad * ((self.xx - x0) / max(1, x1 - x0) - 0.5)
        if bevel > 0:
            def sh_(m, dy, dx):
                out = np.zeros_like(m)
                ys = slice(max(dy, 0), self.h + min(dy, 0))
                yd = slice(max(-dy, 0), self.h + min(-dy, 0))
                xs = slice(max(dx, 0), self.w + min(dx, 0))
                xd = slice(max(-dx, 0), self.w + min(-dx, 0))
                out[yd, xd] = m[ys, xs]
                return out
            b = bevel
            near_top = mask & ~sh_(mask, -b, 0)
            near_left = mask & ~sh_(mask, 0, -b)
            near_bot = mask & ~sh_(mask, b, 0)
            near_right = mask & ~sh_(mask, 0, b)
            sh = sh + edge * (near_top | near_left) - edge * (near_bot | near_right)
        self._put(mask, sh, rname, z, grp, dither, ao)

    def ribbon(self, pts, widths, rname, z=0, grp=0, fold=0.22, bias=0.0, ao=True):
        """Flat cloth strip along pts; alternate segments are lighter/darker like folds."""
        for k in range(len(pts) - 1):
            (x0, y0), (x1, y1) = pts[k], pts[k + 1]
            dx, dy = x1 - x0, y1 - y0
            L = math.hypot(dx, dy) or 1.0
            nx, ny = -dy / L, dx / L
            w0, w1 = widths[k] * 0.5, widths[k + 1] * 0.5
            quad = [(x0 + nx * w0, y0 + ny * w0), (x1 + nx * w1, y1 + ny * w1), (x1 - nx * w1, y1 - ny * w1), (x0 - nx * w0, y0 - ny * w0)]
            b = bias + (fold if k % 2 == 0 else -fold) - 0.05 * k
            self.poly(quad, rname, z=z + k * 0.001, grp=grp, bias=b, bevel=1, flat=0.1, edge=0.3, ao=ao)

    def rect(self, x0, y0, x1, y1, rname, **kw):
        self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], rname, **kw)

    def erase_ellipse(self, cx, cy, rx, ry, zmax=1e9):
        dx = (self.xx - cx) / max(rx, 0.01)
        dy = (self.yy - cy) / max(ry, 0.01)
        m = (dx * dx + dy * dy <= 1.0) & (self.z <= zmax)
        self.ramp[m] = ''
        self.z[m] = -1e9
        self.grp[m] = -1

    def mask_shape(self, mask, shade, rname, z=0, grp=0, dither=False, ao=True):
        self._put(mask, shade, rname, z, grp, dither, ao)

    def px(self, x, y, color, top=False):
        x, y = int(math.floor(x)), int(math.floor(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            (self.top if top else self.decal)[(x, y)] = color if isinstance(color, tuple) else hexc(color)

    def line(self, x0, y0, x1, y1, color, top=False):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for i in range(n + 1):
            t = i / max(n, 1)
            self.px(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, color, top)

    # ---- render ----
    def render(self, outline='#120a1a', ao_strength=2, outline_mode='ramp'):
        h, w = self.h, self.w
        img = np.zeros((h, w, 4), dtype=np.uint8)
        filled = self.ramp != ''
        idx = np.zeros((h, w), dtype=int)
        bayer = np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w]
        for rname in set(self.ramp[filled].tolist()):
            m = self.ramp == rname
            cols = RAMPS[rname]
            n = len(cols)
            s = self.shade.copy()
            v = (s * 0.5 + 0.5)
            v = np.clip(v, 0, 1)
            if rname in EMISSIVE:
                v = np.clip(s, 0, 1)
            f = v * (n - 1) + 0.0
            f = f + np.where(self.dith, bayer * 0.9, 0)
            idx[m] = np.clip(np.round(f[m]), 0, n - 1).astype(int)
        # ambient occlusion where a nearer part overlaps
        if ao_strength:
            zz = self.z
            occl = np.zeros((h, w), dtype=bool)
            for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                nz = np.full((h, w), -1e9)
                ng = np.full((h, w), -1)
                ys = slice(max(dy, 0), h + min(dy, 0))
                yd = slice(max(-dy, 0), h + min(-dy, 0))
                xs = slice(max(dx, 0), w + min(dx, 0))
                xd = slice(max(-dx, 0), w + min(-dx, 0))
                nz[yd, xd] = zz[ys, xs]
                ng[yd, xd] = self.grp[ys, xs]
                occl |= filled & (nz > zz + 0.5) & (ng != self.grp)
            occl &= ~self.no_ao
            for rname in set(self.ramp[occl].tolist()):
                if rname in EMISSIVE:
                    continue
                m = occl & (self.ramp == rname)
                idx[m] = np.maximum(idx[m] - ao_strength, 0)
        for rname in set(self.ramp[filled].tolist()):
            m = self.ramp == rname
            cols = np.array(RAMPS[rname], dtype=np.uint8)
            img[m] = cols[idx[m]]
        for (x, y), c in self.decal.items():
            img[y, x] = c
        # outline
        alpha = img[:, :, 3] > 0
        if outline:
            oc = hexc(outline)
            out = np.zeros((h, w), dtype=bool)
            src_r = np.full((h, w), '', dtype=object)
            for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                ys = slice(max(dy, 0), h + min(dy, 0))
                yd = slice(max(-dy, 0), h + min(-dy, 0))
                xs = slice(max(dx, 0), w + min(dx, 0))
                xd = slice(max(-dx, 0), w + min(-dx, 0))
                shifted = np.zeros((h, w), dtype=bool)
                shifted[yd, xd] = alpha[ys, xs]
                rs = np.full((h, w), '', dtype=object)
                rs[yd, xd] = self.ramp[ys, xs]
                newm = shifted & ~alpha & (src_r == '')
                src_r[newm] = rs[newm]
                out |= shifted & ~alpha
            if outline_mode == 'ramp':
                for y, x in zip(*np.nonzero(out)):
                    r = src_r[y, x]
                    if r and r in RAMPS and r not in EMISSIVE:
                        c0 = RAMPS[r][0]
                        img[y, x] = (int(c0[0] * 0.45 + oc[0] * 0.55), int(c0[1] * 0.45 + oc[1] * 0.55), int(c0[2] * 0.45 + oc[2] * 0.55), 255)
                    elif r in EMISSIVE:
                        c0 = RAMPS[r][0]
                        img[y, x] = (int(c0[0] * 0.6), int(c0[1] * 0.6), int(c0[2] * 0.6), 255)
                    else:
                        img[y, x] = oc
            else:
                img[out] = oc
        for (x, y), c in self.top.items():
            img[y, x] = c
        return img


def poly_mask(pts, w, h):
    from PIL import ImageDraw
    im = Image.new('L', (w, h), 0)
    ImageDraw.Draw(im).polygon([(float(x), float(y)) for x, y in pts], fill=1)
    return np.array(im, dtype=bool)


def silhouette(img):
    out = np.zeros_like(img)
    a = img[:, :, 3] > 0
    out[a] = (255, 255, 255, 255)
    return out


def recolor(img, mapping):
    """Swap exact colours: mapping {(r,g,b): (r,g,b)}."""
    out = img.copy()
    rgb = out[:, :, :3]
    for src, dst in mapping.items():
        m = np.all(rgb == np.array(src[:3], dtype=np.uint8), axis=2) & (out[:, :, 3] > 0)
        out[m, :3] = dst[:3]
    return out


def to_image(arr):
    return Image.fromarray(arr, 'RGBA')


def rot(x, y, ang, ox=0.0, oy=0.0):
    c, s = math.cos(ang), math.sin(ang)
    x, y = x - ox, y - oy
    return ox + x * c - y * s, oy + x * s + y * c


def limb(x, y, length, ang):
    """End point of a limb starting at (x,y) pointing down at angle ang (0 = straight down, + = forward/right)."""
    return x + math.sin(ang) * length, y + math.cos(ang) * length


def preview(frames, scale=4, bg=(40, 36, 58), cols=None, path='preview.png', label_rows=None):
    n = len(frames)
    cols = cols or n
    rows = (n + cols - 1) // cols
    fh, fw = frames[0].shape[:2]
    pad = 4
    W = cols * (fw * scale + pad) + pad
    H = rows * (fh * scale + pad) + pad
    im = Image.new('RGBA', (W, H), bg + (255,))
    for i, f in enumerate(frames):
        r, c = divmod(i, cols)
        fi = to_image(f).resize((fw * scale, fh * scale), Image.NEAREST)
        im.alpha_composite(fi, (pad + c * (fw * scale + pad), pad + r * (fh * scale + pad)))
    im.save(path)
    return path
