"""플레이어 함선 피격음 합성(2026-11-15). 48kHz 모노 16비트 WAV — 기존 효과음과 같은 형식.
녹음 소스가 없으므로 층을 쌓아 만든다: 금속 공명(감쇠 사인파 묶음) · 저음 폭발(주파수가 떨어지는 사인 + 저역 잡음) ·
파편(드문 짧은 충격) · 물기둥(고역 잡음 포락) · 긁힘(진폭이 흔들리는 대역 잡음) · 짧은 잔향(감쇠 잡음과 컨볼루션).
실행: python synth.py <출력 폴더>   (numpy · scipy · soundfile 필요, 시드 고정 — 같은 결과)
"""
import sys
import os
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, fftconvolve

SR = 48000
rng = np.random.default_rng(20261115)


def t_(dur): return np.arange(int(dur * SR)) / SR
def noise(dur): return rng.standard_normal(int(dur * SR))
def bp(x, lo, hi, order=4): return sosfilt(butter(order, [lo, hi], 'band', fs=SR, output='sos'), x)
def lp(x, f, order=4): return sosfilt(butter(order, f, 'low', fs=SR, output='sos'), x)
def hp(x, f, order=4): return sosfilt(butter(order, f, 'high', fs=SR, output='sos'), x)


def env_exp(dur, tau, attack=0.0015):
    t = t_(dur)
    return np.clip(t / max(attack, 1e-4), 0, 1) * np.exp(-t / tau)


def place(buf, x, at):
    i = int(at * SR)
    n = min(len(x), len(buf) - i)
    if n > 0:
        buf[i:i + n] += x[:n]


def sweep(dur, f0, f1, tau):
    """주파수가 f0에서 f1로 떨어지는 사인(폭발의 저음 쿵)."""
    t = t_(dur)
    f = f1 + (f0 - f1) * np.exp(-t / (tau * 0.6))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env_exp(dur, tau, 0.002)


def modal(dur, f0, ratios, decays, amps, detune=0.004):
    """금속판 울림: 비조화 배음의 감쇠 사인 묶음."""
    t = t_(dur)
    y = np.zeros_like(t)
    for r, d, a in zip(ratios, decays, amps):
        f = f0 * r * (1 + rng.uniform(-detune, detune))
        y += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / d)
    return y * np.clip(t / 0.0008, 0, 1)


def debris(dur, count, start, spread, lo, hi, amp):
    y = np.zeros(int(dur * SR))
    for _ in range(count):
        g = bp(noise(0.03), lo, hi, 2) * env_exp(0.03, rng.uniform(0.003, 0.01), 0.0003)
        place(y, g * amp * rng.uniform(0.3, 1.0), start + rng.exponential(spread))
    return y


def room(x, size=0.35, mix=0.25, bright=4000):
    ir = lp(noise(size), bright, 2) * np.exp(-t_(size) / (size / 4))
    ir /= np.sqrt((ir ** 2).sum())
    return x * (1 - mix) + fftconvolve(x, ir)[:len(x)] * mix * 1.6


def fade_end(x, sec=0.08):
    n = int(sec * SR)
    x[-n:] *= np.linspace(1, 0, n) ** 2
    return x


def soft(x, ceil=10 ** (-1 / 20)): return ceil * np.tanh(x / ceil)


