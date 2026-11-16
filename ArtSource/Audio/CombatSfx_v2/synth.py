"""전투 효과음 2차 + 환경음 · 배경음악 합성(2026-11-16). 48kHz 모노 16비트 WAV.
효과음 14종:
  SFX_Torpedo_Launch       어뢰 발사(압축 공기 + 입수 물보라)
  SFX_Underwater_Blast     수중 폭발(어뢰 명중 · 폭뢰 · 기뢰: 둔한 쿵 + 물기둥)
  SFX_Sink_Capsize         적 격침 · 침수 전복(물 밀려드는 소리 + 공기 방울 + 선체 신음)
  SFX_Sink_Groan           적 격침 · 선체 찢어짐(철판 뒤틀리는 소리)
  SFX_Wreck_Blast          적 격침 · 유폭/연쇄 폭발(파열음이 섞인 폭발)
  SFX_EnemyAutocannon_Shot 적 기관포 · 기총 한 발
  SFX_Jet_Pass             제트기 통과
  SFX_Prop_Pass            프로펠러기 통과(WWII)
  SFX_Dive_Siren           급강하(엔진음이 치솟는다)
  SFX_Drone_Buzz           자폭 드론 돌입(2행정 엔진 윙윙)
  SFX_Shell_Splash         빗나간 포탄의 수면 착탄
  SFX_Depth_Drop           폭뢰 · 기뢰 투하(철컹 + 퐁당)
  SFX_Chaff_Launch         기만체 발사(펑 + 치익)
  SFX_Jam_Pulse            전자전 교란(전자음 떨림)
루프 3종(끝과 처음이 이어진다):
  SFX_Sea_Ambience         바다 환경음(파도 · 바람) 12초
  BGM_Battle               전투 음악 19.2초(100BPM 8마디, 라단조)
  BGM_Boss                 보스 음악 15.2초(126BPM 8마디)
실행: python synth.py <출력 폴더> [이름…]   (파이썬 기본 모듈만. CombatSfx_v1/synth.py의 도구를 가져다 쓴다)
"""
import importlib.util
import math
import os
import random
import struct
import sys
import wave

_spec = importlib.util.spec_from_file_location("sfx_v1", os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "CombatSfx_v1", "synth.py"))
v1 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(v1)
SR, TAU = v1.SR, v1.TAU
n_, noise, mul, gain, add, lp, hp, bp, env, place, sweep, modal, debris, room, fade_end, level, loudest = (
    v1.n_, v1.noise, v1.mul, v1.gain, v1.add, v1.lp, v1.hp, v1.bp, v1.env, v1.place, v1.sweep, v1.modal, v1.debris, v1.room, v1.fade_end, v1.level, v1.loudest)
v1.rng.seed(20261117)
rng = v1.rng

TAB = 4096


def table(amps):
    """배음 진폭 목록으로 한 주기 파형표를 만든다."""
    t = [sum(a * math.sin(TAU * (k + 1) * i / TAB) for k, a in enumerate(amps)) for i in range(TAB)]
    peak = max(abs(v) for v in t)
    return [v / peak for v in t]


SAW = table([1 / k for k in range(1, 13)])
BUZZ = table([1 / math.sqrt(k) for k in range(1, 15)])
SOFT = table([1 / k ** 1.6 for k in range(1, 7)])
HORN = table([1, 0.7, 0.55, 0.35, 0.22, 0.12, 0.06])


def osc(dur, freq, tab):
    """파형표 발진기. freq는 숫자이거나 t(초) → Hz 함수."""
    f = freq if callable(freq) else (lambda t: freq)
    y, ph = [], 0.0
    k = TAB / SR
    for i in range(n_(dur)):
        ph += f(i / SR) * k
        y.append(tab[int(ph) & (TAB - 1)])
    return y


def shape(dur, fn): return [fn(i / SR) for i in range(n_(dur))]
def bell(t, mid, width): return math.exp(-((t - mid) / width) ** 2)
def slow(dur, rate): return v1._lp(noise(dur), rate, 0.707)


