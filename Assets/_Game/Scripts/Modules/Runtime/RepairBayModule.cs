using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 손상 통제반. 주변(3칸) 손상된 모듈을 서서히 수리하고, 방수·소화로 받는 피해를 줄인다.
    /// 파괴된(HP 0) 모듈은 복구하지 않는다 — 잃은 것은 잃은 것으로 둔다.
    ///
    /// 2026-10-11 손상통제 개편: 선체 자동 회복과 응급 수리[R]는 함교가 맡는다(<see cref="BridgeModule"/>).
    /// 이 블록은 하나마다 함교의 선체 자동 회복 상한(50% → 67 → 83 → 100%)·속도(+50%)와 응급 수리 횟수(+1회)를 늘린다.
    /// 블록이 없어도 응급 수리 1회·상한 50%는 있다 — 블록 하나의 유무로 생존력이 극단적으로 갈리지 않게.
    /// </summary>
    public class RepairBayModule : ModuleRuntime
    {
        [SerializeField] private DamageControlCrew crew = new();

        private bool _fortified;

        /// <summary>손상 통제는 수리뿐 아니라 방수·소화로 피해 자체도 줄인다.</summary>
        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.AddDamageReduction(Stats.DamageReduction);

        protected override void OnInitialized() => OnShipLayoutChanged();
        protected override void OnShipLayoutChanged() => _fortified = ModuleSynergy.Fortified(Grid, Instance);

        protected override void Tick(float dt)
        {
            // 선체는 함교가 고친다(상한·속도는 손상통제반 수로) — 여기서는 주변 모듈만
            crew.Tick(Ship, Grid, Instance, 0f, Stats.ModuleRepairPerSecond * (_fortified ? 1.15f : 1f), dt);
        }
    }
}
