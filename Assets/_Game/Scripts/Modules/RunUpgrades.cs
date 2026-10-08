using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Modules
{
    /// <summary>성장 카드의 희귀도. 같은 카드라도 등급이 높을수록 효과가 크다(공격력 +5% / +10% / +20%).</summary>
    public enum CardTier { Common, Rare, Epic }

    /// <summary>
    /// 성장 카드가 올리는 능력. 값은 모두 "누적 보너스 비율"이다(0.15 = +15%).
    /// 계열 능력(GunDamage 등)은 해당 무장에만, 나머지 공통 능력은 전 무장·함선 전체에 적용된다.
    /// </summary>
    public enum RunStat
    {
        Damage,          // 전 무장 공격력
        FireRate,        // 전 무장 연사력(재장전 시간 ÷ (1+보너스))
        Magazine,        // 전 무장 장탄수
        Range,           // 전 무장 사거리
        SkillRate,       // 능동 스킬 회복 속도(기만체·재밍·연막·응급 수리·전속)
        Speed,           // 최고 속력
        HullMax,         // 선체 최대치(올라간 만큼 선체도 회복)
        Repair,          // 손상 통제반 수리 효율
        Defense,         // 방어력(받는 피해 ÷ (1+보너스))
        Detection,       // 탐지 거리(레이더·소나)
        GunDamage,       // 함포계(기관포·76mm) 공격력
        MissileDamage,   // 미사일계(VLS·유도로켓) 공격력
        AirDefenseRate,  // 방공계(CIWS·함대공) 연사력
    }

    /// <summary>성장 카드 한 종류의 정의(이름·대상·등급별 수치·추첨 가중치).</summary>
    public sealed class RunStatDefinition
    {
        public RunStat Stat;
        public string Name;          // 카드 이름: "공격력"
        public string Scope;         // 대상 설명: "모든 무장"
        public string Effect;        // 효과 설명 한 줄: "모든 무장의 한 발 피해가 늘어난다"
        public float[] Values;       // 등급별 보너스(Common, Rare, Epic)
        public float Weight;         // 추첨 가중치
        public bool InverseDisplay;  // 방어력처럼 "받는 피해 −x%"로 보여 줄 능력
        public ModuleType[] RequiresAny;  // 이 중 하나라도 설치돼 있어야 후보(계열·수리 카드). null이면 항상

        public float ValueOf(CardTier tier) => Values[(int)tier];
        public string Percent(float v) => $"{v * 100f:0.#}%";
    }

    /// <summary>
    /// 이번 출격 동안 쌓인 성장 카드 보너스(함선 전체). ScriptableObject·모듈 원본 수치는 건드리지 않고,
    /// 최종 수치 계산(ModuleUpgrades.Compute 등)이 이 값을 읽어 곱한다.
    ///
    /// 같은 능력의 카드는 합산해 쌓인다. 중첩 상한은 두지 않는다(밸런스 확인 뒤 정한다).
    /// 대신 연사력·스킬 회복·방어력은 "속도/나눗셈" 형태라 100%를 넘어도 0이 되거나 무한해지지 않는다.
    /// 새 게임(씬 로드·플레이 시작)마다 0으로 돌아간다.
    /// </summary>
    public static class RunUpgrades
    {
        private static readonly float[] Bonus = new float[Enum.GetValues(typeof(RunStat)).Length];
        private static readonly List<(RunStat stat, CardTier tier)> History = new();

        /// <summary>보너스가 바뀌었다. 무장·함선이 스탯을 다시 계산한다.</summary>
        public static event Action Changed;

        // ------------------------------------------------------------ 카탈로그

        private static readonly ModuleType[] GunTypes = { ModuleType.Autocannon, ModuleType.NavalGun };
        private static readonly ModuleType[] MissileTypes = { ModuleType.Vls, ModuleType.GuidedRocket };
        private static readonly ModuleType[] AirDefenseTypes = { ModuleType.Ciws, ModuleType.SamLauncher };

        /// <summary>등급별 수치는 (일반, 희귀, 에픽). 공격력 +5/10/20%를 기준으로 능력마다 체감에 맞춰 잡았다.</summary>
        public static readonly IReadOnlyList<RunStatDefinition> Catalog = new List<RunStatDefinition>
        {
            new() { Stat = RunStat.Damage, Name = "공격력", Scope = "모든 무장", Effect = "모든 무장의 한 발 피해가 늘어난다", Values = new[] { 0.05f, 0.10f, 0.20f }, Weight = 1.0f },
            new() { Stat = RunStat.FireRate, Name = "연사력", Scope = "모든 무장", Effect = "모든 무장의 발사 간격이 짧아진다", Values = new[] { 0.05f, 0.10f, 0.20f }, Weight = 1.0f },
            new() { Stat = RunStat.Magazine, Name = "장탄수", Scope = "탄약을 쓰는 무장", Effect = "탄창·셀 수가 늘어난다", Values = new[] { 0.08f, 0.16f, 0.32f }, Weight = 0.8f },
            new() { Stat = RunStat.Range, Name = "사거리", Scope = "모든 무장", Effect = "무장의 최대 교전 거리가 늘어난다", Values = new[] { 0.04f, 0.08f, 0.16f }, Weight = 0.7f },
            new() { Stat = RunStat.SkillRate, Name = "스킬 회복 속도", Scope = "기만체·재밍·연막·응급 수리·전속", Effect = "능동 스킬이 더 빨리 다시 준비된다", Values = new[] { 0.08f, 0.16f, 0.32f }, Weight = 0.8f },
            new() { Stat = RunStat.Speed, Name = "최고 속력", Scope = "함선", Effect = "최고 속력이 오른다", Values = new[] { 0.04f, 0.08f, 0.16f }, Weight = 0.7f },
            new() { Stat = RunStat.HullMax, Name = "선체 강화", Scope = "함선", Effect = "선체 최대치가 늘고 늘어난 만큼 바로 회복된다", Values = new[] { 0.06f, 0.12f, 0.24f }, Weight = 1.0f },
            new() { Stat = RunStat.Defense, Name = "방어력", Scope = "함선", Effect = "받는 피해가 줄어든다", Values = new[] { 0.03f, 0.06f, 0.12f }, Weight = 0.9f, InverseDisplay = true },
            new() { Stat = RunStat.Repair, Name = "수리 효율", Scope = "손상 통제반", Effect = "선체·모듈 수리 속도가 빨라진다", Values = new[] { 0.10f, 0.20f, 0.40f }, Weight = 0.7f, RequiresAny = new[] { ModuleType.RepairBay } },
            new() { Stat = RunStat.Detection, Name = "탐지 거리", Scope = "레이더·소나", Effect = "탐지 거리가 늘어난다", Values = new[] { 0.08f, 0.16f, 0.32f }, Weight = 0.6f },
            new() { Stat = RunStat.GunDamage, Name = "함포계 공격력", Scope = "기관포 · 76mm 함포", Effect = "포탄 무장의 한 발 피해가 크게 늘어난다", Values = new[] { 0.08f, 0.16f, 0.32f }, Weight = 0.8f, RequiresAny = GunTypes },
            new() { Stat = RunStat.MissileDamage, Name = "미사일계 공격력", Scope = "VLS · 유도로켓", Effect = "미사일 무장의 한 발 피해가 크게 늘어난다", Values = new[] { 0.08f, 0.16f, 0.32f }, Weight = 0.8f, RequiresAny = MissileTypes },
            new() { Stat = RunStat.AirDefenseRate, Name = "방공계 연사력", Scope = "CIWS · 함대공", Effect = "방공 무장의 발사 간격이 짧아진다", Values = new[] { 0.08f, 0.16f, 0.32f }, Weight = 0.7f, RequiresAny = AirDefenseTypes },
        };

        public static RunStatDefinition Definition(RunStat stat)
        {
            foreach (var d in Catalog) if (d.Stat == stat) return d;
            return null;
        }

        public static string TierName(CardTier t) => t switch { CardTier.Rare => "희귀", CardTier.Epic => "에픽", _ => "일반" };

        public static Color TierColor(CardTier t) => t switch
        {
            CardTier.Rare => new Color(0.45f, 0.65f, 1f),
            CardTier.Epic => new Color(1f, 0.72f, 0.2f),
            _ => new Color(0.66f, 0.74f, 0.8f),
        };

        // ------------------------------------------------------------ 누적 값

        /// <summary>누적 보너스 비율(0.15 = +15%).</summary>
        public static float Get(RunStat stat) => Bonus[(int)stat];

        public static bool Any
        {
            get { foreach (var b in Bonus) if (b > 0f) return true; return false; }
        }

        /// <summary>쌓은 카드 기록(희귀도와 함께).</summary>
        public static IReadOnlyList<(RunStat stat, CardTier tier)> Cards => History;

        public static void Add(RunStat stat, CardTier tier)
        {
            var def = Definition(stat);
            if (def == null) return;
            Bonus[(int)stat] += def.ValueOf(tier);
            History.Add((stat, tier));
            Changed?.Invoke();
        }

        /// <summary>검증·초기화용.</summary>
        public static void Reset()
        {
            Array.Clear(Bonus, 0, Bonus.Length);
            History.Clear();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Array.Clear(Bonus, 0, Bonus.Length);
            History.Clear();
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Hook()
        {
            // 새 게임: 씬이 처음부터 다시 열리면(Single) 성장도 0부터
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Reset();
        }

        // ------------------------------------------------------------ 무장 분류

        /// <summary>능력 카드·최종 수치 계산이 보는 "무장" 분류. 능동 스킬 장비(기만체·재밍)는 무장이 아니다.</summary>
        public static bool IsWeapon(ModuleType t) => t is ModuleType.Autocannon or ModuleType.NavalGun or ModuleType.Vls
            or ModuleType.GuidedRocket or ModuleType.Ciws or ModuleType.SamLauncher or ModuleType.AswLauncher
            or ModuleType.TorpedoTube or ModuleType.Howitzer or ModuleType.RamBow or ModuleType.MineRail;

        /// <summary>이 종류의 무장이 받는 공격력 배율.</summary>
        public static float DamageMultiplier(ModuleType t)
        {
            if (!IsWeapon(t)) return 1f;
            float b = Get(RunStat.Damage);
            if (t is ModuleType.Autocannon or ModuleType.NavalGun) b += Get(RunStat.GunDamage);
            else if (t is ModuleType.Vls or ModuleType.GuidedRocket) b += Get(RunStat.MissileDamage);
            return 1f + b;
        }

        /// <summary>이 종류의 무장이 받는 연사 배율(발사 간격에는 역수를 곱한다).</summary>
        public static float FireRateMultiplier(ModuleType t)
        {
            if (!IsWeapon(t)) return 1f;
            float b = Get(RunStat.FireRate);
            if (t is ModuleType.Ciws or ModuleType.SamLauncher) b += Get(RunStat.AirDefenseRate);
            return 1f + b;
        }

        public static float MagazineMultiplier(ModuleType t) => IsWeapon(t) ? 1f + Get(RunStat.Magazine) : 1f;
        public static float RangeMultiplier(ModuleType t) => IsWeapon(t) ? 1f + Get(RunStat.Range) : 1f;
        public static float RepairMultiplier => 1f + Get(RunStat.Repair);
        public static float DetectionMultiplier => 1f + Get(RunStat.Detection);
        public static float SpeedMultiplier => 1f + Get(RunStat.Speed);
        public static float HullMultiplier => 1f + Get(RunStat.HullMax);
        /// <summary>받는 피해에 곱한다(방어력 +25% → 0.8).</summary>
        public static float IncomingDamageMultiplier => 1f / (1f + Get(RunStat.Defense));
        /// <summary>능동 스킬 쿨다운 타이머가 흐르는 속도(+25% → 1.25배).</summary>
        public static float SkillRate => 1f + Get(RunStat.SkillRate);

        /// <summary>카드 표시용: 이 능력의 지금 누적 효과 문구("+25%" 또는 "받는 피해 −20%").</summary>
        public static string Describe(RunStat stat, float bonus, bool shortForm = false)
        {
            var def = Definition(stat);
            if (def != null && def.InverseDisplay)
                return $"{(shortForm ? "" : "받는 피해 ")}−{(1f - 1f / (1f + bonus)) * 100f:0.#}%";
            return $"+{bonus * 100f:0.#}%";
        }

        // ------------------------------------------------------------ 추첨 확률

        /// <summary>진행(레벨)에 따른 등급 가중치. 레벨 1에서는 일반이 대부분이고, 레벨이 오를수록 희귀·에픽 확률이 올라간다.</summary>
        public static (float common, float rare, float epic) TierWeights(int level)
        {
            float p = Mathf.Max(0, level - 1);
            float epic = Mathf.Min(30f, 4f + 1.1f * p);
            float rare = Mathf.Min(45f, 24f + 1.2f * p);
            float common = Mathf.Max(20f, 100f - rare - epic);
            return (common, rare, epic);
        }

        public static CardTier RollTier(int level, System.Random rng = null)
        {
            var (c, r, e) = TierWeights(level);
            float roll = (rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value) * (c + r + e);
            if ((roll -= c) <= 0f) return CardTier.Common;
            if ((roll -= r) <= 0f) return CardTier.Rare;
            return CardTier.Epic;
        }

        /// <summary>무장 강화 카드 추첨 가중 배율: 진행할수록 더 자주 나온다(레벨 1 = ×1, 이후 레벨당 +12%, 최대 ×3).</summary>
        public static float UpgradeCardWeight(int level) => Mathf.Min(3f, 1f + 0.12f * Mathf.Max(0, level - 1));
    }
}