def unit(x):
    peak = max(abs(v) for v in x) or 1.0
    return [v / peak for v in x]


# ---------------------------------------------------------------- 효과음

def torpedo_launch():
    y = [0.0] * n_(1.2)
    place(y, mul(hp(noise(0.5), 1500), env(0.5, 0.11, 0.005)), 0, 1.0)             # 압축 공기
    place(y, sweep(0.3, 115, 55, 0.06), 0, 1.3)                                    # 발사관 쿵
    place(y, mul(bp(noise(0.9), 400, 5000), shape(0.9, lambda t: min(1, t / 0.03) * math.exp(-t / 0.22))), 0.17, 1.0)   # 입수
    bub = mul(bp(noise(0.9), 250, 1200), mul(shape(0.9, lambda t: math.exp(-t / 0.4)), unit([abs(v) for v in slow(0.9, 30)])))
    place(y, bub, 0.25, 0.8)
    return fade_end(room(y, 0.3, 0.15, 5000), 0.2)


def underwater_blast():
    y = [0.0] * n_(2.4)
    place(y, lp(add(gain(sweep(1.4, 72, 30, 0.6), 1.5), gain(mul(noise(1.4), env(1.4, 0.35, 0.01)), 1.0)), 420), 0)
    place(y, modal(1.6, 95, [1, 1.52, 2.21, 3.05], [0.7, 0.5, 0.35, 0.25], [1, 0.7, 0.45, 0.3]), 0.01, 0.3)
    rise = shape(2.0, lambda t: max(0.0, min(1.0, (t - 0.12) / 0.25)) * math.exp(-max(0.0, t - 0.37) / 0.55))
    fall = shape(2.0, lambda t: max(0.0, min(1.0, (t - 0.9) / 0.2)) * math.exp(-max(0.0, t - 1.1) / 0.4))
    place(y, mul(bp(noise(2.0), 500, 7000), rise), 0.05, 0.75)
    place(y, mul(bp(noise(2.0), 300, 3000), fall), 0.05, 0.45)
    return fade_end(room(y, 0.6, 0.25, 2500), 0.3)


def sink_capsize():
    dur = 4.0
    y = [0.0] * n_(dur)
    swell = shape(dur, lambda t: min(1, t / 0.7) * (0.65 + 0.35 * math.sin(TAU * 0.6 * t)) * math.exp(-max(0, t - 2.4) / 0.9))
    place(y, mul(bp(noise(dur), 200, 2500), swell), 0, 1.0)                        # 물 밀려드는 소리
    place(y, mul(hp(noise(dur), 3500), swell), 0, 0.25)
    for _ in range(90):                                                            # 공기 방울
        f0 = rng.uniform(250, 900)
        d = rng.uniform(0.02, 0.05)
        blip = mul(osc(d, lambda t, f0=f0, d=d: f0 * (1 + 0.8 * t / d), SOFT), env(d, d / 2.5, 0.002))
        place(y, blip, rng.uniform(0.2, 3.6), rng.uniform(0.15, 0.4))
    groan = mul(osc(2.6, lambda t: 92 - 14 * t + 4 * math.sin(TAU * 5 * t), SAW), shape(2.6, lambda t: min(1, t / 0.4) * math.exp(-max(0, t - 1.6) / 0.5)))
    place(y, lp(groan, 500), 0.5, 0.5)                                             # 선체 신음
    place(y, modal(2.0, 68, [1, 1.47, 2.08, 2.9], [0.9, 0.6, 0.4, 0.3], [1, 0.7, 0.5, 0.3]), 0.3, 0.3)
    return fade_end(room(y, 0.7, 0.25, 3500), 0.6)


