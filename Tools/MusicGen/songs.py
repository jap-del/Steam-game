"""The game's music: three stage themes, the hub, the boss fight (normal and enraged) and the boss intro sting.

Each function returns a stereo float array. Loops are exactly a whole number of bars long and seamless.
"""
import numpy as np
import synth as S
from synth import Mix, mtof

# ------------------------------------------------------------------ shared pieces


def bars_to_sec(bpm, bars, beats=4):
    return 60.0 / bpm * beats * bars


def pad_chords(mix, chords, tonic, bpm, inst, low, high, gain, pan_spread=0.5, send=0.5, voices=3, bars_per=1, **kw):
    beat = 60.0 / bpm
    prev = None
    for i, (root, q) in enumerate(chords):
        notes = S.voice_lead(prev, S.chord_pcs(tonic + root, q), low, high)
        prev = notes
        dur = beat * 4 * bars_per
        for j, m in enumerate(notes):
            pan = (j / max(1, len(notes) - 1) - 0.5) * 2 * pan_spread
            y = S.ens(inst, mtof(m), dur * 0.98, voices=voices, **kw)
            mix.add(y, i * dur, pan, gain, send)


def melody_line(mix, notes, start_beat, bpm, inst, gain, pan=0.0, send=0.35, legato=0.92, delay=None, **kw):
    beat = 60.0 / bpm
    b = start_beat
    for m, length in notes:
        if m is not None:
            y = inst(mtof(m), length * beat * legato, **kw)
            if delay:
                mix.add_delay(y, b * beat, pan, gain, send, delay, 0.32, 3)
            else:
                mix.add(y, b * beat, pan, gain, send)
        b += length


def arpeggio(mix, chords, tonic, bpm, inst, gain, base, pattern, step, send=0.4, delay=None, pan_wave=0.4,
             skip_bars=(), note_len=None, **kw):
    """Chord tones spread over two octaves, played in a pattern (indices into the tone list)."""
    beat = 60.0 / bpm
    per_bar = int(round(4 / step))
    for bar, (root, q) in enumerate(chords):
        if bar in skip_bars:
            continue
        pcs = S.chord_pcs(tonic + root, q)
        tones = []
        for octv in range(3):
            for pc in sorted((p - base) % 12 for p in pcs):
                tones.append(base + pc + 12 * octv)
        for k in range(per_bar):
            idx = pattern[k % len(pattern)]
            if idx is None:
                continue
            m = tones[idx % len(tones)]
            at = (bar * 4 + k * step) * beat
            pan = pan_wave * np.sin(k * 0.9 + bar)
            y = inst(mtof(m), (note_len or step) * beat * 0.9, **kw)
            if delay:
                mix.add_delay(y, at, pan, gain, send, delay, 0.33, 3)
            else:
                mix.add(y, at, pan, gain, send)


def drums(mix, bpm, bars, pattern, kit, gain=1.0, start_bar=0, skip_bars=()):
    """pattern: dict name -> string of 16 steps per bar ('x' hit, 'o' accent, '.' rest)."""
    step = 60.0 / bpm / 4
    for bar in range(start_bar, bars):
        if bar in skip_bars:
            continue
        for name, steps in pattern.items():
            maker, g, pan, send = kit[name]
            for k, ch in enumerate(steps):
                if ch == '.':
                    continue
                acc = 1.0 if ch == 'o' else 0.62
                human = np.random.default_rng(bar * 64 + k).uniform(-0.004, 0.004)
                mix.add(maker(), (bar * 16 + k) * step + max(0.0, human), pan, g * acc * gain, send)


def bass_line(mix, chords, tonic, bpm, gain, octave_base, rhythm, inst=S.bass, send=0.1, fifths=True, **kw):
    """rhythm: list of (beat offset, length, use_fifth) per bar."""
    beat = 60.0 / bpm
    for bar, (root, q) in enumerate(chords):
        r = octave_base + (tonic + root - octave_base) % 12
        for off, length, fifth in rhythm:
            m = r + (7 if (fifth and fifths) else 0)
            mix.add(inst(mtof(m), length * beat * 0.9, **kw), (bar * 4 + off) * beat, 0.0, gain, send)


