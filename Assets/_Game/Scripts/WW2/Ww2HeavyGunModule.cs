using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Modules;

namespace Game.WW2
{
    /// <summary>
    /// 대구경 주포(WWII 패치 C3a, ww2_mod_heavy_gun — 2×2칸, 마운트 등급 Heavy: 전함만).
    ///   - 평소: 수상함만 자동으로 쏜다(항공기는 쏘지 않는다). 큰 배를 먼저(데이터 효율 가중). 연장 포신이 <b>함께</b> 쏜다(한 번 = 일제 한 번).
    ///   - 느린 선회 · 긴 장전 · 탄이 날아가는 시간 — 빠르게 도는 작은 배는 놓친다. 포신마다 <see cref="dispersion"/>° 안에서 흩어진다.
    ///   - 주포 일제(<see cref="Ww2SalvoControl"/>, 패치 A 4장): 키를 누르고 있는 동안 자동 사격을 멈추고(<see cref="Hold"/>) 겨눈 곳으로 돈다.
    ///     떼면 장전·조준이 끝난 포만 그곳으로 한꺼번에 쏜다 — 흩어짐 × <see cref="salvoDispersionScale"/>. 공짜로 탄을 더 만들지 않는다.
    ///   - 사격각은 발자국 가운데에서 잰다(FireArcCalculator, 2×2). 붙은 블록·상부 구조물 방향으로는 돌지도 쏘지도 않는다.
    ///   - 전투 중(Playing)이 아니면 쏘지 않는다.
    /// </summary>
    public class Ww2HeavyGunModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject muzzleFlash;
        [Tooltip("포신 끝(Muzzle_01~03). 비면 weapon.muzzle 하나")]
        [SerializeField] private Transform[] muzzles = System.Array.Empty<Transform>();
        [SerializeField] private float aimHeight = 0.6f;
        [SerializeField] private float recoilDistance = 0.35f;
        [Tooltip("자동 사격 때 포신마다 흩어지는 각도(±°)")]
        [SerializeField] private float dispersion = 2.2f;
        [Tooltip("주포 일제 때 흩어짐 배율(작을수록 좁게)")]
        [SerializeField] private float salvoDispersionScale = 0.35f;
        [SerializeField] private float missOvershoot = 6f;

        private readonly AmmoMagazine _ammo = new();
        private TargetingSystem _targeting;
        private System.Predicate<Vector3> _canEngage;
        private ITargetable _current;
        private float _lastShotTime = -999f;
        private MuzzleBurstVfx _muzzleVfx;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 1.5f;
        public WeaponController Weapon => weapon;
        public ITargetable CurrentTarget => _current;
        /// <summary>자동 사격으로 쏜 일제 수 · 주포 일제로 쏜 수 · 쏜 포탄 수.</summary>
        public int AutoVolleys { get; private set; }
        public int SalvoVolleys { get; private set; }
        public int ShellsFired { get; private set; }
        /// <summary>주포 일제 조준 중: 자동 사격을 멈추고 이 지점으로 돈다(null이면 평소대로).</summary>
        public Vector3? Hold { get; set; }
        public bool Operational => Instance != null && Instance.IsOperational;
        /// <summary>장전이 끝났고 탄이 있다(조준은 따로).</summary>
        public bool Loaded => Operational && weapon.IsReady && _ammo.CanFire;
        public int Barrels => muzzles != null && muzzles.Length > 0 ? muzzles.Length : 1;
        public float LastDispersion { get; private set; }

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[WW2 주포] TargetingSystem을 찾지 못했습니다.", this);
            if (projectilePrefab == null) Debug.LogError("[WW2 주포] projectilePrefab 미할당.", this);
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
            if (Hold.HasValue)
            {
                // 주포 일제 조준: 쏘지 않고 겨눈 곳으로만 돈다
                if (weapon.IsInFireArc(Hold.Value)) weapon.AimAt(Hold.Value, Stats.TurretTurnRate, dt);
                return;
            }
            if (_targeting == null || projectilePrefab == null || !InCombat) return;

