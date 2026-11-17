using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 4스테이지(1챕터 최종) 보스: 룬급 미사일 위그선(2026-11-16, Codex Stage4Ekranoplan_v1 `ENE_Boss4_Lun`).
    /// 다른 보스처럼 기함 둘레를 돌지 않고 **지나가는 순간에만** 싸울 수 있다.
    ///
    ///   1 고속 통과(100~65%)  수면에 붉은 통과선 예고 → 가로지르며 모스킷 2발 → 지나간 뒤 꼬리 기관포. 크게 돌아 다시 온다
    ///   2 재장전 착수(65~35%) 6발을 다 쓰면 보급 바지선 옆에 내려앉아 재장전(rearmSeconds) — 그동안 수상 표적 · 피해 1.5배
    ///   3 최후의 돌격(35~12%) 더 자주 통과, 남은 발사관 전탄 일제사격, 스치면 제트 후류 피해
    ///   4 추락 활주(12~0% 또는 엔진 5기 이상 파괴) 더는 뜨지 못한다. 수면을 달리며 예고 뒤 들이받고, 남은 발사관을 연달아 쏜다
    ///
    /// 부위(BossParts): 엔진 8(좌4 · 우4 — 부술수록 느려지고 5기부터 못 뜬다) · 발사관 3쌍(쌍마다 2발) · 꼬리 기관포탑 · 기수 레이더(부서지면 통과가 멀리 빗나감).
    /// 날 때는 수면 비행체(WigEnemy) — 대공 무기와 대함 유도탄이 통하고, 내려앉으면 수상 표적이다.
    /// 소켓은 실행 중에 이름으로 찾는다(프리팹에는 탄 · 효과만).
    /// </summary>
    public class LunBoss : WigEnemy, IBossTelegraph, IBossParts
    {
        private enum State { Extend, Pass, ToBarge, Land, Rearm, TakeOff, Taxi, ChargeWarn, Charge }

        [Header("Phases (체력 비율)")]
        [SerializeField, Range(0f, 1f)] private float rearmPhaseAt = 0.65f;
        [SerializeField, Range(0f, 1f)] private float assaultPhaseAt = 0.35f;
        [SerializeField, Range(0f, 1f)] private float crashPhaseAt = 0.12f;

        [Header("Pass")]
        [SerializeField] private float passSpeed = 34f;
        [Tooltip("기함 옆을 이만큼 비켜 지난다")]
        [SerializeField] private float passOffset = 15f;
        [SerializeField] private float runIn = 95f;
        [SerializeField] private float runOut = 70f;
        [SerializeField] private float[] passInterval = { 6f, 5f, 3f };
        [SerializeField] private float warnSeconds = 1.6f;
        [SerializeField] private float fireRange = 62f;
        [SerializeField] private int[] missilesPerPass = { 2, 2, 6 };
        [SerializeField] private float missileSpacing = 0.3f;
        [SerializeField] private float jetWashRadius = 12f;
        [SerializeField] private float jetWashDamage = 5f;

        [Header("Tail gun")]
        [SerializeField] private GameObject gunProjectilePrefab;
        [SerializeField] private float tailRange = 42f;
        [SerializeField] private int tailBurst = 6;
        [SerializeField] private float tailShotInterval = 0.11f;
        [SerializeField] private float tailCooldown = 1.3f;
        [SerializeField] private float tailDamage = 2.5f;
        [SerializeField] private float tailSpeed = 55f;

        [Header("Rearm")]
        [SerializeField] private float bargeDistance = 60f;
        [SerializeField] private float landSeconds = 2.2f;
        [SerializeField] private float rearmSeconds = 10f;
        [SerializeField] private float landedDamageBonus = 1.5f;

        [Header("Crash run")]
        [SerializeField] private float taxiSpeed = 12f;
        [SerializeField] private float chargeSpeed = 30f;
        [SerializeField] private float chargeInterval = 7f;
        [SerializeField] private float chargeWarn = 1.4f;
        [SerializeField] private float chargeSeconds = 2.6f;
        [SerializeField] private float ramRadius = 6.5f;
        [SerializeField] private float ramDamage = 28f;
        [SerializeField] private float crashSalvoInterval = 5f;
        [SerializeField] private int enginesToGround = 5;

        [Header("Effects")]
        [SerializeField] private GameObject muzzleFlash;

        // ---- 모델 소켓(이름으로 찾음)
        private sealed class Engine { public Transform Node, Fire; public BossParts.Part Part; public bool Port; }
        private sealed class Launcher
        {
            public Transform Pair; public BossParts.Part Part; public float Open;
            public readonly List<Transform> Points = new(), Hatches = new();
            public readonly List<Quaternion> HatchRest = new();
        }
        private readonly List<Engine> _engines = new();
        private readonly List<Launcher> _launchers = new();
        private readonly WeaponController _tail = new();
        private Transform _tailTurret, _radome, _mooring;
        private BossParts.Part _tailPart, _radomePart;
        private readonly BossParts _parts = new();
        private bool _bound;

        private State _state;
        private int _phase;
        private float _timer, _speedNow, _tailTimer, _crashSalvoTimer, _chargeTimer, _smokeDebt;
        private int _ammo, _tailRounds;
        private bool _firedThisPass, _washedThisPass, _rammedThisCharge;
        private Vector3 _passDir, _chargeDir;
        private float _side = 1f;
        private Transform _barge;
        private LineRenderer _line;
        private Game.Ship.ShipController _playerShip;
        private static Material s_lineMat;

        public BossParts Parts => _parts;
        public string PhaseName => _phase switch { 0 => "고속 통과", 1 => "재장전 착수", 2 => "최후의 돌격", _ => "추락 활주" };
        public float[] PhaseThresholds => new[] { rearmPhaseAt, assaultPhaseAt, crashPhaseAt };
        public (string name, float seconds) NextAttack => _state switch
        {
            State.Extend => ("고속 통과", Mathf.Max(0f, _timer)),
            State.Pass => (_firedThisPass ? "꼬리 기관포" : "모스킷 미사일", 0f),
            State.ToBarge or State.Land => ("재장전 착수", 0f),
            State.Rearm => ("재장전(공격 기회)", Mathf.Max(0f, _timer)),
            State.TakeOff => ("이수", 0f),
            State.ChargeWarn => ("돌진", Mathf.Max(0f, _timer)),
            State.Charge => ("돌진", 0f),
            _ => ("돌진 준비", Mathf.Max(0f, _chargeTimer)),
        };

        // ---- 검증용
        public int DevPasses { get; private set; }
        public int DevMissiles { get; private set; }
        public int DevTailShots { get; private set; }
        public int DevRearms { get; private set; }
        public int DevCharges { get; private set; }
        public int DevRams { get; private set; }
        public int Phase => _phase;
        public int Ammo => _ammo;
        public int EnginesDown { get { int n = 0; foreach (var e in _engines) if (_parts.IsDestroyed(e.Part)) n++; return n; } }
        public int EngineCount => _engines.Count;
        public int LauncherCount => _launchers.Count;
        public string StateName => _state.ToString();
        public Transform Barge => _barge;
        /// <summary>검증용: 엔진 i의 부위.</summary>
        public BossParts.Part EnginePart(int i) => i >= 0 && i < _engines.Count ? _engines[i].Part : null;
        /// <summary>검증용: 남은 미사일 수를 바꾼다(0이면 다음 통과 대신 재장전하러 간다).</summary>
        public void DevSetAmmo(int ammo) => _ammo = ammo;
        /// <summary>검증용: 곧바로 다음 통과를 시작하게 한다.</summary>
        public void DevSkipWait() { if (_state == State.Extend) _timer = 0f; }

        private bool Grounded => _phase >= 3;
        /// <summary>바지선 옆(우현 쪽 9m) — 바지선 위가 아니라 옆에 내려앉는다.</summary>
        private Vector3 BargeSlot => _barge != null ? _barge.position + _barge.right * 9f : transform.position;
        private int LaunchersAlive { get { int n = 0; foreach (var l in _launchers) if (!_parts.IsDestroyed(l.Part)) n++; return n; } }
        private float SpeedScale => Mathf.Clamp(1f - 0.05f * EnginesDown, 0.6f, 1f);

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            StopAllCoroutines();
            Bind();
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
            _phase = 0;
            _state = State.Extend;
            _timer = 1.5f;
            _speedNow = passSpeed;
            _ammo = 6;
            _tailTimer = 1f;
            _tailRounds = 0;
            _side = Random.value < 0.5f ? -1f : 1f;
            DevPasses = DevMissiles = DevTailShots = DevRearms = DevCharges = DevRams = 0;
            foreach (var l in _launchers) l.Open = 0f;

            float maxHp = def != null ? def.MaxHp : 3600f;
            _parts.Clear();
            int p = 0, s = 0;
            foreach (var e in _engines)
            {
                e.Part = _parts.Add(e.Port ? $"엔진 좌{++p}" : $"엔진 우{++s}", "속력 감소", e.Node, 1.6f, maxHp * 0.025f, e.Node);
                if (e.Part != null) e.Part.Group = "엔진";
            }
            for (int i = 0; i < _launchers.Count; i++)
                _launchers[i].Part = _parts.Add($"발사관 {i + 1}", "일제사격 2발 감소", _launchers[i].Pair, 2.4f, maxHp * 0.06f, _launchers[i].Pair);
            _tailPart = _parts.Add("꼬리 기관포탑", "꼬리 사격 중지", _tailTurret, 2f, maxHp * 0.04f, _tailTurret);
            _radomePart = _parts.Add("기수 레이더", "통과가 멀리 빗나감", _radome, 2.2f, maxHp * 0.04f, _radome);
            HideLine();
            GameEvents.RaiseBossPhaseChanged("룬급 위그선 출현 — 수면에 붉은 통과선이 뜨면 화력을 모으십시오");
        }

        /// <summary>모델 소켓을 이름으로 찾는다. 좌 · 우는 이름이 아니라 실제 위치로 정한다.</summary>
        private void Bind()
        {
            if (_bound) { ResetPose(); return; }
            _bound = true;
            var all = GetComponentsInChildren<Transform>(true);
            Transform F(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
            foreach (var side in new[] { "P", "S" })
                for (int i = 1; i <= 4; i++)
                {
                    var node = F($"Engine_{side}{i}");
                    if (node == null) continue;
                    _engines.Add(new Engine { Node = node, Fire = F($"EngineFire_{side}{i}") ?? node, Port = transform.InverseTransformPoint(node.position).x < 0f });
                }
            for (int i = 1; i <= 3; i++)
            {
                var pair = F($"LauncherPair_0{i}");
                if (pair == null) continue;
                var l = new Launcher { Pair = pair };
                foreach (var lr in new[] { "L", "R" })
                {
                    var point = F($"LaunchPoint_0{i}_{lr}");
                    if (point != null) l.Points.Add(point);
                    var hatch = F($"LauncherHatch_0{i}_{lr}");
                    if (hatch != null) { l.Hatches.Add(hatch); l.HatchRest.Add(hatch.localRotation); }
                }
                _launchers.Add(l);
            }
            _tailTurret = F("TailTurretPivot");
            _tail.BindArtTransforms(_tailTurret, F("TailTurretElevation"), F("TailTurretMuzzle"));
            _radome = F("NoseRadome");
            _mooring = F("BargeMooring");
            foreach (var t in all) t.gameObject.layer = gameObject.layer;
        }

        private void ResetPose()
        {
            foreach (var l in _launchers)
                for (int i = 0; i < l.Hatches.Count; i++) l.Hatches[i].localRotation = l.HatchRest[i];
        }

        // ------------------------------------------------------------ 진행

        protected override void UpdateBehaviour(float dt)
        {
            _parts.Tick(dt);
            UpdatePhase();
            _tail.Tick(dt);
            EmitEngineFire(dt);

            Vector3 toPlayer = Player.position - transform.position; toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            float speed = passSpeed * SpeedScale;
            float turn = Definition.TurnRateDegPerSec;

            switch (_state)
            {
                case State.Extend:
                {
                    // 멀어지며 다음 통과를 기다린다. 충분히 멀고 시간이 되면 돌아서 예고선을 띄운다
                    _timer -= dt;
                    Vector3 away = dist > 1f ? -toPlayer / dist : transform.forward;
                    Vector3 heading = dist < runIn ? away : Vector3.Cross(Vector3.up, away) * _side;   // 멀면 크게 원을 그린다
                    _speedNow = Mathf.MoveTowards(_speedNow, speed, speed * dt);
                    Glide(heading, _speedNow, cruiseAltitude, 3f, turn, dt);
                    if (_timer <= 0f && dist >= runIn * 0.8f)
                    {
                        if (_phase >= 1 && _ammo < Mathf.Min(2, LaunchersAlive * 2) && LaunchersAlive > 0) BeginRearm();
                        else BeginPass(toPlayer / Mathf.Max(dist, 1f));
                    }
                    break;
                }

                case State.Pass:
                {
                    _timer -= dt;
                    // 기함 옆(passOffset)을 겨눠 들어가고, 옆을 지나면 그대로 곧게 빠진다
                    Vector3 lateral = Vector3.Cross(Vector3.up, _passDir) * _side;
                    float miss = passOffset * (_parts.IsDestroyed(_radomePart) ? 2.2f : 1f);
                    Vector3 aim = Player.position + lateral * miss;
                    Vector3 toAim = aim - transform.position; toAim.y = 0f;
                    bool passed = Vector3.Dot(toAim, _passDir) <= 0f;
                    if (_timer > 0f)
                    {
                        // 예고 중: 선을 그리며 진입 방향으로 선회만 한다(천천히)
                        Glide(toAim, _speedNow * 0.55f, cruiseAltitude, 3f, turn * 1.5f, dt);
                        DrawLine(transform.position, aim + toAim.normalized * runOut, 2.4f);
                        break;
                    }
                    _speedNow = Mathf.MoveTowards(_speedNow, speed, speed * 1.5f * dt);
                    Glide(passed ? _passDir : toAim, _speedNow, cruiseAltitude, 3f, passed ? 0f : turn * 2f, dt);
                    if (!passed) { _passDir = toAim.normalized; DrawLine(transform.position, aim + _passDir * runOut, 2.4f); }
                    else HideLine();

                    if (!_firedThisPass && !passed && dist <= fireRange && !PlayerConcealed)
                    {
                        _firedThisPass = true;
                        StartCoroutine(Salvo(Mathf.Min(missilesPerPass[Mathf.Min(_phase, missilesPerPass.Length - 1)], _ammo)));
                    }
                    if (_phase >= 2 && !_washedThisPass && dist <= jetWashRadius)
                    {
                        _washedThisPass = true;
                        JetWash();
                    }
                    if (passed) TailGun(dist, dt);
                    if (passed && dist >= runOut)
                    {
                        DevPasses++;
                        _state = State.Extend;
                        _timer = passInterval[Mathf.Min(_phase, passInterval.Length - 1)];
                        _side = -_side;
                    }
                    break;
                }

                case State.ToBarge:
                {
                    Vector3 to = BargeSlot - transform.position; to.y = 0f;
                    _speedNow = Mathf.MoveTowards(_speedNow, speed * 0.7f, speed * dt);
                    Glide(to, _speedNow, cruiseAltitude, 3f, turn * 1.6f, dt);
                    if (to.magnitude <= _speedNow * landSeconds * 0.55f + 8f) { _state = State.Land; _timer = landSeconds; }
                    break;
                }

                case State.Land:
                {
                    _timer -= dt;
                    Vector3 to = BargeSlot - transform.position; to.y = 0f;
                    _speedNow = Mathf.MoveTowards(_speedNow, 0f, passSpeed * 0.7f / Mathf.Max(0.3f, landSeconds) * dt);
                    Glide(to.magnitude > 9f ? to : transform.forward, _speedNow, WaterY, (cruiseAltitude - WaterY) / Mathf.Max(0.3f, landSeconds), turn, dt);
                    if (_timer <= 0f && transform.position.y <= WaterY + 0.05f)
                    {
                        SetLanded(true);
                        _state = State.Rearm;
                        _timer = rearmSeconds;
                        DevRearms++;
                        GameEvents.RaiseBossPhaseChanged($"룬급 위그선 착수 — 재장전 {rearmSeconds:0}초, 지금 모든 무기가 통합니다(피해 {landedDamageBonus:0.#}배)");
                    }
                    break;
                }

                case State.Rearm:
                {
                    _timer -= dt;
                    _speedNow = 0f;
                    Glide(transform.forward, 0f, WaterY, 5f, 0f, dt);
                    foreach (var l in _launchers) l.Open = Mathf.MoveTowards(l.Open, _parts.IsDestroyed(l.Part) ? 0f : 1f, dt / 0.8f);
                    TailGun(dist, dt);
                    if (_timer <= 0f)
                    {
                        _ammo = LaunchersAlive * 2;
                        _state = State.TakeOff;
                        GameEvents.RaiseBossPhaseChanged("룬급 위그선 재장전 완료 — 이수합니다");
                    }
                    break;
                }

                case State.TakeOff:
                {
                    foreach (var l in _launchers) l.Open = Mathf.MoveTowards(l.Open, 0f, dt / 0.6f);
                    _speedNow = Mathf.MoveTowards(_speedNow, speed, speed / 2.5f * dt);
                    bool lift = _speedNow >= speed * 0.5f;
                    if (lift && Landed) SetLanded(false);
                    Glide(transform.forward, _speedNow, lift ? cruiseAltitude : WaterY, 2f, 0f, dt);
                    if (_speedNow >= speed * 0.95f && transform.position.y >= cruiseAltitude - 0.2f) { _state = State.Extend; _timer = 2f; }
                    break;
                }

                case State.Taxi:
                {
                    // 추락 활주: 수면을 달리며 기함 둘레를 넓게 돈다. 주기적으로 돌진, 남은 발사관은 연달아
                    _chargeTimer -= dt;
                    _crashSalvoTimer -= dt;
                    float want = taxiSpeed * SpeedScale;
                    _speedNow = Mathf.MoveTowards(_speedNow, want, 10f * dt);
                    Vector3 tangent = Vector3.Cross(Vector3.up, toPlayer.normalized) * _side;
                    Vector3 heading = dist > 45f ? toPlayer : dist < 26f ? -toPlayer + tangent : tangent + toPlayer.normalized * 0.2f;
                    Glide(heading, _speedNow, WaterY, 4f, turn * 1.4f, dt);
                    TailGun(dist, dt);
                    if (_crashSalvoTimer <= 0f && LaunchersAlive > 0 && !PlayerConcealed)
                    {
                        _crashSalvoTimer = crashSalvoInterval;
                        _ammo = LaunchersAlive * 2;
                        StartCoroutine(Salvo(2));
                    }
                    if (_chargeTimer <= 0f && dist <= 70f)
                    {
                        _state = State.ChargeWarn;
                        _timer = chargeWarn;
                        GameEvents.RaiseBossPhaseChanged("룬급 위그선 돌진 — 붉은 선 밖으로 비키십시오");
                    }
                    break;
                }

                case State.ChargeWarn:
                {
                    _timer -= dt;
                    _speedNow = Mathf.MoveTowards(_speedNow, 3f, 14f * dt);
                    Glide(toPlayer, _speedNow, WaterY, 4f, turn * 2.5f, dt);
                    _chargeDir = transform.forward; _chargeDir.y = 0f; _chargeDir.Normalize();
                    DrawLine(transform.position, transform.position + _chargeDir * (chargeSpeed * chargeSeconds), 3.2f);
                    if (_timer <= 0f) { _state = State.Charge; _timer = chargeSeconds; _rammedThisCharge = false; DevCharges++; }
                    break;
                }

                case State.Charge:
                {
                    _timer -= dt;
                    _speedNow = Mathf.MoveTowards(_speedNow, chargeSpeed * SpeedScale, 60f * dt);
                    Glide(_chargeDir, _speedNow, WaterY, 4f, 0f, dt);
                    if (!_rammedThisCharge && dist <= ramRadius && Player.TryGetComponent<IDamageable>(out var hull))
                    {
                        _rammedThisCharge = true;
                        DevRams++;
                        hull.TakeDamage(new DamageInfo(ramDamage, Player.position, _chargeDir, DamageSource.Collision, "룬급 돌진"));
                    }
                    if (_timer <= 0f) { HideLine(); _state = State.Taxi; _chargeTimer = chargeInterval; _side = -_side; }
                    break;
                }
            }
            ApplyHatches();
        }

        private void UpdatePhase()
        {
            int next = HpFraction <= crashPhaseAt || EnginesDown >= enginesToGround ? 3
                     : HpFraction <= assaultPhaseAt ? 2 : HpFraction <= rearmPhaseAt ? 1 : 0;
            if (next <= _phase) return;
            _phase = next;
            if (_phase == 3)
            {
                // 더는 뜨지 못한다: 어디에 있든 수면으로 내려가 활주
                StopAllCoroutines();
                HideLine();
                SetLanded(true);
                _state = State.Taxi;
                _chargeTimer = 3f;
                _crashSalvoTimer = 2f;
                foreach (var l in _launchers) l.Open = 0f;
                GameEvents.RaiseBossPhaseChanged("룬급 위그선 추락 — 더는 뜨지 못합니다. 돌진과 남은 미사일에 주의, 모든 무기가 통합니다");
            }
            else
                GameEvents.RaiseBossPhaseChanged(_phase == 1
                    ? "룬급 위그선 — 미사일을 다 쓰면 보급 바지선에 내려앉아 재장전합니다(공격 기회)"
                    : "룬급 위그선 최후의 돌격 — 전탄 일제사격 · 스치면 제트 후류");
        }

        private void BeginPass(Vector3 toward)
        {
            _state = State.Pass;
            _passDir = toward;
            _timer = warnSeconds;
            _firedThisPass = _washedThisPass = false;
            _tailRounds = 0;
            AudioManager.Play(Game.Data.SfxId.JetPass, transform.position, 1f, 0.7f);
        }

        private void BeginRearm()
        {
            if (_barge == null)
            {
                // 보급 바지선: 기함에서 떨어진 곳에 한 번 세운다(외형만 — Codex ENV_RearmBarge)
                Vector3 dir = transform.position - Player.position; dir.y = 0f;
                dir = dir.sqrMagnitude > 1f ? dir.normalized : Vector3.forward;
                Vector3 at = Player.position + dir * bargeDistance;
                for (int i = 0; i < 8 && !Game.World.Islands.IsClear(at, 22f); i++)
                    at = Player.position + Quaternion.Euler(0f, 45f * (i + 1), 0f) * dir * bargeDistance;   // 섬 · 플랫폼을 피해 둔다
                at.y = 0f;
                var go = Game.World.IslandBuilder.PlaceKitModel(null, "Environment/Stage4/ENV_RearmBarge", at + Vector3.up * WaterY, Random.value * 360f);
                _barge = go != null ? go.transform : new GameObject("Rearm point").transform;
                if (go == null) _barge.position = at;
            }
            _state = State.ToBarge;
            HideLine();
        }

        // ------------------------------------------------------------ 공격

        private IEnumerator Salvo(int count)
        {
            if (count <= 0 || Definition.MissilePrefab == null) yield break;
            // 쏠 발사관 덮개를 먼저 연다
            var points = new List<(Launcher l, Transform p)>();
            foreach (var l in _launchers)
            {
                if (_parts.IsDestroyed(l.Part)) continue;
                foreach (var p in l.Points) if (points.Count < count) points.Add((l, p));
            }
            foreach (var (l, _) in points) l.Open = Mathf.Max(l.Open, 0.01f);
            float t = 0f;
            while (t < 0.35f) { t += Time.deltaTime; foreach (var (l, _) in points) l.Open = Mathf.MoveTowards(l.Open, 1f, Time.deltaTime / 0.35f); yield return null; }
            GameEvents.RaiseBossPhaseChanged($"룬급 모스킷 {points.Count}발 — 요격 준비");
            foreach (var (l, p) in points)
            {
                if (!IsAlive || Player == null) yield break;
                if (_parts.IsDestroyed(l.Part)) continue;
                var go = PoolManager.Instance?.Spawn(Definition.MissilePrefab, p.position, p.rotation);
                go?.GetComponent<Missile>()?.Launch(Player, Definition.AttackDamage);
                PooledEffect.Spawn(muzzleFlash, p.position);
                _ammo = Mathf.Max(0, _ammo - 1);
                DevMissiles++;
                yield return new WaitForSeconds(missileSpacing);
            }
            yield return new WaitForSeconds(0.5f);
            if (_state != State.Rearm) foreach (var (l, _) in points) l.Open = 0f;
        }

        private void TailGun(float dist, float dt)
        {
            if (_parts.IsDestroyed(_tailPart) || gunProjectilePrefab == null || _tail.Muzzle == null) return;
            Vector3 aim = Player.position + Vector3.up * 0.8f;
            if (_playerShip != null) aim = Ballistics.PredictIntercept(_tail.Muzzle.position, aim, Player.forward * _playerShip.CurrentSpeed, tailSpeed);
            _tail.AimAt(aim, 160f, dt);
            _tailTimer -= dt;
            if (dist > tailRange || PlayerConcealed) return;
            if (_tailRounds <= 0)
            {
                if (_tailTimer > 0f || !_tail.IsAimedAt(aim)) return;
                _tailRounds = tailBurst;
                _tailTimer = 0f;
            }
            if (_tailTimer > 0f) return;
            _tailTimer = --_tailRounds > 0 ? tailShotInterval : tailCooldown;
            float spread = _parts.IsDestroyed(_radomePart) ? 4f : 1.6f;
            Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * _tail.FireDirection;
            var shot = PoolManager.Instance?.Spawn(gunProjectilePrefab, _tail.Muzzle.position, Quaternion.LookRotation(dir));
            shot?.GetComponent<Projectile>()?.Launch(dir, tailSpeed, tailDamage, DamageSource.Gun);
            AudioManager.Play(Game.Data.SfxId.EnemyAutocannonShot, _tail.Muzzle.position, 0.5f, 0.95f);
            DevTailShots++;
        }

        private void JetWash()
        {
            if (!Player.TryGetComponent<IDamageable>(out var hull)) return;
            hull.TakeDamage(new DamageInfo(jetWashDamage, Player.position, transform.forward, DamageSource.Collision, "룬급 제트 후류"));
            GameEvents.RaiseBossPhaseChanged("제트 후류 — 통과선에서 더 멀리 비키십시오");
        }

        // ------------------------------------------------------------ 연출

        private void ApplyHatches()
        {
            // 덮개: 기준 자세에서 기체 오른쪽 축 둘레 −70°(Codex 납품 메모) — 위로 들린다
            Vector3 right = transform.right;
            foreach (var l in _launchers)
                for (int i = 0; i < l.Hatches.Count; i++)
                {
                    l.Hatches[i].localRotation = l.HatchRest[i];
                    if (l.Open > 0.001f) l.Hatches[i].Rotate(right, -70f * l.Open, Space.World);
                }
        }

        private void EmitEngineFire(float dt)
        {
            int down = EnginesDown;
            if (down == 0) return;
            _smokeDebt += dt * 14f;
            while (_smokeDebt >= 1f)
            {
                _smokeDebt -= 1f;
                var e = _engines[Random.Range(0, _engines.Count)];
                if (!_parts.IsDestroyed(e.Part)) continue;
                Vector3 p = e.Fire.position + Random.insideUnitSphere * 0.3f;
                Game.View.DecorFx.Emit(Game.View.DecorFx.Fire, p, Vector3.up * Random.Range(0.6f, 1.2f) - transform.forward * _speedNow * 0.15f,
                    Random.Range(0.3f, 0.55f), Random.Range(0.6f, 1.1f), new Color(1f, Random.Range(0.3f, 0.6f), 0.08f, 0.85f));
                Game.View.DecorFx.Emit(Game.View.DecorFx.WreckSmoke, p, Vector3.up * Random.Range(1f, 1.8f) + Game.View.DecorFx.Wind * 0.4f,
                    Random.Range(1.6f, 2.4f), Random.Range(0.8f, 1.4f), new Color(0.1f, 0.11f, 0.12f, 0.65f));
            }
        }

        private void DrawLine(Vector3 from, Vector3 to, float width)
        {
            if (_line == null)
            {
                if (s_lineMat == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    s_lineMat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "Lun pass line (Runtime)" };
                }
                var go = new GameObject("Lun pass line");
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _line = go.AddComponent<LineRenderer>();
                _line.sharedMaterial = s_lineMat;
                _line.useWorldSpace = true;
                _line.alignment = LineAlignment.TransformZ;
                _line.positionCount = 2;
                _line.numCapVertices = 2;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.receiveShadows = false;
            }
            _line.enabled = true;
            float pulse = 0.55f + 0.3f * Mathf.Sin(Time.time * 9f);
            _line.startColor = _line.endColor = new Color(1f, 0.25f, 0.18f, pulse);
            _line.startWidth = _line.endWidth = width;
            _line.SetPosition(0, new Vector3(from.x, 0.14f, from.z));
            _line.SetPosition(1, new Vector3(to.x, 0.14f, to.z));
        }

        private void HideLine() { if (_line != null) _line.enabled = false; }

        /// <summary>검증용: 통과 예고선이 보이는가.</summary>
        public bool LineVisible => _line != null && _line.enabled;

        // ------------------------------------------------------------ 피해 · 퇴장

        public override void TakeDamage(in DamageInfo info)
        {
            var use = _state == State.Rearm && Landed
                ? new DamageInfo(info.Amount * landedDamageBonus, info.HitPoint, info.Direction, info.Source, info.Tag)
                : info;
            base.TakeDamage(use);
            if (IsAlive) _parts.Hit(use, this);
        }

        public override void OnDespawned()
        {
            StopAllCoroutines();
            ResetPose();
            _parts.Clear();
            if (_line != null) Destroy(_line.gameObject);
            _line = null;
            if (_barge != null) Destroy(_barge.gameObject);
            _barge = null;
            base.OnDespawned();
        }

        protected override void Die()
        {
            StopAllCoroutines();
            Game.View.Explosions.Spawn(transform.position + Vector3.up * 2f, 4f, Game.View.Explosions.Kind.Impact);
            base.Die();
            GameEvents.RaiseBossDefeated();
        }
    }
}