# ------------------------------------------------------------------ stage 1: stone citadel (D minor)

def citadel():
    bpm = 92.0
    tonic = 50  # D3
    A = [(0, 'm'), (8, 'M'), (10, 'M'), (7, 'm'), (0, 'm'), (8, 'M'), (5, 'm'), (7, 'M')]
    B = [(8, 'M'), (10, 'M'), (0, 'm'), (0, 'm'), (5, 'm'), (8, 'M'), (7, 'M'), (7, 'M')]
    chords = A + B
    bars = len(chords)
    mix = Mix(bars_to_sec(bpm, bars))
    pad_chords(mix, chords, tonic, bpm, S.pad, 53, 67, 0.32, bright=5.0)
    pad_chords(mix, chords, tonic, bpm, S.strings, 41, 50, 0.16, voices=2, pan_spread=0.0, send=0.3)
    bass_line(mix, chords, tonic, bpm, 0.5, 38, [(0, 1.5, False), (1.5, 0.5, False), (2, 1, True), (3, 1, False)])
    arpeggio(mix, chords, tonic, bpm, S.pluck, 0.24, 62, [0, 2, 3, 4, 3, 2, 1, 2], 0.5, delay=60 / bpm * 0.75,
             send=0.45)
    mel = [(77, 2), (74, 1), (77, 1), (79, 1.5), (77, 0.5), (76, 1), (72, 1), (74, 3), (69, 1), (74, 1), (76, 1),
           (77, 1), (81, 1), (82, 2), (81, 1), (79, 1), (77, 1.5), (79, 0.5), (77, 1), (74, 1), (76, 2), (73, 1),
           (76, 1), (69, 3), (None, 1)]
    melody_line(mix, mel, 32, bpm, S.flute, 0.34, pan=0.1, send=0.45, delay=60 / bpm * 0.75)
    melody_line(mix, [(m - 12 if m else None, l) for m, l in mel], 32, bpm, S.strings, 0.08, pan=-0.2, send=0.5)
    kit = {'k': (S.kick, 0.55, 0.0, 0.05), 'r': (S.rim, 0.22, 0.25, 0.25), 's': (S.shaker, 0.16, -0.3, 0.15)}
    pat = {'k': 'o.......x.....x.', 'r': '....x.......x...', 's': 'x.x.x.x.x.x.x.x.'}
    drums(mix, bpm, bars, pat, kit, start_bar=0, skip_bars=(0, 1))
    kit2 = {'k': (S.kick, 0.4, 0.0, 0.05)}
    drums(mix, bpm, 2, {'k': 'o.......x.......'}, kit2)
    return S.master(mix.render(reverb_time=2.0, reverb_tone=4500), rms_db=-17)


# ------------------------------------------------------------------ stage 2: sanctum of light (E minor, airy)

def sanctum():
    bpm = 80.0
    tonic = 52  # E3
    A = [(0, 'm'), (5, 'M'), (0, 'm'), (5, 'M'), (8, 'M'), (10, 'M'), (7, 'm'), (0, 'm')]
    B = [(8, 'M'), (10, 'M'), (0, 'm'), (0, 'm'), (5, 'm'), (8, 'M'), (10, 'M'), (7, 'M')]
    chords = A + B
    bars = len(chords)
    mix = Mix(bars_to_sec(bpm, bars), tail=6.0)
    pad_chords(mix, chords, tonic, bpm, S.choir, 55, 71, 0.3, voices=3, spread=10, send=0.7)
    pad_chords(mix, chords, tonic, bpm, S.pad, 43, 52, 0.18, voices=2, pan_spread=0.0, send=0.4, bright=3.0)
    arpeggio(mix, chords, tonic, bpm, S.bell, 0.2, 71, [0, 2, 1, 3, 2, 4, 3, 1], 0.5, delay=60 / bpm * 0.75,
             send=0.6, decay=0.8)
    bass_line(mix, chords, tonic, bpm, 0.32, 40, [(0, 3.5, False)], inst=S.sub, fifths=False, send=0.05)
    mel = [(79, 3), (76, 1), (78, 2), (81, 1), (78, 1), (76, 3), (71, 1), (76, 1), (78, 1), (79, 1), (83, 1),
           (84, 2), (83, 1), (81, 1), (79, 2), (76, 2), (78, 2), (81, 1), (74, 1), (75, 2), (78, 1), (71, 1)]
    melody_line(mix, mel, 32, bpm, S.flute, 0.3, pan=0.15, send=0.6, delay=60 / bpm * 0.75)
    melody_line(mix, [(m + 12 if m else None, l) for m, l in mel], 32, bpm, S.bell, 0.07, pan=-0.3, send=0.6)
    kit = {'k': (lambda: S.taiko(1.6, 0.6), 0.35, 0.0, 0.3), 's': (S.shaker, 0.1, 0.3, 0.3)}
    drums(mix, bpm, bars, {'k': 'o.........x.....', 's': '....x.......x...'}, kit, skip_bars=(0, 1, 2, 3))
    return S.master(mix.render(reverb_time=3.2, reverb_tone=6000), rms_db=-18)


