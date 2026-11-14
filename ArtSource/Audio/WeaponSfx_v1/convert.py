import sys, os, numpy as np, soundfile as sf
from scipy.signal import resample_poly
from math import gcd
DL = r"C:/Users/최병욱/Downloads"
SFX = r"C:/Users/최병욱/UnityProjects/NavalRoguelike/NavalRoguelike/Assets/_Game/Audio/SFX"
OUT = sys.argv[1]
SR = 48000

def load(name):
    d, sr = sf.read(os.path.join(DL, name), always_2d=True, dtype='float64')
    m = d.mean(axis=1)
    if sr != SR:
        g = gcd(SR, sr); m = resample_poly(m, SR // g, sr // g)
    return m

def loudest(m, win=0.1):
    w = int(SR * win); best = 0
    for i in range(0, max(1, len(m) - w), w // 4):
        best = max(best, np.sqrt((m[i:i+w] ** 2).mean()))
    return best

def onset(m, t0, t1):
    a, b = int(t0 * SR), int(t1 * SR)
    seg = np.abs(m[a:b]); thr = seg.max() * 0.5
    return (a + int(np.argmax(seg > thr))) / SR

def cut(m, t0, t1): return m[int(t0 * SR):int(t1 * SR)].copy()

def fade(m, fin=0.002, fout_from=None):
    n = len(m); k = int(fin * SR)
    if k: m[:k] *= np.linspace(0, 1, k)
    if fout_from is not None:
        s = int(fout_from * SR)
        if s < n: m[s:] *= np.linspace(1, 0, n - s) ** 2
    return m

def level(m, target_rms, peak_db=-1.0, max_push_db=7.0):
    """기존 클립과 같은 음량(가장 큰 100ms RMS)으로 맞춘다. 피크가 넘치면 부드러운 리미터(tanh)로 누르고,
    그래도 모자라면(피크가 아주 뾰족한 녹음) 리미터를 max_push_db까지만 쓴다."""
    lim = 10 ** (peak_db / 20)
    def soft(x): return lim * np.tanh(x / lim)
    for _ in range(12):
        g = target_rms / max(loudest(soft(m * 1.0)), 1e-9)
        if abs(20 * np.log10(g)) < 0.1: break
        pk = np.abs(m * g).max() / lim
        if pk > 10 ** (max_push_db / 20): g = 10 ** (max_push_db / 20) * lim / np.abs(m).max()
        m = m * g
        if pk > 10 ** (max_push_db / 20): break
    return soft(m)

ref_auto = loudest(sf.read(os.path.join(SFX, "SFX_Autocannon_Shot.wav"), dtype='float64')[0])
ref_ciws = loudest(sf.read(os.path.join(SFX, "SFX_CIWS_Burst.wav"), dtype='float64')[0])
print(f"ref loudest100ms: autocannon {20*np.log10(ref_auto):.1f}dB ciws {20*np.log10(ref_ciws):.1f}dB")

outs = {}
# 기관포: 연사 녹음의 마지막 한 발(잔향이 깨끗함)
m = load("기관포.wav"); t = onset(m, 30.69, 30.76) - 0.002
outs["SFX_Autocannon_Shot"] = level(fade(cut(m, t, t + 0.60), fout_from=0.22), ref_auto)
# 76mm: 한 발 + 굴러가는 메아리
m = load("76mm 함포.mp3"); t = onset(m, 0.08, 0.20) - 0.004
outs["SFX_NavalGun76_Shot"] = level(fade(cut(m, t, t + 1.95), fout_from=1.25), ref_auto * 10 ** (2 / 20))
# 40mm 쌍열포: 10발 연사의 마지막 발
m = load("40mm 쌍열포.wav"); t = onset(m, 1.54, 1.62) - 0.002
outs["SFX_TwinGun40_Shot"] = level(fade(cut(m, t, t + 0.95), fout_from=0.45), ref_auto)
# CIWS: 연사 1.15초(게임 코드의 연사→꼬리 전환점) + 원래 꼬리를 60ms 겹쳐 잇는다
m = load("ciws.flac")
burst = cut(m, 0.12, 0.12 + 1.15 + 0.06); tail = cut(m, 1.38, 3.95)
x = int(0.06 * SR); r = np.linspace(0, 1, x)
spliced = np.concatenate([burst[:-x], burst[-x:] * (1 - r) + tail[:x] * r, tail[x:]])
outs["SFX_CIWS_Burst"] = level(fade(spliced, fin=0.004, fout_from=3.0), ref_ciws)

for name, m in outs.items():
    p = os.path.join(OUT, name + ".wav")
    sf.write(p, m.astype(np.float32), SR, subtype='PCM_16')
    print(f"{name}: {len(m)/SR:.2f}s peak {20*np.log10(np.abs(m).max()):.1f}dBFS loudest100ms {20*np.log10(loudest(m)):.1f}dB")
