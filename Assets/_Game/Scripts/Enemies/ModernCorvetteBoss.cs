using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Ship;
using Game.View;

namespace Game.Enemies
{
    /// <summary>
    /// 스테이지 2 보스: 현대화 초계함(2026-10-03, Codex ModernCorvette v11.1 모델, 20m).
    /// 선수·선미 76mm 함포, 중앙 좌우 경사 미사일 발사대(4관, 고정식 — 돌지 않음), 좌우 3연장 어뢰 발사관(플레이어 쪽으로 ±35° 회전).
    /// 거대한 주포·항공갑판은 없다.
    ///
    /// 거리를 오가며 싸운다: 원거리(44m) → 중거리(30m) → 근거리(19m)를 돌아가며 선회 반경을 바꾸고, 거리마다 공격이 다르다.
    ///   - 원거리(32m 밖): 플레이어와 마주 보는 현측 발사대에서만 대함미사일 2발 시간차 발사(발사대 고정식). 탄수를 늘리지 않고
    ///     접근 방향을 바꾼다 — 일제사격마다 ±75° / ±30°를 번갈아 돌아 들어와 두 방위에서 온다(<see cref="Missile.SetApproach"/>).
    ///   - 중거리(12~42m): 선수·선미 함포가 번갈아 예고 사격(수면 경고 원, 포탄 비행 2.4초). 두 포가 같은 곳을 동시에 덮지 않게
    ///     아직 떨어지지 않은 착탄점과 겹치면 플레이어 진행 방향의 옆으로 비켜 겨눈다. 함포마다 함교를 넘어 돌지 않는다(±150°).
    ///   - 근거리(26m 안, 플레이어가 현측 쪽일 때): 그쪽 측면 발사관이 플레이어 쪽으로 돌고 붉은 등이 1.8초 깜빡인 뒤(사격 제원은 그 순간에 고정)
    ///     어뢰 2발 — 하나는 지금 침로·속력의 만나는 점, 하나는 그보다 앞쪽. 수상함 어뢰라 소나 없이도 보인다(수면 항적).
    ///   - 체력 50% 이하: 공격 간격 ×0.75, 거리 순환도 빨라지고, 어뢰 뒤에 함포 예고 사격을 이어 쏜다. 연기(75%부터)·화재(50%부터).
    /// 연막 안이거나 섬 뒤면 쏘지 않는다. 정찰기가 있으면 재장전이 빨라진다(다른 적과 같다).
    /// </summary>
    public class ModernCorvetteBoss : EnemyController
    {
        [Header("Range cycle (원거리 → 중거리 → 근거리)")]
        [SerializeField] private float[] cycleRanges = { 44f, 30f, 19f };
        [SerializeField] private float[] cycleSeconds = { 11f, 10f, 9f };

        [Header("Missiles (원거리)")]
        [SerializeField] private Transform[] missileLaunchPoints;
        [SerializeField] private float missileMinRange = 32f;
        [SerializeField] private float missileMaxRange = 80f;
        [SerializeField] private float missileInterval = 12f;
        [SerializeField] private float missileSpacing = 0.55f;
        [Tooltip("발사 위치→플레이어 선 기준으로 돌아 들어오는 각도. 일제사격(2발)마다 두 개씩 차례로 쓴다")]
        [SerializeField] private float[] missileApproachAngles = { -75f, 75f, -30f, 30f };
        [SerializeField] private float missileApproachRadius = 22f;

        [Header("Guns (중거리 예고 사격)")]
        [SerializeField] private GameObject shellPrefab;
        [SerializeField] private GameObject gunFlashMaterialSource;
        [SerializeField] private float gunMinRange = 12f;
        [SerializeField] private float gunMaxRange = 42f;
        [SerializeField] private float gunInterval = 7f;
        [SerializeField, Min(1)] private int shotsPerVolley = 4;
        [SerializeField] private float shotSpacing = 1.0f;
        [SerializeField] private float shellFlightTime = 2.4f;
        [SerializeField] private float shellDamage = 12f;
        [SerializeField] private float shellRadius = 3f;
        [SerializeField] private float gunTurnRate = 90f;