# ------------------------------------------------------------------ stage 3: stopped clocktower (C minor, ticking)

def clocktower():
    bpm = 104.0
    tonic = 48  # C3
    A = [(0, 'm'), (8, 'M'), (5, 'm'), (7, 'M'), (0, 'm'), (8, 'M'), (10, 'M'), (7, 'M')]
    B = [(8, 'M'), (10, 'M'), (0, 'm'), (0, 'm'), (5, 'm'), (8, 'M'), (7, 'M'), (7, 'M')]
    chords = A + B
    bars = len(chords)
    mix = Mix(bars_to_sec(bpm, bars))
    pad_chords(mix, chords, tonic, bpm, S.strings, 51, 65, 0.22, voices=3, send=0.45, bright=7.0)
    arpeggio(mix, chords, tonic, bpm, S.harpsichord, 0.26, 60, [0, 1, 2, 1, 3, 2, 1, 2, 0, 1, 2, 1, 4, 3, 2, 1], 0.25,
             send=0.3)
    bass_line(mix, chords, tonic, bpm, 0.5, 36,
              [(0, 0.5, False), (1, 0.5, True), (2, 0.5, False), (3, 0.5, True)], bite=8.0)
    mel = [(75, .5), (72, .5), (68, .5), (72, .5), (75, 1), (80, 1), (77, .5), (74, .5), (70, .5), (74, .5), (77, 1),
           (82, 1), (79, 1.5), (75, .5), (72, 2), (72, .5), (74, .5), (75, .5), (77, .5), (79, 1), (84, 1), (80, 1),
           (79, .5), (77, .5), (75, 1), (72, 1), (75, 1), (77, .5), (75, .5), (72, 2), (71, .5), (74, .5), (77, .5),
           (74, .5), (71, 1), (67, 1), (67, 2), (None, 2)]
    melody_line(mix, mel, 32, bpm, S.bell, 0.3, pan=0.2, send=0.4, legato=1.0, decay=1.6)
    melody_line(mix, [(m - 12 if m else None, l) for m, l in mel], 32, bpm, S.brass, 0.1, pan=-0.15, send=0.35,
                legato=0.7, power=0.35)
    kit = {'t': (lambda: S.tick(1.0, True), 0.5, 0.45, 0.1), 'c': (lambda: S.tick(1.0, False), 0.55, -0.45, 0.1),
           'k': (S.kick, 0.5, 0.0, 0.05), 'r': (S.rim, 0.2, 0.2, 0.2)}
    pat = {'t': 'x...x...x...x...', 'c': '..x...x...x...x.', 'k': 'o.....x.o.......', 'r': '....x.......x..x'}
    drums(mix, bpm, bars, pat, kit, skip_bars=())
    return S.master(mix.render(reverb_time=1.6, reverb_tone=5000), rms_db=-17)


# ------------------------------------------------------------------ hub: resting place (A minor, calm, no drums)

