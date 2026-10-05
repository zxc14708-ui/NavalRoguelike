using UnityEngine;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 함교. 지휘소이자 기관실이고, 기만체 발사기·손상 통제반·항해 레이더를 하나씩 품고 있다.
    ///
    /// 어떤 카드를 뽑든 모든 배가 미사일 기만, 최소한의 수리, 미사일정을 볼 수 있는 탐지거리를 갖게 해
    /// 초반 미사일정 앞에서 판이 운으로 결정되지 않도록 한다.
    /// 전용 모듈보다 모두 약하다(수치는 데이터에서). 함교가 부서지면 셋 다 잃는다.
    ///
    /// 기관실이므로 전속 스킬[Shift]을 제공한다. 내장 발사기에는 연막탄이 없다(전용 기만체 발사기만).
    /// </summary>
    public class BridgeModule : DecoyLauncherModule, IFlankSource, IRadarSource
    {
        [SerializeField] private DamageControlCrew crew = new();

        [Header("Radar")]
        [Tooltip("함교 마스트의 회전 안테나. 비어 있어도 탐지 기능은 동작한다.")]
        [SerializeField] private Transform radarAntenna;
        [SerializeField] private float antennaRpm = 20f;

        [Header("Flank Speed [Shift]")]
        [SerializeField] private float flankDuration = 5f;
        [SerializeField] private float flankCooldown = 22f;
        [Tooltip("전속 동안 최고속력 배수. 가속·선회도 함께 오른다.")]
        [SerializeField] private float flankSpeedMultiplier = 1.6f;

        private float _flankCooldown;
        private readonly RadarAntennaTracker _radar = new();

        // --- 항해 레이더(레이더 화면이 이 안테나에 맞춰 스윕한다)
        public bool RadarOperational => this != null && Instance != null && Instance.IsOperational && isActiveAndEnabled;
        public float RadarRange => Stats.DetectionRange;
        public float AntennaRpm => antennaRpm;
        public float AntennaBearing => _radar.Bearing(radarAntenna, Ship != null ? Ship.transform : transform);

        protected override bool HasSmokeRounds => false;

        public bool FlankReady => _flankCooldown <= 0f && Ship != null && Instance != null && Instance.IsOperational;
        public float FlankReadiness01 => flankCooldown > 0f ? 1f - Mathf.Clamp01(_flankCooldown / flankCooldown) : 1f;

        public void Flank()
        {
            if (!FlankReady) return;
            Ship.BeginFlank(flankSpeedMultiplier, flankDuration);
            _flankCooldown = flankCooldown;
        }

        public override void ContributeToShipSystems(ShipSystems systems)
        {
            systems.AddDamageReduction(Stats.DamageReduction);
            systems.RaiseDetectionRange(Stats.DetectionRange);
            systems.AddTrackedTargets(Stats.ExtraTrackedTargets);
        }

        protected override void Tick(float dt)
        {
            base.Tick(dt);
            if (_flankCooldown > 0f) _flankCooldown -= dt * RunUpgrades.SkillRate;   // 성장 카드: 스킬 회복 속도
            crew.Tick(Ship, Grid, Instance, Stats.HullRepairPerSecond, Stats.ModuleRepairPerSecond, dt);

            if (radarAntenna != null) radarAntenna.Rotate(Vector3.up, antennaRpm * 6f * dt, Space.World);
            _radar.Advance(antennaRpm, dt);
        }

        public override void OnModuleDestroyed()
        {
            // 파괴되면 Tick이 멈춰 기만체도 수리도 전속도 멈춘다.
            // TODO: 파괴 시 전체 모듈 효율 감소
        }
    }
}
