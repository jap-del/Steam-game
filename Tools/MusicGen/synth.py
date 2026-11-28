"""Small additive synthesizer for the game's music. Everything is numpy, no samples.

A song is rendered into a stereo float buffer. Instruments render one note into a mono array and
`Mix.add` places it in time with a pan. Percussion are short pre-rendered arrays.
"""
import math
import numpy as np

SR = 44100
RNG = np.random.default_rng(7)


def mtof(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def secs(n):
    return int(round(n * SR))


# ------------------------------------------------------------------ envelopes and filters

def adsr(n_gate, n_total, a, d, s, r):
    """Gate-based envelope. a, d, r in seconds, s = sustain level."""
    env = np.zeros(n_total, dtype=np.float64)
    na = max(1, secs(a))
    t = np.arange(n_total) / SR
    attack = np.clip(t / max(a, 1e-4), 0, 1)
    decay = s + (1 - s) * np.exp(-np.maximum(t - a, 0) / max(d, 1e-4) * 3.0)
    env = np.where(t < a, attack, decay)
    if n_gate < n_total:
        level = env[min(n_gate, n_total - 1)]
        tr = (np.arange(n_total - n_gate)) / SR
        env[n_gate:] = level * np.exp(-tr / max(r, 1e-4) * 5.0)
    return env


def butter_mask(freqs, lo=None, hi=None, order=2):
    m = np.ones_like(freqs)
    if hi is not None:
        m *= 1.0 / np.sqrt(1.0 + (freqs / hi) ** (2 * order))
    if lo is not None:
        with np.errstate(divide='ignore'):
            m *= 1.0 / np.sqrt(1.0 + (lo / np.maximum(freqs, 1e-3)) ** (2 * order))
    return m


def fft_filter(x, lo=None, hi=None, order=2):
    """Zero-phase band filter through the FFT. Works on 1-D or (channels, n)."""
    n = x.shape[-1]
    size = 1 << int(math.ceil(math.log2(max(n, 2))))
    spec = np.fft.rfft(x, size, axis=-1)
    freqs = np.fft.rfftfreq(size, 1.0 / SR)
    spec *= butter_mask(freqs, lo, hi, order)
    return np.fft.irfft(spec, size, axis=-1)[..., :n]


def noise(n):
    return RNG.standard_normal(n)


# ------------------------------------------------------------------ additive core

def additive(freq, n_gate, n_total, amp_fn, max_h=40, vib=0.0, vib_rate=5.0, vib_delay=0.25,
             detune=0.0, scoop=0.0, top=14000.0, phase_seed=None):
    """Sum of harmonics. amp_fn(h, t) -> scalar or array. freq in Hz."""
    t = np.arange(n_total) / SR
    f = freq * 2.0 ** (detune / 1200.0)
    inst = np.full(n_total, f)
    if vib > 0:
        ramp = np.clip((t - vib_delay) / 0.4, 0, 1)
        inst = inst * (1.0 + vib * ramp * np.sin(2 * np.pi * vib_rate * t + RNG.uniform(0, 6.28)))
    if scoop:
        inst = inst * (1.0 - scoop * np.exp(-t / 0.04))
    phi = 2 * np.pi * np.cumsum(inst) / SR
    out = np.zeros(n_total)
    rng = np.random.default_rng(phase_seed) if phase_seed is not None else RNG
    for h in range(1, max_h + 1):
        if f * h > top:
            break
        a = amp_fn(h, t)
        if np.isscalar(a) and a < 1e-4:
            continue
        out += a * np.sin(h * phi + rng.uniform(0, 6.28))
    return out


def ens(fn, freq, dur, voices=3, spread=8.0, **kw):
    """Several slightly detuned copies of an instrument, like a section."""
    out = None
    for i in range(voices):
        d = 0.0 if voices == 1 else (i / (voices - 1) - 0.5) * 2 * spread
        y = fn(freq, dur, detune=d, **kw)
        out = y if out is None else out + y
    return out / math.sqrt(voices)


# ------------------------------------------------------------------ instruments (return mono arrays)

def pad(freq, dur, detune=0.0, bright=6.0, a=0.7, r=1.4, s=0.85):
    ng, nt = secs(dur), secs(dur + r)
    y = additive(freq, ng, nt, lambda h, t: (1.0 / h) * math.exp(-(h - 1) / bright), max_h=24, detune=detune,
                 vib=0.0015, vib_rate=0.3, vib_delay=0)
    return y * adsr(ng, nt, a, 0.6, s, r) * 0.5


def strings(freq, dur, detune=0.0, bright=9.0, a=0.25, r=0.6, s=0.9):
    ng, nt = secs(dur), secs(dur + r)
    y = additive(freq, ng, nt, lambda h, t: (1.0 / h) * math.exp(-(h - 1) / bright), max_h=30, detune=detune,
                 vib=0.004, vib_rate=5.3, vib_delay=0.15)
    return y * adsr(ng, nt, a, 0.4, s, r) * 0.5


def spiccato(freq, dur, detune=0.0, bright=11.0):
    ng, nt = secs(min(dur, 0.14)), secs(min(dur, 0.14) + 0.12)
    y = additive(freq, ng, nt, lambda h, t: (1.0 / h) * math.exp(-(h - 1) / bright), max_h=30, detune=detune)
    return y * adsr(ng, nt, 0.004, 0.07, 0.35, 0.08) * 0.6


def tremolo_strings(freq, dur, detune=0.0, rate=12.0):
    ng, nt = secs(dur), secs(dur + 0.3)
    y = additive(freq, ng, nt, lambda h, t: (1.0 / h) * math.exp(-(h - 1) / 12.0), max_h=30, detune=detune,
                 vib=0.003, vib_rate=5.8, vib_delay=0)
    t = np.arange(nt) / SR
    trem = 0.55 + 0.45 * np.abs(np.sin(np.pi * rate * t + RNG.uniform(0, 3)))
    return y * trem * adsr(ng, nt, 0.08, 0.3, 0.9, 0.25) * 0.45


def brass(freq, dur, detune=0.0, power=1.0, a=0.05, r=0.35):
    ng, nt = secs(dur), secs(dur + r)
    t_env = np.arange(nt) / SR
    swell = np.clip(t_env / max(a * 2, 1e-3), 0, 1) * (0.65 + 0.35 * np.exp(-t_env / 0.25))
    bright = 1.5 + 9.0 * power * swell

    def amp(h, t):
        return (1.0 / h) * np.exp(-(h - 1) / bright)
    y = additive(freq, ng, nt, amp, max_h=30, detune=detune, scoop=0.012, vib=0.002, vib_rate=5.0, vib_delay=0.3)
    return y * adsr(ng, nt, a, 0.35, 0.8, r) * 0.55


VOWEL_A = [(730, 140, 1.0), (1090, 170, 0.55), (2440, 260, 0.25)]
VOWEL_O = [(570, 120, 1.0), (840, 150, 0.45), (2410, 260, 0.15)]


def choir(freq, dur, detune=0.0, vowel=VOWEL_A, a=0.6, r=1.2):
    ng, nt = secs(dur), secs(dur + r)

    def amp(h, t):
        hf = h * freq
        g = 0.04 / h
        for F, bw, gain in vowel:
            g += gain * math.exp(-((hf - F) / bw) ** 2)
        return g
    y = additive(freq, ng, nt, amp, max_h=40, detune=detune, vib=0.006, vib_rate=5.1, vib_delay=0.2, top=6000)
    breath = fft_filter(noise(nt), 900, 4000) * 0.015
    return (y + breath) * adsr(ng, nt, a, 0.5, 0.95, r) * 0.6


def flute(freq, dur, detune=0.0, a=0.07, r=0.25):
    ng, nt = secs(dur), secs(dur + r)
    amps = {1: 1.0, 2: 0.22, 3: 0.09, 4: 0.03}
    y = additive(freq, ng, nt, lambda h, t: amps.get(h, 0.0), max_h=4, detune=detune, vib=0.005, vib_rate=5.0,
                 vib_delay=0.18)
    breath = fft_filter(noise(nt), freq * 1.5, freq * 6) * 0.04
    return (y + breath) * adsr(ng, nt, a, 0.3, 0.85, r) * 0.45


def pluck(freq, dur, detune=0.0, ring=1.6, tone=1.3):
    nt = secs(dur + ring)

    def amp(h, t):
        return (1.0 / h ** tone) * np.exp(-t * (1.2 + 0.9 * h))
    y = additive(freq, nt, nt, amp, max_h=24, detune=detune)
    t = np.arange(nt) / SR
    env = np.clip(t / 0.003, 0, 1)
    gate = secs(dur)
    env[gate:] *= np.exp(-(t[gate:] - t[gate]) / 0.25)
    return y * env * 0.5


def harpsichord(freq, dur, detune=0.0):
    nt = secs(dur + 0.6)

    def amp(h, t):
        return (1.0 / h ** 0.75) * np.exp(-t * (3.0 + 1.4 * h))
    y = additive(freq, nt, nt, amp, max_h=30, detune=detune)
    t = np.arange(nt) / SR
    return y * np.clip(t / 0.002, 0, 1) * 0.35


def bell(freq, dur, detune=0.0, decay=1.0):
    nt = secs(max(dur, 0.2) + 2.2 / decay)
    t = np.arange(nt) / SR
    f = freq * 2.0 ** (detune / 1200)
    parts = [(1.0, 1.0, 1.1), (2.0, 0.45, 1.8), (3.0, 0.3, 2.6), (4.16, 0.22, 3.4), (5.43, 0.14, 4.4), (6.8, 0.08, 5.5)]
    y = np.zeros(nt)
    for ratio, gain, dk in parts:
        if f * ratio > 15000:
            continue
        y += gain * np.sin(2 * np.pi * f * ratio * t + RNG.uniform(0, 6.28)) * np.exp(-t * dk * decay)
    return y * np.clip(t / 0.002, 0, 1) * 0.35


def bass(freq, dur, detune=0.0, bite=6.0):
    ng, nt = secs(dur), secs(dur + 0.12)
    t_env = np.arange(nt) / SR
    bright = 1.2 + bite * np.exp(-t_env * 9.0)
    y = additive(freq, ng, nt, lambda h, t: (1.0 / h) * np.exp(-(h - 1) / bright), max_h=16, detune=detune)
    sub = np.sin(2 * np.pi * freq * t_env) * 0.5
    return (y + sub) * adsr(ng, nt, 0.004, 0.25, 0.55, 0.1) * 0.55


def sub(freq, dur, a=0.02, r=0.3):
    ng, nt = secs(dur), secs(dur + r)
    t = np.arange(nt) / SR
    return np.sin(2 * np.pi * freq * t) * adsr(ng, nt, a, 0.3, 0.9, r) * 0.6


# ------------------------------------------------------------------ percussion

def _sweep_sine(f0, f1, k, n):
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t * k)
    return np.sin(2 * np.pi * np.cumsum(f) / SR), t


