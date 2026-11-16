"""전투 효과음 합성(2026-11-16). 48kHz 모노 16비트 WAV — 기존 효과음과 같은 형식.
  SFX_EnemyGun_Shot   적 포탑 사격(쾅 + 낮은 울림 + 먼 메아리)
  SFX_Missile_Launch  미사일 발사(점화 '펑' + 로켓 분사음이 멀어짐) — 적·아군 공통, 기존 파일을 바꾼다
  SFX_Missile_Hit     미사일 명중(큰 폭발 + 철판 찢김 + 2차 파열 + 긴 잔향) — 적·아군 공통
  SFX_Missile_Burst   미사일 공중 폭파(요격·격추: 날카로운 파열 + 흩어지는 파편)
  SFX_Shell_Impact    포탄이 적 선체에 명중(둔한 쿵 + 철판 울림)
  SFX_HullHit_Gun     기함 피격: 기관포탄 — 묵직하게 다시 만듦(HullHitSfx_v1의 '팅'이 가벼웠다)
  SFX_HullHit_Shell   기함 피격: 포탄 — 저음을 키워 다시 만듦
녹음 소스 없이 층을 쌓는다(저음 쿵 = 주파수가 떨어지는 사인, 잡음 대역, 금속 공명, 파편, 잔향).
실행: python synth.py <출력 폴더>   (파이썬 기본 모듈만 사용, 시드 고정 — 같은 결과)
"""
import math
import os
import random
import struct
import sys
import wave

SR = 48000
rng = random.Random(20261116)
TAU = 2 * math.pi


def n_(dur): return int(dur * SR)
def noise(dur): return [rng.gauss(0, 1) for _ in range(n_(dur))]
def mul(x, y): return [a * b for a, b in zip(x, y)]
def gain(x, g): return [a * g for a in x]
def add(x, y): return [a + b for a, b in zip(x, y)]