def loudest(m, win=0.1):
    w = int(SR * win)
    return max(np.sqrt((m[i:i + w] ** 2).mean()) for i in range(0, max(1, len(m) - w), w // 4))


def level(x, target_db, push_db=6.0):
    """가장 큰 100ms RMS를 target_db에 맞춘다. 피크는 부드러운 리미터로 −1dBFS 아래."""
    lim = 10 ** (-1 / 20)
    target = 10 ** (target_db / 20)
    for _ in range(10):
        g = target / max(loudest(soft(x)), 1e-9)
        if abs(20 * np.log10(g)) < 0.1:
            break
        x = x * g
        if np.abs(x).max() / lim > 10 ** (push_db / 20):
            x *= 10 ** (push_db / 20) * lim / np.abs(x).max()
            break
    return soft(x)


def gun():
    """기관포탄: 강판에 튕기는 '팅' + 탁 (0.5초)."""
    y = np.zeros(int(0.5 * SR))
    place(y, hp(noise(0.004), 2500) * env_exp(0.004, 0.0012, 0.0002) * 0.9, 0)
    place(y, modal(0.45, 1650, [1, 2.31, 3.86, 5.37, 7.1], [0.11, 0.07, 0.045, 0.03, 0.02], [1, 0.6, 0.45, 0.3, 0.2]) * 0.55, 0.0005)
    place(y, lp(noise(0.06), 260) * env_exp(0.06, 0.018) * 1.2, 0)
    place(y, bp(noise(0.12), 3000, 9000) * env_exp(0.12, 0.025) * 0.15, 0.004)
    return fade_end(room(y, 0.25, 0.18, 6000), 0.06)


def shell():
    """포탄: 쾅 + 철판 울림 + 파편 (1.4초)."""
    y = np.zeros(int(1.4 * SR))
    place(y, hp(noise(0.012), 1200) * env_exp(0.012, 0.004, 0.0003) * 1.2, 0)
    place(y, sweep(1.0, 110, 38, 0.32) * 1.3, 0.002)
    place(y, lp(noise(0.9), 900) * env_exp(0.9, 0.22, 0.003) * 1.0, 0.002)
    place(y, bp(noise(0.25), 1200, 4500) * env_exp(0.25, 0.05, 0.001) * 0.5, 0.003)
    place(y, modal(0.9, 380, [1, 1.73, 2.64, 3.9, 5.2], [0.35, 0.22, 0.15, 0.1, 0.07], [1, 0.7, 0.5, 0.35, 0.2]) * 0.35, 0.004)
    place(y, debris(1.4, 18, 0.08, 0.18, 1500, 6000, 0.35), 0)
    return fade_end(room(y, 0.5, 0.3, 3500), 0.15)


def missile():
    """미사일: 큰 폭발 + 철판 찢김 + 2차 파열 + 긴 잔향 (2.2초)."""
    y = np.zeros(int(2.2 * SR))
    place(y, hp(noise(0.02), 900) * env_exp(0.02, 0.006, 0.0005) * 1.3, 0)
    place(y, sweep(1.6, 85, 28, 0.55) * 1.5, 0.003)
    place(y, lp(noise(1.8), 1300) * env_exp(1.8, 0.45, 0.006) * 1.2, 0.002)
    place(y, lp(noise(1.2), 220) * env_exp(1.2, 0.5, 0.01) * 1.4, 0.01)
    tt = t_(0.7)
    tear = bp(noise(0.7), 600, 2600) * (0.5 + 0.5 * np.sin(2 * np.pi * (18 + 30 * tt) * tt)) * env_exp(0.7, 0.2, 0.02)
    place(y, tear * 0.45, 0.05)
    place(y, modal(1.5, 260, [1, 1.61, 2.37, 3.3, 4.6], [0.6, 0.4, 0.28, 0.2, 0.12], [1, 0.8, 0.5, 0.4, 0.2]) * 0.3, 0.01)
    place(y, debris(2.2, 30, 0.12, 0.35, 1200, 5500, 0.4), 0)
    for c in 0.25 + rng.uniform(0, 0.15, 6):
        place(y, hp(noise(0.05), 2000) * env_exp(0.05, 0.01, 0.0005) * 0.35, c)
    return fade_end(room(y, 0.8, 0.35, 3000), 0.3)


def torpedo():
    """어뢰·기뢰: 물속에서 둔한 쿵 + 선체 신음 + 솟구쳤다 떨어지는 물기둥 (2.4초)."""
    y = np.zeros(int(2.4 * SR))
    place(y, lp(sweep(1.4, 70, 30, 0.6) * 1.6 + noise(1.4) * env_exp(1.4, 0.35, 0.01) * 0.9, 380, 4), 0)
    place(y, modal(1.6, 95, [1, 1.52, 2.21, 3.05], [0.7, 0.5, 0.35, 0.25], [1, 0.7, 0.45, 0.3]) * 0.45, 0.01)
    st = t_(2.0)
    rise = np.clip((st - 0.12) / 0.25, 0, 1) * np.exp(-np.clip(st - 0.37, 0, None) / 0.55)
    place(y, bp(noise(2.0), 500, 7000, 2) * rise * 0.55, 0.05)
    fall = np.clip((st - 0.9) / 0.2, 0, 1) * np.exp(-np.clip(st - 1.1, 0, None) / 0.4)
    place(y, bp(noise(2.0), 300, 3000, 2) * fall * 0.35, 0.05)
    return fade_end(room(y, 0.6, 0.25, 2500), 0.3)


def collision():
    """충돌·좌초: 쿵 + 긁힘 + 선체 신음 (1.3초)."""
    y = np.zeros(int(1.3 * SR))
    place(y, lp(noise(0.25), 300) * env_exp(0.25, 0.07, 0.004) * 1.4 + np.pad(sweep(0.25, 75, 45, 0.15), (0, 0)) * 1.0, 0)
    st = t_(1.1)
    am = 0.55 + 0.45 * np.abs(np.sin(2 * np.pi * (34 + 9 * np.sin(2 * np.pi * 1.3 * st)) * st))
    place(y, bp(noise(1.1), 250, 1800) * am * np.clip(st / 0.05, 0, 1) * np.exp(-st / 0.45) * 0.7, 0.03)
    place(y, modal(1.2, 140, [1, 1.47, 2.08, 2.9], [0.5, 0.35, 0.25, 0.18], [1, 0.7, 0.5, 0.3]) * 0.35, 0.02)
    place(y, debris(1.3, 10, 0.05, 0.3, 800, 3500, 0.25), 0)
    return fade_end(room(y, 0.45, 0.22, 3000), 0.12)


# 이름 → (만드는 함수, 목표 음량: 가장 큰 100ms RMS dB)
CLIPS = {
    "SFX_HullHit_Gun": (gun, -13.0),
    "SFX_HullHit_Shell": (shell, -9.5),
    "SFX_HullHit_Missile": (missile, -8.5),
    "SFX_HullHit_Torpedo": (torpedo, -9.5),
    "SFX_HullHit_Collision": (collision, -11.0),
}

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, (make, target) in CLIPS.items():
        x = make()
        x = level(x - x.mean(), target)
        sf.write(os.path.join(out, name + ".wav"), x.astype(np.float32), SR, subtype='PCM_16')
        print(f"{name}: {len(x) / SR:.2f}s peak {20 * np.log10(np.abs(x).max()):.1f}dBFS loudest100ms {20 * np.log10(loudest(x)):.1f}dB")
