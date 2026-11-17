using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 이번 판 결과 분석(2026-10-11 점검 O02). 적에게 <b>실제로 깎은 체력</b>(남은 체력을 넘는 몫은 빼고)과 격침(마지막 한 방)을
    /// 무장별로 모은다 — 명중 수가 아니라 피해량. 무장 이름은 탄의 통계 열쇠(CombatStats.KeyFor: 기관포·76mm·VLS…),
    /// 호위함·항공대 탄은 발사할 때 따로 이름을 붙인다(StatsKey). 열쇠가 없으면 피해 종류(충돌 등)로 센다.
    /// 적 미사일의 끝(요격/명중/소멸)은 CombatStats.Threats가 이미 센다. 판이 바뀌면(RunRestart) 비워진다.
    /// </summary>
    public static class RunReport
    {
        public sealed class Entry
        {
            public string Key;
            public float Damage;
            public int Kills;
        }

        private static readonly Dictionary<string, Entry> s_entries = new();

        public static float TotalDamage { get; private set; }
        public static int TotalKills { get; private set; }

        /// <summary>적이 받은 피해(EnemyController가 부른다). dealt = 실제로 줄어든 체력.</summary>
        public static void RecordDamage(string key, DamageSource source, float dealt, bool kill)
        {
            if (dealt <= 0f && !kill) return;
            key = string.IsNullOrEmpty(key) ? SourceName(source) : key;
            if (!s_entries.TryGetValue(key, out var e)) s_entries[key] = e = new Entry { Key = key };
            e.Damage += Mathf.Max(0f, dealt);
            TotalDamage += Mathf.Max(0f, dealt);
            if (kill) { e.Kills++; TotalKills++; }
        }

        /// <summary>피해가 큰 순.</summary>
        public static List<Entry> Ranked()
        {
            var list = new List<Entry>(s_entries.Values);
            list.Sort((a, b) => b.Damage.CompareTo(a.Damage));
            return list;
        }

        private static string SourceName(DamageSource source) => source switch
        {
            DamageSource.Collision => "충돌·충각",
            DamageSource.Torpedo => "어뢰·폭뢰",
            DamageSource.Missile => "미사일",
            DamageSource.CookOff => "유폭",
            _ => "포격",
        };

        public static void Reset()
        {
            s_entries.Clear();
            TotalDamage = 0f;
            TotalKills = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