def sink_groan():
    dur = 3.0
    y = [0.0] * n_(dur)
    place(y, mul(bp(noise(0.4), 600, 4000), env(0.4, 0.08, 0.002)), 0, 0.8)        # 찢어지는 순간
    place(y, sweep(0.6, 100, 40, 0.2), 0, 0.9)
    for start, length, r0, r1, lo, hi, amp in ((0.15, 1.2, 22, 46, 300, 1500, 0.9), (1.1, 1.1, 40, 18, 220, 1100, 0.8), (1.9, 1.0, 16, 30, 400, 1900, 0.6)):
        stick = osc(length, lambda t, r0=r0, r1=r1, length=length: r0 + (r1 - r0) * t / length, SAW)     # 붙었다 미끄러지는 떨림
        creak = mul(mul(bp(noise(length), lo, hi), [0.5 + 0.5 * v for v in stick]),
                    shape(length, lambda t, length=length: min(1, t / 0.08) * min(1, (length - t) / 0.25)))
        place(y, creak, start, amp)
    place(y, modal(2.4, 120, [1, 1.47, 2.08, 2.9, 3.7], [0.8, 0.55, 0.4, 0.3, 0.2], [1, 0.7, 0.5, 0.35, 0.2]), 0.05, 0.35)
    place(y, modal(1.5, 310, [1, 1.61, 2.37], [0.4, 0.3, 0.2], [1, 0.6, 0.4]), 1.15, 0.2)
    return fade_end(room(y, 0.8, 0.3, 3000), 0.4)


def wreck_blast():
    y = [0.0] * n_(1.6)
    place(y, mul(hp(noise(0.015), 1200), env(0.015, 0.005, 0.0004)), 0, 1.6)
    place(y, sweep(1.0, 125, 40, 0.3), 0.002, 0.9)
    place(y, mul(lp(noise(1.2), 1800), env(1.2, 0.28, 0.004)), 0.002, 1.5)
    place(y, mul(bp(noise(0.4), 800, 4500), env(0.4, 0.08, 0.001)), 0.003, 0.7)
    place(y, debris(1.6, 34, 0.06, 0.25, 1500, 7000, 0.6), 0)
    for _ in range(6):
        place(y, add(mul(hp(noise(0.05), 1500), env(0.05, 0.01, 0.0005)), gain(sweep(0.05, 160, 80, 0.04), 1.0)), 0.15 + rng.uniform(0, 0.6), 0.4)
    return fade_end(room(y, 0.6, 0.3, 3500), 0.25)


def enemy_autocannon():
    y = [0.0] * n_(0.5)
    place(y, mul(hp(noise(0.004), 2000), env(0.004, 0.0012, 0.0002)), 0, 1.3)
    place(y, sweep(0.2, 230, 90, 0.04), 0.0005, 1.0)
    place(y, mul(bp(noise(0.15), 400, 3000), env(0.15, 0.035, 0.0005)), 0.0005, 1.3)
    place(y, mul(bp(noise(0.4), 300, 1500), env(0.4, 0.1, 0.01)), 0.01, 0.25)
    return fade_end(room(y, 0.3, 0.2, 4500), 0.1)


def jet_pass():
    dur = 2.5
    y = [0.0] * n_(dur)
    amp = shape(dur, lambda t: bell(t, 1.0, 0.42) + 0.25 * bell(t, 1.5, 0.6))
    hi = mul(bp(noise(dur), 1800, 7500), shape(dur, lambda t: bell(t, 0.85, 0.35)))            # 다가올 때 날카롭게
    lo = mul(bp(noise(dur), 150, 900), shape(dur, lambda t: bell(t, 1.25, 0.5)))               # 지나간 뒤 낮은 굉음
    mid = mul(bp(noise(dur), 500, 2500), amp)
    whine = mul(osc(dur, lambda t: 2500 - 900 / (1 + math.exp(-(t - 1.0) / 0.12)), SOFT), amp)   # 도플러로 떨어지는 터빈음
    place(y, hi, 0, 0.9)
    place(y, lo, 0, 1.6)
    place(y, mid, 0, 1.0)
    place(y, whine, 0, 0.12)
    return fade_end(room(y, 0.5, 0.2, 5000), 0.4)


