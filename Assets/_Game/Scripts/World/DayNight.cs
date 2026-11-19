using System;
using UnityEngine;

namespace Game.World
{
    /// <summary>하루 중 때(2026-11-19). 조명 기준점이기도 하다.</summary>
    public enum DayPhase { Day, Dusk, Night, Dawn }

    /// <summary>
    /// 낮과 밤(2026-11-19). 스테이지 안에서 구간 번호로 정한다 — 저장 형식은 그대로(스테이지를 다시 하면 낮부터).
    ///   - RoundSet.NightFromRound 앞 구간: 낮(함대함). 그 바로 앞 구간 끝 DuskSeconds초가 해질녘(낮 → 노을 → 밤).
    ///   - NightFromRound ~ 보스 앞 구간: 밤(잠수함·반잠수정 위주 — StageDirector가 밤 적 목록을 쓴다).
    ///   - 마지막(보스) 구간: 새벽 — 처음 DawnSeconds초에 밤 → 동틀 녘.
    ///   - NightFromRound &lt; 0(WWII 시험 전투 등)·훈련: 늘 낮.
    /// 조명(DayNightLighting)은 From → To 기준점을 Blend만큼 섞는다. 항해등·등대 같은 밤 조명은 Darkness01로 켠다.
    /// </summary>
    public static class DayNight
    {
        public const float DuskSeconds = 24f;
        public const float DawnSeconds = 16f;
        /// <summary>항해등·탐조등·등대 빛줄기를 켜는 어둠 정도.</summary>
        public const float LightsOnDarkness = 0.35f;

        public static DayPhase Phase { get; private set; } = DayPhase.Day;
        public static DayPhase From { get; private set; } = DayPhase.Day;
        public static DayPhase To { get; private set; } = DayPhase.Day;
        /// <summary>From → To 섞는 정도(0~1).</summary>
        public static float Blend { get; private set; }
        /// <summary>0 낮 ~ 1 한밤(조명 기준점의 어둠을 섞은 값).</summary>
        public static float Darkness01 { get; private set; }
        /// <summary>밤 구간인가(적 구성 · 조명탄 효과). 해질녘·새벽은 아니다.</summary>
        public static bool IsNight => Phase == DayPhase.Night;
        /// <summary>밤이 가까웠거나 밤인가(해질녘 앞 구간 ~ 보스 앞) — 정비 카드에서 대잠 블록이 더 자주 나온다.</summary>
        public static bool NightAhead { get; private set; }
        /// <summary>NightAhead일 때 대잠 블록(소나 · 폭뢰 · 경어뢰 · 헬기 갑판) 카드 가중치 배율.</summary>
        public const float AswCardWeight = 2.2f;

        /// <summary>항해등·등대 빛을 켤 만큼 어두운가.</summary>
        public static bool LightsOn => Darkness01 >= LightsOnDarkness;

        /// <summary>때가 바뀌었다(낮 → 해질녘 → 밤 → 새벽).</summary>
        public static event Action<DayPhase> Changed;

        /// <summary>검증용: 이 때로 고정한다(null이면 풀기).</summary>
        public static DayPhase? Forced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Phase = From = To = DayPhase.Day; Blend = 0f; Darkness01 = 0f; Changed = null; Forced = null; NightAhead = false; }

        public static float DarknessOf(DayPhase p) => p switch { DayPhase.Dusk => 0.45f, DayPhase.Night => 1f, DayPhase.Dawn => 0.3f, _ => 0f };

        /// <summary>늘 낮으로(훈련 · WWII 시험 전투 · 스테이지 사이).</summary>
        public static void Reset() { NightAhead = false; Set(DayPhase.Day, DayPhase.Day, 0f, DayPhase.Day); }

        /// <summary>
        /// StageDirector가 매 프레임 부른다. round = 지금 구간(0부터), count = 구간 수(마지막이 보스),
        /// nightFrom = 밤이 시작하는 구간(음수면 낮만), elapsed/duration = 지금 구간에서 지난 시간과 길이.
        /// </summary>
        public static void Tick(int round, int count, int nightFrom, float elapsed, float duration)
        {
            if (Forced is DayPhase f) { NightAhead = f == DayPhase.Night || f == DayPhase.Dusk; Set(f, f, 0f, f); return; }
            if (nightFrom < 0 || count < 2 || nightFrom >= count - 1) { Reset(); return; }
            int boss = count - 1;
            NightAhead = round >= nightFrom - 1 && round < boss;
            if (round >= boss)
            {
                float u = Mathf.Clamp01(elapsed / DawnSeconds);
                Set(DayPhase.Night, DayPhase.Dawn, u, DayPhase.Dawn);
            }
            else if (round >= nightFrom) Set(DayPhase.Night, DayPhase.Night, 0f, DayPhase.Night);
            else if (round == nightFrom - 1 && duration - elapsed <= DuskSeconds)
            {
                float u = Mathf.Clamp01(1f - (duration - elapsed) / DuskSeconds);
                if (u < 0.5f) Set(DayPhase.Day, DayPhase.Dusk, u * 2f, DayPhase.Dusk);
                else Set(DayPhase.Dusk, DayPhase.Night, (u - 0.5f) * 2f, DayPhase.Dusk);
            }
            else Set(DayPhase.Day, DayPhase.Day, 0f, DayPhase.Day);
        }

        /// <summary>이 구간이 밤 구간인가(적 구성용 — 보스 구간은 아니다).</summary>
        public static bool IsNightRound(int round, int count, int nightFrom)
            => Forced is DayPhase f ? f == DayPhase.Night : nightFrom >= 0 && round >= nightFrom && round < count - 1;

        private static void Set(DayPhase from, DayPhase to, float blend, DayPhase phase)
        {
            From = from; To = to; Blend = Mathf.Clamp01(blend);
            Darkness01 = Mathf.Lerp(DarknessOf(from), DarknessOf(to), Mathf.SmoothStep(0f, 1f, Blend));
            if (phase == Phase) return;
            Phase = phase;
            Changed?.Invoke(phase);
        }
    }
}
