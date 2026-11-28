"""Export every sprite sheet into the Unity project and write ArtCatalog.cs.

Sheets go to Assets/Resources/Art/<Name>.png. A character sheet holds its colour frames first and then
the same frames as white silhouettes (used for the hit / warning flash overlay).
"""
import hashlib
import os
import uuid
import numpy as np
from PIL import Image

import player
import enemies
import bosses
import statics
from px import recolor

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets'))
OUT = os.path.join(ROOT, 'Resources', 'Art')
CATALOG = os.path.join(ROOT, 'Scripts', 'Prototype', 'ArtCatalog.cs')
PPU = 32
MAX_W = 2048

# clip name -> (fps, loop)
CHAR_CLIPS = {
    'idle': (6, True), 'run': (12, True), 'walk': (8, True), 'jump': (8, False), 'fall': (8, True),
    'dash': (16, True), 'attack': (14, False), 'hurt': (8, False), 'windup': (6, False),
    'recover': (6, False), 'down': (6, False),
}
PLAYER_ORDER = ['idle', 'run', 'jump', 'fall', 'dash', 'attack', 'hurt']
ENEMY_ORDER = ['idle', 'walk', 'windup', 'attack', 'recover', 'down']


def guid_for(path):
    """Keep the guid stable between runs so Unity does not see a new asset each time."""
    meta = path + '.meta'
    if os.path.exists(meta):
        for line in open(meta):
            if line.startswith('guid:'):
                return line.split()[1]
    return uuid.uuid4().hex


TEXTURE_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 32
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationMethod: 0
  spriteTessellationDetail: -1
  spriteGeometrySubdivision: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SCRIPT_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(path, template, **kw):
    guid = guid_for(path)
    kw.setdefault('sprite_id', hashlib.md5(guid.encode()).hexdigest())
    with open(path + '.meta', 'w') as f:
        f.write(template.format(guid=guid, **kw))