        [Header("Torpedoes (근거리)")]
        [SerializeField] private GameObject torpedoPrefab;
        [SerializeField] private Transform[] torpedoLaunchPoints;
        [SerializeField] private Renderer[] torpedoLamps;
        [SerializeField] private Color lampColor = new(1f, 0.18f, 0.08f);
        [SerializeField] private float torpedoRange = 26f;
        [SerializeField] private float torpedoInterval = 11f;
        [SerializeField] private float torpedoWarning = 1.8f;
        [SerializeField] private float torpedoDamage = 30f;
        [Tooltip("두 번째 어뢰는 만나는 점보다 플레이어 진행 방향 쪽으로 이만큼(°) 더 앞을 겨눈다")]
        [SerializeField] private float torpedoLeadSpread = 9f;
        [Tooltip("발사관이 플레이어 쪽으로 도는 최대 각도")]
        [SerializeField] private float launcherTrainLimit = 35f;

        [Header("Damage (체력 50% 이하)")]
        [SerializeField, Range(0f, 1f)] private float hurtAt = 0.5f;
        [SerializeField] private float hurtIntervalScale = 0.75f;
        [SerializeField] private float chainGunDelay = 1.2f;

        [Header("Model")]
        [SerializeField] private Transform radar;
        [SerializeField] private float radarRpm = 18f;

        private readonly WeaponController _bowGun = new();
        private readonly WeaponController _aftGun = new();
        private ShipController _playerShip;
        private Collider _playerHull;
        private MuzzleBurstVfx _muzzleFx;
        private Coroutine _attack;
        private float _missileTimer, _gunTimer, _torpedoTimer, _cycleTimer;
        private int _cycle, _damageStage, _missileSalvos;
        private bool _useAftNext, _preparing;
        private float _smokeDebt, _fireDebt;
        private readonly List<(Vector3 point, float lands)> _pending = new();
        private MaterialPropertyBlock _mpb;

        // 회전부 기본 자세(풀에서 다시 나와도 같은 자세에서 시작)
        private bool _restCaptured;
        private Transform _bowTurret, _aftTurret, _bowElevation, _aftElevation;
        private Quaternion _bowRest, _aftRest, _bowElevRest, _aftElevRest;
        private readonly List<Launcher> _launchers = new();
        private Transform _bridgeSmoke, _engineSmoke, _aftFire;

        private sealed class Launcher
        {
            public Transform Pivot;
            public Quaternion Rest;
            public Vector3 RestOutward;   // 함 기준(로컬) 바깥 방향
            public bool Starboard, Torpedo;
            public float Yaw, WantYaw;
            public readonly List<Transform> Points = new();
            public readonly List<Renderer> Lamps = new();
        }

        /// <summary>경고 표시용: 어뢰 사격 제원을 정하고 발사관 등이 깜빡이는 중.</summary>
        public bool IsPreparingTorpedo => _preparing;

        /// <summary>보스는 기함과의 결투로 설계됐다 — 호위함으로 표적을 돌리지 않는다.</summary>
        protected override bool TargetsEscorts => false;

        private bool Hurt => _damageStage >= 2;
        private float IntervalScale => Hurt ? hurtIntervalScale : 1f;

        public override void Setup(EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<ShipController>() : null;
            _playerHull = player != null ? player.GetComponent<Collider>() : null;
            if (_attack != null) StopCoroutine(_attack);
            _attack = null;
            _missileTimer = 3f;
            _gunTimer = 4f;
            _torpedoTimer = 5f;
            _cycle = 0;
            _missileSalvos = 0;
            _cycleTimer = cycleSeconds.Length > 0 ? cycleSeconds[0] : 10f;
            _damageStage = 0;
            _smokeDebt = _fireDebt = 0f;
            _useAftNext = false;
            _preparing = false;
            _pending.Clear();

            BindModel();
            _muzzleFx ??= GetComponent<MuzzleBurstVfx>() ?? gameObject.AddComponent<MuzzleBurstVfx>();
            SetLamps(null, 0f);
            GameEvents.RaiseBossPhaseChanged("현대화 초계함 출현 — 원거리 미사일 · 중거리 함포 · 근거리 어뢰");
        }

