using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>강습 추진 흡기. 최고속력·가속과 고속 항해 중 실탄 피해를 보강한다.</summary>
    public sealed class TurboIntakeModule : ModuleRuntime
    {
        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.RaisePropulsionBonuses(Stats.SpeedBonus, Stats.AccelerationBonus, Stats.MovingGunDamageBonus);
    }
}