def hub():
    bpm = 70.0
    tonic = 57  # A3
    A = [(0, 'm'), (8, 'M'), (3, 'M'), (10, 'M'), (0, 'm'), (8, 'M'), (5, 'm'), (7, 'M')]
    chords = A + A
    bars = len(chords)
    mix = Mix(bars_to_sec(bpm, bars), tail=6.0)
    pad_chords(mix, chords, tonic, bpm, S.pad, 52, 67, 0.3, bright=3.5, send=0.6, a=1.2, r=2.0)
    arpeggio(mix, chords, tonic, bpm, S.pluck, 0.22, 57, [0, 2, 4, 2, 3, 2, 4, 5], 0.5, send=0.55,
             delay=60 / bpm * 0.75, ring=2.2, tone=1.6)
    bass_line(mix, chords, tonic, bpm, 0.3, 33, [(0, 3.8, False)], inst=S.sub, fifths=False, send=0.05)
    mel = [(76, 2), (72, 1), (69, 1), (77, 3), (76, 1), (79, 2), (76, 1), (72, 1), (74, 3), (71, 1), (72, 1), (76, 1),
           (81, 2), (81, 1), (79, 1), (77, 2), (77, 2), (76, 1), (74, 1), (76, 2), (68, 1), (71, 1)]
    melody_line(mix, mel, 32, bpm, S.bell, 0.26, pan=0.15, send=0.6, legato=1.0, decay=0.7)
    melody_line(mix, mel, 32, bpm, S.flute, 0.12, pan=-0.1, send=0.6)
    return S.master(mix.render(reverb_time=3.0, reverb_tone=4500), rms_db=-19)


# ------------------------------------------------------------------ boss fight (D minor, 132 bpm)

BOSS_BPM = 132.0
BOSS_TONIC = 38  # D2
BOSS_A = [(0, 'm'), (0, 'm'), (8, 'M'), (10, 'M'), (0, 'm'), (0, 'm'), (5, 'm'), (7, 'M')]
BOSS_B = [(8, 'M'), (10, 'M'), (0, 'm'), (8, 'M'), (5, 'm'), (1, 'M'), (7, 'M'), (7, 'M')]