        /// <summary>모델 소켓을 이름으로 찾는다(프리팹에 연결이 없어도 동작). 회전부는 처음 자세를 기억해 두고 다시 나올 때 되돌린다.</summary>
        private void BindModel()
        {
            var all = GetComponentsInChildren<Transform>(true);
            Transform Find(string n) { foreach (var t in all) if (t.name == n) return t; return null; }

            _bowTurret = Find("BowGun_TurretPivot");
            _aftTurret = Find("AftGun_TurretPivot");
            _bowElevation = Find("BowGun_ElevationPivot");
            _aftElevation = Find("AftGun_ElevationPivot");
            if (!_restCaptured)
            {
                _restCaptured = true;
                if (_bowTurret != null) _bowRest = _bowTurret.localRotation;
                if (_aftTurret != null) _aftRest = _aftTurret.localRotation;
                if (_bowElevation != null) _bowElevRest = _bowElevation.localRotation;
                if (_aftElevation != null) _aftElevRest = _aftElevation.localRotation;
                _launchers.Clear();
                // 미사일 발사대는 고정식(돌리지 않는다 — 모델 자세 그대로 발사점에서 쏜다). 도는 것은 어뢰 발사관뿐.
                foreach (var n in new[] { "PortTorpedoLauncherPivot", "StarboardTorpedoLauncherPivot" })
                {
                    var p = Find(n);
                    if (p == null) continue;
                    var l = new Launcher { Pivot = p, Rest = p.localRotation, Torpedo = n.Contains("Torpedo"), Starboard = n.StartsWith("Starboard") };
                    string prefix = l.Torpedo ? "TorpedoLaunchPoint_" : "MissileLaunchPoint_";
                    foreach (var t in p.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith(prefix, System.StringComparison.Ordinal)) l.Points.Add(t);
                    l.Points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    if (torpedoLamps != null)
                        foreach (var r in torpedoLamps)
                            if (r != null && r.transform.IsChildOf(p)) l.Lamps.Add(r);
                    // 바깥 방향 = 피벗에서 발사점 쪽(없으면 현측). 실제 위치로 정해 FBX 축 보정에 상관없게 한다.
                    Vector3 outward = l.Points.Count > 0
                        ? transform.InverseTransformPoint(l.Points[0].position) - transform.InverseTransformPoint(p.position)
                        : new Vector3(l.Starboard ? 1f : -1f, 0f, 0f);
                    outward.y = 0f;
                    l.RestOutward = outward.sqrMagnitude > 1e-4f ? outward.normalized : new Vector3(l.Starboard ? 1f : -1f, 0f, 0f);
                    // 현측 판정도 실제 위치로
                    l.Starboard = transform.InverseTransformPoint(p.position).x >= 0f;
                    _launchers.Add(l);
                }
            }
            else
            {
                if (_bowTurret != null) _bowTurret.localRotation = _bowRest;
                if (_aftTurret != null) _aftTurret.localRotation = _aftRest;
                if (_bowElevation != null) _bowElevation.localRotation = _bowElevRest;
                if (_aftElevation != null) _aftElevation.localRotation = _aftElevRest;
                foreach (var l in _launchers) { l.Pivot.localRotation = l.Rest; l.Yaw = l.WantYaw = 0f; }
            }

            _bowGun.BindArtTransforms(_bowTurret, _bowElevation, Find("BowGun_Muzzle"));
            _aftGun.BindArtTransforms(_aftTurret, _aftElevation, Find("AftGun_Muzzle"));
            var arc = LimitedGunArc(150f);   // 함교·마스트를 가로질러 돌지 않는다
            _bowGun.SetFireArc(arc);
            _aftGun.SetFireArc(arc);

            if (missileLaunchPoints == null || missileLaunchPoints.Length == 0)
            {
                var list = new List<Transform>();
                foreach (var t in all) if (t.name.StartsWith("MissileLaunchPoint_", System.StringComparison.Ordinal)) list.Add(t);
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                missileLaunchPoints = list.ToArray();
            }
            if (radar == null) radar = Find("RadarPivot");
            _bridgeSmoke = Find("BridgeSmokePoint");
            _engineSmoke = Find("EngineSmokePoint") ?? Find("FunnelTop");
            _aftFire = Find("AftFirePoint");
        }

