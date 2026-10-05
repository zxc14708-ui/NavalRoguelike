using System.Collections.Generic;
using UnityEngine;
using Game.Ship;

namespace Game.Modules
{
    /// <summary>
    /// 설치된 블록 하나의 강화 상태(이번 출격 동안만). 재배치로 ModuleInstance가 새로 만들어져도 이 객체를 넘겨받아
    /// 같은 블록으로 이어진다. 좌표가 아니라 InstanceId로 구분한다(재배치 때 다른 블록에 붙지 않게).
    /// 씬이 새로 열리면(새 게임) 모든 인스턴스가 새로 만들어지므로 자동으로 0부터 다시 시작한다.
    /// </summary>
    public sealed class ModuleUpgradeState
    {
        private static int s_nextId = 1;

        public int InstanceId { get; }
        /// <summary>0 = 기본, 1 = 강화 I, 2 = 강화 II.</summary>
        public int Level { get; internal set; }

        public ModuleUpgradeState() => InstanceId = s_nextId++;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIds() => s_nextId = 1;   // 도메인 리로드를 끈 에디터 대비
    }

    /// <summary>
    /// 블록 강화 조회와 최종 스탯 계산의 단일 경로.
    ///
    /// 최종 수치 계산 순서(같은 효과가 두 번 들어가지 않게 이 순서만 쓴다):
    ///   1. ModuleDefinition 기본값
    ///   2. 블록 강화(UpgradeLevel) — 여기(Compute)
    ///   3. 성장 카드(RunUpgrades) — 공격력·연사력·장탄수·사거리·수리 효율. 같은 Compute에서 2 위에 곱한다
    ///   4. 인접 시너지·탄약고 지원 — 각 무기 런타임이 2~3의 결과 위에 배율로 곱한다(MagazineSupport, ModuleSynergy)
    ///   5. 함선 전체 성장(속력·선체·방어력·탐지·스킬 회복) — 각 시스템이 RunUpgrades를 직접 읽는다
    /// 런타임은 ModuleRuntime.Stats(= ModuleInstance.EffectiveStats)만 읽는다. ScriptableObject 원본은 바꾸지 않는다.
    /// </summary>
    public static class ModuleUpgrades
    {
        private static Dictionary<ModuleDefinition, ModuleUpgradeProfile> s_profiles;

        /// <summary>이 블록의 강화 데이터. 없으면 null(강화 불가 블록).</summary>
        public static ModuleUpgradeProfile ProfileFor(ModuleDefinition def)
        {
            if (def == null) return null;
            if (s_profiles == null)
            {
                s_profiles = new Dictionary<ModuleDefinition, ModuleUpgradeProfile>();
                foreach (var p in Resources.LoadAll<ModuleUpgradeProfile>("Upgrades"))
                {
                    if (p == null || p.Module == null) continue;
                    if (s_profiles.ContainsKey(p.Module))
                        Debug.LogWarning($"[Upgrade] {p.Module.DisplayName}의 강화 데이터가 둘 이상입니다. '{p.name}'은 무시합니다.", p);
                    else s_profiles[p.Module] = p;
                }
            }
            return s_profiles.TryGetValue(def, out var profile) ? profile : null;
        }

        public static int MaxLevel(ModuleDefinition def) => ProfileFor(def)?.MaxLevel ?? 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => s_profiles = null;

        // ------------------------------------------------------------ 계산

        /// <summary>기본값에 강화 단계 배율과 성장 카드를 적용한 스탯(1~2단계). 단계별 배율은 기본값 대비라 누적 곱하지 않는다.</summary>
        public static ModuleStats Compute(ModuleDefinition def, int upgradeLevel)
        {
            if (def == null) return default;
            var s = def.Stats;
            var stage = upgradeLevel > 0 ? ProfileFor(def)?.GetStage(upgradeLevel) : null;
            if (stage != null) ApplyStage(ref s, stage);
            ApplyRunUpgrades(def.Type, ref s);
            return s;
        }

