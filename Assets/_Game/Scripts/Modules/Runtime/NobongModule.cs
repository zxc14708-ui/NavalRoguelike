using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 노봉 40mm 쌍열포(2026-10-08). 국산 40mm 쌍열 함포(1996, 해군·해경 고속정·초계함 탑재)를 참고했다.
    /// 공개 자료: 유효 사거리 대함 6km · 대공 4km, 포신당 분당 310발(포탑 620발), 준비탄 768발, 성형파편 고폭탄,
    /// 자체 사격통제 레이더 없음(함정 레이더 사용). 게임 수치는 기관포(0~22)와 76mm(6~34) 사이로 옮겼다.
    ///   - 사거리: 수상 0~Range(28), 항공기·드론은 Range × airRangeRatio(대공 4km / 대함 6km ≈ 0.7).
    ///   - 쌍열: 좌우 포신이 번갈아 한 발씩 쏜다(2026-10-11 — 예전에는 두 발이 함께 나갔다). 발 간격 = ReloadTime ÷ 2라
    ///     좌우 한 쌍의 간격·화력은 예전과 같다. 8발(좌우 4쌍) 쏘고 잠깐 쉰다. 탄은 근접신관 — 드론·항공기 곁을 지나면 공중에서 터진다.
    ///   - 강화(UPG_Nobong): I 피해 +25%·탄창 +25%, II 피해 +50%·탄창 +50%·사거리 +15%.
    ///   - 자체 센서가 없다: 레이더·근접 센서(TargetingSystem)가 잡은 표적만 쏜다(CIWS와 다름).
    ///   - 기관포처럼 붙은 블록·상부 구조물 방향은 쏘지 못한다(FireArc). 탄약고 보너스를 받는다.
    /// </summary>
    public class NobongModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject muzzleSmoke;
        [Tooltip("좌우 포신 간격의 절반(m) — 번갈아 쏘는 두 포신의 포구 위치")]
        [SerializeField] private float barrelOffset = 0.14f;
        [SerializeField] private float spreadDegrees = 0.8f;
        [Tooltip("대공 사거리 = 사거리 × 이 값(대공 4km / 대함 6km)")]
        [SerializeField] private float airRangeRatio = 0.7f;

        [Header("Burst")]
        [Tooltip("한 번 몰아 쏘는 발 수(좌우 번갈아). 다 쏘면 burstPause만큼 쉰다")]
        [SerializeField, Min(1)] private int burstRounds = 8;
        [SerializeField] private float burstPause = 0.5f;
        [SerializeField] private float recoilDistance = 0.08f;

        private readonly AmmoMagazine _ammo = new();
        private TargetingSystem _targeting;
        private System.Func<ITargetable, float, float> _score;
        private System.Predicate<Vector3> _inArc;
        private float _pause, _lastShotTime = -999f;
        private int _burst, _salvoCount, _barrel;
        private MuzzleBurstVfx _muzzleVfx;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;
        /// <summary>쏜 발 수(좌우 번갈아 한 발씩).</summary>
        public int Salvos { get; private set; }
        /// <summary>마지막으로 쏜 포신: −1 = 왼쪽, +1 = 오른쪽.</summary>
        public int LastBarrel { get; private set; }
        public float AirRange => Stats.Range * airRangeRatio;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[Nobong] TargetingSystem을 찾지 못했습니다.", this);
            if (projectilePrefab == null) Debug.LogError("[Nobong] projectilePrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
            _score = Score;
            _inArc = CanEngage;
            OnShipLayoutChanged();
        }

        protected override void OnShipLayoutChanged()
        {
            if (Grid == null || Instance == null) return;
            weapon.SetFireArc(FireArcCalculator.Compute(Grid, Instance.Origin, Instance.RotationSteps, Instance.EffectiveHeight));
            var bonus = MagazineSupport.For(Grid, Instance, includeAmmo: true);
            weapon.SetReloadMultiplier(bonus.ReloadMultiplier);
            _ammo.ApplyBonus(bonus.CapacityMultiplier, bonus.ResupplyMultiplier);
        }

        protected override void Tick(float dt)
        {
            weapon.Tick(dt);
            _ammo.Tick(dt);
            if (_pause > 0f) _pause -= dt;
            if (_targeting == null || projectilePrefab == null) return;

            // 드론·항공기를 먼저(대공 사거리 안), 없으면 수상함
            var target = _targeting.GetBest(transform.position, AirRange, TargetClass.Air, _score, _inArc)
                         ?? _targeting.GetBest(transform.position, Stats.Range, TargetClass.Surface | TargetClass.Submarine, _score, _inArc);
            if (target == null) { _burst = 0; return; }

            Vector3 aim = LeadPoint(target);
            weapon.AimAt(aim, Stats.TurretTurnRate, dt);
            if (!weapon.IsInFireArc(aim) || !weapon.IsAimedAt(aim)) return;
            if (_pause > 0f || !_ammo.CanFire) return;
            if (!weapon.TryFire(Stats.ReloadTime * 0.5f)) return;   // 좌우 번갈아 — 한 쌍 간격은 ReloadTime

            _ammo.Consume();   // 한 발(AmmoPerShot 1)
            _lastShotTime = Time.time;
            Salvos++;
            _barrel = _barrel < 0 ? 1 : -1;
            FireRound(DamageAgainst(target), _barrel);
            if (++_burst >= burstRounds) { _burst = 0; _pause = burstPause; }
        }

        /// <summary>한 포신(−1 = 왼쪽, +1 = 오른쪽)에서 한 발.</summary>
        private void FireRound(float damage, int barrel)
        {
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            Vector3 dir = weapon.FireDirection;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 d = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), Vector3.up) *
                        Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees) * 0.5f, side) * dir;
            Vector3 from = muzzle.position + side * (barrelOffset * barrel);
            var go = PoolManager.Instance?.Spawn(projectilePrefab, from, Quaternion.LookRotation(d));
            go?.GetComponent<Projectile>()?.Launch(d, Stats.ProjectileSpeed, damage, DamageSource.Gun);
            LastBarrel = barrel;

            weapon.Kick(recoilDistance * 0.6f);   // 포신 둘이 한 덩어리라 한 발 반동은 조금 작게
            if (_muzzleVfx == null) _muzzleVfx = gameObject.AddComponent<MuzzleBurstVfx>();
            _muzzleVfx.Emit(from, dir, MuzzleBurstVfx.Style.Autocannon, muzzleSmoke, ++_salvoCount % 2 == 0);
            AudioManager.Play(Game.Data.SfxId.AutocannonShot, from, 0.8f, 0.72f);   // 기관포보다 낮은 쿵쿵
        }

        /// <summary>강화 외형(Visual_Level1~3)을 바꾼 뒤 새 포탑·고각부·포구에 다시 연결한다.</summary>
        public override void BindUpgradeVisual(Transform visualRoot)
        {
            var turret = FindVisualPart(visualRoot, "TurretPivot");
            var elevation = FindVisualPart(visualRoot, "ElevationPivot");
            var muzzle = FindVisualPart(visualRoot, "Muzzle");
            if (turret != null && elevation != null && muzzle != null)
                weapon.BindArtTransforms(turret, elevation, muzzle);
            else
                Debug.LogError("[Nobong] 강화 모델의 TurretPivot/ElevationPivot/Muzzle을 찾지 못했습니다.", this);
        }

        /// <summary>드론·항공기는 기체를, 수상함은 선체 중앙을 앞질러 겨눈다.</summary>
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

        private float Score(ITargetable t, float sqrDistance) => TargetInfo.Weighted(sqrDistance, EfficiencyAgainst(t));

        private bool CanEngage(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange && weapon.IsInFireArc(p);
        }
    }
}
