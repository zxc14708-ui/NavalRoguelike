using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>미사일 재장전 구획. 기존 VLS·유도로켓 탄창의 발사·셀 보급 간격을 줄인다.</summary>
    public sealed class MissileLogisticsModule : ModuleRuntime
    {
        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.RaiseMissileLogistics(Stats.MissileReloadReduction);
    }
}
