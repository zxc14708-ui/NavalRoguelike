using UnityEngine;
using Game.Core;
using Game.Data;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 설치된 모듈들이 만들어내는 함선 전체의 집계값을 계산한다(탐지, 방어 등).
    ///
    /// 기동성은 모듈 수와 무관하다. 기관은 함교에 통합되어 있고, 모듈을 많이 얻을수록
    /// 느려지면 기관 카드를 못 뽑은 판이 회복 불가능해지기 때문이다.
    ///
    /// 모듈이 설치/파괴/철거될 때만 재계산한다(매 프레임 아님).
    /// </summary>
    public class ShipSystems : MonoBehaviour
    {
        [SerializeField] private ShipGrid grid;
        [SerializeField] private ShipConfig config;

        public float DetectionRange { get; private set; }

        /// <summary>
        /// 지금 가장 멀리 듣는 소나의 반경(형태·속력 반영, 0 = 소나 없음). 방향 조건까지 따지려면 SonarCovers를 쓴다.
        /// </summary>
        public float SonarRange
        {
            get
            {
                float best = 0f, speed = SpeedRatio;
                foreach (var s in _sonars) best = Mathf.Max(best, ModuleVariants.SonarRange(s.variant, s.range, speed));
                return best;
            }
        }

        /// <summary>설치된 소나(형태, 성장 카드까지 반영한 기본 반경). Recalculate가 채운다.</summary>
        private readonly System.Collections.Generic.List<(ModuleVariant variant, float range)> _sonars = new();
        private ShipController _controller;

        public int SonarCount => _sonars.Count;

        /// <summary>최고 속력 대비 지금 속력(예인 소나 성능).</summary>
        public float SpeedRatio
        {
            get
            {
                if (_controller == null) _controller = GetComponentInParent<ShipController>();
                return _controller != null && _controller.BaseMaxSpeed > 0.01f ? Mathf.Abs(_controller.CurrentSpeed) / _controller.BaseMaxSpeed : 0f;
            }
        }

        /// <summary>이 위치를 소나 중 하나라도 듣는가(반경 + 선수 소나의 앞쪽 ±70°).</summary>
        public bool SonarCovers(Vector3 world)
        {
            if (_sonars.Count == 0) return false;
            Vector3 d = world - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            float bearing = dist > 0.01f ? Vector3.SignedAngle(Flat(transform.forward), d, Vector3.up) : 0f;
            float speed = SpeedRatio;
            foreach (var s in _sonars)
                if (dist <= ModuleVariants.SonarRange(s.variant, s.range, speed) && ModuleVariants.SonarSees(s.variant, bearing)) return true;
            return false;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        public float DamageReduction { get; private set; }
        public int MaxTrackedTargets { get; private set; }
        public int CommandCapacity { get; private set; }

        private float _escortFireRateBonus, _speedBonus, _accelerationBonus;
        private float _movingGunDamageBonus, _guidedRangeBonus, _missileReloadReduction;
        private int _fireControlTracks;
        public float EscortFireRateMultiplier => 1f + _escortFireRateBonus;
        public float SpeedMultiplier => 1f + _speedBonus;
        public float AccelerationMultiplier => 1f + _accelerationBonus;
        public float GuidedWeaponRangeMultiplier => 1f + _guidedRangeBonus;
        public float MissileReloadMultiplier => 1f - _missileReloadReduction;
        public float MissileResupplyMultiplier => 1f - _missileReloadReduction;
        public float MovingGunDamageMultiplier => SpeedRatio >= 0.5f ? 1f + _movingGunDamageBonus : 1f;

        private void OnEnable()
        {
            GameEvents.ModuleInstalled += OnModuleChanged;
            GameEvents.ModuleDestroyed += OnModuleChanged;
            GameEvents.ModuleRemoved += OnModuleChanged;
            GameEvents.ModuleUpgraded += OnModuleChanged;
            RunUpgrades.Changed += Recalculate;   // 성장 카드(탐지 거리)
        }

        private void OnDisable()
        {
            RunUpgrades.Changed -= Recalculate;
            GameEvents.ModuleInstalled -= OnModuleChanged;
            GameEvents.ModuleDestroyed -= OnModuleChanged;
            GameEvents.ModuleRemoved -= OnModuleChanged;
            GameEvents.ModuleUpgraded -= OnModuleChanged;
        }

        private void Start() => Recalculate();

        private void OnModuleChanged(ModuleInstance _) => Recalculate();

        /// <summary>모듈 구성이 바뀔 때마다 전체 집계를 다시 만든다.</summary>
        public void Recalculate()
        {
            if (grid == null || config == null)
            {
                Debug.LogError("[ShipSystems] grid 또는 config가 Inspector에서 할당되지 않았습니다.", this);
                return;
            }

            DetectionRange = config.BaseDetectionRange;
            _sonars.Clear();
            DamageReduction = 0f;
            MaxTrackedTargets = config.BaseTrackedTargets;
            CommandCapacity = config.BaseCommandCapacity;
            _escortFireRateBonus = _speedBonus = _accelerationBonus = 0f;
            _movingGunDamageBonus = _guidedRangeBonus = _missileReloadReduction = 0f;
            _fireControlTracks = 0;

            foreach (var m in grid.Modules)
            {
                if (m == null || !m.IsOperational) continue;
                // 성장 카드 이벤트 구독 순서와 무관하게 최신 수치를 집계한다.
                m.RefreshStats();
                m.Runtime?.ContributeToShipSystems(this);
            }
            MaxTrackedTargets += _fireControlTracks;

            // 작동 중인 탄창을 새 보급 간격에 맞춘다. 탄수·보급 진행률은 유지한다.
            // 비활성화된 무기도 갱신하여 다시 켰을 때 파괴된 지원의 보너스가 남지 않게 한다.
            foreach (var m in grid.Modules)
                if (m != null && m.IsOperational && m.Runtime is Game.Combat.IAmmoUser ammoUser)
                    ammoUser.Ammo?.Reconfigure(m.Runtime.Stats);

            // 성장 카드: 탐지 거리(레이더·소나)
            DetectionRange *= RunUpgrades.DetectionMultiplier;
            for (int i = 0; i < _sonars.Count; i++)
                _sonars[i] = (_sonars[i].variant, _sonars[i].range * RunUpgrades.DetectionMultiplier);

            GameEvents.RaiseDetectionRangeChanged(DetectionRange);
        }

        // --- 모듈 런타임이 호출하는 기여 API
        public void RaiseDetectionRange(float v) => DetectionRange = Mathf.Max(DetectionRange, v);
        /// <summary>형태 없는 소나(예전 호출 호환): 사방을 듣는 함내 기준이 아니라 기본 반경 그대로.</summary>
        public void RaiseSonarRange(float v) => AddSonar(ModuleVariant.None, v);

        /// <summary>소나 하나를 더한다(형태별 반경·방향은 SonarRange/SonarCovers가 계산).</summary>
        public void AddSonar(ModuleVariant variant, float baseRange)
        {
            if (baseRange > 0f) _sonars.Add((variant, baseRange));
        }
        public void AddTrackedTargets(int v) => MaxTrackedTargets += v;

        /// <summary>동종 지원은 가장 강한 하나만 적용한다. 잘못된 데이터도 무한 연사로 이어지지 않게 제한한다.</summary>
        public void RaiseEscortFireRateBonus(float bonus)
            => _escortFireRateBonus = Mathf.Max(_escortFireRateBonus, Mathf.Clamp(bonus, 0f, 0.5f));

        public void RaisePropulsionBonuses(float speed, float acceleration, float movingGunDamage)
        {
            _speedBonus = Mathf.Max(_speedBonus, Mathf.Clamp(speed, 0f, 0.5f));
            _accelerationBonus = Mathf.Max(_accelerationBonus, Mathf.Clamp(acceleration, 0f, 1f));
            _movingGunDamageBonus = Mathf.Max(_movingGunDamageBonus, Mathf.Clamp(movingGunDamage, 0f, 0.5f));
        }

        public void RaiseFireControl(int tracks, float guidedRange)
        {
            _fireControlTracks = Mathf.Max(_fireControlTracks, Mathf.Clamp(tracks, 0, 8));
            _guidedRangeBonus = Mathf.Max(_guidedRangeBonus, Mathf.Clamp(guidedRange, 0f, 0.5f));
        }

        public void RaiseMissileLogistics(float reduction)
            => _missileReloadReduction = Mathf.Max(_missileReloadReduction, Mathf.Clamp(reduction, 0f, 0.5f));

        /// <summary>집계한 지원을 무기 스탯 복사본에만 적용한다. 사격·표적 선택·UI가 같은 값을 읽는다.</summary>
        public void ApplyModuleBonuses(ModuleType type, ref ModuleStats stats)
        {
            if (type is ModuleType.Vls or ModuleType.GuidedRocket or ModuleType.SamLauncher)
                stats.Range *= GuidedWeaponRangeMultiplier;
            if (type is ModuleType.Vls or ModuleType.GuidedRocket)
            {
                stats.ReloadTime *= MissileReloadMultiplier;
                stats.AmmoReloadTime *= MissileResupplyMultiplier;
            }
            if (type is ModuleType.Autocannon or ModuleType.NavalGun)
                stats.Damage *= MovingGunDamageMultiplier;
        }

        /// <summary>여러 개를 달아도 무적이 되지 않도록 상한을 둔다.</summary>
        public void AddDamageReduction(float pct01)
            => DamageReduction = Mathf.Clamp(DamageReduction + pct01, 0f, 0.6f);
    }
}
