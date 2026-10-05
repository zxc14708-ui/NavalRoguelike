using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 76mm 속사 함포(6~34). 수상함과 몰려 있는 표적을 친다.
    ///
    /// 표적 선택: 수상함(드러난 잠수함 포함)을 먼저, 주변에 다른 적이 많을수록 먼저 — 고폭 파편이 여럿에 닿는다.
    /// 수상함이 없을 때만 항공기를 쏜다(대공 겸용).
    /// 사격통제장치로 표적의 진로를 앞질러 쏘고, 쏠 때마다 포신이 뒤로 밀린다. 빗나간 탄은 표적 너머 수면에 물기둥을 세운다.
    /// 선회는 느리고 비유도라 급선회하는 적에게는 빗나갈 수 있다.
    /// </summary>
    public class NavalGunModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("포구 화염·연기")]
        [SerializeField] private GameObject muzzleFlash;
        [Tooltip("표적 선체 중앙을 겨누는 높이")]
        [SerializeField] private float aimHeight = 0.5f;

        [Header("Target Priority")]
        [Tooltip("이 반경 안에 다른 적이 있으면 밀집 표적으로 본다")]
        [SerializeField] private float clusterRadius = 6f;
        [Tooltip("주변 적 한 척당 거리 가중치 감소")]
        [SerializeField] private float clusterBonus = 0.5f;

        [Header("Feedback")]
        [SerializeField] private float recoilDistance = 0.18f;
        [Tooltip("조준점 너머 이만큼 더 날아가도 맞지 않으면 빗나간 탄으로 보고 수면에 물기둥")]
        [SerializeField] private float missOvershoot = 4f;

        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;
        private MuzzleBurstVfx _muzzleVfx;
        private NavalGunModule _batteryPartner;
        private float _batteryReadyAt;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;

        private TargetingSystem _targeting;
        private System.Predicate<Vector3> _canEngage;
        private System.Func<ITargetable, float, float> _surfaceScore;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[Gun76] TargetingSystem을 찾지 못했습니다.", this);
            if (projectilePrefab == null) Debug.LogError("[Gun76] projectilePrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;

            _canEngage = CanEngage;
            _surfaceScore = SurfaceScore;
            OnShipLayoutChanged();
        }

        /// <summary>이웃이 바뀌면 사격각과 탄약고 보너스를 다시 잡는다.</summary>
        protected override void OnShipLayoutChanged()
        {
            if (Grid == null || Instance == null) return;
            weapon.SetFireArc(FireArcCalculator.Compute(Grid, Instance.Origin, Instance.RotationSteps, Instance.EffectiveHeight));
            var bonus = MagazineSupport.For(Grid, Instance, includeAmmo: true);
            weapon.SetReloadMultiplier(bonus.ReloadMultiplier);
            _batteryPartner = null;
            foreach (var dir in Game.Ship.GridCoord.Neighbors)
            {
                var magazine = Grid.Get(Instance.Origin + dir);
                if (magazine == null || !magazine.IsOperational || magazine.Definition.Type != ModuleType.Magazine) continue;
                var other = Grid.Get(magazine.Origin + dir);
                if (other != null && other.IsOperational && other.Definition.Type == ModuleType.NavalGun)
                { _batteryPartner = other.Runtime as NavalGunModule; break; }
            }
            float resupply = _batteryPartner != null ? Mathf.Min(1.35f, bonus.ResupplyMultiplier + 0.1f) : bonus.ResupplyMultiplier;
            _ammo.ApplyBonus(bonus.CapacityMultiplier, resupply);
        }

        protected override void Tick(float dt)
        {
            weapon.Tick(dt);
            _ammo.Tick(dt);
            if (_targeting == null || projectilePrefab == null) return;

            var target = _targeting.GetBest(transform.position, Stats.Range, TargetClass.Surface | TargetClass.Submarine,
                                            _surfaceScore, _canEngage)
                         ?? _targeting.GetBest(transform.position, Stats.Range, TargetClass.Air,
                                                 (t, sqr) => TargetInfo.Weighted(sqr, EfficiencyAgainst(t)), _canEngage);
            if (target == null) return;

            Vector3 aim = LeadPoint(target);
            weapon.AimAt(aim, Stats.TurretTurnRate, dt);

            if (!weapon.IsInFireArc(aim)) return;
            if (!weapon.IsAimedAt(aim)) return;
            if (!_ammo.CanFire) return;
            if (Time.time < _batteryReadyAt) return;
            if (!weapon.TryFire(Stats.ReloadTime)) return;

            _ammo.Consume();
            _lastShotTime = Time.time;
            if (_batteryPartner != null)
                _batteryPartner._batteryReadyAt = Mathf.Max(_batteryPartner._batteryReadyAt,
                    Time.time + Stats.ReloadTime * MagazineSupport.ReloadMultiplier(Grid, Instance) * 0.5f);
            Fire(aim, DamageAgainst(target));
        }

        /// <summary>가까울수록, 주변에 다른 수상 표적이 많을수록 낮은 점수.</summary>
        private float SurfaceScore(ITargetable t, float sqrDistance)
        {
            int neighbours = 0;
            float radius = clusterRadius * (CombatPolicies.Doctrine == NavalDoctrine.Gunnery ? 1.3f : 1f);
            float r2 = radius * radius;
            var list = _targeting.DetectedSurface;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || o == t || !o.IsAlive || o.Transform == null) continue;
                if ((o.Transform.position - t.Transform.position).sqrMagnitude <= r2) neighbours++;
            }
            float eff = EfficiencyAgainst(t);
            if (eff <= 0.001f) return float.MaxValue;
            return Mathf.Sqrt(sqrDistance) / (1f + clusterBonus * neighbours) / eff;
        }

        /// <summary>표적이 지금 진로를 유지하면 포탄과 만나는 지점.</summary>
        private Vector3 LeadPoint(ITargetable target)
        {
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;

            // 항공기는 기체 자체를, 함정은 선체 중앙 높이를 겨눈다(대공 겸용 함포)
            if (target is IHasVelocity air)
                return Ballistics.PredictIntercept(muzzle.position, target.Transform.position, air.Velocity, Stats.ProjectileSpeed);

            Vector3 p = target.Transform.position + Vector3.up * aimHeight;
            if (target is not EnemyController enemy) return p;

            Vector3 velocity = enemy.transform.forward * enemy.CurrentSpeed;
            return Ballistics.PredictIntercept(muzzle.position, p, velocity, Stats.ProjectileSpeed);
        }

        /// <summary>포신 방향으로 발사. 반동, 포구 화염, 빗나간 탄의 물기둥 거리 설정.</summary>
        private void Fire(Vector3 aim, float damage)
        {
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            Vector3 shotOrigin = muzzle.position;
            Vector3 dir = weapon.FireDirection;

            var go = PoolManager.Instance?.Spawn(projectilePrefab, shotOrigin, Quaternion.LookRotation(dir));
            if (go == null) return;

            if (go.TryGetComponent<Projectile>(out var shell))
            {
                shell.Launch(dir, Stats.ProjectileSpeed, damage, DamageSource.Gun);
                shell.SetSplashMultiplier(CombatPolicies.Doctrine == NavalDoctrine.Gunnery ? 1.25f : 1f);
                shell.SetMaxTravel(Vector3.Distance(shotOrigin, aim) + missOvershoot);
            }

            weapon.Kick(recoilDistance);
            if (_muzzleVfx == null) _muzzleVfx = gameObject.AddComponent<MuzzleBurstVfx>();
            _muzzleVfx.Emit(shotOrigin, dir, MuzzleBurstVfx.Style.NavalGun, muzzleFlash, true);
            AudioManager.Play(Game.Data.SfxId.NavalGunShot, shotOrigin, 1f, 0.55f);   // 낮고 무거운 포성
        }

        /// <summary>교전거리 대역과 사격각을 모두 만족하는 표적만.</summary>
        private bool CanEngage(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange && weapon.IsInFireArc(p);
        }

        public override void BindUpgradeVisual(Transform visualRoot)
        {
            var turret = FindVisualPart(visualRoot, "TurretPivot");
            var elevation = FindVisualPart(visualRoot, "ElevationPivot");
            var muzzle = FindVisualPart(visualRoot, "Muzzle");
            if (turret != null && elevation != null && muzzle != null)
                weapon.BindArtTransforms(turret, elevation, muzzle);
            else
                Debug.LogError("[Gun76] 업그레이드 모델의 TurretPivot/ElevationPivot/Muzzle을 찾지 못했습니다.", this);
        }
    }
}