def prop_pass():
    dur = 2.5
    y = [0.0] * n_(dur)
    amp = shape(dur, lambda t: bell(t, 1.1, 0.5) + 0.2 * bell(t, 1.7, 0.6))
    f = lambda t: 118 - 30 / (1 + math.exp(-(t - 1.1) / 0.15))                                 # 도플러
    engine = mul(mul(osc(dur, f, BUZZ), shape(dur, lambda t: 0.75 + 0.25 * math.sin(TAU * 13 * t))), amp)
    place(y, lp(engine, 2200), 0, 1.0)
    place(y, mul(bp(noise(dur), 200, 1500), amp), 0, 0.5)
    return fade_end(room(y, 0.4, 0.18, 4000), 0.4)


def dive_siren():
    dur = 2.2
    y = [0.0] * n_(dur)
    up = lambda t: (t / dur) ** 1.5
    amp = shape(dur, lambda t: 0.25 + 0.75 * up(t))
    place(y, mul(lp(osc(dur, lambda t: 110 + 90 * up(t), BUZZ), 2500), amp), 0, 0.9)           # 엔진
    place(y, mul(osc(dur, lambda t: 300 + 420 * up(t), HORN), amp), 0, 0.55)                   # 사이렌
    place(y, mul(osc(dur, lambda t: (300 + 420 * up(t)) * 1.012, HORN), amp), 0, 0.35)
    place(y, mul(hp(noise(dur), 1500), amp), 0, 0.3)                                          # 바람
    return fade_end(room(y, 0.4, 0.2, 5000), 0.12)


def drone_buzz():
    dur = 2.0
    y = [0.0] * n_(dur)
    f = lambda t: 148 + 70 * (t / dur) ** 2
    amp = shape(dur, lambda t: min(1, t / 0.15) * (0.4 + 0.6 * t / dur))
    rough = shape(dur, lambda t: 0.7 + 0.3 * math.sin(TAU * 37 * t) * math.sin(TAU * 3.1 * t))
    place(y, mul(mul(bp(osc(dur, f, BUZZ), 250, 4500), rough), amp), 0, 1.0)
    place(y, mul(bp(noise(dur), 1500, 6000), amp), 0, 0.12)
    return fade_end(room(y, 0.3, 0.15, 5000), 0.15)


def shell_splash():
    y = [0.0] * n_(0.9)
    place(y, sweep(0.2, 100, 60, 0.04), 0, 0.9)
    place(y, mul(bp(noise(0.7), 600, 7000), shape(0.7, lambda t: min(1, t / 0.015) * math.exp(-t / 0.16))), 0, 1.0)
    place(y, mul(hp(noise(0.6), 3000), shape(0.6, lambda t: min(1, t / 0.08) * math.exp(-t / 0.18))), 0.22, 0.3)   # 떨어지는 물방울
    return fade_end(room(y, 0.3, 0.15, 6000), 0.15)


def depth_drop():
    y = [0.0] * n_(0.8)
    place(y, modal(0.25, 720, [1, 1.52, 2.4, 3.3], [0.06, 0.04, 0.03, 0.02], [1, 0.7, 0.5, 0.3]), 0, 0.5)   # 레일 철컹
    place(y, mul(lp(noise(0.08), 500), env(0.08, 0.02, 0.001)), 0, 0.7)
    place(y, mul(osc(0.25, lambda t: 150 + 300 * math.exp(-t / 0.035), SOFT), env(0.25, 0.06, 0.003)), 0.13, 1.0)   # 퐁당
    place(y, mul(bp(noise(0.6), 500, 6000), shape(0.6, lambda t: min(1, t / 0.02) * math.exp(-t / 0.14))), 0.14, 0.7)
    return fade_end(room(y, 0.3, 0.15, 5000), 0.12)


def chaff_launch():
    y = [0.0] * n_(1.0)
    place(y, mul(hp(noise(0.006), 1800), env(0.006, 0.002, 0.0002)), 0, 1.2)
    place(y, sweep(0.2, 270, 90, 0.035), 0, 1.2)                                               # 박격포 '펑'
    gate = [1.0 if v > 0.2 else 0.15 for v in unit(slow(0.8, 220))]
    place(y, mul(mul(hp(noise(0.8), 5000), gate), env(0.8, 0.3, 0.01)), 0.02, 0.7)             # 치익
    place(y, mul(bp(noise(0.3), 1000, 5000), env(0.3, 0.07, 0.003)), 0.26, 0.6)                # 공중에서 퍼짐
    return fade_end(room(y, 0.35, 0.2, 7000), 0.15)