def kick(gain=1.0):
    n = secs(0.45)
    s, t = _sweep_sine(130, 46, 28, n)
    click = fft_filter(noise(n), 1500, 6000) * np.exp(-t * 300) * 0.3
    return (s * np.exp(-t * 9.0) + click) * gain


def taiko(pitch=1.0, gain=1.0):
    n = secs(1.2)
    s, t = _sweep_sine(150 * pitch, 62 * pitch, 22, n)
    body = s * np.exp(-t * 5.0) + 0.35 * np.sin(2 * np.pi * 1.62 * 62 * pitch * t) * np.exp(-t * 9.0)
    skin = fft_filter(noise(n), 80, 900) * np.exp(-t * 26.0) * 0.6
    return (body + skin) * gain


def snare(gain=1.0, bright=1.0):
    n = secs(0.35)
    t = np.arange(n) / SR
    tone = np.sin(2 * np.pi * 185 * t) * np.exp(-t * 30) * 0.5
    rattle = fft_filter(noise(n), 1200, 7000 * bright) * np.exp(-t * 16) * 0.9
    return (tone + rattle) * gain


def rim(gain=1.0):
    n = secs(0.12)
    t = np.arange(n) / SR
    y = np.sin(2 * np.pi * 820 * t) * np.exp(-t * 70) + fft_filter(noise(n), 2000, 8000) * np.exp(-t * 90) * 0.5
    return y * gain * 0.6


