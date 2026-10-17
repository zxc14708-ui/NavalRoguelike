using System.Collections.Generic;
using UnityEngine;

namespace Game.WW2
{
    /// <summary>
    /// 공격 단계가 있는 적 항공기(뇌격기·급강하폭격기, 패치 B). 공격은 네 단계로 나뉜다:
    /// 접근 → <b>공격 방향 확정(예고 표시)</b> → 투하 → 이탈. 확정된 뒤에는 방향을 바꾸지 않으므로 예고를 보고 피할 수 있고,
    /// 투하 전에 격추하면 공격이 취소된다(이미 투하한 어뢰·폭탄은 그대로 간다).
    /// </summary>
    public interface IAirStrike
    {
        string StrikeName { get; }
        Transform Transform { get; }
        /// <summary>방향이 확정돼 예고를 띄운 상태(아직 투하 전).</summary>
        bool IsCommitted { get; }
        bool HasReleased { get; }
        /// <summary>투하까지 남은 초(확정 전이면 음수).</summary>
        float SecondsToRelease { get; }
        /// <summary>어뢰가 떨어질 지점 또는 폭탄이 떨어질 지점(수면).</summary>
        Vector3 StrikePoint { get; }
        /// <summary>
        /// 위험한 곳(수면): 급강하는 착탄점, 뇌격은 어뢰가 기함 예상 침로와 만나는 곳. 화면 표식·방향 안내가 가리킨다.
        /// </summary>
        Vector3 DangerPoint { get; }
    }

    /// <summary>지금 하늘에 있는 공격기 목록(HUD 경보·양용포·대공포 우선순위·검증이 읽는다).</summary>
    public static class AirStrikes
    {
        private static readonly List<IAirStrike> s_active = new();
        public static IReadOnlyList<IAirStrike> Active => s_active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();

        public static void Register(IAirStrike s) { if (s != null && !s_active.Contains(s)) s_active.Add(s); }
        public static void Unregister(IAirStrike s) => s_active.Remove(s);

        /// <summary>방향을 확정한 공격기인가(무장 우선순위).</summary>
        public static bool IsCommitted(object target) => target is IAirStrike s && s.IsCommitted && !s.HasReleased;
    }
}