        /// <summary>성장 카드: 무장 공격력·연사력·장탄수·사거리, 수리 효율. 원본 데이터는 바꾸지 않고 복사본(s)에만 곱한다.</summary>
        private static void ApplyRunUpgrades(ModuleType type, ref ModuleStats s)
        {
            if (!RunUpgrades.Any) return;
            if (RunUpgrades.IsWeapon(type))
            {
                s.Damage *= RunUpgrades.DamageMultiplier(type);
                s.Range *= RunUpgrades.RangeMultiplier(type);
                s.ReloadTime /= RunUpgrades.FireRateMultiplier(type);
                s.MagazineCapacity = ScaleCapacity(s.MagazineCapacity, RunUpgrades.MagazineMultiplier(type), s.AmmoPerShot);
            }
            if (type == ModuleType.RepairBay)
            {
                s.HullRepairPerSecond *= RunUpgrades.RepairMultiplier;
                s.ModuleRepairPerSecond *= RunUpgrades.RepairMultiplier;
            }
        }

        private static void ApplyStage(ref ModuleStats s, ModuleUpgradeStage stage)
        {

            var m = stage.Modifier;
            s.Damage *= M(m.damageMultiplier);
            s.Range *= M(m.rangeMultiplier);
            s.ReloadTime *= M(m.cooldownMultiplier);
            s.MagazineCapacity = ScaleCapacity(s.MagazineCapacity, M(m.magazineMultiplier), s.AmmoPerShot);
            s.AmmoReloadTime *= M(m.ammoReloadMultiplier);
            s.TurretTurnRate *= M(m.turnSpeedMultiplier);

            float supply = M(m.supplyMultiplier);
            s.ReloadBonus = Mathf.Min(0.9f, s.ReloadBonus * supply);   // 발사 간격이 0이 되지 않게
            s.AmmoCapacityBonus *= supply;
            s.ResupplyBonus *= supply;

            s.CookOffDamage *= M(m.explosionDamageMultiplier);
            s.HullRepairPerSecond *= M(m.repairMultiplier);
            s.ModuleRepairPerSecond *= M(m.repairMultiplier);
            s.DetectionRange *= M(m.detectionMultiplier);
        }

        private static float M(float v) => v > 0f ? v : 1f;

        /// <summary>
        /// 정수 탄창: 반올림(0.5는 올림)하고 최소 1발(또는 한 번 쏘는 양). 한 번에 여러 발 쓰는 무기(CIWS)는 그 배수로 맞춘다.
        /// 0(무한 탄약)은 그대로 둔다.
        /// </summary>
        public static int ScaleCapacity(int capacity, float multiplier, int perShot)
        {
            if (capacity <= 0) return capacity;
            int step = Mathf.Max(1, perShot);
            int scaled = Mathf.FloorToInt(capacity * multiplier / step + 0.5f) * step;
            return Mathf.Max(step, scaled);
        }

        // ------------------------------------------------------------ 대상

        /// <summary>
        /// 이 블록을 한 단계 더 강화할 수 있는가. card = null은 통합 "장비 강화" 카드(2026-10-03) — 강화 단계가 남은 모든 블록,
        /// card가 있으면 같은 정의의 블록만(예전 블록별 카드·검증용).
        /// </summary>
        public static bool CanUpgradeWith(ModuleInstance target, ModuleDefinition card)
            => target != null && !target.IsDestroyed &&
               (card == null ? target.CanUpgrade : target.Definition == card && target.UpgradeLevel < MaxLevel(card));

        /// <summary>이 카드로 강화할 수 있는 설치 블록 목록.</summary>
        public static void GetTargets(ShipGrid grid, ModuleDefinition card, List<ModuleInstance> into)
        {
            into.Clear();
            if (grid == null || card == null) return;
            foreach (var m in grid.Modules)
                if (CanUpgradeWith(m, card)) into.Add(m);
        }

        public static bool HasUpgradeTarget(ShipGrid grid, ModuleDefinition card)
        {
            if (grid == null || card == null || MaxLevel(card) == 0) return false;
            foreach (var m in grid.Modules)
                if (CanUpgradeWith(m, card)) return true;
            return false;
        }

        /// <summary>강화 단계 표시(0 = 없음).</summary>
        public static string Roman(int level) => level switch { 1 => "I", 2 => "II", 3 => "III", _ => "" };
    }
}