def jam_pulse():
    dur = 1.4
    y = [0.0] * n_(dur)
    amp = shape(dur, lambda t: min(1, t / 0.01) * math.exp(-t / 0.55))
    chirp = lambda t: 600 + 1800 * ((t % 0.35) / 0.35) + 180 * (1 if math.sin(TAU * 37 * t) > 0 else -1)
    place(y, mul(osc(dur, chirp, SOFT), amp), 0, 0.8)
    place(y, mul(osc(dur, lambda t: 1210 + 240 * math.sin(TAU * 11 * t), HORN), amp), 0, 0.3)
    held, v = [], 0.0
    for i in range(n_(dur)):                                                                    # 디지털 잡음(표본 유지)
        if i % 16 == 0: v = rng.uniform(-1, 1)
        held.append(v)
    place(y, mul(bp(held, 1500, 6000), mul(amp, [1.0 if (int(i / SR * 22) % 3 == 0) else 0.0 for i in range(n_(dur))])), 0, 0.5)
    return fade_end(room(y, 0.3, 0.2, 7000), 0.2)


# ---------------------------------------------------------------- 루프

def circ(f, x):
    """루프용 필터: 두 번 이어 붙여 걸고 뒤쪽 절반을 쓴다(처음과 끝이 이어지게)."""
    return f(x + x)[len(x):]


def sea_ambience():
    dur, xf = 12.0, 1.0
    total = dur + xf
    am = lambda ks, depth: shape(total, lambda t: 1 - depth + depth * (0.5 + 0.5 * sum(math.sin(TAU * k / dur * t + 1.7 * k) for k in ks) / len(ks)))
    y = add(add(gain(mul(bp(noise(total), 150, 1800), am((2, 3, 5), 0.75)), 1.0),
                gain(mul(hp(noise(total), 2500), am((4, 7), 0.85)), 0.22)),
            gain(mul(lp(noise(total), 400), am((1, 2), 0.6)), 0.9))
    n, m = n_(dur), n_(xf)
    out = y[:n]
    for i in range(m):                                                                          # 끝자락을 처음에 겹쳐 잇는다
        w = i / m
        out[i] = y[i] * w + y[n + i] * (1 - w)
    return out


class Song:
    """박자표에 음을 놓는 루프. 끝을 넘는 소리는 처음으로 돌아가 겹친다."""

    def __init__(self, bpm, bars):
        self.beat = 60.0 / bpm
        self.n = int(round(bars * 4 * self.beat * SR))
        self.tracks = {}

    def buf(self, name): return self.tracks.setdefault(name, [0.0] * self.n)

    def put(self, name, x, beat, g=1.0):
        b, n = self.buf(name), self.n
        i0 = int(round(beat * self.beat * SR))
        for k, v in enumerate(x):
            b[(i0 + k) % n] += v * g

    def note(self, name, freq, beat, beats, tab, g=1.0, attack=0.01, release=0.08, detune=0.0):
        dur = beats * self.beat + release
        hold = beats * self.beat
        e = shape(dur, lambda t: min(1, t / attack) * (1.0 if t < hold else max(0.0, 1 - (t - hold) / release)))
        x = mul(osc(dur, freq, tab), e)
        if detune: x = add(x, mul(osc(dur, freq * (1 + detune), tab), e))
        self.put(name, x, beat, g)

    def mix(self, gains, filters=None):
        out = [0.0] * self.n
        for name, g in gains.items():
            b = self.tracks.get(name)
            if b is None: continue
            if filters and name in filters: b = circ(filters[name], b)
            for i, v in enumerate(b): out[i] += v * g
        return out