def boss(rage=False):
    bpm, tonic = BOSS_BPM, BOSS_TONIC
    chords = BOSS_A + BOSS_B
    bars = len(chords)
    beat = 60.0 / bpm
    mix = Mix(bars_to_sec(bpm, bars), tail=5.0)

    # Low string ostinato: root eighths with an octave kick on the off-beats.
    for bar, (root, q) in enumerate(chords):
        r = 38 + (tonic + root - 38) % 12
        pattern = [0, 0, 12, 0, 0, 12, 0, 12] if not rage else [0, 12, 0, 12, 0, 12, 7, 12]
        for k, off in enumerate(pattern):
            y = S.ens(S.spiccato, mtof(r + off), beat * 0.5, voices=2, spread=6)
            mix.add(y, (bar * 4 + k * 0.5) * beat, -0.15 if k % 2 else 0.15, 0.42, 0.25)
        if rage:
            for k in range(8):
                y = S.spiccato(mtof(r + 24 + (7 if k % 4 == 3 else 0)), beat * 0.5)
                mix.add(y, (bar * 4 + k * 0.5 + 0.25) * beat, 0.4, 0.14, 0.3)

    # Choir: sustained chords, two bars each in the A part, one bar in the B part.
    prev = None
    for bar, (root, q) in enumerate(chords):
        notes = S.voice_lead(prev, S.chord_pcs(tonic + root, q), 57 if not rage else 62, 72 if not rage else 77)
        prev = notes
        for j, m in enumerate(notes):
            y = S.ens(S.choir, mtof(m), beat * 4 * 0.98, voices=3, spread=12, vowel=S.VOWEL_A if bar < 8 else S.VOWEL_O)
            mix.add(y, bar * 4 * beat, (j - 1) * 0.45, 0.2 if bar < 8 else 0.26, 0.6)

    # Brass stabs: on every bar in the B part, every other bar in the A part (every bar when enraged).
    prev = None
    for bar, (root, q) in enumerate(chords):
        if bar < 8 and bar % 2 == 1 and not rage:
            continue
        notes = S.voice_lead(prev, S.chord_pcs(tonic + root, q), 50, 65)
        prev = notes
        for j, m in enumerate(notes):
            y = S.ens(S.brass, mtof(m), beat * (1.5 if bar >= 8 else 1.0), voices=2, spread=6, power=1.0)
            mix.add(y, bar * 4 * beat, (j - 1) * 0.35, 0.3, 0.35)
            if bar >= 8:
                y2 = S.ens(S.brass, mtof(m), beat * 0.9, voices=2, spread=6, power=0.9)
                mix.add(y2, (bar * 4 + 2.5) * beat, (j - 1) * 0.35, 0.22, 0.35)
        mix.add(S.ens(S.brass, mtof(36 + (tonic + root - 36) % 12), beat * 2, voices=2, power=0.8), bar * 4 * beat, 0,
                0.25, 0.25)

    # High string tremolo for tension in the B part.
    for bar, (root, q) in enumerate(chords):
        if bar < 8:
            continue
        pcs = S.chord_pcs(tonic + root, q)
        for j, pc in enumerate(pcs[:2]):
            m = 74 + (pc - 74) % 12
            mix.add(S.ens(S.tremolo_strings, mtof(m), beat * 4, voices=2, spread=8), bar * 4 * beat, (j - 0.5) * 0.8,
                    0.12, 0.5)

    if rage:
        # Heroic brass line over the B part.
        line = [(74, 1.5), (72, .5), (74, 1), (77, 1), (76, 1.5), (74, .5), (72, 1), (67, 1), (69, 1.5), (74, .5),
                (77, 1), (81, 1), (82, 3), (81, .5), (79, .5), (79, 1.5), (77, .5), (74, 1), (70, 1), (75, 2), (79, 1),
                (82, 1), (81, 1.5), (79, .5), (76, 1), (73, 1), (69, 2), (73, 1), (76, 1)]
        melody_line(mix, [(m - 12, l) for m, l in line], 32, bpm, lambda f, d, **k: S.ens(S.brass, f, d, voices=3,
                    spread=7, power=1.2), 0.3, pan=0.0, send=0.35, legato=0.95)
        melody_line(mix, line, 32, bpm, lambda f, d, **k: S.ens(S.strings, f, d, voices=3, spread=10), 0.14, pan=0.2,
                    send=0.4)

    # Percussion: taiko ensemble, snare on the back beat in the B part, cymbal swells into each phrase.
    kit = {'T': (lambda: S.taiko(0.8, 1.0), 0.75, 0.0, 0.3), 't': (lambda: S.taiko(1.25, 1.0), 0.45, 0.3, 0.3),
           'u': (lambda: S.taiko(1.6, 1.0), 0.35, -0.3, 0.3), 's': (lambda: S.snare(1.0), 0.32, 0.1, 0.25),
           'h': (lambda: S.hat(1.0), 0.18, -0.4, 0.1)}
    pat_a = {'T': 'o.....x...o.....', 't': '....x.......x.x.', 'u': '..x.......x...xx'}
    pat_b = {'T': 'o.....x...o.....', 't': '....x.x.....x.x.', 'u': '..x...x...x.xxxx', 's': '....o.......o...',
             'h': 'x.x.x.x.x.x.x.x.'}
    if rage:
        pat_a = {'T': 'o.....x...o..x..', 't': '..x.x.x.x...x.x.', 'u': 'x.x.xxx...x.xxxx', 's': '....o.......o...',
                 'h': 'xxxxxxxxxxxxxxxx'}
        pat_b = {'T': 'o..x..x...o..x..', 't': '..x.x.x.x.x.x.x.', 'u': 'xxx.xxx.xxx.xxxx', 's': '....o..x....o.xx',
                 'h': 'xxxxxxxxxxxxxxxx'}
    drums(mix, bpm, 8, pat_a, kit)
    drums(mix, bpm, 16, pat_b, kit, start_bar=8)
    for bar in (0, 4, 8, 12):
        mix.add(S.crash(1.0), bar * 4 * beat, 0.3, 0.5, 0.4)
        mix.add(S.timpani(mtof(38), 1.0), bar * 4 * beat, -0.2, 0.6, 0.3)
    for bar in (3, 7, 11, 15):
        sw = S.reverse_swell(beat * 2)
        mix.add(sw, (bar * 4 + 2) * beat, 0.0, 0.5, 0.3)
    # Sub pulse under everything.
    for bar, (root, q) in enumerate(chords):
        r = 26 + (tonic + root - 26) % 12
        mix.add(S.sub(mtof(r), beat * 4 * 0.95), bar * 4 * beat, 0, 0.35, 0.0)

    return S.master(mix.render(reverb_time=2.4, reverb_tone=5000), rms_db=-15.5 if not rage else -15.0)


