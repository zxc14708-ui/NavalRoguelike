using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>기함 통신 중계. 현행 자율 호위함 능력의 재사용 속도를 높인다.</summary>
    public sealed class FleetRelayModule : ModuleRuntime
    {
        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.RaiseEscortFireRateBonus(Stats.EscortFireRateBonus);
    }
}
