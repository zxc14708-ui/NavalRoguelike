using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Modules;

namespace Game.WW2
{
    /// <summary>
    /// 40mm급 4연장 대공포(WWII 패치 B, ww2_mod_quad_aa). 노봉(근접신관)과 다른 새 동작 — 탄은 직격해야 맞는다.
    ///   - 표적: 항공기 우선(공격 방향을 확정한 기체를 먼저), 항공기가 없으면 사거리 × <see cref="SurfaceRangeRatio"/> 안의 수상함.
    ///     중장갑·대형 표적에는 효율(데이터)이 낮아 거의 피해가 없다. 어뢰·포탄·폭탄은 막지 못한다(요격 무장이 아니다).
    ///   - 추적 후 연사: 표적을 향해 돈 뒤 네 포신이 번갈아 한 발씩, <see cref="BurstRounds"/>발 쏘고 <see cref="BurstPause"/>초 쉰다.
    ///   - 붙은 블록·상부 구조물 방향은 쏘지 못한다(FireArc). 전투 중(Playing)이 아니면 쏘지 않는다.
    /// </summary>
    public class Ww2QuadAaModule : ModuleRuntime, IAmmoUser
    {
        public const float SurfaceRangeRatio = 0.6f;
        public const int BurstRounds = 8;
        public const float BurstPause = 0.6f;

        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject muzzleSmoke;
        [Tooltip("포신 4개 포구(없으면 가운데 Muzzle)")]
        [SerializeField] private Transform[] muzzles = System.Array.Empty<Transform>();
        [SerializeField] private float spreadDegrees = 1.2f;
        [SerializeField] private float recoilDistance = 0.05f;

        private readonly AmmoMagazine _ammo = new();
        private TargetingSystem _targeting;
        private System.Predicate<Vector3> _canEngage;
        private float _pause, _lastShotTime = -999f;
        private int _burst, _barrel;
        private MuzzleBurstVfx _muzzleVfx;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;
        public int RoundsFired { get; private set; }
        public ITargetable CurrentTarget { get; private set; }
        public float SurfaceRange => Stats.Range * SurfaceRangeRatio;
        public WeaponController Weapon => weapon;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[WW2 대공포] TargetingSystem을 찾지 못했습니다.", this);
            if (projectilePrefab == null) Debug.LogError("[WW2 대공포] projectilePrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
            _canEngage = CanEngage;
            OnShipLayoutChanged();
        }

        protected override void OnShipLayoutChanged()
        {
            if (Grid == null || Instance == null) return;
            weapon.SetFireArc(FireArcCalculator.Compute(Grid, Instance.Origin, Instance.RotationSteps, Instance.EffectiveHeight));
        }

        private static bool InCombat => GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;

        protected override void Tick(float dt)
        {
            weapon.Tick(dt);
            _ammo.Tick(dt);
            if (_pause > 0f) _pause -= dt;
            if (_targeting == null || projectilePrefab == null || !InCombat) return;

            Vector3 from = transform.position;
            var target = _targeting.GetBest(from, Stats.Range, TargetClass.Air, AirScore, _canEngage)
                         ?? _targeting.GetBest(from, SurfaceRange, TargetClass.Surface, SurfaceScore, _canEngage);
            CurrentTarget = target;
            if (target == null) { _burst = 0; return; }

            Vector3 aim = LeadPoint(target);
            weapon.AimAt(aim, Stats.TurretTurnRate, dt);
            if (!weapon.IsInFireArc(aim) || !weapon.IsAimedAt(aim)) return;
            if (_pause > 0f || !_ammo.CanFire) return;
            if (!weapon.TryFire(Stats.ReloadTime)) return;

            _ammo.Consume();
            _lastShotTime = Time.time;
            RoundsFired++;
            FireRound(DamageAgainst(target));
            if (++_burst >= BurstRounds) { _burst = 0; _pause = BurstPause; }
        }

        /// <summary>항공기: 공격 방향을 확정한 기체를 먼저(점수 절반), 그다음 가까운 순.</summary>
        private float AirScore(ITargetable t, float sqr)
        {
            float s = TargetInfo.Weighted(sqr, EfficiencyAgainst(t));
            return AirStrikes.IsCommitted(t) ? s * 0.25f : s;
        }

        /// <summary>수상함: 효율이 있는(경수상) 표적만, 가까운 순.</summary>
        private float SurfaceScore(ITargetable t, float sqr)
        {
            float eff = EfficiencyAgainst(t);
            return eff < 0.3f ? float.MaxValue : TargetInfo.Weighted(sqr, eff);
        }

        private void FireRound(float damage)
        {
            var center = weapon.Muzzle != null ? weapon.Muzzle : transform;
            Vector3 origin = center.position;
            if (muzzles != null && muzzles.Length > 0)
            {
                _barrel = (_barrel + 1) % muzzles.Length;
                if (muzzles[_barrel] != null) origin = muzzles[_barrel].position;
            }
            Vector3 dir = weapon.FireDirection;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 d = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), Vector3.up) *
                        Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees) * 0.6f, side) * dir;
            var go = PoolManager.Instance?.Spawn(projectilePrefab, origin, Quaternion.LookRotation(d));
            go?.GetComponent<Projectile>()?.Launch(d, Stats.ProjectileSpeed, damage, DamageSource.Gun);
            weapon.Kick(recoilDistance);
            if (_muzzleVfx == null) _muzzleVfx = gameObject.AddComponent<MuzzleBurstVfx>();
            _muzzleVfx.Emit(origin, dir, MuzzleBurstVfx.Style.Autocannon, muzzleSmoke, RoundsFired % 2 == 0);
            AudioManager.Play(Game.Data.SfxId.AutocannonShot, origin, 0.7f, 0.8f);
        }

        private Vector3 LeadPoint(ITargetable target)
        {
            Vector3 origin = weapon.Muzzle != null ? weapon.Muzzle.position : transform.position;
            Vector3 p = target.Transform.position;
            Vector3 velocity = target switch
            {
                IHasVelocity moving => moving.Velocity,
                EnemyController enemy => enemy.MoveVelocity,
                _ => Vector3.zero,
            };
            if (target is EnemyController && target is not AirEnemy) p += Vector3.up * 0.5f;
            return velocity.sqrMagnitude > 0.01f ? Ballistics.PredictIntercept(origin, p, velocity, Stats.ProjectileSpeed) : p;
        }

        private bool CanEngage(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange && weapon.IsInFireArc(p);
        }
    }
}