def hat(gain=1.0, open_=False):
    n = secs(0.5 if open_ else 0.09)
    t = np.arange(n) / SR
    y = fft_filter(noise(n), 7000, 16000) * np.exp(-t * (9 if open_ else 55))
    return y * gain * 0.5


def shaker(gain=1.0):
    n = secs(0.11)
    t = np.arange(n) / SR
    env = np.clip(t / 0.015, 0, 1) * np.exp(-t * 32)
    return fft_filter(noise(n), 4000, 11000) * env * gain * 0.45


def crash(gain=1.0, length=3.2):
    n = secs(length)
    t = np.arange(n) / SR
    y = fft_filter(noise(n), 3500, 15000) * np.exp(-t * 1.4)
    y += fft_filter(noise(n), 900, 4000) * np.exp(-t * 3.5) * 0.35
    return y * np.clip(t / 0.003, 0, 1) * gain * 0.45


def reverse_swell(length=1.6, gain=1.0):
    n = secs(length)
    t = np.arange(n) / SR
    y = fft_filter(noise(n), 2500, 14000) * np.exp(-t * 2.2)
    y = y[::-1] * np.clip(np.arange(n)[::-1] / secs(0.01), 0, 1)
    return y * gain * 0.4


def timpani(freq, gain=1.0):
    n = secs(2.2)
    t = np.arange(n) / SR
    y = np.zeros(n)
    for ratio, g, dk in [(1.0, 1.0, 1.8), (1.5, 0.45, 3.0), (1.99, 0.3, 3.8), (2.44, 0.18, 4.8)]:
        y += g * np.sin(2 * np.pi * freq * ratio * t + RNG.uniform(0, 6.28)) * np.exp(-t * dk)
    y += fft_filter(noise(n), 60, 600) * np.exp(-t * 30) * 0.5
    return y * np.clip(t / 0.002, 0, 1) * gain * 0.6