N = {'A1': 55.0, 'Bb1': 58.27, 'C2': 65.41, 'D2': 73.42, 'Eb2': 77.78, 'F2': 87.31, 'G2': 98.0, 'A2': 110.0, 'Bb2': 116.54,
     'C3': 130.81, 'D3': 146.83, 'Eb3': 155.56, 'E3': 164.81, 'F3': 174.61, 'G3': 196.0, 'A3': 220.0, 'Bb3': 233.08,
     'C4': 261.63, 'C#4': 277.18, 'D4': 293.66, 'E4': 329.63, 'F4': 349.23, 'G4': 392.0, 'A4': 440.0, 'Bb4': 466.16, 'D5': 587.33}


def kick(): return add(gain(sweep(0.4, 125, 44, 0.13), 1.0), gain(mul(lp(noise(0.4), 300), env(0.4, 0.05, 0.002)), 0.5))
def tom(f): return add(gain(sweep(0.35, f * 1.5, f, 0.1), 1.0), gain(mul(bp(noise(0.35), 200, 1200), env(0.35, 0.04, 0.002)), 0.3))
def snare(): return add(gain(mul(bp(noise(0.25), 180, 4500), env(0.25, 0.06, 0.001)), 1.0), gain(mul(osc(0.25, 190, SOFT), env(0.25, 0.04, 0.001)), 0.6))


def bgm_battle():
    s = Song(100, 8)
    chords = [('D2', ('D3', 'F3', 'A3')), ('D2', ('D3', 'F3', 'A3')), ('Bb1', ('Bb2', 'D3', 'F3')), ('C2', ('C3', 'E3', 'G3')),
              ('D2', ('D3', 'F3', 'A3')), ('F2', ('F3', 'A3', 'C4')), ('G2', ('G3', 'Bb3', 'D4')), ('A1', ('A3', 'C#4', 'E4'))]
    k, sn = kick(), snare()
    for bar, (root, triad) in enumerate(chords):
        b0 = bar * 4
        for e in range(8):                                                       # 8분음표 베이스
            s.note('bass', N[root] * (2 if e in (3, 7) else 1), b0 + e * 0.5, 0.4, SAW, 1.0 if e % 2 == 0 else 0.7, 0.005, 0.05)
        for f in triad:                                                          # 현악 패드
            s.note('pad', N[f], b0, 4, SAW, 0.5, 0.25, 0.3, detune=0.006)
        for beat in (0, 2, 3.5): s.put('drum', k, b0 + beat, 1.0)
        for beat in (1, 3): s.put('drum', sn, b0 + beat, 0.55)
        s.put('drum', tom(70), b0 + 2.5, 0.5)
        if bar in (3, 7):                                                        # 마디 끝 작은북 연타
            for i in range(4): s.put('drum', sn, b0 + 3 + i * 0.25, 0.3 + 0.12 * i)
    melody = [(16, 2, 'D4'), (18, 1, 'F4'), (19, 1, 'A4'), (20, 1.5, 'A4'), (21.5, 0.5, 'G4'), (22, 2, 'F4'),
              (24, 2, 'G4'), (26, 1, 'Bb4'), (27, 1, 'A4'), (28, 3, 'A4'), (31, 1, 'E4'),
              (4, 1.5, 'A3'), (5.5, 0.5, 'D4'), (6, 2, 'D4'), (12, 1.5, 'G3'), (13.5, 0.5, 'C4'), (14, 2, 'E4')]
    for beat, beats, f in melody:
        s.note('lead', N[f], beat, beats, HORN, 1.0, 0.04, 0.15, detune=0.004)
    return s.mix({'bass': 0.55, 'pad': 0.28, 'drum': 0.9, 'lead': 0.4},
                 {'bass': lambda x: lp(x, 520), 'pad': lambda x: lp(x, 1700), 'lead': lambda x: lp(x, 2400)})