def biquad(x, b0, b1, b2, a1, a2):
    y = [0.0] * len(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, o
        y[i] = o
    return y


def _lp(x, f, q):
    w = TAU * f / SR
    c, al = math.cos(w), math.sin(w) / (2 * q)
    a0 = 1 + al
    return biquad(x, (1 - c) / 2 / a0, (1 - c) / a0, (1 - c) / 2 / a0, -2 * c / a0, (1 - al) / a0)


def _hp(x, f, q):
    w = TAU * f / SR
    c, al = math.cos(w), math.sin(w) / (2 * q)
    a0 = 1 + al
    return biquad(x, (1 + c) / 2 / a0, -(1 + c) / a0, (1 + c) / 2 / a0, -2 * c / a0, (1 - al) / a0)


def lp(x, f): return _lp(_lp(x, f, 0.541), f, 1.307)      # 4차 버터워스
def hp(x, f): return _hp(_hp(x, f, 0.541), f, 1.307)
def bp(x, lo, hi): return _lp(_hp(x, lo, 0.707), hi, 0.707)


def env(dur, tau, attack=0.0015):
    return [min(1.0, (i / SR) / attack) * math.exp(-(i / SR) / tau) for i in range(n_(dur))]


def place(buf, x, at, g=1.0):
    i = int(at * SR)
    for k in range(min(len(x), len(buf) - i)):
        buf[i + k] += x[k] * g


def sweep(dur, f0, f1, tau):
    """주파수가 f0에서 f1로 떨어지는 사인(폭발·포성의 저음 쿵)."""
    y, ph = [], 0.0
    for i in range(n_(dur)):
        t = i / SR
        ph += TAU * (f1 + (f0 - f1) * math.exp(-t / (tau * 0.6))) / SR
        y.append(math.sin(ph) * min(1.0, t / 0.002) * math.exp(-t / tau))
    return y


def modal(dur, f0, ratios, decays, amps):
    """금속판 울림: 비조화 배음의 감쇠 사인 묶음."""
    parts = [(TAU * f0 * r * (1 + rng.uniform(-0.004, 0.004)), rng.uniform(0, TAU), d, a) for r, d, a in zip(ratios, decays, amps)]
    y = []
    for i in range(n_(dur)):
        t = i / SR
        y.append(min(1.0, t / 0.0008) * sum(a * math.sin(w * t + p) * math.exp(-t / d) for w, p, d, a in parts))
    return y


def debris(dur, count, start, spread, lo, hi, amp):
    y = [0.0] * n_(dur)
    for _ in range(count):
        g = mul(bp(noise(0.03), lo, hi), env(0.03, rng.uniform(0.003, 0.01), 0.0003))
        place(y, g, start + rng.expovariate(1 / spread), amp * rng.uniform(0.3, 1.0))
    return y


def flutter(dur, rate, depth):
    """로켓 분사의 거친 떨림: 느린 잡음으로 진폭을 흔든다."""
    m = _lp(noise(dur), rate, 0.707)
    peak = max(abs(v) for v in m) or 1.0
    return [1 - depth + depth * (0.5 + 0.5 * v / peak) for v in m]


def room(x, size=0.35, mix=0.25, bright=4000):
    """짧은 잔향(슈뢰더: 빗살 4 + 올패스 2). size ≈ 잔향이 60dB 줄어드는 시간(초)."""
    wet = [0.0] * len(x)
    for ms in (29.7, 37.1, 41.1, 43.7):
        d = int(ms * SR / 1000)
        g = 10 ** (-3 * (ms / 1000) / size)
        buf = [0.0] * d
        for i, v in enumerate(x):
            o = buf[i % d]
            buf[i % d] = v + o * g
            wet[i] += o * 0.25
    for ms, g in ((5.0, 0.7), (1.7, 0.7)):
        d = int(ms * SR / 1000)
        buf = [0.0] * d
        for i, v in enumerate(wet):
            o = buf[i % d]
            w = v + o * g
            buf[i % d] = w
            wet[i] = o - g * w
    wet = _lp(wet, bright, 0.707)
    return [a * (1 - mix) + b * mix * 2.2 for a, b in zip(x, wet)]


def fade_end(x, sec=0.08):
    n = int(sec * SR)
    for k in range(n):
        x[-n + k] *= (1 - k / n) ** 2
    return x


CEIL = 10 ** (-1 / 20)
def soft(x): return [CEIL * math.tanh(v / CEIL) for v in x]


def loudest(m, win=0.1):
    w = int(SR * win)
    sq = [v * v for v in m]
    return max(math.sqrt(sum(sq[i:i + w]) / w) for i in range(0, max(1, len(m) - w), w // 4))


def level(x, target_db, push_db=6.0):
    """가장 큰 100ms RMS를 target_db에 맞춘다. 피크는 부드러운 리미터로 −1dBFS 아래."""
    target = 10 ** (target_db / 20)
    for _ in range(10):
        g = target / max(loudest(soft(x)), 1e-9)
        if abs(20 * math.log10(g)) < 0.1:
            break
        x = gain(x, g)
        peak = max(abs(v) for v in x)
        if peak / CEIL > 10 ** (push_db / 20):
            x = gain(x, 10 ** (push_db / 20) * CEIL / peak)
            break
    return soft(x)


# ---------------------------------------------------------------- 소리

def enemy_gun():
    """적 포탑 사격: 날카로운 쾅 + 낮은 울림 + 0.3초 뒤 먼 메아리 (1.7초)."""
    y = [0.0] * n_(1.7)
    place(y, mul(hp(noise(0.008), 1800), env(0.008, 0.0025, 0.0002)), 0, 1.8)
    place(y, sweep(0.9, 150, 42, 0.2), 0.001, 0.8)
    place(y, mul(bp(noise(0.3), 250, 1800), env(0.3, 0.055, 0.001)), 0.001, 1.6)
    place(y, mul(lp(noise(0.9), 240), env(0.9, 0.3, 0.004)), 0.004, 0.5)
    place(y, mul(bp(noise(0.25), 2500, 8000), env(0.25, 0.04, 0.001)), 0.002, 0.5)
    echo = add(mul(lp(noise(0.9), 700), env(0.9, 0.22, 0.03)), gain(sweep(0.9, 90, 40, 0.25), 0.8))
    place(y, echo, 0.3, 0.3)
    return fade_end(room(y, 0.6, 0.28, 3200), 0.25)


def missile_launch():
    """미사일 발사: 점화 '펑' + 로켓 분사음(밝게 시작해 멀어지며 둔해짐) (2.0초)."""
    dur = 2.0
    y = [0.0] * n_(dur)
    place(y, mul(hp(noise(0.01), 1500), env(0.01, 0.003, 0.0003)), 0, 0.9)
    place(y, sweep(0.4, 190, 60, 0.09), 0.001, 1.0)
    fl = flutter(dur, 55, 0.45)
    bright = mul(mul(bp(noise(dur), 900, 6500), fl), [min(1, (i / SR) / 0.03) * math.exp(-(i / SR) / 0.45) for i in range(n_(dur))])
    dull = mul(mul(bp(noise(dur), 180, 1400), fl), [(1 - math.exp(-(i / SR) / 0.25)) * math.exp(-(i / SR) / 0.7) for i in range(n_(dur))])
    hiss = mul(hp(noise(dur), 4500), [min(1, (i / SR) / 0.02) * math.exp(-(i / SR) / 0.3) for i in range(n_(dur))])
    rumble = mul(lp(noise(dur), 160), [min(1, (i / SR) / 0.05) * math.exp(-(i / SR) / 0.6) for i in range(n_(dur))])
    place(y, bright, 0.004, 1.0)
    place(y, dull, 0.004, 1.5)
    place(y, hiss, 0.004, 0.35)
    place(y, rumble, 0.004, 1.2)
    return fade_end(room(y, 0.5, 0.2, 5000), 0.5)


def missile_hit():
    """미사일 명중: 큰 폭발 + 철판 찢김 + 2차 파열 + 긴 잔향 (2.5초)."""
    y = [0.0] * n_(2.5)
    place(y, mul(hp(noise(0.02), 900), env(0.02, 0.006, 0.0005)), 0, 1.8)
    place(y, sweep(1.8, 95, 27, 0.6), 0.003, 0.9)
    place(y, mul(lp(noise(1.9), 1500), env(1.9, 0.42, 0.006)), 0.002, 1.8)
    place(y, mul(lp(noise(1.5), 190), env(1.5, 0.6, 0.01)), 0.01, 0.7)
    tt = [i / SR for i in range(n_(0.7))]
    tear = mul(mul(bp(noise(0.7), 500, 2400), [0.5 + 0.5 * math.sin(TAU * (16 + 28 * t) * t) for t in tt]), env(0.7, 0.2, 0.02))
    place(y, tear, 0.06, 0.65)
    place(y, modal(1.4, 210, [1, 1.61, 2.37, 3.3], [0.5, 0.35, 0.25, 0.16], [1, 0.7, 0.45, 0.3]), 0.01, 0.2)
    place(y, debris(2.5, 30, 0.15, 0.4, 1200, 5500, 0.6), 0)
    for _ in range(5):   # 2차 파열
        place(y, add(mul(hp(noise(0.06), 1500), env(0.06, 0.012, 0.0005)), gain(sweep(0.06, 140, 70, 0.05), 1.2)), 0.28 + rng.uniform(0, 0.5), 0.35)
    return fade_end(room(y, 0.9, 0.34, 2800), 0.4)


def missile_burst():
    """미사일 공중 폭파(요격·격추): 날카로운 파열 + 짧은 쿵 + 흩어지는 파편, 저음은 적게 (1.3초)."""
    y = [0.0] * n_(1.3)
    place(y, mul(hp(noise(0.012), 2200), env(0.012, 0.003, 0.0002)), 0, 1.4)
    place(y, sweep(0.5, 170, 65, 0.13), 0.001, 0.6)
    place(y, mul(bp(noise(0.5), 500, 4500), env(0.5, 0.1, 0.001)), 0.001, 1.1)
    place(y, mul(bp(noise(1.0), 1500, 7000), env(1.0, 0.3, 0.02)), 0.02, 0.3)
    place(y, debris(1.3, 26, 0.04, 0.22, 2500, 9000, 0.45), 0)
    return fade_end(room(y, 0.7, 0.3, 6000), 0.3)


def shell_impact():
    """포탄이 적 선체에 명중: 둔한 쿵 + 낮은 철판 울림 + 파편 조금 (0.9초)."""
    y = [0.0] * n_(0.9)
    place(y, mul(hp(noise(0.008), 1400), env(0.008, 0.0025, 0.0003)), 0, 1.0)
    place(y, sweep(0.6, 135, 48, 0.14), 0.001, 0.8)
    place(y, mul(bp(noise(0.5), 150, 1500), env(0.5, 0.11, 0.002)), 0.001, 1.4)
    place(y, modal(0.6, 330, [1, 1.73, 2.64, 3.9], [0.2, 0.13, 0.09, 0.06], [1, 0.6, 0.4, 0.25]), 0.003, 0.45)
    place(y, debris(0.9, 8, 0.05, 0.12, 1500, 6000, 0.25), 0)
    return fade_end(room(y, 0.4, 0.24, 3000), 0.12)


def hull_gun():
    """기함 피격 · 기관포탄(묵직하게): 탁 + 두꺼운 강판 '텅' + 짧은 저음 (0.7초)."""
    y = [0.0] * n_(0.7)
    place(y, mul(hp(noise(0.005), 1800), env(0.005, 0.0015, 0.0002)), 0, 0.8)
    place(y, sweep(0.35, 160, 62, 0.08), 0.0005, 1.5)
    place(y, mul(lp(noise(0.25), 420), env(0.25, 0.05, 0.001)), 0, 1.2)
    place(y, modal(0.6, 560, [1, 1.52, 2.31, 3.86, 5.37], [0.18, 0.13, 0.08, 0.05, 0.03], [1, 0.7, 0.45, 0.25, 0.15]), 0.0005, 0.4)
    place(y, mul(bp(noise(0.12), 2500, 8000), env(0.12, 0.025)), 0.004, 0.1)
    return fade_end(room(y, 0.3, 0.2, 4500), 0.08)


def hull_shell():
    """기함 피격 · 포탄(묵직하게): 쾅 + 깊은 저음 + 낮은 철판 울림 + 파편 (1.6초)."""
    y = [0.0] * n_(1.6)
    place(y, mul(hp(noise(0.012), 1100), env(0.012, 0.004, 0.0003)), 0, 1.1)
    place(y, sweep(1.2, 92, 30, 0.42), 0.002, 1.0)
    place(y, mul(lp(noise(1.1), 800), env(1.1, 0.25, 0.003)), 0.002, 1.5)
    place(y, mul(lp(noise(1.1), 170), env(1.1, 0.45, 0.008)), 0.006, 0.7)
    place(y, mul(bp(noise(0.25), 1000, 4000), env(0.25, 0.05, 0.001)), 0.003, 0.7)
    place(y, modal(1.1, 235, [1, 1.73, 2.64, 3.9, 5.2], [0.45, 0.3, 0.2, 0.12, 0.08], [1, 0.7, 0.5, 0.3, 0.18]), 0.004, 0.45)
    place(y, debris(1.6, 18, 0.09, 0.2, 1400, 5500, 0.3), 0)
    return fade_end(room(y, 0.6, 0.3, 3000), 0.2)


# 이름 → (만드는 함수, 목표 음량: 가장 큰 100ms RMS dB)
CLIPS = {
    "SFX_EnemyGun_Shot": (enemy_gun, -9.5),
    "SFX_Missile_Launch": (missile_launch, -11.0),
    "SFX_Missile_Hit": (missile_hit, -8.5),
    "SFX_Missile_Burst": (missile_burst, -10.5),
    "SFX_Shell_Impact": (shell_impact, -10.5),
    "SFX_HullHit_Gun": (hull_gun, -11.0),
    "SFX_HullHit_Shell": (hull_shell, -8.5),
}

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, (make, target) in CLIPS.items():
        x = make()
        mean = sum(x) / len(x)
        x = level([v - mean for v in x], target)
        with wave.open(os.path.join(out, name + ".wav"), "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(struct.pack("<%dh" % len(x), *[int(round(v * 32767)) for v in x]))
        low = lp(x, 200)
        share = sum(v * v for v in low) / max(1e-12, sum(v * v for v in x))
        print(f"{name}: {len(x) / SR:.2f}s peak {20 * math.log10(max(abs(v) for v in x)):.1f}dBFS "
              f"loudest100ms {20 * math.log10(loudest(x)):.1f}dB under200Hz {share * 100:.0f}%")