            var target = PickTarget();
            if (target == null) return;
            Vector3 aim = LeadPoint(target);
            weapon.AimAt(aim, Stats.TurretTurnRate, dt);
            if (!weapon.IsInFireArc(aim) || !weapon.IsAimedAt(aim) || !_ammo.CanFire) return;
            if (!weapon.TryFire(Stats.ReloadTime)) return;
            Volley(aim, DamageAgainst(target), dispersion);
            AutoVolleys++;
        }

        /// <summary>수상함만. 큰 배(효율이 높은 표적)를 먼저.</summary>
        private ITargetable PickTarget()
        {
            if (_current != null && (!_current.IsAlive || _current.Transform == null || !CanEngage(_current.Transform.position))) _current = null;
            var pick = _targeting.GetBest(transform.position, Stats.Range, TargetClass.Surface,
                (t, sqr) => TargetInfo.Weighted(sqr, EfficiencyAgainst(t)), _canEngage);
            if (pick != null) _current = pick;
            return _current;
        }

        /// <summary>이 지점으로 일제를 쏠 수 있는가(이유).</summary>
        public bool CanSalvoAt(Vector3 point, out string reason)
        {
            reason = null;
            if (!Operational) { reason = "주포 파괴됨"; return false; }
            if (!_ammo.CanFire) { reason = "포탄 없음"; return false; }
            if (!weapon.IsReady) { reason = "장전 중"; return false; }
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > Stats.Range * Stats.Range) { reason = $"사거리 밖({d.magnitude:0}/{Stats.Range:0} m)"; return false; }
            if (d.sqrMagnitude < Stats.MinRange * Stats.MinRange) { reason = "너무 가까움"; return false; }
            if (!weapon.IsInFireArc(point)) { reason = "사격각 밖(구조물에 가림)"; return false; }
            if (!weapon.IsAimedAt(point)) { reason = "포탑이 아직 돌고 있음"; return false; }
            return true;
        }

        /// <summary>주포 일제: 조건이 맞으면 그 지점으로 좁게 쏜다. 쏜 포탄 수(못 쏘면 0).</summary>
        public int FireSalvo(Vector3 point, float damage)
        {
            if (!InCombat || !CanSalvoAt(point, out _) || !weapon.TryFire(Stats.ReloadTime)) return 0;
            SalvoVolleys++;
            return Volley(point, damage, dispersion * salvoDispersionScale);
        }

        /// <summary>표적 효율을 반영한 한 발 피해(주포 일제가 표적을 알 때).</summary>
        public float DamageFor(ITargetable target) => target != null ? DamageAgainst(target) : Stats.Damage;

        private int Volley(Vector3 aim, float damage, float spread)
        {
            _ammo.Consume();
            _lastShotTime = Time.time;
            LastDispersion = spread;
            int n = 0;
            var list = muzzles != null && muzzles.Length > 0 ? muzzles : new[] { weapon.Muzzle != null ? weapon.Muzzle : transform };
            Vector3 baseDir = weapon.FireDirection;
            foreach (var m in list)
            {
                if (m == null) continue;
                Vector3 dir = Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * baseDir;
                var go = PoolManager.Instance?.Spawn(projectilePrefab, m.position, Quaternion.LookRotation(dir));
                if (go != null && go.TryGetComponent<Projectile>(out var shell))
                {
                    shell.Launch(dir, Stats.ProjectileSpeed, damage, DamageSource.Gun);
                    shell.SetMaxTravel(Vector3.Distance(m.position, aim) + missOvershoot);
                }
                if (_muzzleVfx == null) _muzzleVfx = gameObject.AddComponent<MuzzleBurstVfx>();
                _muzzleVfx.Emit(m.position, dir, MuzzleBurstVfx.Style.NavalGun, muzzleFlash, true);
                n++;
            }
            ShellsFired += n;
            weapon.Kick(recoilDistance);
            AudioManager.Play(Game.Data.SfxId.NavalGunShot, transform.position, 1f, 0.42f);
            return n;
        }

        /// <summary>표적 앞을 겨눈다(탄 비행 시간).</summary>
        public Vector3 LeadPoint(ITargetable target)
        {
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            Vector3 p = target.Transform.position + Vector3.up * aimHeight;
            return target is EnemyController enemy
                ? Ballistics.PredictIntercept(muzzle.position, p, enemy.MoveVelocity, Stats.ProjectileSpeed)
                : p;
        }

        private bool CanEngage(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange && weapon.IsInFireArc(p);
        }
    }
}
