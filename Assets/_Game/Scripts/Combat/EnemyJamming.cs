using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 적 전자전 방해(2026-10-05, 스테이지 3 전자전 코르벳 — EwJammer).
    /// 방해 중인 적이 하나라도 있으면 플레이어 레이더 탐지 거리 × <see cref="JammedRadarMultiplier"/>(TargetingSystem).
    /// 육안 거리(visualRange)는 그대로라 가까운 적은 계속 보인다. 여러 척이 겹쳐도 더 줄지 않는다.
    /// </summary>
    public static class EnemyJamming
    {
        public const float JammedRadarMultiplier = 0.75f;

        private static readonly HashSet<Object> s_jammers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_jammers.Clear();

        public static int ActiveCount
        {
            get
            {
                s_jammers.RemoveWhere(j => j == null);
                return s_jammers.Count;
            }
        }

        public static bool IsJammed => ActiveCount > 0;
        public static float RadarMultiplier => IsJammed ? JammedRadarMultiplier : 1f;

        /// <summary>방해를 켜고 끈다. 상태가 실제로 바뀌었으면 true.</summary>
        public static bool Set(Object source, bool on) => on ? s_jammers.Add(source) : s_jammers.Remove(source);
    }
}
