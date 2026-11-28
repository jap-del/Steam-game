"""Render the game's music into Assets/Resources/Audio as OGG files (needs numpy and ffmpeg with libvorbis).

    python3 Tools/MusicGen/build.py            # everything
    python3 Tools/MusicGen/build.py Boss Hub   # only some tracks
    python3 Tools/MusicGen/build.py --preview DIR   # also write MP3 copies into DIR
"""
import hashlib
import os
import subprocess
import sys
import tempfile
import time
import uuid

import numpy as np

import songs
import synth

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets'))
OUT = os.path.join(ASSETS, 'Resources', 'Audio')

TRACKS = {
    'Music_Hub': songs.hub,
    'Music_Citadel': songs.citadel,
    'Music_Sanctum': songs.sanctum,
    'Music_Clocktower': songs.clocktower,
    'Music_Boss': lambda: songs.boss(False),
    'Music_BossRage': lambda: songs.boss(True),
    'Sting_BossIntro': songs.boss_intro,
}

AUDIO_META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 1
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 0
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


def guid_for(path):
    meta = path + '.meta'
    if os.path.exists(meta):
        for line in open(meta):
            if line.startswith('guid:'):
                return line.split()[1]
    return uuid.uuid4().hex


def encode(wav, dst, codec_args):
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', wav] + codec_args + [dst], check=True)


def main(argv):
    preview = None
    if '--preview' in argv:
        i = argv.index('--preview')
        preview = argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
        os.makedirs(preview, exist_ok=True)
    names = argv or list(TRACKS)
    os.makedirs(OUT, exist_ok=True)
    if not os.path.exists(OUT + '.meta'):
        with open(OUT + '.meta', 'w') as f:
            f.write(FOLDER_META.format(guid=uuid.uuid4().hex))

    lengths = {}
    for name in names:
        started = time.time()
        # Each track gets its own fixed random seed, so rebuilding one track gives the same sound.
        synth.RNG = np.random.default_rng(int(hashlib.md5(name.encode()).hexdigest()[:8], 16))
        x = TRACKS[name]()
        lengths[name] = x.shape[1]
        peak = np.abs(x).max()
        rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)
        with tempfile.TemporaryDirectory() as tmp:
            wav = os.path.join(tmp, name + '.wav')
            synth.write_wav(wav, x)
            dst = os.path.join(OUT, name + '.ogg')
            encode(wav, dst, ['-c:a', 'libvorbis', '-q:a', '6'])
            if preview:
                encode(wav, os.path.join(preview, name + '.mp3'), ['-c:a', 'libmp3lame', '-b:a', '192k'])
        guid = guid_for(dst)
        with open(dst + '.meta', 'w') as f:
            f.write(AUDIO_META.format(guid=guid))
        print('%-18s %6.2fs  peak %.2f  rms %5.1f dB  %5.1f KB  (%.1fs)' % (
            name, x.shape[1] / synth.SR, peak, rms, os.path.getsize(dst) / 1024, time.time() - started))
    if 'Music_Boss' in lengths and 'Music_BossRage' in lengths:
        assert lengths['Music_Boss'] == lengths['Music_BossRage'], 'boss layers must be the same length'


if __name__ == '__main__':
    main(sys.argv[1:])
