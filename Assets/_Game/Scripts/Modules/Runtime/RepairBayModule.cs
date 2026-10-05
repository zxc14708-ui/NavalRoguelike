using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 손상 통제반. 선체와 손상된 모듈을 서서히 수리한다.
    /// 파괴된(HP 0) 모듈은 복구하지 않는다 — 잃은 것은 잃은 것으로 둔다.
    /// 즉시 회복이 아니라 지속 회복이라, 맞으면서 버티는 것보다 피하는 쪽이 여전히 유리하다.
    ///
    /// 위기 때 한 번 크게 고치는 응급 수리 스킬[R]을 제공한다.
    /// </summary>
    public class RepairBayModule : ModuleRuntime, IRepairBurstSource
    {
        [SerializeField] private DamageControlCrew crew = new();

        [Header("Emergency Repair [R]")]
        [SerializeField] private float burstHull = 30f;
        [Tooltip("모든 (파괴되지 않은) 모듈을 최대 내구의 이 비율만큼 수리")]
        [SerializeField, Range(0f, 1f)] private float burstModuleRatio = 0.4f;
        [SerializeField] private float burstCooldown = 35f;
        [SerializeField] private GameObject burstEffect;

        private float _burstCooldown;
        private bool _fortified;

        public bool BurstReady => _burstCooldown <= 0f && Ship != null && Instance != null && Instance.IsOperational;
        public float BurstReadiness01 => burstCooldown > 0f ? 1f - Mathf.Clamp01(_burstCooldown / burstCooldown) : 1f;

        public void Burst()
        {
            if (!BurstReady) return;

            Ship.RepairHull(burstHull);
            if (Grid != null)
                foreach (var m in Grid.Modules)
                    if (m != null && !m.IsDestroyed) m.Repair(m.MaxHp * burstModuleRatio);

            PooledEffect.Spawn(burstEffect, Ship.transform.position);
            AudioManager.Play(Game.Data.SfxId.SonarPing, transform.position, 0.6f, 0.8f);
            _burstCooldown = burstCooldown;
        }

        /// <summary>손상 통제는 수리뿐 아니라 방수·소화로 피해 자체도 줄인다.</summary>
        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.AddDamageReduction(Stats.DamageReduction);

        protected override void OnInitialized() => OnShipLayoutChanged();
        protected override void OnShipLayoutChanged() => _fortified = ModuleSynergy.Fortified(Grid, Instance);

        protected override void Tick(float dt)
        {
            if (_burstCooldown > 0f) _burstCooldown -= dt * RunUpgrades.SkillRate;   // 성장 카드: 스킬 회복 속도
            crew.Tick(Ship, Grid, Instance, Stats.HullRepairPerSecond,
                Stats.ModuleRepairPerSecond * (_fortified ? 1.15f : 1f), dt);
        }
    }
}