def tick(gain=1.0, hi=True):
    n = secs(0.06)
    t = np.arange(n) / SR
    f = 3100 if hi else 1500
    y = np.sin(2 * np.pi * f * t) * np.exp(-t * 110) + fft_filter(noise(n), 3000, 12000) * np.exp(-t * 160) * 0.5
    return y * gain * 0.35


def heartbeat(gain=1.0):
    n = secs(0.7)
    s, t = _sweep_sine(85, 38, 18, n)
    a = s * np.exp(-t * 11.0)
    b = np.zeros(n)
    off = secs(0.22)
    b[off:] = a[:n - off] * 0.6
    return (a + b) * gain


def boom(length=2.5, gain=1.0):
    n = secs(length)
    s, t = _sweep_sine(75, 28, 3.0, n)
    y = s * np.exp(-t * 1.6) + fft_filter(noise(n), 20, 180) * np.exp(-t * 2.5) * 0.6
    return y * np.clip(t / 0.004, 0, 1) * gain


def rumble(length, gain=1.0, base=36.7):
    n = secs(length)
    t = np.arange(n) / SR
    y = fft_filter(noise(n), 20, 140, order=3) * 1.2
    y *= 0.7 + 0.3 * np.sin(2 * np.pi * 0.8 * t)
    y += np.sin(2 * np.pi * base * t) * 0.5 + np.sin(2 * np.pi * base * 1.01 * t) * 0.3
    return y * gain


# ------------------------------------------------------------------ mixing

