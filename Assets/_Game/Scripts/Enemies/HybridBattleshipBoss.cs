using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 스테이지 2 보스: 항공전함(앞은 전함 주포, 뒤는 비행갑판). 체력에 따라 세 단계로 싸운다.
    ///
    ///   1단계 전함 모드 (100~70%): 주포 일제사격 — 수면에 착탄 원이 뜨고 2초 뒤 떨어진다. 조함·전속·연막으로 대응.
    ///   2단계 항모 모드 (70~40%):  주포 + 비행갑판에서 드론 편대 발진 + 정찰기 상시 운용(격추되면 다시 띄움).
    ///   3단계 총력전   (40~0%):   주포 연사 + 드론 + 대함미사일 일제사격, 더 가깝고 빠르게 돈다.
    ///
    /// 모든 단계에서 대공포가 날아오는 아군 미사일·로켓을 요격한다. 한 번에 쏠 수 있는 탄에 한계가 있어
    /// 여러 발을 한꺼번에 퍼붓거나 함포로 싸우면 뚫린다.
    /// 연막 중에는 조준 사격(주포·미사일)을 멈추지만 함재기 발진은 계속한다.
    ///
    /// 사격 금지 구역: 전방 주포는 뒤쪽 함교·격납고 방향(선미 ±45°)을 쏘지 못하고,
    /// 우현 대공포는 함교탑 너머 좌현 낮은 곳을 쏘지 못한다. 수면에 옅은 초록 부채꼴로 보여 준다 —
    /// 플레이어가 선미 쪽 사각으로 파고들면 주포를 피할 수 있고, 보스는 선회해 현측을 돌린다.
    /// 3단계에서는 드론 편대와 번갈아 전투기 편대를 띄운다.
    /// </summary>
    public class HybridBattleshipBoss : EnemyController
    {
        [Header("Phase")]
        [SerializeField, Range(0f, 1f)] private float carrierPhaseAt = 0.7f;
        [SerializeField, Range(0f, 1f)] private float finalPhaseAt = 0.4f;

        [Header("Main Battery")]
        [SerializeField] private GameObject shellPrefab;
        [SerializeField] private Transform[] gunMuzzles;
        [Tooltip("주포 선회부(미니어처 적 v9: MainGun_01/02). 플레이어 쪽으로 돌린다 — 선미 사격 금지 구역 쪽(±135° 밖)으로는 돌지 않는다.")]
        [SerializeField] private Transform[] mainTurrets;
        [SerializeField] private float turretTurnRate = 28f;
        [SerializeField] private float turretMaxYaw = 135f;
        [Tooltip("쉬는 자세에서 옆을 보는 포탑(현대화 이세급 v10의 중앙 포탑)은 그 방향에서 ±이만큼만 돈다 — 함교·항공갑판을 가로지르지 않게")]
        [SerializeField] private float sideTurretArc = 80f;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(1)] private int shellsPerSalvo = 4;
        [Tooltip("착탄까지 걸리는 시간 = 경고 원이 떠 있는 시간")]
        [SerializeField] private float shellFlightTime = 2.2f;
        [SerializeField] private float shellDamage = 14f;
        [SerializeField] private float shellBlastRadius = 3f;
        [Tooltip("착탄점이 예측 위치 주변으로 흩어지는 반경")]
        [SerializeField] private float salvoSpread = 5f;
        [SerializeField] private float gunRange = 45f;
        [Tooltip("단계별 일제사격 간격(초)")]
        [SerializeField] private float[] salvoInterval = { 7f, 9f, 5f };
        [Tooltip("주포 사격 금지 구역(선체 기준 방위). 기본: 선미 ±45° — 함교·격납고")]
        [SerializeField] private BearingSector[] mainBatteryCutouts = { new BearingSector(180f, 45f) };

        [Header("Flight Deck")]
        [SerializeField] private Transform flightDeckLaunch;
        [SerializeField, Min(1)] private int dronesPerWave = 4;
        [SerializeField] private float droneLaunchSpacing = 0.5f;
        [Tooltip("2·3단계 드론 발진 간격(초)")]
        [SerializeField] private float[] droneWaveInterval = { 14f, 11f };
        [SerializeField] private float reconRelaunchDelay = 20f;
        [Tooltip("3단계에서 드론 편대 대신 번갈아 띄우는 전투기 수")]
        [SerializeField, Min(1)] private int fightersPerWave = 2;

        [Header("Missile Salvo (3단계)")]
        [SerializeField] private Transform[] missileLaunchPoints;
        [SerializeField, Min(1)] private int missilesPerSalvo = 8;
        [SerializeField] private float missileSalvoInterval = 10f;
        [SerializeField] private float missileSpacing = 0.15f;

        [Header("Anti-Air")]
        [SerializeField] private Transform aaMount;
        [SerializeField] private GameObject aaProjectilePrefab;
        [SerializeField] private float aaRange = 16f;
        [SerializeField] private float aaProjectileSpeed = 70f;
        [SerializeField] private float aaShotInterval = 0.1f;
        [Tooltip("연속으로 쏠 수 있는 발 수. 다 쏘면 식힌다 — 동시에 많이 날아오면 뚫린다.")]
        [SerializeField, Min(1)] private int aaBurstRounds = 10;
        [SerializeField] private float aaCooldown = 1.5f;
        [SerializeField] private float aaSpreadDegrees = 2.5f;
        [Tooltip("대공포 사격 금지 구역. 기본: 좌현 ±60°(함교탑 너머), 앙각 35° 이상은 넘겨 쏜다")]
        [SerializeField] private BearingSector[] aaCutouts = { new BearingSector(270f, 60f, 35f) };

        [Header("Visual")]
        [Tooltip("마스트 회전 레이더(선택). 월드 수직축으로 돈다.")]
        [SerializeField] private Transform radar;
        [SerializeField] private float radarRpm = 15f;
        [Tooltip("사격 금지 구역 표시(수면 부채꼴) 머티리얼. 비우면 표시하지 않는다")]
        [SerializeField] private Material cutoutZoneMaterial;
        [SerializeField] private float cutoutZoneRadius = 18f;

        private int _phase;
        private float _salvoTimer, _droneTimer, _missileTimer, _reconTimer;
        private float _aaTimer;
        private int _aaRounds;
        private int _waveCount;
        private bool _indicatorBuilt;
        private readonly System.Collections.Generic.List<Transform> _bearingMuzzles = new(8);
        private EnemyController _recon;
        private Collider _playerHull;
        private Game.Ship.ShipController _playerShip;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _phase = 0;
            _salvoTimer = 4f;
            _droneTimer = 3f;
            _missileTimer = 4f;
            _reconTimer = 2f;
            _aaRounds = aaBurstRounds;
            _recon = null;
            _waveCount = 0;

            // 수면 표시는 주포 금지 구역만(대공포는 앙각에 따라 달라 표시하면 오히려 헷갈린다)
            if (!_indicatorBuilt)
            {
                _indicatorBuilt = true;
                EnemyFireCutout.BuildIndicator(transform, mainBatteryCutouts, cutoutZoneRadius, cutoutZoneMaterial);
            }
            _playerHull = player != null ? player.GetComponent<Collider>() : null;
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
            GameEvents.RaiseBossPhaseChanged("항공전함 출현 — 주포 착탄 원을 피하라");
        }

        /// <summary>보스는 기함과의 결투로 설계됐다 — 호위함으로 표적을 돌리지 않는다.</summary>
        protected override bool TargetsEscorts => false;

        protected override void UpdateBehaviour(float dt)
        {
            UpdatePhase();

            float range = Definition.PreferredRange * (_phase == 2 ? 0.7f : 1f);
            OrbitPlayer(range, _phase == 2 ? 0.9f : 0.6f, dt);

            // 정찰기가 표적을 지시하면 보스의 모든 패턴도 빨라진다
            float tick = dt * (ReconAircraft.ActiveCount > 0 ? ReconAircraft.SpottedAttackRate : 1f);

            UpdateTurrets(dt);
            UpdateMainBattery(tick);
            if (_phase >= 1) UpdateFlightDeck(tick);
            if (_phase >= 2) UpdateMissileSalvo(tick);
            UpdateAntiAir(dt);

            if (radar != null) radar.Rotate(Vector3.up, radarRpm * 6f * dt, Space.World);
        }

        private void UpdatePhase()
        {
            int next = HpFraction <= finalPhaseAt ? 2 : HpFraction <= carrierPhaseAt ? 1 : 0;
            if (next <= _phase) return;

            _phase = next;
            _droneTimer = 1.5f;
            GameEvents.RaiseBossPhaseChanged(_phase == 1
                ? "항모 모드 — 비행갑판에서 드론 발진"
                : "총력전 — 미사일 일제사격 · 전투기 발진");
        }

        // ------------------------------------------------------------ 주포

        private void UpdateMainBattery(float tick)
        {
            _salvoTimer -= tick;
            if (_salvoTimer > 0f) return;
            if (PlayerConcealed) { _salvoTimer = 0.5f; return; }

            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist > gunRange) { _salvoTimer = 1f; return; }

            // 착탄 시각의 플레이어 위치를 예측하고, 그쪽이 사격 금지 구역이 아닌 포구만 쓴다
            Vector3 velocity = _playerShip != null ? Player.forward * _playerShip.CurrentSpeed : Vector3.zero;
            Vector3 predicted = Player.position + velocity * shellFlightTime;
            predicted.y = 0f;

            _bearingMuzzles.Clear();
            if (gunMuzzles != null)
                foreach (var m in gunMuzzles)
                    if (m != null && !EnemyFireCutout.IsBlocked(transform, m.position, predicted, mainBatteryCutouts))
                        _bearingMuzzles.Add(m);

            // 포가 하나도 향하지 못하면(플레이어가 선미 사각에 있으면) 잠깐 기다린다. 그동안 선회해 현측을 돌린다.
            if (_bearingMuzzles.Count == 0 && (gunMuzzles != null && gunMuzzles.Length > 0 ||
                                               EnemyFireCutout.IsBlocked(transform, transform.position, predicted, mainBatteryCutouts)))
            {
                _salvoTimer = 0.5f;
                return;
            }

            _salvoTimer = salvoInterval[Mathf.Clamp(_phase, 0, salvoInterval.Length - 1)];
            StartCoroutine(FireSalvo(predicted, _bearingMuzzles.ToArray()));
        }

        private Quaternion[] _turretRest;
        private float[] _turretYaw;
        private float[] _turretRestBearing;   // 쉬는 자세의 포신 방위(함 기준, 0 = 선수)

        /// <summary>
        /// 주포를 플레이어 쪽으로 천천히 돌린다(장식 — 착탄 계산은 포구 위치에서 따로 한다). 함 기준 각도로 계산한다.
        /// 앞을 보는 포탑은 ±turretMaxYaw 안에서, 옆을 보는 포탑은 쉬는 방향 ±sideTurretArc 안에서 직선으로 움직여
        /// 선미(사격 금지 구역)·함교를 가로질러 돌지 않는다.
        /// </summary>
        private void UpdateTurrets(float dt)
        {
            if (mainTurrets == null || mainTurrets.Length == 0 || Player == null) return;
            if (_turretRest == null || _turretRest.Length != mainTurrets.Length)
            {
                _turretRest = new Quaternion[mainTurrets.Length];
                _turretYaw = new float[mainTurrets.Length];
                _turretRestBearing = new float[mainTurrets.Length];
                for (int i = 0; i < mainTurrets.Length; i++)
                {
                    var t = mainTurrets[i];
                    if (t == null) continue;
                    _turretRest[i] = t.localRotation;
                    // 쉬는 방위: 포탑 → 그 아래 가장 먼 포구(없으면 선수)
                    Vector3 far = t.position;
                    foreach (var c in t.GetComponentsInChildren<Transform>())
                        if (c.name.Contains("Muzzle") && (c.position - t.position).sqrMagnitude > (far - t.position).sqrMagnitude) far = c.position;
                    Vector3 d = transform.InverseTransformPoint(far) - transform.InverseTransformPoint(t.position);
                    _turretRestBearing[i] = d.sqrMagnitude > 1e-4f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
                }
            }
            Vector3 local = transform.InverseTransformPoint(Player.position);
            float bearing = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            for (int i = 0; i < mainTurrets.Length; i++)
            {
                var t = mainTurrets[i];
                if (t == null) continue;
                float rest = _turretRestBearing[i];
                float want = Mathf.Abs(rest) < 30f
                    ? Mathf.Clamp(bearing, -turretMaxYaw, turretMaxYaw) - rest                 // 앞을 보는 포탑
                    : Mathf.Clamp(Mathf.DeltaAngle(rest, bearing), -sideTurretArc, sideTurretArc);   // 옆을 보는 포탑
                _turretYaw[i] = Mathf.MoveTowards(_turretYaw[i], want, turretTurnRate * dt);
                Vector3 axis = t.parent != null ? t.parent.InverseTransformDirection(transform.up) : Vector3.up;
                t.localRotation = Quaternion.AngleAxis(_turretYaw[i], axis) * _turretRest[i];
            }
        }

        private IEnumerator FireSalvo(Vector3 predicted, Transform[] muzzles)
        {
            if (shellPrefab == null) yield break;

            for (int i = 0; i < shellsPerSalvo; i++)
            {
                if (!IsAlive || Player == null) yield break;

                var muzzle = muzzles.Length > 0 ? muzzles[i % muzzles.Length] : transform;
                if (muzzle == null) continue;

                // 한 발은 예측점에 정확히, 나머지는 주변에 흩뿌린다
                Vector2 jitter = i == 0 ? Vector2.zero : Random.insideUnitCircle * salvoSpread;
                Vector3 impact = predicted + new Vector3(jitter.x, 0f, jitter.y);

                var go = PoolManager.Instance?.Spawn(shellPrefab, muzzle.position, muzzle.rotation);
                go?.GetComponent<ArtilleryShell>()?.Launch(muzzle.position, impact, shellFlightTime,
                                                           shellDamage, shellBlastRadius, _playerHull);

                PooledEffect.Spawn(muzzleFlash, muzzle.position);
                AudioManager.Play(Game.Data.SfxId.NavalGunShot, muzzle.position, 1f, 0.6f);

                yield return new WaitForSeconds(0.15f);
            }
        }

        // ------------------------------------------------------------ 비행갑판

        private void UpdateFlightDeck(float tick)
        {
            var spawner = EnemySpawner.Instance;
            if (spawner == null) return;
            var origin = flightDeckLaunch != null ? flightDeckLaunch : transform;

            _droneTimer -= tick;
            if (_droneTimer <= 0f && Definition.LaunchedDrone != null)
            {
                _droneTimer = droneWaveInterval[Mathf.Clamp(_phase - 1, 0, droneWaveInterval.Length - 1)];
                _waveCount++;

                bool fighters = _phase >= 2 && Definition.LaunchedFighter != null && _waveCount % 2 == 0;
                StartCoroutine(fighters
                    ? LaunchWave(spawner, origin, Definition.LaunchedFighter, fightersPerWave, 0.9f)
                    : LaunchWave(spawner, origin, Definition.LaunchedDrone, dronesPerWave, droneLaunchSpacing));
            }

            // 정찰기는 한 대를 계속 띄워 둔다
            bool reconAlive = _recon != null && _recon.IsAlive && _recon.Definition == Definition.LaunchedRecon;
            if (reconAlive || Definition.LaunchedRecon == null) return;

            _reconTimer -= tick;
            if (_reconTimer > 0f) return;
            _reconTimer = reconRelaunchDelay;
            _recon = spawner.SpawnAt(Definition.LaunchedRecon, origin.position, LaunchRotation(origin));
        }

        private IEnumerator LaunchWave(EnemySpawner spawner, Transform origin, Game.Data.EnemyDefinition def, int count, float spacing)
        {
            for (int i = 0; i < count; i++)
            {
                if (!IsAlive) yield break;
                spawner.SpawnAt(def, origin.position, LaunchRotation(origin));
                AudioManager.Play(Game.Data.SfxId.MissileLaunch, origin.position, 0.3f, 1.4f);
                yield return new WaitForSeconds(spacing);
            }
        }

        /// <summary>갑판 앞쪽으로, 살짝 들린 채 이륙한다.</summary>
        private Quaternion LaunchRotation(Transform origin)
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            return Quaternion.LookRotation((f.normalized + Vector3.up * 0.35f).normalized, Vector3.up);
        }

        // ------------------------------------------------------------ 대함미사일 (3단계)

        private void UpdateMissileSalvo(float tick)
        {
            _missileTimer -= tick;
            if (_missileTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;   // 주포는 곡사라 섬 너머로도 쏜다
            _missileTimer = missileSalvoInterval;
            StartCoroutine(FireMissileSalvo());
        }

        private IEnumerator FireMissileSalvo()
        {
            if (Definition.MissilePrefab == null) yield break;

            for (int i = 0; i < missilesPerSalvo; i++)
            {
                if (!IsAlive || Player == null) yield break;

                var origin = missileLaunchPoints != null && missileLaunchPoints.Length > 0 && missileLaunchPoints[i % missileLaunchPoints.Length] != null
                    ? missileLaunchPoints[i % missileLaunchPoints.Length]
                    : transform;

                var go = PoolManager.Instance?.Spawn(Definition.MissilePrefab, origin.position, Quaternion.LookRotation(Vector3.up));
                go?.GetComponent<Missile>()?.Launch(Player, Definition.AttackDamage);

                yield return new WaitForSeconds(missileSpacing);
            }
        }

        // ------------------------------------------------------------ 대공포

        /// <summary>반경 안으로 들어온 아군 미사일·로켓 중 가장 가까운 것을 예측 사격한다. 연사 한도가 있다.</summary>
        private void UpdateAntiAir(float dt)
        {
            if (aaProjectilePrefab == null) return;

            _aaTimer -= dt;
            if (_aaTimer > 0f) return;

            var mount = aaMount != null ? aaMount : transform;
            Missile target = null;
            float best = aaRange * aaRange;

            var list = Missile.ActiveFriendly;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || !m.IsAlive) continue;
                float d = (m.transform.position - mount.position).sqrMagnitude;
                if (d >= best) continue;
                if (EnemyFireCutout.IsBlocked(transform, mount.position, m.transform.position, aaCutouts)) continue;
                best = d;
                target = m;
            }

            if (target == null)
            {
                // 쏠 것이 없으면 탄창을 서서히 채운다
                if (_aaRounds < aaBurstRounds) { _aaRounds++; _aaTimer = aaCooldown / aaBurstRounds; }
                return;
            }

            if (_aaRounds <= 0)
            {
                _aaRounds = aaBurstRounds;
                _aaTimer = aaCooldown;
                return;
            }

            Vector3 aim = Ballistics.PredictIntercept(mount.position, target.transform.position, target.Velocity, aaProjectileSpeed);
            Vector3 dir = (aim - mount.position).normalized;
            dir = Quaternion.Euler(Random.Range(-aaSpreadDegrees, aaSpreadDegrees), Random.Range(-aaSpreadDegrees, aaSpreadDegrees), 0f) * dir;

            var go = PoolManager.Instance?.Spawn(aaProjectilePrefab, mount.position, Quaternion.LookRotation(dir));
            go?.GetComponent<Projectile>()?.Launch(dir, aaProjectileSpeed, 1f, DamageSource.Gun);
            AudioManager.Play(Game.Data.SfxId.AutocannonShot, mount.position, 0.3f, 0.9f);

            _aaRounds--;
            _aaTimer = aaShotInterval;
        }

        protected override void Die()
        {
            StopAllCoroutines();
            base.Die();
            GameEvents.RaiseBossDefeated();
        }
    }
}
