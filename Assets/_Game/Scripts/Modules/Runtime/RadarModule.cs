using UnityEngine;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 함선의 탐지거리와 동시 추적 수를 늘린다.
    /// 파괴되면 ShipSystems가 재계산되어 탐지거리가 기본값으로 돌아간다.
    /// </summary>
    public class RadarModule : ModuleRuntime, IRadarSource
    {
        [Header("Visual")]
        [SerializeField] private Transform antenna;
        [SerializeField] private float antennaRpm = 30f;

        private readonly RadarAntennaTracker _radar = new();

        public bool RadarOperational => this != null && Instance != null && Instance.IsOperational && isActiveAndEnabled;
        public float RadarRange => Stats.DetectionRange;
        public float AntennaRpm => antennaRpm;
        public float AntennaBearing => _radar.Bearing(antenna, Ship != null ? Ship.transform : transform);

        public override void ContributeToShipSystems(ShipSystems systems)
        {
            systems.RaiseDetectionRange(Stats.DetectionRange);
            systems.AddTrackedTargets(Stats.ExtraTrackedTargets);
        }

        protected override void Tick(float dt)
        {
            // 회전하는 안테나는 레이더가 살아있음을 알리는 시각 신호다
            // 아트 모델 피벗은 Blender 축이라 로컬 축으로 돌리면 안테나가 굴러간다. 월드 위축으로 돌린다.
            if (antenna != null) antenna.Rotate(Vector3.up, antennaRpm * 6f * dt, Space.World);
            _radar.Advance(antennaRpm, dt);
        }

        public override void OnModuleDestroyed()
        {
            if (antenna != null) antenna.gameObject.SetActive(false);
        }
    }
}
