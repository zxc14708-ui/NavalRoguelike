using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Modules;

namespace Game.WW2
{
    /// <summary>
    /// 양용포(WWII 패치 B, ww2_mod_dual_purpose_gun). 수상함이 본업이고 항공기는 보조다 — 한 번에 한 표적만 쏜다.
    ///   - 표적: 평소에는 수상함(밀집·가까운 것). 공격 방향을 확정한 항공기(뇌격기 진입·급강하)가 대공 사거리 안에 들어오면
    ///     그쪽으로 돌린다(<see cref="AirStrikes.IsCommitted"/>). 수상함이 없으면 항공기.
    ///   - 대공 사거리 = 사거리 × <see cref="AirRangeRatio"/>, 항공기에 대한 피해는 데이터의 효율(Air)로 줄어든다.
    ///   - 포탑이 느리게 돌고(선회 속도) 탄이 날아가는 시간이 있어 급선회하는 표적은 빗나간다. 표적을 바꾸면
    ///     <see cref="RetargetHold"/>초 동안 그 표적을 유지한다(두 표적 사이를 오가며 아무것도 못 맞히지 않게).
    ///   - 붙은 블록·상부 구조물 방향으로는 돌지도 쏘지도 않는다(FireArc — 자기 구조물 관통 금지).
    ///   - 쌍열: Muzzle_01·02가 있으면 번갈아 쏜다. 전투 중(Playing)이 아니면 쏘지 않는다(일시정지·정비 중 오발 금지).
    /// </summary>
    public class Ww2DualPurposeGunModule : ModuleRuntime, IAmmoUser
    {
        public const float AirRangeRatio = 0.65f;
        public const float RetargetHold = 1.2f;

        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField] private float aimHeight = 0.5f;
        [SerializeField] private float recoilDistance = 0.22f;
        [Tooltip("조준점 너머 이만큼 더 날아가도 맞지 않으면 빗나간 탄(물기둥)")]
        [SerializeField] private float missOvershoot = 5f;
        [SerializeField] private Transform muzzleA, muzzleB;

        private readonly AmmoMagazine _ammo = new();
        private TargetingSystem _targeting;
        private System.Predicate<Vector3> _canEngage;
        private ITargetable _current;
        private float _holdUntil, _lastShotTime = -999f;
        private int _barrel;
        private MuzzleBurstVfx _muzzleVfx;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.8f;
        /// <summary>지금 항공기를 쏘는 중(사전·진단).</summary>
        public bool AirMode { get; private set; }
        public ITargetable CurrentTarget => _current;
        public float AirRange => Stats.Range * AirRangeRatio;
        public int ShotsFired { get; private set; }
        public WeaponController Weapon => weapon;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[WW2 양용포] TargetingSystem을 찾지 못했습니다.", this);
            if (projectilePrefab == null) Debug.LogError("[WW2 양용포] projectilePrefab 미할당.", this);
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
            if (_targeting == null || projectilePrefab == null || !InCombat) return;

            var target = PickTarget();
            if (target == null) { AirMode = false; return; }

            Vector3 aim = LeadPoint(target);
            weapon.AimAt(aim, Stats.TurretTurnRate, dt);
            if (!weapon.IsInFireArc(aim) || !weapon.IsAimedAt(aim) || !_ammo.CanFire) return;
            if (!weapon.TryFire(Stats.ReloadTime)) return;

            _ammo.Consume();
            _lastShotTime = Time.time;
            ShotsFired++;
            Fire(aim, DamageAgainst(target));
        }

        /// <summary>한 표적: 지금 표적을 잠깐 유지하고, 확정된 공격기가 가까우면 그쪽, 아니면 수상함, 없으면 항공기.</summary>
        private ITargetable PickTarget()
        {
            if (_current != null && (!_current.IsAlive || _current.Transform == null || !CanEngage(_current.Transform.position)
                                     || !InRange(_current))) _current = null;
            if (_current != null && Time.time < _holdUntil) return _current;

            Vector3 from = transform.position;
            var threat = _targeting.GetBest(from, AirRange, TargetClass.Air,
                (t, sqr) => AirStrikes.IsCommitted(t) ? TargetInfo.Weighted(sqr, EfficiencyAgainst(t)) : float.MaxValue, _canEngage);
            if (threat != null && !AirStrikes.IsCommitted(threat)) threat = null;
            var surface = _targeting.GetBest(from, Stats.Range, TargetClass.Surface, (t, sqr) => TargetInfo.Weighted(sqr, EfficiencyAgainst(t)), _canEngage);
            var air = threat ?? (surface == null
                ? _targeting.GetBest(from, AirRange, TargetClass.Air, (t, sqr) => TargetInfo.Weighted(sqr, EfficiencyAgainst(t)), _canEngage)
                : null);
            var pick = threat ?? surface ?? air;
            if (pick != _current) { _current = pick; _holdUntil = Time.time + RetargetHold; }
            AirMode = pick != null && pick.Kind == TargetKind.Aircraft;
            return pick;
        }

        private bool InRange(ITargetable t)
        {
            float range = t.Kind == TargetKind.Aircraft ? AirRange : Stats.Range;
            Vector3 d = t.Transform.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= range * range;
        }

        private Vector3 LeadPoint(ITargetable target)
        {
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            if (target is IHasVelocity air)
                return Ballistics.PredictIntercept(muzzle.position, target.Transform.position, air.Velocity, Stats.ProjectileSpeed);
            Vector3 p = target.Transform.position + Vector3.up * aimHeight;
            return target is EnemyController enemy
                ? Ballistics.PredictIntercept(muzzle.position, p, enemy.MoveVelocity, Stats.ProjectileSpeed)
                : p;
        }

        private void Fire(Vector3 aim, float damage)
        {
            var center = weapon.Muzzle != null ? weapon.Muzzle : transform;
            Vector3 origin = center.position;
            if (muzzleA != null && muzzleB != null)
            {
                _barrel ^= 1;
                origin = (_barrel == 0 ? muzzleA : muzzleB).position;
            }
            Vector3 dir = weapon.FireDirection;
            var go = PoolManager.Instance?.Spawn(projectilePrefab, origin, Quaternion.LookRotation(dir));
            if (go != null && go.TryGetComponent<Projectile>(out var shell))
            {
                shell.Launch(dir, Stats.ProjectileSpeed, damage, DamageSource.Gun);
                shell.SetMaxTravel(Vector3.Distance(origin, aim) + missOvershoot);
            }
            weapon.Kick(recoilDistance);
            if (_muzzleVfx == null) _muzzleVfx = gameObject.AddComponent<MuzzleBurstVfx>();
            _muzzleVfx.Emit(origin, dir, MuzzleBurstVfx.Style.NavalGun, muzzleFlash, true);
            AudioManager.Play(Game.Data.SfxId.NavalGunShot, origin, 0.9f, 0.62f);
        }

        private bool CanEngage(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange && weapon.IsInFireArc(p);
        }
    }
}