        protected override void UpdateBehaviour(float dt)
        {
            float distance = Vector3.Distance(transform.position, Player.position);

            // 거리 순환: 원거리 → 중거리 → 근거리. 다친 뒤엔 더 빨리 돈다.
            _cycleTimer -= dt;
            if (_cycleTimer <= 0f && cycleRanges.Length > 0)
            {
                _cycle = (_cycle + 1) % cycleRanges.Length;
                float sec = cycleSeconds.Length > 0 ? cycleSeconds[Mathf.Min(_cycle, cycleSeconds.Length - 1)] : 10f;
                _cycleTimer = sec * (Hurt ? 0.8f : 1f);
            }
            float desired = cycleRanges.Length > 0 ? cycleRanges[_cycle] : Definition.PreferredRange;
            OrbitPlayer(desired, Hurt ? 0.85f : 0.7f, dt);

            if (radar != null) radar.Rotate(Vector3.up, radarRpm * 6f * dt, Space.World);
            EmitDamageFx(dt);
            UpdateLaunchers(dt);
            _bowGun.Tick(dt);
            _aftGun.Tick(dt);
            Vector3 lob = Player.position + Vector3.up * (1f + distance * 0.12f);   // 곡사처럼 포신을 조금 든다
            _bowGun.AimAt(lob, gunTurnRate, dt);
            _aftGun.AimAt(lob, gunTurnRate, dt);
            _pending.RemoveAll(p => p.lands < Time.time);

            float tick = dt * (ReconAircraft.ActiveCount > 0 ? ReconAircraft.SpottedAttackRate : 1f);
            _missileTimer -= tick;
            _gunTimer -= tick;
            _torpedoTimer -= tick;
            if (_attack != null || PlayerConcealed || PlayerBehindIsland) return;

            // 가까운 거리의 공격부터
            if (_torpedoTimer <= 0f && distance <= torpedoRange && torpedoPrefab != null && PlayerAbeam(out bool starboard))
            {
                _torpedoTimer = torpedoInterval * IntervalScale;
                _attack = StartCoroutine(TorpedoAttack(starboard));
            }
            else if (_gunTimer <= 0f && distance >= gunMinRange && distance <= gunMaxRange && shellPrefab != null)
            {
                _gunTimer = gunInterval * IntervalScale;
                _attack = StartCoroutine(GunVolley());
            }
            else if (_missileTimer <= 0f && distance >= missileMinRange && distance <= missileMaxRange && Definition.MissilePrefab != null)
            {
                _missileTimer = missileInterval * IntervalScale;
                _attack = StartCoroutine(MissileSalvo());
            }
        }

        // ------------------------------------------------------------ 원거리: 미사일

        /// <summary>
        /// 플레이어와 마주 보는 현측의 발사대에서만 2발(그 현측 발사점 2개). 발사대는 고정식이라 반대편 발사대는 쓰지 않는다.
        /// 접근 각도는 missileApproachAngles에서 일제사격마다 두 개씩 차례로 쓴다(기본: ±75° → ±30° → …) — 넓게·좁게 번갈아 돌아 들어온다.
        /// </summary>
        private IEnumerator MissileSalvo()
        {
            GameEvents.RaiseBossPhaseChanged("초계함 대함미사일 2발 — 두 방위에서 접근");
            bool starboard = transform.InverseTransformPoint(Player.position).x >= 0f;
            var sockets = new List<Transform>(2);
            if (missileLaunchPoints != null)
                foreach (var t in missileLaunchPoints)
                    if (t != null && (transform.InverseTransformPoint(t.position).x >= 0f) == starboard) sockets.Add(t);

            const int count = 2;
            for (int i = 0; i < count; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed) break;
                Transform socket = sockets.Count > 0 ? sockets[i % sockets.Count] : null;
                Vector3 pos = socket != null ? socket.position
                    : transform.position + Vector3.up * 2f + transform.right * (starboard ? 1.5f : -1.5f);
                Quaternion rot = socket != null ? socket.rotation
                    : Quaternion.LookRotation(transform.right * (starboard ? 1f : -1f) + Vector3.up * 0.4f);
                var go = PoolManager.Instance?.Spawn(Definition.MissilePrefab, pos, rot);
                var missile = go != null ? go.GetComponent<Missile>() : null;
                if (missile != null)
                {
                    missile.Launch(Player, Definition.AttackDamage);
                    if (missileApproachAngles != null && missileApproachAngles.Length > 0)
                        missile.SetApproach(missileApproachAngles[(_missileSalvos * count + i) % missileApproachAngles.Length], missileApproachRadius);
                }
                if (i < count - 1) yield return new WaitForSeconds(missileSpacing);
            }
            _missileSalvos++;
            _attack = null;
        }

