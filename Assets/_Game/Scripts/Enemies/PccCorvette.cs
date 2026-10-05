using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.Enemies
{
    /// <summary>
    /// PCC풍 엘리트 초계함. 함수 2문·함미 2문, 모두 4문의 함포로 중거리 집중포화를 퍼붓는다.
    /// 미사일은 쏘지 않는다.
    ///
    ///   - 포탑마다 따로 돌아 플레이어를 겨눈다. 함수 포탑은 뒤쪽(함교·상부 구조물), 함미 포탑은 앞쪽으로
    ///     돌지도 쏘지도 않는다(포탑별 사격 금지 구역, EnemyFireCutout).
    ///   - 네 포탑이 모두 플레이어를 향했을 때만 일제사격한다 — 현측을 돌려야 쏠 수 있다.
    ///     플레이어가 함수·함미 쪽 사각으로 파고들면 포화를 피할 수 있다.
    ///   - 한 살보 4발, 한 발 피해 = AttackDamage / 포탑 수. 포탑 사이 0.12~0.18초 간격.
    ///   - 연막 중에는 조준 사격을 멈춘다(살보 도중이라도).
    ///   - 엘리트 특성: 체력이 절반 이하가 되면 한 번, 짧게 전속 회피(속력 ×1.35, 큰 원으로 2.5초).
    ///     회복·무적 없이 위치만 바꾼다.
    /// </summary>
    public class PccCorvette : EnemyController
    {
        [System.Serializable]
        public class GunMount
        {
            public string Name;
            public WeaponController Weapon = new();
            [Tooltip("포구. 쌍열포는 두 개를 번갈아 쓴다. 비면 Weapon의 포구.")]
            public Transform[] Muzzles;
            [Tooltip("이 포탑의 사격 금지 구역(선체 선수 기준 방위)")]
            public BearingSector[] Cutouts;

            [System.NonSerialized] public int NextMuzzle;
            [System.NonSerialized] public bool ArcBuilt;
        }

        [Header("Guns")]
        [SerializeField] private GunMount[] mounts = System.Array.Empty<GunMount>();
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float turretTurnRate = 90f;
        [Tooltip("예광탄이 눈으로 따라갈 수 있을 만큼 느려야 한다")]
        [SerializeField] private float projectileSpeed = 42f;
        [Tooltip("포탑 사이 발사 간격(초, 최소~최대 무작위)")]
        [SerializeField] private Vector2 shotSpacing = new(0.12f, 0.18f);
        [Tooltip("교전 반경에 이만큼 더한 거리 안에서만 쏜다")]
        [SerializeField] private float fireRangeMargin = 10f;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField] private float muzzleFlashScale = 0.6f;

        [Header("Maneuver")]
        [SerializeField, Range(0f, 1f)] private float cruiseThrottle = 0.8f;

        [Header("Elite Evasion")]
        [SerializeField, Range(0f, 1f)] private float evadeAtHp = 0.5f;
        [SerializeField] private float evadeSpeedMultiplier = 1.35f;
        [SerializeField] private float evadeDuration = 2.5f;
        [Tooltip("회피 중 교전 원 반경 배율")]
        [SerializeField] private float evadeRadiusMultiplier = 1.8f;

        private ShipController _playerShip;
        private Coroutine _salvo;
        private bool _evadeUsed;
        private float _evadeTimer;

        public bool IsEvading => _evadeTimer > 0f;
        public bool EvadeUsed => _evadeUsed;
        public bool IsFiring => _salvo != null;
        public int MountCount => mounts != null ? mounts.Length : 0;
        /// <summary>개발·검증용: 스폰 뒤 끝낸 살보 수와 쏜 포탄 수.</summary>
        public int SalvosFired { get; private set; }
        public int ShellsFired { get; private set; }

        protected override float SpeedMultiplier => IsEvading ? evadeSpeedMultiplier : 1f;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<ShipController>() : null;
            ResetCombatState();
            BuildArcs();
        }

        protected override void UpdateBehaviour(float dt)
        {
            UpdateEvasion(dt);

            float radius = Definition.PreferredRange * (IsEvading ? evadeRadiusMultiplier : 1f);
            OrbitPlayer(radius, IsEvading ? 1f : cruiseThrottle, dt);

            Vector3 aim = AimPoint();
            foreach (var m in mounts)
            {
                if (m?.Weapon == null) continue;
                m.Weapon.Tick(dt);
                m.Weapon.AimAt(aim, turretTurnRate, dt);
            }

            if (_salvo != null || AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;

            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist > EngageRadius(Definition.PreferredRange) + fireRangeMargin) return;
            if (!AllMountsBear(aim)) return;

            AttackTimer = Definition.AttackCooldown;
            _salvo = StartCoroutine(FireSalvo());
        }

        // ------------------------------------------------------------ 함포

        /// <summary>네 포탑이 모두 사격 금지 구역 밖이고 포신이 표적을 향했는가.</summary>
        private bool AllMountsBear(Vector3 aim)
        {
            if (mounts == null || mounts.Length == 0) return false;
            foreach (var m in mounts)
                if (!CanFire(m, aim, requireAimed: true)) return false;
            return true;
        }

        private bool CanFire(GunMount m, Vector3 aim, bool requireAimed)
        {
            if (m?.Weapon == null || !m.Weapon.HasTurret) return false;
            var from = MountOrigin(m);
            if (EnemyFireCutout.IsBlocked(transform, from, aim, m.Cutouts)) return false;
            if (!m.Weapon.IsInFireArc(aim)) return false;
            return !requireAimed || m.Weapon.IsAimedAt(aim);
        }

        private IEnumerator FireSalvo()
        {
            int shots = 0;
            for (int i = 0; i < mounts.Length; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed) break;

                var m = mounts[i];
                Vector3 aim = AimPoint();
                // 살보 도중 선체가 돌아 그 포탑이 사각에 들어가면 그 포탑만 거른다(구조물 관통 금지)
                if (CanFire(m, aim, requireAimed: false))
                {
                    FireRound(m, aim);
                    shots++;
                }

                if (i < mounts.Length - 1)
                    yield return new WaitForSeconds(Random.Range(shotSpacing.x, Mathf.Max(shotSpacing.x, shotSpacing.y)));
            }

            if (shots > 0)
            {
                SalvosFired++;
                CombatLog.Add("적 함포", $"{Definition.DisplayName} 일제사격 {shots}발");
            }
            _salvo = null;
        }

        private void FireRound(GunMount m, Vector3 aim)
        {
            var muzzle = NextMuzzle(m);
            float damage = Definition.AttackDamage / Mathf.Max(1, mounts.Length);

            AudioManager.Play(Game.Data.SfxId.NavalGunShot, muzzle.position, 0.45f, 1.15f);
            PooledEffect.Spawn(muzzleFlash, muzzle.position, muzzleFlashScale);
            ShellsFired++;

            var go = projectilePrefab != null
                ? PoolManager.Instance?.Spawn(projectilePrefab, muzzle.position, Quaternion.LookRotation((aim - muzzle.position).normalized))
                : null;

            if (go == null || !go.TryGetComponent<Projectile>(out var projectile))
            {
                // 방어 코드: 탄 프리팹이 없으면 즉시 피해를 준다
                if (Player.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    target.TakeDamage(new DamageInfo(damage, Player.position, transform.forward, DamageSource.Gun));
                return;
            }

            projectile.Launch((aim - muzzle.position).normalized, projectileSpeed, damage, DamageSource.Gun);
        }

        private Transform NextMuzzle(GunMount m)
        {
            if (m.Muzzles != null && m.Muzzles.Length > 0)
            {
                for (int tries = 0; tries < m.Muzzles.Length; tries++)
                {
                    var t = m.Muzzles[m.NextMuzzle++ % m.Muzzles.Length];
                    if (t != null) return t;
                }
            }
            return m.Weapon.Muzzle != null ? m.Weapon.Muzzle : transform;
        }

        private Vector3 MountOrigin(GunMount m)
        {
            var muzzle = m.Weapon.Muzzle;
            return muzzle != null ? muzzle.position : transform.position;
        }

        /// <summary>플레이어 배가 움직이는 만큼 앞을 겨눈다. 선체 중앙 약간 위.</summary>
        private Vector3 AimPoint()
        {
            Vector3 target = Player.position + Vector3.up * 0.8f;
            if (_playerShip == null) return target;

            Vector3 velocity = Player.forward * _playerShip.CurrentSpeed;
            return Ballistics.PredictIntercept(transform.position, target, velocity, projectileSpeed);
        }

        /// <summary>
        /// 포탑 선회 제한: 선체 기준 사격 금지 구역을 포탑 설치 방향 기준 사격각(FireArc)으로 옮긴다.
        /// 포탑이 금지 구역(함교 쪽)으로 돌아가지 않게 한다. 모델이 중립 자세일 때(첫 스폰) 한 번 만든다.
        /// </summary>
        private void BuildArcs()
        {
            if (mounts == null) return;
            foreach (var m in mounts)
            {
                if (m?.Weapon == null || m.ArcBuilt || !m.Weapon.HasTurret) continue;
                m.ArcBuilt = true;
                if (m.Cutouts == null || m.Cutouts.Length == 0) continue;

                var muzzle = m.Weapon.Muzzle;
                if (muzzle == null) continue;

                // 설치 방향(중립 포신)의 선체 기준 방위. 함수 포탑 ≈ 0, 함미 포탑 ≈ 180
                Transform pivot = TurretOf(m);
                float rest = EnemyFireCutout.Bearing(transform, pivot.position, muzzle.position);

                var clearance = new byte[FireArc.Steps];
                var noTrain = new bool[FireArc.Steps];
                bool any = false;
                for (int s = 0; s < FireArc.Steps; s++)
                {
                    float rel = (s + 0.5f) * FireArc.StepDeg;
                    float shipBearing = Mathf.Repeat(rest + rel, 360f);
                    foreach (var c in m.Cutouts)
                    {
                        if (Mathf.Abs(Mathf.DeltaAngle(c.Center, shipBearing)) > c.HalfWidth) continue;
                        clearance[s] = 90;
                        noTrain[s] = true;
                        any = true;
                        break;
                    }
                }
                if (any) m.Weapon.SetFireArc(new FireArc(clearance, noTrain));
            }
        }

        private Transform TurretOf(GunMount m)
        {
            // WeaponController는 선회부를 공개하지 않으므로 포구의 조상 중 이름이 포탑인 것을 찾는다. 없으면 포구 부모.
            var muzzle = m.Weapon.Muzzle;
            for (var t = muzzle; t != null && t != transform; t = t.parent)
                if (!string.IsNullOrEmpty(m.Name) && t.name == m.Name) return t;
            return muzzle.parent != null ? muzzle.parent : muzzle;
        }

        // ------------------------------------------------------------ 엘리트 회피

        private void UpdateEvasion(float dt)
        {
            if (_evadeTimer > 0f) _evadeTimer -= dt;
            if (_evadeUsed || HpFraction > evadeAtHp) return;

            _evadeUsed = true;
            _evadeTimer = evadeDuration;
            CombatLog.Add("적 함정", $"{Definition.DisplayName} 전속 회피 기동({evadeDuration:0.#}초)");
        }

        // ------------------------------------------------------------ 풀

        public override void OnDespawned()
        {
            base.OnDespawned();
            ResetCombatState();
        }

        /// <summary>사망·스테이지 정리·풀 반환 때 사격 코루틴과 회피 상태를 지운다.</summary>
        private void ResetCombatState()
        {
            StopAllCoroutines();
            _salvo = null;
            _evadeUsed = false;
            _evadeTimer = 0f;
            SalvosFired = 0;
            ShellsFired = 0;
            if (mounts == null) return;
            foreach (var m in mounts) if (m != null) m.NextMuzzle = 0;
        }
    }
}
