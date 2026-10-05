using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 소나(1x1 함내). 잠수함 탐지 반경을 만든다.
    /// 소나는 잠수함의 수중 위치를 확정해 대잠 무기와 헬기에 공유한다.
    /// 자리에 따라 형태가 바뀐다(ModuleVariants): 선수 소나(앞쪽 ±70°) · 예인 소나(사방, 1.5배, 빠르면 절반) · 함내 소나(사방, 짧음).
    /// </summary>
    public class SonarModule : ModuleRuntime
    {
        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.AddSonar(Instance != null ? Instance.Variant : ModuleVariant.None, Stats.DetectionRange);
    }
}
