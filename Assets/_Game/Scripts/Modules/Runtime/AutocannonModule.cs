using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 기관포(근거리 0~22). 가까운 고속정과 드론·전투기를 먼저 노린다.
    /// 빠르게 선회해 짧은 점사를 끊어 쏜다 — 연속 사격보다 표적 전환이 잦고 탄이 어디로 가는지 눈에 보인다.
    ///
    /// 탄은 목표 방향이 아니라 실제 포신 방향으로 나간다(작은 탄 퍼짐만 더함). 조준 오차가 허용치(weapon.aimTolerance)보다
    /// 크면 쏘지 않는다. 붙어 있는 블록이 가리는 방향으로는 선회·사격하지 못한다(FireArcCalculator).
    /// 전용 레이더가 없어도 근접 센서(TargetingSystem.visualRange 24) 안의 표적과 교전한다. 탄약고 보너스를 받는다.
    /// </summary>
    public class AutocannonModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("탄 퍼짐(도)")]
        [SerializeField] private float spreadDegrees = 1.2f;
        [Tooltip("포구 연기. 속사라 매 발이 아니라 몇 발에 한 번 뿜는다")]
        [SerializeField] private GameObject muzzleSmoke;
        [SerializeField, Min(1)] private int smokeEveryShots = 3;

        [Header("Burst")]
        [Tooltip("한 점사의 발 수")]
        [SerializeField, Min(1)] private int burstRounds = 6;
        [Tooltip("점사 사이 쉬는 시간(초)")]
        [SerializeField] private float burstPause = 0.25f;


        private int _shotCount;
        private MuzzleBurstVfx _muzzleVfx;
        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;
        private int _burstShots;
        private float _pause;

        private TargetingSystem _targeting;
        private System.Predicate<Vector3> _inArc;
        private System.Func<ITargetable, float, float> _score;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            _inArc = CanEngage;
            _score = Score;
            if (_targeting == null) Debug.LogError("[Autocannon] TargetingSystem을 찾지 못했습니다.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
            if (projectilePrefab == null) Debug.LogError("[Autocannon] projectilePrefab 미할당.", this);

            OnShipLayoutChanged();
        }

        /// <summary>이웃 블록이 바뀌면 사격각과 탄약고 보너스를 다시 잡는다.</summary>
        protected override void OnShipLayoutChanged()
        {
            RefreshMagazineBonus();
            if (Grid != null && Instance != null)
                weapon.SetFireArc(FireArcCalculator.Compute(Grid, Instance.Origin, Instance.RotationSteps, Instance.EffectiveHeight));
        }

        protected override void Tick(float dt)
        {
            weapon.Tick(dt);
            _ammo.Tick(dt);
            if (_pause > 0f) _pause -= dt;
            if (_targeting == null) return;

            var target = _targeting.GetBest(transform.position, Stats.Range,
                                            TargetClass.Surface | TargetClass.Submarine | TargetClass.Air, _score, _inArc);
            if (target == null) { _burstShots = 0; return; }

            Vector3 p = LeadPoint(target);
            weapon.AimAt(p, Stats.TurretTurnRate, dt);

            if (!weapon.IsInFireArc(p)) return;
            if (!weapon.IsAimedAt(p)) return;
            if (_pause > 0f) return;
            if (!_ammo.CanFire) return;
            if (!weapon.TryFire(Stats.ReloadTime)) return;

            _ammo.Consume();
            _lastShotTime = Time.time;
            Fire(DamageAgainst(target));

            if (++_burstShots >= burstRounds)
            {
                _burstShots = 0;
                _pause = burstPause * (CombatPolicies.Doctrine == NavalDoctrine.Gunnery ? 0.8f : 1f);
            }
        }

        /// <summary>효율이 높은 표적(고속정·드론)은 더 멀어도 먼저. 효율 0인 분류는 쏘지 않는다.</summary>
        private float Score(ITargetable t, float sqrDistance) => TargetInfo.Weighted(sqrDistance, EfficiencyAgainst(t));

        /// <summary>움직이는 표적은 탄이 도착할 때의 위치를 겨눈다.</summary>
        private Vector3 LeadPoint(ITargetable target)
        {
            Vector3 origin = weapon.Muzzle != null ? weapon.Muzzle.position : transform.position;
            Vector3 p = target.Transform.position;

            Vector3 velocity = target switch
            {
                IHasVelocity moving => moving.Velocity,
                EnemyController enemy => enemy.transform.forward * enemy.CurrentSpeed,
                _ => Vector3.zero,
            };
            if (target is EnemyController && target is not AirEnemy) p += Vector3.up * 0.5f;   // 수상함은 선체 중앙
            return velocity.sqrMagnitude > 0.01f ? Ballistics.PredictIntercept(origin, p, velocity, Stats.ProjectileSpeed) : p;
        }

        /// <summary>포신 방향으로 발사. 작은 퍼짐만 더한다.</summary>
        private void Fire(float damage)
        {
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            Vector3 dir = weapon.FireDirection;
            float spread = spreadDegrees * (CombatPolicies.Doctrine == NavalDoctrine.Gunnery ? 0.85f : 1f);
            dir = Quaternion.AngleAxis(Random.Range(-spread, spread) * 0.5f, Vector3.Cross(dir, Vector3.up).normalized) *
                  Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * dir;

            var go = PoolManager.Instance?.Spawn(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
            if (go == null) return;

            go.GetComponent<Projectile>()?.Launch(dir, Stats.ProjectileSpeed, damage, DamageSource.Gun);

            AudioManager.Play(Game.Data.SfxId.AutocannonShot, muzzle.position);
            if (_muzzleVfx == null) _muzzleVfx = gameObject.AddComponent<MuzzleBurstVfx>();
            _muzzleVfx.Emit(muzzle.position, weapon.FireDirection, MuzzleBurstVfx.Style.Autocannon,
                muzzleSmoke, ++_shotCount % smokeEveryShots == 0);
        }

        /// <summary>교전거리 대역과 사격각을 모두 만족하는 표적만.</summary>
        private bool CanEngage(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange && weapon.IsInFireArc(p);
        }

        /// <summary>탄약고 근접 보너스: 발사 간격, 탄약 용량, 보급 속도.</summary>
        private void RefreshMagazineBonus()
        {
            var bonus = MagazineSupport.For(Grid, Instance, includeAmmo: true);
            weapon.SetReloadMultiplier(bonus.ReloadMultiplier);
            _ammo.ApplyBonus(bonus.CapacityMultiplier, bonus.ResupplyMultiplier);
        }

        public override void BindUpgradeVisual(Transform visualRoot)
        {
            var turret = FindVisualPart(visualRoot, "TurretPivot");
            var elevation = FindVisualPart(visualRoot, "ElevationPivot");
            var muzzle = FindVisualPart(visualRoot, "Muzzle");
            if (turret != null && elevation != null && muzzle != null)
                weapon.BindArtTransforms(turret, elevation, muzzle);
            else
                Debug.LogError("[Autocannon] 업그레이드 모델의 TurretPivot/ElevationPivot/Muzzle을 찾지 못했습니다.", this);
        }
    }
}
