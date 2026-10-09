#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Vonvert - built-in original ambience synthesizer.

Synthesizes 10 seamless-looping ambience WAVs (48 kHz / mono / 16-bit PCM)
using purely procedural DSP generated in Python (std library only).

IMPORTANT (source disclosure):
  * Every clip is ORIGINAL, procedurally synthesized from scratch in this
    repository. There is NO third-party copyrighted material and NO third
    party audio was downloaded, copied or transcoded.
  * This script and its outputs are released under Apache-2.0 (same as the
    repository), so they are fully license-clean and do NOT need to appear
    in any third-party notices file.

Usage:
    python synth_ambience.py <outdir>            # all clips
    python synth_ambience.py <outdir> rain white # subset
Outputs go to   <outdir>/<name>.wav
"""
import array, math, os, random, sys, wave, zlib

SR = 48000
SECONDS = 8
CF = 0.50          # crossfade seconds -> seamless loop
N = int(SR * SECONDS)
CFN = int(SR * CF)
OUTDIR = None

# --------------------------------------------------------------------------
# Low level helpers
# --------------------------------------------------------------------------
def rng(seed):
    return random.Random(seed)

def one_pole_lp(x, fc):
    """Single-pole (IIR) low-pass. Returns new list."""
    a = 1.0 - math.exp(-2.0 * math.pi * fc / SR)
    y = [0.0] * len(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y

def highpass_1p(x, fc):
    """Single-pole high-pass = input - lowpass(input)."""
    lp = one_pole_lp(x, fc)
    return [s - l for s, l in zip(x, lp)]

def db_to_amp(db):
    return 10.0 ** (db / 20.0)

def normalize(x, peak=0.9):
    m = max(1e-9, max(abs(v) for v in x))
    k = peak / m
    return [v * k for v in x]

def clamp16(v):
    v = int(v)
    if v > 32767: return 32767
    if v < -32768: return -32768
    return v

def loopify(seg, length, cf, r):
    """Blend head and tail so the clip loops seamlessly (equal-power)."""
    out = seg[:length]
    for i in range(cf):
        w = i / cf
        c = math.cos(w * math.pi / 2.0)
        s = math.sin(w * math.pi / 2.0)
        tail = seg[length + i]
        head = out[i]
        out[i] = c * head + s * tail
    return out

def write_wav(path, samples):
    data = array.array('h', (clamp16(v * 32767.0) for v in samples))
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"  wrote {path}  ({len(data)*2} bytes)")

def noise(n, r, lo=0.85, hi=0.85):
    return [r.uniform(-hi, hi) if i % 2 == 0 else r.uniform(-lo, lo)
            for i in range(n)]

def fade_envelope(n, attack=0.005, release=0.005):
    return None

# --------------------------------------------------------------------------
# Clip generators (each returns mono float samples, length N + CFN + margin)
# --------------------------------------------------------------------------
def gen_white(r):
    return noise(N + CFN + 64, r)

def gen_rain(r):
    total = N + CFN + 256
    # fine hiss (rain falling on leaves) -> high-passed noise
    hiss = noise(total, r, 0.5, 0.5)
    hiss_hp = highpass_1p(hiss, 2600.0)
    # low rumbling wash -> low-passed noise
    rumble = one_pole_lp(noise(total, r, 0.7, 0.7), 220.0)
    # scattered droplets: short exponential-decay high-freq pips
    drop = [0.0] * total
    t = 0
    while t < total:
        t += int(r.uniform(1800, 9000))
        if t >= total: break
        dur = int(r.uniform(12, 34))
        a = r.uniform(0.25, 0.7)
        f = r.uniform(3600.0, 7500.0)
        for k in range(min(dur, total - t)):
            drop[t + k] += a * math.sin(2 * math.pi * f * k / SR) * math.exp(-6.0 * k / dur)
    mix = [0.55 * h + 0.30 * b + d for h, b, d in zip(hiss_hp, rumble, drop)]
    return normalize(mix)

def gen_cafe(r):
    total = N + CFN + 256
    base = noise(total, r, 0.6, 0.6)
    lp = one_pole_lp(base, 1100.0)
    # babble: several band-filtered noises with slow amplitude wobble
    babble = [0.0] * total
    for band, amp in ((320, 0.5), (700, 0.6), (1250, 0.42), (2100, 0.22)):
        bn = one_pole_lp(highpass_1p(noise(total, r, 0.5, 0.5), band * 0.9), band * 1.6)
        ph = r.uniform(0, 2 * math.pi)
        rate = r.uniform(0.12, 0.4)
        for i in range(total):
            v = math.sin(2 * math.pi * rate * i / SR + ph)
            wob = 0.62 + 0.38 * v
            babble[i] += amp * wob * bn[i]
    # occasional clink (cup/plate) -> tiny metallic ping
    for _ in range(int(total / SR * 1.1)):
        t0 = r.randrange(total)
        f = r.uniform(2200.0, 4200.0)
        for k in range(220):
            if t0 + k >= total: break
            babble[t0 + k] += 0.08 * math.sin(2 * math.pi * f * k / SR) * math.exp(-9.0 * k / 220)
    mix = [0.85 * l + 0.30 * b for l, b in zip(lp, babble)]
    return normalize(mix)

def gen_forest(r):
    total = N + CFN + 1024
    breeze = one_pole_lp(noise(total, r, 0.35, 0.35), 900.0)
    # leaves rustle -> mid band
    rustle = one_pole_lp(highpass_1p(noise(total, r, 0.4, 0.4), 900.0), 4200.0)
    mix = [0.65 * b + 0.55 * s for b, s in zip(breeze, rustle)]
    # birdsong: chirps (rising/falling sine sweeps) at random offsets
    t = int(r.uniform(0.4, 1.6) * SR)
    while t < total:
        # a short phrase of 1-3 chirps
        for _ in range(r.randint(1, 3)):
            if t >= total: break
            dur = int(r.uniform(0.18, 0.5) * SR)
            f0 = r.uniform(2100.0, 4000.0)
            f1 = f0 * r.uniform(0.75, 1.35)
            a = r.uniform(0.10, 0.22)
            for k in range(dur):
                if t + k >= total: break
                frac = k / dur
                f = f0 + (f1 - f0) * frac
                ph = 2 * math.pi * f * k / SR
                env = math.sin(math.pi * min(1.0, frac * 1.0)) ** 2
                mix[t + k] += a * env * math.sin(ph)
            t += dur + int(r.uniform(0.06, 0.3) * SR)
        t += int(r.uniform(0.5, 2.8) * SR)
    return normalize(mix)

def gen_ocean(r):
    total = N + CFN + 4096
    # pink-ish surf: heavily low-passed noise with slow swell LFO
    raw = noise(total, r, 1.0, 1.0)
    surf = one_pole_lp(raw, 700.0)
    surf = one_pole_lp(surf, 700.0)
    # slow swell (0.07-0.11 Hz) -> wave breaks
    out = [0.0] * total
    for i in range(total):
        t = i / SR
        swell = math.sin(2 * math.pi * 0.085 * t + 1.3)
        swell2 = math.sin(2 * math.pi * 0.19 * t + 0.4)
        env = 0.55 + 0.32 * swell + 0.13 * swell2
        out[i] = surf[i] * env
    # spume hiss on swells peak
    hiss = highpass_1p(noise(total, r, 0.4, 0.4), 4800.0)
    for i in range(total):
        t = i / SR
        swell = math.sin(2 * math.pi * 0.085 * t + 1.3)
        gate = max(0.0, swell) ** 6
        out[i] += 0.8 * gate * hiss[i]
    return normalize(out)

def gen_static(r):
    total = N + CFN + 256
    # radio hiss -> slightly band-limited noise
    hiss = one_pole_lp(noise(total, r, 0.9, 0.9), 15000.0)
    hiss = highpass_1p(hiss, 900.0)
    # weak carrier whistle drifting
    out = [0.0] * total
    for i in range(total):
        t = i / SR
        fc = 1450.0 + 60.0 * math.sin(2 * math.pi * 0.05 * t)
        out[i] = hiss[i] + 0.045 * math.sin(2 * math.pi * fc * t) * math.sin(2 * math.pi * 18 * t + 1.0)
    # random pops / crackle (reception breakup)
    t = 0
    while t < total:
        t += int(r.uniform(6000, 40000))
        if t >= total: break
        for k in range(int(r.uniform(6, 30))):
            if t + k >= total: break
            out[t + k] += r.uniform(-0.7, 0.7) * math.exp(-4.0 * k / 30)
    return normalize(out)

def gen_fan(r):
    total = N + CFN + 1024
    # rotor wash -> band-passed noise around blade-passing frequency
    wash = one_pole_lp(noise(total, r, 0.8, 0.8), 900.0)
    wash = highpass_1p(wash, 90.0)
    # blade ripple ~ 25-45 Hz + slow airflow changes
    out = [0.0] * total
    for i in range(total):
        t = i / SR
        ripple = 0.82 + 0.18 * math.sin(2 * math.pi * 33.0 * t)
        drift = 0.85 + 0.15 * math.sin(2 * math.pi * 0.13 * t + 0.7)
        out[i] = wash[i] * ripple * drift
    # motor hum (weak)
    for i in range(total):
        t = i / SR
        out[i] += 0.03 * math.sin(2 * math.pi * 120.0 * t + 0.3)
    return normalize(out)

def gen_keyboard(r):
    total = N + CFN + 256
    out = [0.0] * total
    # barely audible room floor
    floor = one_pole_lp(noise(total, r, 0.02, 0.02), 800.0)
    out[:] = floor
    t = int(r.uniform(0.2, 1.0) * SR)
    while t < total:
        # keystroke: click is a short damped noise burst + low thump
        dur = int(r.uniform(0.03, 0.09) * SR)
        f = r.uniform(2200.0, 5200.0)
        for k in range(dur):
            if t + k >= total: break
            env = math.exp(-14.0 * k / dur)
            out[t + k] += 0.9 * env * math.sin(2 * math.pi * f * k / SR)
            out[t + k] += 0.5 * env * r.uniform(-1, 1)
        # low thump tail
        for k in range(int(0.06 * SR)):
            if t + dur + k >= total: break
            out[t + dur + k] += 0.22 * math.exp(-18.0 * k / int(0.06 * SR)) * math.sin(2 * math.pi * 160.0 * k / SR)
        t += dur + int(r.uniform(0.06, 0.55) * SR)
    return normalize(out)

def gen_campfire(r):
    total = N + CFN + 512
    # low fire roar -> low-passed noise with flicker
    roar = one_pole_lp(noise(total, r, 0.55, 0.55), 500.0)
    out = [0.0] * total
    for i in range(total):
        t = i / SR
        flick = 0.7 + 0.3 * math.sin(2 * math.pi * 0.24 * t) * math.sin(2 * math.pi * 0.37 * t + 1.2)
        out[i] = roar[i] * flick
    # crackles: short broadband pops (log sap / embers)
    t = int(r.uniform(0.1, 0.5) * SR)
    while t < total:
        t += int(r.uniform(1200, 9000))
        if t >= total: break
        dur = int(r.uniform(18, 90))
        a = r.uniform(0.3, 1.0)
        f = r.uniform(900.0, 5200.0)
        for k in range(dur):
            if t + k >= total: break
            env = math.exp(-9.0 * k / dur)
            out[t + k] += a * env * math.sin(2 * math.pi * f * k / SR)
            out[t + k] += 0.6 * a * env * r.uniform(-1, 1)
        t += dur
    return normalize(out)

def gen_livestream(r):
    total = N + CFN + 256
    floor = noise(total, r, 0.10, 0.10)
    # air conditioning wash -> low-passed
    ac = one_pole_lp(noise(total, r, 0.18, 0.18), 1200.0)
    out = [0.0] * total
    for i in range(total):
        t = i / SR
        # 50 Hz mains hum + harmonics
        hum = (math.sin(2 * math.pi * 50.0 * t)
               + 0.32 * math.sin(2 * math.pi * 100.0 * t + 0.4)
               + 0.12 * math.sin(2 * math.pi * 150.0 * t + 1.1)
               + 0.05 * math.sin(2 * math.pi * 200.0 * t + 2.0))
        out[i] = floor[i] + 0.55 * ac[i] + 0.30 * hum
        # faint monitor coil whine
        out[i] += 0.02 * math.sin(2 * math.pi * 16000.0 * t)
    return normalize(out)

GENERATORS = {
    'white': gen_white,
    'rain': gen_rain,
    'cafe': gen_cafe,
    'forest': gen_forest,
    'ocean': gen_ocean,
    'static': gen_static,
    'fan': gen_fan,
    'keyboard': gen_keyboard,
    'campfire': gen_campfire,
    'livestream': gen_livestream,
}

def main():
    global OUTDIR
    args = sys.argv[1:]
    OUTDIR = args[0] if args else 'out_wav'
    os.makedirs(OUTDIR, exist_ok=True)
    names = args[1:] if len(args) > 1 else list(GENERATORS)
    for name in names:
        if name not in GENERATORS:
            print(f"  !! unknown clip {name}")
            continue
        print(f"synth {name}")
        # Deterministic seed (zlib.crc32) — Python's hash() is salted per process,
        # so hash() here would make every run produce different audio.
        r = rng(zlib.crc32(('vonvert-' + name).encode('utf-8')))
        seg = GENERATORS[name](r)
        loop = loopify(seg, N, CFN, r)
        write_wav(os.path.join(OUTDIR, name + '.wav'), loop)

if __name__ == '__main__':
    main()