def bgm_boss():
    s = Song(126, 8)
    chords = [('D2', ('D3', 'F3', 'A3')), ('D2', ('D3', 'F3', 'A3')), ('Eb2', ('Eb3', 'G3', 'Bb3')), ('D2', ('D3', 'F3', 'A3')),
              ('D2', ('D3', 'F3', 'A3')), ('D2', ('D3', 'F3', 'A3')), ('Eb2', ('Eb3', 'G3', 'Bb3')), ('A2', ('A3', 'C#4', 'G4'))]
    k, sn = kick(), snare()
    for bar, (root, triad) in enumerate(chords):
        b0 = bar * 4
        for e in range(16):                                                      # 16분음표 베이스 오스티나토
            s.note('bass', N[root] * (2 if e % 4 == 2 else 1), b0 + e * 0.25, 0.2, SAW, 1.0 if e % 4 == 0 else 0.65, 0.004, 0.03)
        for stab in (0, 1.5, 3):                                                 # 금관 찌르기
            for f in triad: s.note('brass', N[f], b0 + stab, 0.4, HORN, 0.6, 0.015, 0.12, detune=0.005)
        for f in triad: s.note('pad', N[f] * 2, b0, 4, SAW, 0.4, 0.4, 0.3, detune=0.007)
        for beat in range(4): s.put('drum', k, b0 + beat, 1.0)
        for beat in (1, 3): s.put('drum', sn, b0 + beat, 0.6)
        for beat in (0.5, 2.5): s.put('drum', tom(60), b0 + beat, 0.45)
        if bar == 7:
            for i, f in enumerate((95, 85, 75, 65, 60, 55)): s.put('drum', tom(f), b0 + 2.5 + i * 0.25, 0.7)
        if bar in (2, 3, 6, 7):                                                  # 경보 같은 높은 음형
            for e in range(8): s.note('lead', N['A4'] if e % 2 == 0 else N['Bb4'], b0 + e * 0.5, 0.35, HORN, 0.8, 0.01, 0.05)
    for beat, beats, f in ((0, 3, 'D4'), (3, 1, 'F4'), (4, 4, 'E4'), (16, 3, 'D4'), (19, 1, 'A4'), (20, 4, 'G4')):
        s.note('lead', N[f], beat, beats, HORN, 1.0, 0.03, 0.2, detune=0.004)
    return s.mix({'bass': 0.6, 'brass': 0.3, 'pad': 0.16, 'drum': 1.0, 'lead': 0.33},
                 {'bass': lambda x: lp(x, 600), 'brass': lambda x: lp(x, 2600), 'pad': lambda x: lp(x, 2200), 'lead': lambda x: lp(x, 3000)})


# 이름 → (만드는 함수, 목표 음량: 가장 큰 100ms RMS dB)
CLIPS = {
    "SFX_Torpedo_Launch": (torpedo_launch, -12.0),
    "SFX_Underwater_Blast": (underwater_blast, -9.0),
    "SFX_Sink_Capsize": (sink_capsize, -13.0),
    "SFX_Sink_Groan": (sink_groan, -12.0),
    "SFX_Wreck_Blast": (wreck_blast, -9.5),
    "SFX_EnemyAutocannon_Shot": (enemy_autocannon, -13.0),
    "SFX_Jet_Pass": (jet_pass, -13.0),
    "SFX_Prop_Pass": (prop_pass, -13.0),
    "SFX_Dive_Siren": (dive_siren, -12.0),
    "SFX_Drone_Buzz": (drone_buzz, -13.0),
    "SFX_Shell_Splash": (shell_splash, -14.0),
    "SFX_Depth_Drop": (depth_drop, -13.0),
    "SFX_Chaff_Launch": (chaff_launch, -13.0),
    "SFX_Jam_Pulse": (jam_pulse, -15.0),
    "SFX_Sea_Ambience": (sea_ambience, -18.0),
    "BGM_Battle": (bgm_battle, -13.0),
    "BGM_Boss": (bgm_boss, -12.0),
}

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    only = sys.argv[2:]
    os.makedirs(out, exist_ok=True)
    for name, (make, target) in CLIPS.items():
        if only and name not in only: continue
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
              f"loudest100ms {20 * math.log10(loudest(x)):.1f}dB under200Hz {share * 100:.0f}% seam {abs(x[0] - x[-1]):.3f}", flush=True)
