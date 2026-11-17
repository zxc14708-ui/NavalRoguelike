using System;
using Game.Core;

namespace Game.Modules
{
    /// <summary>
    /// 해군 기술연구소 연구(2026-10-28 군항 건물 2단계). 작전 포인트로 기본 능력을 조금씩, 상한까지 올린다 — 판 안 성장 카드와 같은 능력에
    /// "기본 보너스"로 더해진다(RunUpgrades.Get). 원본 데이터(ScriptableObject)는 건드리지 않는다.
    ///   - 능력 연구 9줄: 단계 5, 비용 2 · 3 · 4 · 5 · 6(한 줄 20), 단계당 작은 % — 성장 카드 한 장(일반 +8~15%)보다 작게, 다 올려도 +7.5~20%
    ///   - 작전 기획: 정비 카드 다시 뽑기 +1(단계 2, 비용 5 · 8)
    /// 규칙 집합마다 따로(프로필이 현대 / WWII 따로). 되돌리면 쓴 포인트를 모두 돌려받는다.
    /// 자동 검증(-combatVerify)에서는 수치가 흔들리지 않게 꺼 둔다(검사가 DevForce로 켤 때만).
    /// </summary>
    public static class MetaResearch
    {
        public sealed class Node
        {
            public string Id, Name, Scope;
            public RunStat? Stat;       // 없으면 특수(작전 기획)
            public float PerLevel;
            public int[] Costs;
            public int MaxLevel => Costs.Length;

            /// <summary>단계 n의 효과 문구("+6%" · "다시 뽑기 +1").</summary>
            public string Effect(int level) => Stat.HasValue ? $"+{PerLevel * level * 100f:0.#}%" : $"+{level}회";
        }

        private static readonly int[] StatCosts = { 2, 3, 4, 5, 6 };

        public static readonly Node[] Nodes =
        {
            new() { Id = "gun", Name = "함포 연구", Scope = "함포계(기관포 · 40mm · 76mm) 공격력", Stat = RunStat.GunDamage, PerLevel = 0.02f, Costs = StatCosts },
            new() { Id = "missile", Name = "미사일 연구", Scope = "미사일계(VLS · 유도로켓) 공격력", Stat = RunStat.MissileDamage, PerLevel = 0.02f, Costs = StatCosts },
            new() { Id = "air_defense", Name = "방공 연구", Scope = "방공계(CIWS · 함대공) 연사력", Stat = RunStat.AirDefenseRate, PerLevel = 0.02f, Costs = StatCosts },
            new() { Id = "fire_control", Name = "사격 통제", Scope = "모든 무장 사거리", Stat = RunStat.Range, PerLevel = 0.015f, Costs = StatCosts },
            new() { Id = "hull", Name = "선체 구조", Scope = "선체 최대치", Stat = RunStat.HullMax, PerLevel = 0.03f, Costs = StatCosts },
            new() { Id = "damage_control", Name = "손상 통제", Scope = "수리 효율", Stat = RunStat.Repair, PerLevel = 0.04f, Costs = StatCosts },
            new() { Id = "engine", Name = "기관 개량", Scope = "최고 속력", Stat = RunStat.Speed, PerLevel = 0.02f, Costs = StatCosts },
            new() { Id = "sensor", Name = "탐지 체계", Scope = "탐지 거리(레이더 · 소나)", Stat = RunStat.Detection, PerLevel = 0.03f, Costs = StatCosts },
            new() { Id = "tactics", Name = "전술 훈련", Scope = "능동 스킬 회복 속도", Stat = RunStat.SkillRate, PerLevel = 0.03f, Costs = StatCosts },
            new() { Id = "planning", Name = "작전 기획", Scope = "정비 카드 다시 뽑기(한 판)", Stat = null, PerLevel = 1f, Costs = new[] { 5, 8 } },
        };

        /// <summary>검사가 연구 효과를 켜서 볼 때만(평소 자동 검증에서는 꺼 둔다).</summary>
        public static bool DevForce;
        private static readonly bool s_verify = Array.IndexOf(Environment.GetCommandLineArgs(), "-combatVerify") >= 0;
        public static bool Active => !s_verify || DevForce;

        private static readonly float[] s_bonus = new float[Enum.GetValues(typeof(RunStat)).Length];
        private static int s_rerolls;
        private static bool s_dirty = true;

        /// <summary>연구가 바뀌었을 때(군항 연구소 · 되돌리기 · 규칙 집합 전환).</summary>
        public static event Action Changed;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_dirty = true; Changed = null; DevForce = false; }

        public static void Invalidate() => s_dirty = true;

        public static Node Find(string id) => Array.Find(Nodes, n => n.Id == id);

        public static int Level(string id)
        {
            var e = PlayerProfile.Current.research.Find(r => r.id == id);
            return e != null ? e.level : 0;
        }

        /// <summary>다음 단계 비용(이미 끝까지면 -1).</summary>
        public static int NextCost(Node n)
        {
            int l = Level(n.Id);
            return l >= n.MaxLevel ? -1 : n.Costs[l];
        }

        /// <summary>한 단계 연구한다(포인트가 모자라거나 끝까지면 false).</summary>
        public static bool TryResearch(string id)
        {
            var n = Find(id);
            if (n == null) return false;
            int cost = NextCost(n);
            if (cost < 0 || !PlayerProfile.SpendPoints(cost)) return false;
            var list = PlayerProfile.Current.research;
            var e = list.Find(r => r.id == id);
            if (e == null) { e = new PlayerProfile.ResearchLevel { id = id }; list.Add(e); }
            e.level++;
            PlayerProfile.Save();
            Raise();
            return true;
        }

        /// <summary>모든 연구를 되돌리고 쓴 포인트를 모두 돌려준다. 돌려준 포인트.</summary>
        public static int ResetAll()
        {
            int refund = Spent();
            PlayerProfile.Current.research.Clear();
            PlayerProfile.RefundPoints(refund);   // 저장 포함
            Raise();
            return refund;
        }

        /// <summary>지금까지 연구에 쓴 포인트.</summary>
        public static int Spent()
        {
            int sum = 0;
            foreach (var n in Nodes)
            {
                int l = Math.Min(Level(n.Id), n.MaxLevel);
                for (int i = 0; i < l; i++) sum += n.Costs[i];
            }
            return sum;
        }

        private static void Raise()
        {
            s_dirty = true;
            Changed?.Invoke();
        }

        /// <summary>이 능력의 연구 보너스(0.06 = +6%). 꺼져 있으면 0.</summary>
        public static float Bonus(RunStat stat)
        {
            if (!Active) return 0f;
            Ensure();
            return s_bonus[(int)stat];
        }

        /// <summary>작전 기획: 한 판의 다시 뽑기 추가 횟수.</summary>
        public static int ExtraRerolls
        {
            get
            {
                if (!Active) return 0;
                Ensure();
                return s_rerolls;
            }
        }

        private static void Ensure()
        {
            if (!s_dirty) return;
            s_dirty = false;
            Array.Clear(s_bonus, 0, s_bonus.Length);
            s_rerolls = 0;
            foreach (var n in Nodes)
            {
                int l = Math.Min(Level(n.Id), n.MaxLevel);
                if (l <= 0) continue;
                if (n.Stat.HasValue) s_bonus[(int)n.Stat.Value] += n.PerLevel * l;
                else s_rerolls += l;
            }
        }
    }
}