# ------------------------------------------------------------------ boss intro sting (3.8 s, not looped)

INTRO_IMPACT = 1.6
INTRO_LENGTH = 3.8


def boss_intro():
    mix = Mix(INTRO_LENGTH, tail=2.5)
    beat = 60.0 / BOSS_BPM
    # Door shut and rising rumble.
    mix.add(S.boom(2.0, 1.0), 0.0, 0.0, 0.5, 0.3)
    r = S.rumble(INTRO_IMPACT + 0.1, 1.0)
    r *= np.linspace(0.25, 1.0, len(r)) ** 1.5
    mix.add(r, 0.0, 0.0, 0.25, 0.2)
    # Heartbeats getting faster.
    for t in (0.15, 0.7, 1.15, 1.45):
        mix.add(S.heartbeat(1.0), t, 0.0, 0.8, 0.15)
    # Rising dissonant strings and a reversed cymbal into the hit.
    for m, p in ((62, -0.4), (63, 0.4), (69, 0.0)):
        y = S.ens(S.tremolo_strings, mtof(m), INTRO_IMPACT - 0.35, voices=2, spread=10, rate=16)
        y *= np.linspace(0.0, 1.0, len(y)) ** 2
        mix.add(y, 0.35, p, 0.2, 0.5)
    mix.add(S.reverse_swell(1.2, 1.2), INTRO_IMPACT - 1.2, 0.0, 0.7, 0.4)
    # The hit: taiko ensemble, timpani, crash, low brass and choir on D minor, sub drop.
    hit = INTRO_IMPACT
    for p, pan, g in ((0.7, 0.0, 1.0), (0.9, -0.4, 0.8), (1.1, 0.4, 0.8), (1.5, 0.0, 0.5)):
        mix.add(S.taiko(p, 1.0), hit, pan, g, 0.35)
    mix.add(S.timpani(mtof(38), 1.3), hit, -0.2, 0.9, 0.3)
    mix.add(S.crash(1.0, 3.0), hit, 0.25, 0.6, 0.45)
    mix.add(S.boom(2.2, 1.0), hit, 0.0, 0.9, 0.2)
    for j, m in enumerate((38, 45, 50, 53, 57)):
        mix.add(S.ens(S.brass, mtof(m), 1.5, voices=3, spread=7, power=1.3, r=0.9), hit, (j - 2) * 0.3, 0.32, 0.45)
    for j, m in enumerate((62, 65, 69, 74)):
        mix.add(S.ens(S.choir, mtof(m), 1.6, voices=3, spread=12, a=0.05, r=0.9), hit, (j - 1.5) * 0.4, 0.26, 0.6)
    # Timpani roll building into the fight.
    roll_start, roll_end = 2.75, INTRO_LENGTH - 0.03
    t = roll_start
    k = 0
    while t < roll_end:
        g = 0.15 + 0.6 * ((t - roll_start) / (roll_end - roll_start)) ** 2
        mix.add(S.timpani(mtof(38 if k % 2 else 45), 1.0), t, -0.25 if k % 2 else 0.25, g, 0.3)
        t += 0.085 - 0.03 * (t - roll_start) / (roll_end - roll_start)
        k += 1
    out = mix.render(reverb_time=2.6, reverb_tone=5000, loop=False)
    out = out[:, :S.secs(INTRO_LENGTH + 1.5)]
    return S.fade(S.master(out, rms_db=-14.5, ceiling=0.84), 0.0, 1.2)
