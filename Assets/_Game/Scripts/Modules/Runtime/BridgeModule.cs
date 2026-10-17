using UnityEngine;
using Game.Core;
using Game.Combat;
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
    /// 지붕에 약한 원격 기관총(<see cref="BridgeMachineGun"/>, 2026-10-11)을 얹어, 무기 블록이 없어도 소형정·드론에 반격한다.
    ///
    /// 손상 통제 중심(2026-10-11 개편): 선체 자동 회복과 응급 수리[R]를 함교가 맡고, 손상통제반 수(작동 중)로 커진다.
    ///   - 자동 회복 상한: 50% + 손상통제반마다 (50% ÷ 최대 3개) → 50 / 67 / 83 / 100%. 상한을 넘으면 고치지 않는다.
    ///   - 자동 회복 속도: Stats.HullRepairPerSecond × (1 + 0.5 × 손상통제반 수) × 성장 카드 수리 효율.
    ///   - 응급 수리 횟수: 1 + 손상통제반 수. 쓰면 1회 줄고(연달아 쓰지 않게 4초 간격), 스테이지 보스를 격침하면 가득 찬다.
    ///     손상통제반을 새로 놓으면 늘어난 만큼 바로 생기고, 잃으면 최대치에 맞춰 줄어든다. 응급 수리는 상한과 상관없이 고친다.
    /// </summary>
    public class BridgeModule : DecoyLauncherModule, IFlankSource, IRadarSource, IRepairBurstSource, ISkillCharges
    {
        [SerializeField] private DamageControlCrew crew = new();

        [Header("Damage Control")]
        [Tooltip("손상통제반이 없을 때 선체 자동 회복 상한(최대 선체 대비)")]
        [SerializeField, Range(0f, 1f)] private float baseHullCap = 0.5f;
        [Tooltip("손상통제반 최대 수(이만큼이면 상한 100%)")]
        [SerializeField, Min(1)] private int maxRepairBays = 3;
        [Tooltip("손상통제반 하나마다 자동 회복 속도 증가")]
        [SerializeField] private float speedPerRepairBay = 0.5f;
        [SerializeField] private float burstHull = 30f;
        [Tooltip("응급 수리: 모든 (파괴되지 않은) 모듈을 최대 내구의 이 비율만큼 수리")]
        [SerializeField, Range(0f, 1f)] private float burstModuleRatio = 0.4f;
        [Tooltip("응급 수리를 연달아 쓰지 않게 두는 간격(초)")]
        [SerializeField] private float burstGap = 4f;
        [SerializeField] private GameObject burstEffect;

        [Header("Machine Gun")]
        [SerializeField] private BridgeMachineGun machineGun = new();
        public BridgeMachineGun MachineGun => machineGun;

        [Header("Ruleset (WWII 패치 A 9.1)")]
        [Tooltip("내장 발사기로 기만체를 쏘는가. WWII 함교는 끈다(현대 함교 프리팹은 그대로 켜 둔다).")]
        [SerializeField] private bool decoyEnabled = true;
        [Tooltip("내장 발사기에 연막탄을 싣는가. 현대 함교는 기만체만, WWII 함교는 기만체 대신 연막.")]
        [SerializeField] private bool smokeRounds;

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
        private int _repairBays, _maxCharges = -1, _charges;
        private float _burstGapLeft;

        // --- 손상 통제(함교가 중심)
        public int RepairBays => _repairBays;
        public float HullRepairCap => Mathf.Clamp01(baseHullCap + (1f - baseHullCap) * Mathf.Min(_repairBays, maxRepairBays) / maxRepairBays);
        public float HullRepairRate => Stats.HullRepairPerSecond * (1f + speedPerRepairBay * _repairBays) * RunUpgrades.RepairMultiplier;
        public int Charges => _charges;
        public int MaxCharges => 1 + _repairBays;
        public bool BurstReady => _charges > 0 && _burstGapLeft <= 0f && Ship != null && Instance != null && Instance.IsOperational;
        public float BurstReadiness01 => _charges <= 0 ? 0f : burstGap > 0f ? 1f - Mathf.Clamp01(_burstGapLeft / burstGap) : 1f;

        public void Burst()
        {
            if (!BurstReady) return;
            Ship.RepairHull(burstHull);
            if (Grid != null)
                foreach (var m in Grid.Modules)
                    if (m != null && !m.IsDestroyed) m.Repair(m.MaxHp * burstModuleRatio);
            _charges--;
            _burstGapLeft = burstGap;
            PooledEffect.Spawn(burstEffect, Ship.transform.position);
            AudioManager.Play(Game.Data.SfxId.SonarPing, transform.position, 0.6f, 0.8f);
            CombatLog.Add("응급 수리", $"선체 +{burstHull:0} · 모듈 {burstModuleRatio * 100f:0}% · 남은 {_charges}/{MaxCharges}회");
        }

        /// <summary>작동 중인 손상통제반 수를 다시 센다. 최대 횟수가 늘면 늘어난 만큼 바로 채우고, 줄면 맞춰 줄인다.</summary>
        private void RecountRepairBays()
        {
            int bays = 0;
            if (Grid != null)
                foreach (var m in Grid.Modules)
                    if (m != null && m.IsOperational && m.Definition != null && m.Definition.Type == ModuleType.RepairBay) bays++;
            _repairBays = bays;
            int max = MaxCharges;
            if (_maxCharges < 0) _charges = max;                       // 처음
            else if (max > _maxCharges) _charges += max - _maxCharges;  // 새로 놓은 손상통제반
            _charges = Mathf.Clamp(_charges, 0, max);
            _maxCharges = max;
        }

        private void RefillCharges()
        {
            if (this == null) { GameEvents.BossDefeated -= RefillCharges; return; }
            _charges = MaxCharges;
        }
        private readonly RadarAntennaTracker _radar = new();

        // --- 항해 레이더(레이더 화면이 이 안테나에 맞춰 스윕한다)
        public bool RadarOperational => this != null && Instance != null && Instance.IsOperational && isActiveAndEnabled;
        public float RadarRange => Stats.DetectionRange;
        public float AntennaRpm => antennaRpm;
        public float AntennaBearing => _radar.Bearing(radarAntenna, Ship != null ? Ship.transform : transform);

        protected override bool HasSmokeRounds => smokeRounds;
        public override bool DecoyEnabled => decoyEnabled;

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

        protected override void OnInitialized()
        {
            base.OnInitialized();
            machineGun.Init(this);
            RecountRepairBays();
            GameEvents.BossDefeated += RefillCharges;   // 스테이지를 깨면 응급 수리 가득
        }

        protected override void OnShipLayoutChanged()
        {
            base.OnShipLayoutChanged();
            RecountRepairBays();
        }

        protected override void Tick(float dt)
        {
            base.Tick(dt);
            machineGun.Tick(dt, Ship != null ? Ship.transform : transform);
            if (_flankCooldown > 0f) _flankCooldown -= dt * RunUpgrades.SkillRate;   // 성장 카드: 스킬 회복 속도
            if (_burstGapLeft > 0f) _burstGapLeft -= dt;
            crew.Tick(Ship, Grid, Instance, HullRepairRate, Stats.ModuleRepairPerSecond, dt, HullRepairCap);

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