        // ------------------------------------------------------------ 중거리: 함포 예고 사격

        private IEnumerator GunVolley()
        {
            yield return FireGuns(shotsPerVolley);
            _attack = null;
        }

        private IEnumerator FireGuns(int shots)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < shots; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed || PlayerBehindIsland) yield break;
                Vector3 target = Player.position;
                bool bowBears = _bowGun.Muzzle != null && _bowGun.IsInFireArc(target);
                bool aftBears = _aftGun.Muzzle != null && _aftGun.IsInFireArc(target);
                if (bowBears || aftBears)
                {
                    var gun = aftBears && (!bowBears || _useAftNext) ? _aftGun : _bowGun;
                    _useAftNext = !_useAftNext;
                    side = -side;
                    FireShell(gun.Muzzle, ImpactPoint(side));
                }
                yield return new WaitForSeconds(shotSpacing);
            }
        }

        /// <summary>
        /// 착탄점: 포탄이 떨어질 때 플레이어가 있을 곳. 아직 떨어지지 않은 다른 착탄점과 겹치면(두 원이 같은 곳을 덮으면)
        /// 플레이어 진행 방향의 옆(side 쪽)으로 비켜 겨눈다 — 두 포가 한 지점을 동시에 덮지 않게.
        /// </summary>
        private Vector3 ImpactPoint(float side)
        {
            Vector3 fwd = Player.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            float speed = _playerShip != null ? _playerShip.CurrentSpeed : 0f;
            Vector3 p = Player.position + fwd * (speed * shellFlightTime);
            p.y = 0f;
            float minSep = shellRadius * 2f + 1.5f;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            for (int k = 0; k < 3; k++)
            {
                bool clash = false;
                foreach (var q in _pending)
                    if ((q.point - p).sqrMagnitude < minSep * minSep) { clash = true; break; }
                if (!clash) break;
                p += right * (side * minSep);
            }
            return p;
        }

        private void FireShell(Transform muzzle, Vector3 impact)
        {
            var go = PoolManager.Instance?.Spawn(shellPrefab, muzzle.position, muzzle.rotation);
            var shell = go != null ? go.GetComponent<ArtilleryShell>() : null;
            if (shell == null) return;
            shell.Launch(muzzle.position, impact, shellFlightTime, shellDamage, shellRadius, _playerHull);
            _pending.Add((impact, Time.time + shellFlightTime));
            Vector3 dir = (impact + Vector3.up * 6f - muzzle.position).normalized;
            _muzzleFx.Emit(muzzle.position, dir, MuzzleBurstVfx.Style.NavalGun, gunFlashMaterialSource, true);
            AudioManager.Play(SfxId.NavalGunShot, muzzle.position, 0.85f, 0.95f);
        }

        // ------------------------------------------------------------ 근거리: 측면 어뢰

        /// <summary>플레이어가 현측 쪽(선수·선미 35° 밖)에 있는가. starboard = 우현 쪽.</summary>
        private bool PlayerAbeam(out bool starboard)
        {
            Vector3 local = transform.InverseTransformPoint(Player.position);
            starboard = local.x >= 0f;
            float bearing = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
            return bearing > 35f && bearing < 145f;
        }

        private IEnumerator TorpedoAttack(bool starboard)
        {
            var launcher = _launchers.Find(l => l.Torpedo && l.Starboard == starboard);
            // 사격 제원: 경고가 시작되는 순간의 위치·침로·속력으로 고정한다 — 경고 뒤 조함이 곧 회피
            Vector3 solutionPos = Player.position;
            Vector3 fwd = Player.forward; fwd.y = 0f;
            Vector3 solutionVel = _playerShip != null && fwd.sqrMagnitude > 1e-6f ? fwd.normalized * _playerShip.CurrentSpeed : Vector3.zero;
            float solutionTime = Time.time;

            _preparing = true;
            GameEvents.RaiseBossPhaseChanged($"초계함 {(starboard ? "우현" : "좌현")} 어뢰 발사관 — 2발 경고");
            CombatLog.Add("어뢰", $"{Definition.DisplayName} {(starboard ? "우현" : "좌현")} 발사관 사격 제원 결정 · {torpedoWarning:0.0}초 뒤 2발");
            AudioManager.Play(SfxId.SonarPing, transform.position, 0.9f, 2.1f);
            var torpedoData = torpedoPrefab.GetComponent<Torpedo>();
            if (launcher != null && launcher.Points.Count > 0 && torpedoData != null)
            {
                Vector3 tube = launcher.Points[0].position;
                PooledEffect.Spawn(torpedoData.ImpactEffect, new Vector3(tube.x, 0f, tube.z), 0.3f);
            }

            float t0 = Time.time;
            while (Time.time - t0 < torpedoWarning)
            {
                if (!IsAlive || Player == null || PlayerConcealed) { _preparing = false; SetLamps(null, 0f); _attack = null; yield break; }
                if (launcher != null) AimLauncher(launcher, solutionPos);
                float blink = Mathf.PingPong((Time.time - t0) * 5f, 1f);
                SetLamps(launcher, blink);
                yield return null;
            }
            SetLamps(null, 0f);
            _preparing = false;

            float speed = torpedoData != null ? Mathf.Max(0.1f, torpedoData.Speed) : 16f;
            for (int i = 0; i < 2; i++)
            {
                Transform tube = launcher != null && launcher.Points.Count > 0 ? launcher.Points[(i * 2) % launcher.Points.Count] : null;
                Vector3 from = tube != null ? tube.position : transform.position + transform.right * (starboard ? 2f : -2f);
                from.y = transform.position.y;
                Vector3 aim = Intercept(from, solutionPos, solutionVel, Time.time - solutionTime, speed);
                if (i == 1)
                {
                    // 둘째 발은 진행 방향 쪽으로 더 앞 — 그대로 달아나거나 속력을 올리면 이쪽에 맞는다
                    Vector3 to = aim - from; to.y = 0f;
                    float sign = Mathf.Sign(Vector3.Dot(Vector3.Cross(to, solutionVel), Vector3.up)) * -1f;
                    if (solutionVel.sqrMagnitude < 0.25f) sign = 1f;
                    aim = from + Quaternion.Euler(0f, torpedoLeadSpread * sign, 0f) * to;
                }
                var go = PoolManager.Instance?.Spawn(torpedoPrefab, from, Quaternion.LookRotation(Flat(aim - from)));
                if (go != null && go.TryGetComponent<Torpedo>(out var torpedo)) torpedo.Launch(aim, torpedoDamage);
                yield return new WaitForSeconds(0.35f);
            }

            // 다친 뒤엔 어뢰 뒤에 함포를 잇는다(어뢰를 피해 침로를 바꾼 곳으로)
            if (Hurt && shellPrefab != null)
            {
                yield return new WaitForSeconds(chainGunDelay);
                float d = Vector3.Distance(transform.position, Player.position);
                if (d >= gunMinRange * 0.7f && d <= gunMaxRange) yield return FireGuns(2);
            }
            _attack = null;
        }

        private static Vector3 Intercept(Vector3 from, Vector3 pos, Vector3 vel, float elapsed, float speed)
        {
            Vector3 aim = pos + vel * elapsed;
            float travel = Vector3.Distance(from, aim) / speed;
            for (int i = 0; i < 4; i++)
            {
                aim = pos + vel * (elapsed + travel);
                travel = Vector3.Distance(from, aim) / speed;
            }
            aim.y = 0f;
            return aim;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v : Vector3.forward; }

        private void AimLauncher(Launcher l, Vector3 worldPoint)
        {
            Vector3 to = transform.InverseTransformPoint(worldPoint) - transform.InverseTransformPoint(l.Pivot.position);
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;
            l.WantYaw = Mathf.Clamp(Vector3.SignedAngle(l.RestOutward, to.normalized, Vector3.up), -launcherTrainLimit, launcherTrainLimit);
        }

        /// <summary>발사관이 원하는 각도로 천천히 돌고, 쏠 일이 없으면 제자리로.</summary>
        private void UpdateLaunchers(float dt)
        {
            foreach (var l in _launchers)
            {
                if (l.Pivot == null) continue;
                if (!(_preparing && l.Torpedo)) l.WantYaw = Mathf.MoveTowards(l.WantYaw, 0f, 20f * dt);
                l.Yaw = Mathf.MoveTowards(l.Yaw, l.WantYaw, 60f * dt);
                Vector3 axis = l.Pivot.parent != null ? l.Pivot.parent.InverseTransformDirection(transform.up) : Vector3.up;
                l.Pivot.localRotation = Quaternion.AngleAxis(l.Yaw, axis) * l.Rest;
            }
        }

        private void SetLamps(Launcher lit, float on)
        {
            if (torpedoLamps == null) return;
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in torpedoLamps)
            {
                if (r == null) continue;
                bool active = lit != null && lit.Lamps.Contains(r);
                float k = active ? on : 0f;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", lampColor * Mathf.Max(0.18f, k));
                _mpb.SetColor("_EmissionColor", lampColor * (k * 4f));
                r.SetPropertyBlock(_mpb);
            }
        }

        // ------------------------------------------------------------ 손상

        public override void TakeDamage(in DamageInfo info)
        {
            base.TakeDamage(info);
            if (!IsAlive) return;
            int stage = HpFraction <= 0.25f ? 3 : HpFraction <= hurtAt ? 2 : HpFraction <= 0.75f ? 1 : 0;
            if (stage <= _damageStage) return;
            _damageStage = stage;
            GameEvents.RaiseBossPhaseChanged(stage == 1 ? "현대화 초계함 손상 — 연기 발생" :
                stage == 2 ? "현대화 초계함 화재 — 공격이 빨라지고 어뢰 뒤 함포가 이어진다" : "현대화 초계함 대파 — 화재 확산");
        }

        private void EmitDamageFx(float dt)
        {
            if (_damageStage == 0) return;
            _smokeDebt += dt * (_damageStage == 1 ? 8f : _damageStage == 2 ? 15f : 22f);
            while (_smokeDebt >= 1f)
            {
                _smokeDebt -= 1f;
                var from = _damageStage >= 2 && _bridgeSmoke != null && Random.value < 0.4f ? _bridgeSmoke : _engineSmoke;
                Vector3 p = (from != null ? from.position : transform.TransformPoint(new Vector3(0f, 2.6f, -1.2f))) + Random.insideUnitSphere * 0.3f;
                DecorFx.Emit(DecorFx.WreckSmoke, p, Vector3.up * Random.Range(1.4f, 2.3f) + DecorFx.Wind * 0.4f,
                    Random.Range(1.8f, 2.8f), Random.Range(0.9f, 1.5f), new Color(0.12f, 0.14f, 0.15f, 0.7f));
            }
            if (_damageStage < 2) return;
            _fireDebt += dt * (_damageStage == 2 ? 14f : 26f);
            while (_fireDebt >= 1f)
            {
                _fireDebt -= 1f;
                var from = _damageStage == 3 && _bridgeSmoke != null && Random.value < 0.45f ? _bridgeSmoke : _aftFire;
                Vector3 p = (from != null ? from.position : transform.TransformPoint(new Vector3(0f, 1.5f, -3f))) + Random.insideUnitSphere * 0.25f;
                DecorFx.Emit(DecorFx.Fire, p, Vector3.up * Random.Range(0.8f, 1.5f) + DecorFx.Wind * 0.12f,
                    Random.Range(0.35f, 0.65f), Random.Range(0.55f, 1.0f), new Color(1f, Random.Range(0.28f, 0.58f), 0.07f, 0.85f));
            }
        }

        private static FireArc LimitedGunArc(float halfAngle)
        {
            var noTrain = new bool[FireArc.Steps];
            for (int i = 0; i < noTrain.Length; i++)
                noTrain[i] = Mathf.Abs(Mathf.DeltaAngle(0f, (i + 0.5f) * FireArc.StepDeg)) > halfAngle;
            return new FireArc(null, noTrain);
        }

        public override void OnDespawned()
        {
            if (_attack != null) StopCoroutine(_attack);
            _attack = null;
            _preparing = false;
            SetLamps(null, 0f);
            base.OnDespawned();
        }

        protected override void Die()
        {
            _preparing = false;
            SetLamps(null, 0f);
            base.Die();
            GameEvents.RaiseBossDefeated();
        }
    }
}
