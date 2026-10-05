using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 개발용 전투 기록. "왜 방어에 실패했는가"를 따라가기 위한 것으로, 일반 UI에는 나오지 않는다.
    ///   예) [적 미사일] #152 발사 → [기만체] #152 유인 실패(90%) → [CIWS-1] #152 대기: 채널 사용 중(#151 교전) → [피격] #152 칸 (7,1) 기관포
    /// 에디터·개발 빌드에서만 컴파일된다(릴리스 빌드에서는 호출과 인자 계산이 모두 사라진다).
    /// 최근 기록은 F3 오버레이에 보이고, F4로 콘솔 출력을 켜고 끈다(자동 검증은 파일로 남긴다).
    /// </summary>
    public static class CombatLog
    {
        public readonly struct Entry
        {
            public readonly float Time;
            public readonly string Category;
            public readonly string Message;

            public Entry(float time, string category, string message)
            {
                Time = time;
                Category = category;
                Message = message;
            }

            public override string ToString() => $"{Time,7:0.00}  [{Category}] {Message}";
        }

        private const int Capacity = 400;
        private static readonly Queue<Entry> s_entries = new(Capacity);
        private static int s_nextMissileId;

        /// <summary>콘솔에도 출력할지(개발 오버레이 F4).</summary>
        public static bool ToConsole { get; set; }

        public static IReadOnlyCollection<Entry> Entries => s_entries;
        public static int Count => s_entries.Count;

        /// <summary>미사일마다 기록에 쓸 번호.</summary>
        public static int NextMissileId() => ++s_nextMissileId;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Add(string category, string message)
        {
            if (s_entries.Count >= Capacity) s_entries.Dequeue();
            var e = new Entry(UnityEngine.Time.time, category, message);
            s_entries.Enqueue(e);
            if (ToConsole) UnityEngine.Debug.Log($"[Combat] {e}");
        }

        /// <summary>가장 최근 count개(오래된 것부터).</summary>
        public static void CopyRecent(List<Entry> into, int count)
        {
            into.Clear();
            int skip = Mathf.Max(0, s_entries.Count - count);
            foreach (var e in s_entries)
            {
                if (skip-- > 0) continue;
                into.Add(e);
            }
        }

        public static void Clear() => s_entries.Clear();

        /// <summary>기록에 쓸 표적 이름: 미사일은 #번호, 적은 종류 이름.</summary>
        public static string Describe(ITargetable t) => t switch
        {
            null => "(없음)",
            Missile m => m.LogName,
            Game.Enemies.EnemyController e when e.Definition != null => e.Definition.DisplayName,
            _ => t.Kind.ToString(),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_entries.Clear();
            s_nextMissileId = 0;
            ToConsole = false;
        }
    }
}
