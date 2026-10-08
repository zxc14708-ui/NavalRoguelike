using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>다중 표적 사격통제. 탐지 정보의 동시 추적과 유도무장 교전 범위를 보강한다.</summary>
    public sealed class FireControlArrayModule : ModuleRuntime
    {
        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.RaiseFireControl(Stats.ExtraTrackedTargets, Stats.GuidedRangeBonus);
    }
}
