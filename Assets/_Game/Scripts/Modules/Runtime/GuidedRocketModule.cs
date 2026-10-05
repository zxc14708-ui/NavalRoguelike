using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 130mm 유도로켓 발사기(10~38). 빠른 소형 수상함(고속정)을 정리한다.
    ///
    /// 한 번 교전하면 여러 발을 짧은 간격으로 차례로 쏘고, 발마다 아직 충분히 배정되지 않은 표적을 고른다(TargetAllocator).
    /// 그래서 가까운 적을 쏟아붓는 기관포, 튼튼한 적 한 척을 노리는 VLS와 구분된다.
    /// 재장전은 발당 ReloadTime × 연발 수(평균 화력은 예전과 같다). 항공기는 노리지 않는다. 탄약고 보너스를 받는다.
    /// </summary>
    public class GuidedRocketModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject rocketPrefab;
        [SerializeField] private Transform[] launchPoints;

        [Header("Salvo")]
        [SerializeField, Min(1)] private int salvoSize = 3;
        [SerializeField] private float salvoInterval = 0.22f;
        [Tooltip("고속정의 거리 가중치. 작을수록 더 멀어도 먼저")]
        [SerializeField, Range(0.1f, 1f)] private float fastBoatDistanceFactor = 0.5f;

        private TargetingSystem _targeting;
        private System.Predicate<Vector3> _inArc;
        private System.Func<ITargetable, float, float> _score;
        private int _nextTube;
        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;
        private int _salvoLeft;
        private float _salvoTimer;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[GuidedRocket] TargetingSystem을 찾지 못했습니다.", this);
            if (rocketPrefab == null) Debug.LogError("[GuidedRocket] rocketPrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;

            _inArc = CanEngage;
            _score = Score;
            OnShipLayoutChanged();
        }

        /// <summary>이웃이 바뀌면 사격각과 탄약고 보너스를 다시 잡는다.</summary>
        protected override void OnShipLayoutChanged()
        {
            if (Grid == null || Instance == null) return;
            weapon.SetFireArc(FireArcCalculator.Compute(Grid, Instance.Origin, Instance.RotationSteps, Instance.EffectiveHeight));
            weapon.SetReloadMultiplier(MagazineSupport.ReloadMultiplier(Grid, Instance));
        }

        /// <summary>중거리 대역과 사격각을 모두 만족하는 표적만. 너무 가까운 적은 기관포 몫이다.</summary>
        private bool CanEngage(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange && weapon.IsInFireArc(p);
        }

        /// <summary>이미 배정이 충분한 표적은 제외. 고속정은 더 멀어도 먼저.</summary>
        private float Score(ITargetable t, float sqrDistance)
        {
            if (TargetAllocator.IsCovered(t)) return float.MaxValue;
            float factor = t is FastAttackBoat ? fastBoatDistanceFactor : 1f;
            return TargetInfo.Weighted(sqrDistance * factor * factor, EfficiencyAgainst(t));
        }

        protected override void Tick(float dt)
        {
            weapon.Tick(dt);
            _ammo.Tick(dt);
            if (_targeting == null || rocketPrefab == null) return;

            var target = _targeting.GetBest(transform.position, Stats.Range, TargetClass.Surface | TargetClass.Submarine, _score, _inArc);

            // 연발 진행 중: 발마다 표적을 다시 골라 여러 척에 나눠 쏜다
            if (_salvoLeft > 0)
            {
                if (target != null) weapon.AimAt(target.Transform.position, Stats.TurretTurnRate, dt);
                _salvoTimer -= dt;
                if (_salvoTimer > 0f) return;

                bool fired = target != null && _ammo.CanFire && Launch(target);
                _salvoLeft = fired ? _salvoLeft - 1 : 0;
                _salvoTimer = salvoInterval;
                return;
            }

            if (target == null || !_ammo.CanFire) return;

            Vector3 p = target.Transform.position;
            weapon.AimAt(p, Stats.TurretTurnRate, dt);

            // 유도탄이라 정밀하게 겨눌 필요는 없지만, 발사대가 엉뚱한 쪽을 보고 쏘면 어색하다
            if (!weapon.IsAimedAt(p)) return;
            if (!weapon.TryFire(Stats.ReloadTime * salvoSize)) return;

            Launch(target);
            _salvoLeft = salvoSize - 1;
            _salvoTimer = salvoInterval;
        }

        private bool Launch(ITargetable target)
        {
            if (!_ammo.Consume()) return false;
            _lastShotTime = Time.time;

            var tube = NextTube();
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;

            // 발사대가 향한 쪽으로 내보낸다. 발사관 소켓의 회전은 모델 축을 믿을 수 없으므로 포신 방향을 쓴다.
            Vector3 dir = weapon.HasTurret ? weapon.FireDirection : target.Transform.position - tube.position;
            dir.y = Mathf.Max(dir.y, 0.05f);
            var rotation = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : muzzle.rotation;

            var go = PoolManager.Instance?.Spawn(rocketPrefab, tube.position, rotation);
            if (go == null) return true;

            go.GetComponent<Missile>()?.Launch(target.Transform, DamageAgainst(target));
            return true;
        }

        private Transform NextTube()
        {
            if (launchPoints == null || launchPoints.Length == 0)
                return weapon.Muzzle != null ? weapon.Muzzle : transform;

            var t = launchPoints[_nextTube % launchPoints.Length];
            _nextTube++;
            return t != null ? t : transform;
        }
    }
}