def pack(frames, pad):
    """Lay frames out in rows. Returns (image, columns)."""
    h, w = frames[0].shape[:2]
    cw, ch = w + pad * 2, h + pad * 2
    cols = max(1, min(len(frames), MAX_W // cw))
    rows = (len(frames) + cols - 1) // cols
    sheet = np.zeros((rows * ch, cols * cw, 4), dtype=np.uint8)
    for i, fr in enumerate(frames):
        assert fr.shape[:2] == (h, w), (fr.shape, (h, w))
        c, r = i % cols, i // cols
        sheet[r * ch + pad:r * ch + pad + h, c * cw + pad:c * cw + pad + w] = fr
    return sheet, cols


def white(fr):
    s = np.zeros_like(fr)
    on = fr[..., 3] > 0
    s[on] = (255, 255, 255, 255)
    return s


def feet_pivot(frames, cx):
    """Pivot x at the character's centre line, y at the bottom edge of the lowest idle pixel."""
    a = frames[0][..., 3] > 0
    ys = np.where(a.any(axis=1))[0]
    bottom = ys.max() + 1          # top-down row index of the bottom edge
    h = frames[0].shape[0]
    return cx + 0.5, h - bottom


def gamma_to_mean(img, target):
    """Brighten a grayscale tile so the theme colours keep the brightness they were picked for."""
    a = img[..., 3] > 0
    rgb = img[..., :3].astype(float) / 255.0
    lo, hi = 0.1, 1.0
    for _ in range(40):
        g = (lo + hi) / 2
        m = (rgb[a] ** g).mean() * 255
        if m < target:
            hi = g
        else:
            lo = g
    out = img.copy()
    out[..., :3] = np.clip((rgb ** g) * 255 + 0.5, 0, 255).astype(np.uint8)
    out[~a] = 0
    return out


entries = []


def export_character(name, clips, order, pivot, extra=None):
    frames, clip_rows = [], []
    for k in order:
        if k not in clips:
            continue
        start = len(frames)
        frames += clips[k]
        fps, loop = CHAR_CLIPS[k]
        clip_rows.append((k, start, len(clips[k]), fps, loop))
    if extra:
        clip_rows = extra(clip_rows)
    n = len(frames)
    allf = frames + [white(f) for f in frames]
    sheet, cols = pack(allf, 1)
    path = os.path.join(OUT, name + '.png')
    Image.fromarray(sheet, 'RGBA').save(path, optimize=True)
    write_meta(path, TEXTURE_META)
    h, w = frames[0].shape[:2]
    entries.append(dict(name=name, w=w, h=h, pad=1, cols=cols, count=n, white=True,
                        px=pivot[0], py=pivot[1], clips=clip_rows))
    print('%-12s %4dx%-4d frames %2d sheet %dx%d' % (name, w, h, n, sheet.shape[1], sheet.shape[0]))
    return frames


def export_static(name, img, pivot=None):
    path = os.path.join(OUT, name + '.png')
    Image.fromarray(img, 'RGBA').save(path, optimize=True)
    write_meta(path, TEXTURE_META)
    h, w = img.shape[:2]
    px, py = pivot if pivot else (w / 2.0, h / 2.0)
    entries.append(dict(name=name, w=w, h=h, pad=0, cols=1, count=1, white=False, px=px, py=py,
                        clips=[('idle', 0, 1, 1, True)]))
    print('%-12s %4dx%-4d static' % (name, w, h))


def player_attack_clip(rows):
    # The slash lands the moment the key is pressed, so the swing starts on the second drawing.
    out = []
    for k, s, c, fps, loop in rows:
        if k == 'attack':
            out.append((k, s + 1, c - 1, fps, loop))
        else:
            out.append((k, s, c, fps, loop))
    return out


def fmt(v):
    s = ('%.2f' % v).rstrip('0').rstrip('.')
    return s + 'f'


def write_catalog():
    lines = []
    lines.append('namespace SteamGame.Prototype')
    lines.append('{')
    lines.append('    /// <summary>')
    lines.append('    /// Assets/Resources/Art 안 그림 판의 목록. 그림 생성 도구가 그림 파일과 함께 만든다.')
    lines.append('    /// 칸 크기, 발 기준점, 움직임 묶음(이름, 시작 칸, 칸 수, 1초에 넘기는 칸 수, 반복 여부)을 적는다.')
    lines.append('    /// </summary>')
    lines.append('    public static class ArtCatalog')
    lines.append('    {')
    lines.append('        public static ArtSheetInfo Find(string name)')
    lines.append('        {')
    lines.append('            switch (name)')
    lines.append('            {')
    for e in entries:
        clips = ', '.join('new ArtClip("%s", %d, %d, %s, %s)' % (k, s, c, fmt(fps), 'true' if loop else 'false')
                          for k, s, c, fps, loop in e['clips'])
        lines.append('                case "%s":' % e['name'])
        lines.append('                    return new ArtSheetInfo("%s", %d, %d, %d, %d, %d, %s, %s, %s,' % (
            e['name'], e['w'], e['h'], e['pad'], e['cols'], e['count'], 'true' if e['white'] else 'false',
            fmt(e['px']), fmt(e['py'])))
        lines.append('                        new[] { %s });' % clips)
    lines.append('                default:')
    lines.append('                    return null;')
    lines.append('            }')
    lines.append('        }')
    lines.append('    }')
    lines.append('}')
    with open(CATALOG, 'w') as f:
        f.write('\n'.join(lines) + '\n')
    if not os.path.exists(CATALOG + '.meta'):
        write_meta(CATALOG, SCRIPT_META)


def main():
    os.makedirs(OUT, exist_ok=True)
    folder = os.path.join(ROOT, 'Resources', 'Art')
    if not os.path.exists(folder + '.meta'):
        write_meta(folder, FOLDER_META)

    # Player and shadow clone share one layout so the clone can show the frame the player showed.
    pf = player.frames()
    piv = feet_pivot(pf['idle'], player.CX)
    export_character('Player', pf, PLAYER_ORDER, piv, player_attack_clip)
    sm = player.shadow_palette()
    sf = {k: [recolor(f, sm) for f in v] for k, v in pf.items()}
    export_character('PlayerShadow', sf, PLAYER_ORDER, piv, player_attack_clip)

    for name, (fn, (cx, gy)) in list(enemies.ALL.items()) + list(bosses.ALL.items()):
        fr = fn()
        if name == 'Flyer':
            # Flies: centre the drawing on the collider (0.6 tall) instead of standing on its feet.
            a = fr['idle'][0][..., 3] > 0
            ys = np.where(a.any(axis=1))[0]
            mid = (ys.min() + ys.max() + 1) / 2.0
            h = fr['idle'][0].shape[0]
            piv = (cx + 0.5, h - (mid + 0.3 * PPU))
        elif name == 'LightLord':
            # Hovers: keep the drawn gap under the robe.
            piv = (cx + 0.5, fr['idle'][0].shape[0] - (gy + 1))
        else:
            piv = feet_pivot(fr['idle'], cx)
        export_character(name, fr, ENEMY_ORDER, piv)

    targets = {'Stone': 200, 'Cap': 212, 'Glass': 190}
    for name, fn in statics.ALL.items():
        img = fn()
        if name in targets:
            img = gamma_to_mean(img, targets[name])
        export_static(name, img)

    write_catalog()


if __name__ == '__main__':
    main()