class Mix:
    """Stereo bus with a reverb send. Times in seconds."""

    def __init__(self, seconds, tail=5.0):
        self.n = secs(seconds)
        self.tail = secs(tail)
        self.dry = np.zeros((2, self.n + self.tail))
        self.wet = np.zeros((2, self.n + self.tail))

    def add(self, y, at, pan=0.0, gain=1.0, send=0.25):
        start = secs(at)
        if start >= self.dry.shape[1]:
            return
        end = min(start + len(y), self.dry.shape[1])
        y = y[:end - start] * gain
        th = (pan + 1) * math.pi / 4
        l, r = math.cos(th), math.sin(th)
        self.dry[0, start:end] += y * l
        self.dry[1, start:end] += y * r
        if send > 0:
            self.wet[0, start:end] += y * l * send
            self.wet[1, start:end] += y * r * send

    def add_delay(self, y, at, pan, gain, send, delay, feedback=0.4, taps=5):
        """Ping-pong echo for arps and bells."""
        self.add(y, at, pan, gain, send)
        g = gain
        for k in range(1, taps + 1):
            g *= feedback
            p = -0.7 if k % 2 else 0.7
            self.add(fft_filter(y, None, 6000 / k ** 0.5, 1), at + delay * k, p, g, send)

    def render(self, reverb_time=2.0, reverb_tone=5000.0, loop=True):
        ir = make_ir(reverb_time, reverb_tone)
        wet = np.stack([fft_convolve(self.wet[0], ir[0]), fft_convolve(self.wet[1], ir[1])])[:, :self.dry.shape[1]]
        out = self.dry + wet
        if loop:
            # The tail rings into the start of the next repeat, so the loop has no seam.
            body = out[:, :self.n].copy()
            body[:, :self.tail] += out[:, self.n:self.n + self.tail]
            return body
        return out


def make_ir(t60, tone):
    n = secs(t60 * 1.3)
    t = np.arange(n) / SR
    ir = np.zeros((2, n))
    pre = secs(0.022)
    for c in range(2):
        nz = noise(n)
        low = fft_filter(nz, None, tone, 1) * np.exp(-6.9 * t / t60)
        high = fft_filter(nz, tone, None, 1) * np.exp(-6.9 * t / (t60 * 0.45))
        x = low + high * 0.5
        x[:pre] = 0
        ir[c] = x
    ir /= np.sqrt((ir ** 2).sum() / 2) * 1.6
    return ir


def fft_convolve(a, b):
    n = len(a) + len(b) - 1
    size = 1 << int(math.ceil(math.log2(n)))
    return np.fft.irfft(np.fft.rfft(a, size) * np.fft.rfft(b, size), size)[:n]


def master(x, rms_db=-16.0, ceiling=0.89, lowcut=28.0):
    x = fft_filter(x, lowcut, None, 2)
    rms = np.sqrt(np.mean(x ** 2))
    x = x * (10 ** (rms_db / 20) / max(rms, 1e-9))
    knee = ceiling * 0.78
    mag = np.abs(x)
    over = mag > knee
    mag[over] = knee + (ceiling - knee) * np.tanh((mag[over] - knee) / (ceiling - knee))
    return np.sign(x) * mag


def fade(x, fade_in=0.0, fade_out=0.0):
    x = x.copy()
    if fade_in > 0:
        k = secs(fade_in)
        x[..., :k] *= np.linspace(0, 1, k)
    if fade_out > 0:
        k = secs(fade_out)
        x[..., -k:] *= np.linspace(1, 0, k) ** 2
    return x


# ------------------------------------------------------------------ harmony helpers

QUALITY = {'m': (0, 3, 7), 'M': (0, 4, 7), 'd': (0, 3, 6), 's4': (0, 5, 7), 'm7': (0, 3, 7, 10), 'M7': (0, 4, 7, 11)}


def chord_pcs(root, quality):
    return [(root + i) % 12 for i in QUALITY[quality]]


def voice_lead(prev, pcs, low, high):
    """Pick a voicing of the pitch classes close to the previous voicing, inside [low, high]."""
    if not prev:
        center = (low + high) / 2.0
        prev = [center - 4, center, center + 4]
    notes = []
    targets = sorted(prev)
    for i, pc in enumerate(pcs):
        tgt = targets[min(i, len(targets) - 1)]
        best = None
        for octv in range(0, 10):
            m = pc + 12 * octv
            if low <= m <= high and (best is None or abs(m - tgt) < abs(best - tgt)):
                best = m
        if best is None:
            # The range is narrower than an octave and misses this note: take the closest one just outside.
            best = min((pc + 12 * o for o in range(10)), key=lambda m: min(abs(m - low), abs(m - high)))
        notes.append(best)
    return sorted(set(notes))


def write_wav(path, x):
    import wave
    data = np.clip(x, -1, 1)
    pcm = (data.T * 32767).astype('<i2').tobytes()
    with wave.open(path, 'wb') as w:
        w.setnchannels(x.shape[0])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm)
