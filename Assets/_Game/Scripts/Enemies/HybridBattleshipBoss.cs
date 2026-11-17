using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 스테이지 3 보스: 항공전함(2026-11-14 개편 — 사용자 모델 `ArtSource/Stage3HybridCarrier_v6`). 앞은 2연장 주포 2기, 가운데 함교와 양현 기관포,
    /// 뒤는 비행갑판(가장자리 대공포 좌3·우3, 드론 엘리베이터)과 함미 램프(웰덱). 체력에 따라 세 단계:
    ///
    ///   1단계 포격   (100~70%): 함교 양현 기관포 점사(그 현만) + 가까이 오면 주포 직사 + 대공포가 아군 미사일 요격.
    ///   2단계 항모   (70~40%):  + 엘리베이터 드론 편대(2기씩 3번 — 내려갔다 올라오면 이륙).
    ///   3단계 총력전 (40~0%):   + 함미 램프를 열고 자폭 보트 3척, 드론과 동시에. 주포 간격이 짧아지고 더 가까이 돈다.
    ///
    /// 주포: 거리에 따라 탄이 날아가는 시간 0.85~1.6초(예전 2.2초), 탄도 최고점이 거리 제곱에 비례 — 가까우면 낮게 곧장(포신 수평), 멀면 높게(포신이 든다).
    ///   포탑이 실제로 선회·고각을 맞춘 뒤 쏜다(WeaponController). 선미 ±45°는 함교·비행갑판이 막아 못 쏜다(수면에 초록 부채꼴).
    /// 대공: 내 미사일·로켓이 선체 14m 안에 들어오면 한 발에 한 번 판정 — 50% × (그 현의 살아 있는 대공포 ÷ 3) − 10%p × 지친 정도.
    ///   격추할 때마다 지친 정도 +1(50 → 40 → 30 → 20 → 10 → 0% — 몰아 쏘면 늦어도 6번째는 반드시 들어간다), 4초마다 1씩 회복(2026-11-14 사용자 제안).
    ///   대공포 6기는 하나씩 부서진다. 판정에 성공한 포가 예광탄을 쏘고 미사일이 공중에서 터진다(예광탄 자체는 피해 0 — 확률이 정확하게).
    ///   보스 막대의 대공포 칸이 지금 요격 확률("대공 30%")을 보여 준다.
    /// 약점: 엘리베이터는 내려가 있는 동안만, 함미 웰덱은 램프가 열려 있는 동안만 피해를 받는다(BossParts.Shielded). 열려 있으면 반경 안 피해를 먼저 받고,
    ///   웰덱에 맞은 피해는 선체에도 1.5배. 부서지면 각각 드론·보트가 멈춘다.
    /// 겨냥점(IAimPointProvider): 플레이어 무장은 보스 중심 대신 가까운 부분(함수 주포 · 함교 · 함미)을, 약점이 열려 있으면 그쪽을 겨눈다 —
    ///   엘리베이터는 현측(40~140°)에서 또는 위에서 꽂히는 유도탄으로, 함미는 뒤쪽(115° 밖)에서만. 앞뒤에서 쏜 포탄은 선체에 막힌다.
    /// 레이더가 부서지면 주포·기관포가 앞질러 쏘지 못하고 더 흩어진다.
    /// 드론은 엘리베이터 판에 실려(겉모습 사본) 함께 올라오고, 다 올라오면 그 자리에서 진짜 드론으로 바뀌어 이륙한다.
    /// 부위·소켓은 모델 이름으로 실행 중에 찾는다(프리팹을 다시 만들지 않아도 모델 교체만으로 연결). 예전 미사일 일제사격·정찰기·전투기는 뺐다(새 모델에 발사대 없음).
    /// </summary>
    public class HybridBattleshipBoss : EnemyController, IBossTelegraph, IBossParts, IAimPointProvider
    {
        // ---- 보스 학습(점검 K05)
        public string PhaseName => _phase switch
        {
            0 => "포격 — 현측 기관포 · 근거리 주포 · 대공 요격",
            1 => "항모 — 엘리베이터 드론 편대",
            _ => "총력전 — 드론 + 함미 자폭 보트",
        };
        public float[] PhaseThresholds => new[] { carrierPhaseAt, finalPhaseAt };
        public (string name, float seconds) NextAttack
        {
            get
            {
                (string, float) best = (null, float.MaxValue);
                if (MainBatteryLeft && Player != null && Vector3.Distance(transform.position, Player.position) <= gunRange)
                    best = ("주포 직사", Mathf.Max(0f, _salvoTimer));
                if (_phase >= 1 && WaveAvailable && !_elevatorBusy && !_gateBusy && _waveTimer < best.Item2)
                    best = (WaveName, Mathf.Max(0f, _waveTimer));
                return best;
            }
        }

        [Header("Phase")]
        [SerializeField, Range(0f, 1f)] private float carrierPhaseAt = 0.7f;
        [SerializeField, Range(0f, 1f)] private float finalPhaseAt = 0.4f;

        [Header("Main Battery (2연장 2기)")]
        [SerializeField] private GameObject shellPrefab;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(1)] private int shellsPerSalvo = 4;
        [SerializeField] private float shellDamage = 13f;
        [SerializeField] private float shellBlastRadius = 2.6f;
        [Tooltip("착탄점이 예측 위치 주변으로 흩어지는 반경")]
        [SerializeField] private float salvoSpread = 3.5f;
        [SerializeField] private float gunRange = 45f;
        [Tooltip("단계별 일제사격 간격(초)")]
        [SerializeField] private float[] salvoInterval = { 5.5f, 5f, 3.8f };
        [Tooltip("가장 가까울 때 탄이 날아가는 시간(초). 거리 60m마다 +1초, 최대 maxFlightTime")]
        [SerializeField] private float minFlightTime = 0.85f;
        [SerializeField] private float maxFlightTime = 1.6f;
        [Tooltip("탄도 최고점 = a + b × 거리² (가까우면 낮게 곧장, 멀면 높게)")]
        [SerializeField] private float apexBase = 0.4f;
        [SerializeField] private float apexPerDistSq = 0.0035f;
        [SerializeField] private float turretTurnRate = 40f;
        [Tooltip("주포 사격 금지 구역(선체 기준 방위). 기본: 선미 ±45° — 함교·비행갑판")]
        [SerializeField] private BearingSector[] mainBatteryCutouts = { new BearingSector(180f, 45f) };

        [Header("Side Guns (함교 양현 기관포)")]
        [SerializeField] private GameObject gunProjectilePrefab;
        [SerializeField] private float sideGunRange = 32f;
        [SerializeField, Min(1)] private int sideGunBurst = 6;
        [SerializeField] private float sideGunShotInterval = 0.12f;
        [Tooltip("단계별 점사 간격(초)")]
        [SerializeField] private float[] sideGunCooldown = { 2.6f, 2.2f, 1.7f };
        [SerializeField] private float sideGunDamage = 2f;
        [SerializeField] private float sideGunSpeed = 55f;
        [SerializeField] private float sideGunSpread = 2.2f;
        [SerializeField] private float sideGunTurnRate = 140f;

        [Header("Anti-Air (비행갑판 대공포 6기)")]
        [Tooltip("예광탄(장식 — 피해 0). 격추는 확률로 정한다.")]
        [SerializeField] private GameObject aaProjectilePrefab;
        [Tooltip("선체에서 이 거리 안으로 들어온 아군 유도탄을 한 번 판정한다")]
        [SerializeField] private float aaRange = 14f;
        [Tooltip("그 현의 대공포가 모두 살아 있을 때 격추 확률")]
        [SerializeField, Range(0f, 1f)] private float aaBaseChance = 0.5f;
        [SerializeField] private float aaProjectileSpeed = 80f;
        [Tooltip("한 발 격추할 때마다 깎이는 요격 확률(%p) — 몰아 쏘면 뚫린다")]
        [SerializeField, Range(0f, 0.5f)] private float aaChanceDropPerKill = 0.1f;
        [Tooltip("깎인 확률이 한 단계 돌아오는 시간(초)")]
        [SerializeField] private float aaRecoverSeconds = 4f;

        [Header("Drone Elevator (2단계~)")]
        [SerializeField, Min(1)] private int dronesPerLift = 2;
        [SerializeField, Min(1)] private int liftsPerWave = 3;
        [SerializeField] private float elevatorMoveTime = 1f;
        [Tooltip("내려가 있는 시간 — 이 동안(+오르내리는 동안) 엘리베이터가 약점")]
        [SerializeField] private float elevatorLoadTime = 0.9f;
        [Tooltip("2·3단계 공격 간격(초)")]
        [SerializeField] private float[] waveInterval = { 16f, 14f };

        [Header("Stern Boats (3단계)")]
        [SerializeField] private Game.Data.EnemyDefinition sternBoat;
        [SerializeField, Min(1)] private int boatsPerWave = 3;
        [SerializeField] private float boatSpacing = 0.8f;
        [SerializeField] private float gateOpenTime = 1.2f;
        [SerializeField] private float gateOpenDegrees = 96f;
        [Tooltip("램프가 열린 동안 웰덱에 맞은 피해 배율(선체에도)")]
        [SerializeField] private float wellDeckBonus = 1.5f;

        [Header("Visual")]
        [SerializeField] private float radarRpm = 15f;
        [Tooltip("사격 금지 구역 표시(수면 부채꼴) 머티리얼. 비우면 표시하지 않는다")]
        [SerializeField] private Material cutoutZoneMaterial;
        [SerializeField] private float cutoutZoneRadius = 36f;

        // ------------------------------------------------------------ 상태

        private sealed class Gun
        {
            public readonly WeaponController Weapon = new();
            public Transform Turret, Muzzle;
            public BossParts.Part Part;
            public bool Port;
            public int Rounds;
            public float ShotTimer, Cooldown, Busy;
        }

        private int _phase;
        private float _salvoTimer, _waveTimer;
        private bool _bound, _indicatorBuilt;
        private readonly Gun[] _main = { new(), new() };
        private readonly List<Transform> _mainMuzzles = new(4);
        private readonly bool[] _mainAimed = new bool[2];
        private readonly List<Gun> _sideGuns = new(2);
        private readonly List<Gun> _aa = new(6);
        private Transform _elevatorPivot, _elevatorUpper, _elevatorMount, _gatePivot, _boatExit, _radar;
        private Transform _bowAnchor, _bridgeAnchor, _wellAnchor;
        private Vector3 _elevatorRest, _elevatorTravel, _gateAxis;
        private Quaternion _gateRest;
        private float _gateSign = 1f, _elevator01, _gate01;
        private bool _elevatorBusy, _gateBusy;
        private Collider _hull, _playerHull;
        private Game.Ship.ShipController _playerShip;
        private readonly HashSet<int> _aaJudged = new();
        private float _aaFatigue;   // 아직 회복하지 못한 격추 수(시간에 따라 줄어든다)
        private readonly List<GameObject> _droneDummies = new();   // 엘리베이터에 실린 드론(겉모습 사본)
        private readonly BossParts _parts = new();
        private BossParts.Part _elevatorPart, _wellPart, _radarPart;

        /// <summary>검증용: 멈춰 세우되(선회 없음) 공격은 한다.</summary>
        [System.NonSerialized] public bool DevStationary;
        /// <summary>검증용 기록.</summary>
        public int DevAaJudged { get; private set; }
        public int DevAaKills { get; private set; }
        public int DevSideShots { get; private set; }
        public int DevSalvos { get; private set; }
        public int DevDronesLaunched { get; private set; }
        public int DevBoatsLaunched { get; private set; }
        public float DevLastFlightTime { get; private set; }
        public float DevLastWellMultiplier { get; private set; } = 1f;

        public BossParts Parts => _parts;
        public bool ElevatorExposed => _elevatorPart != null && !_elevatorPart.Destroyed && !_elevatorPart.Shielded;
        public bool GateExposed => _wellPart != null && !_wellPart.Destroyed && !_wellPart.Shielded;
        public float Elevator01 => _elevator01;
        public float Gate01 => _gate01;
        public Transform ElevatorAnchor => _elevatorUpper;
        public Transform WellAnchor => _wellAnchor;
        public Transform BowAnchor => _bowAnchor;
        /// <summary>검증용: 주포 포신의 지금 앙각(도).</summary>
        public float MainGunElevation(int i)
        {
            if (i < 0 || i >= _main.Length || _main[i].Turret == null) return 0f;
            return 90f - Vector3.Angle(_main[i].Weapon.FireDirection, Vector3.up);
        }
        public BossParts.Part AaPart(int i) => i >= 0 && i < _aa.Count ? _aa[i].Part : null;
        public bool AaIsPort(int i) => i >= 0 && i < _aa.Count && _aa[i].Port;
        public int AaCount => _aa.Count;
        /// <summary>지친 정도(격추 수 — 올림해 센다).</summary>
        public int AaFatigue => Mathf.CeilToInt(_aaFatigue - 1e-4f);
        /// <summary>검증용: 지친 정도를 정한다.</summary>
        public void DevSetAaFatigue(float v) => _aaFatigue = Mathf.Max(0f, v);
        /// <summary>엘리베이터에 실린 드론 사본이 보이는가(검증용).</summary>
        public int DronesOnElevator { get { int n = 0; foreach (var d in _droneDummies) if (d != null && d.activeSelf) n++; return n; } }

        /// <summary>지금 그 현에서 오는 유도탄을 떨어뜨릴 확률: 기본 × 살아 있는 비율 − 격추마다 깎인 만큼(0 아래로는 안 감).</summary>
        public float InterceptChance(bool port)
        {
            int alive = 0, total = 0;
            foreach (var g in _aa)
            {
                if (g.Port != port) continue;
                total++;
                if (g.Part == null || !g.Part.Destroyed) alive++;
            }
            if (total == 0) return 0f;
            return Mathf.Max(0f, aaBaseChance * alive / total - aaChanceDropPerKill * AaFatigue);
        }

        private bool MainBatteryLeft => (_main[0].Part != null && !_main[0].Part.Destroyed) || (_main[1].Part != null && !_main[1].Part.Destroyed);
        private bool RadarDown => _radarPart != null && _radarPart.Destroyed;
        private bool DronesAvailable => Definition != null && Definition.LaunchedDrone != null && _elevatorPivot != null && !_parts.IsDestroyed(_elevatorPart);
        private bool BoatsAvailable => sternBoat != null && _gatePivot != null && _boatExit != null && !_parts.IsDestroyed(_wellPart);
        private bool WaveAvailable => DronesAvailable || (_phase >= 2 && BoatsAvailable);
        private string WaveName => _phase >= 2 && BoatsAvailable
            ? (DronesAvailable ? "드론 + 자폭 보트 총공격" : "함미 자폭 보트")
            : "엘리베이터 드론 발진";

        // ------------------------------------------------------------ 준비

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            Bind();
            StopAllCoroutines();
            ResetPose();
            _phase = 0;
            _salvoTimer = 3f;
            _waveTimer = 3f;
            _aaJudged.Clear();
            _aaFatigue = 0f;
            DevAaJudged = DevAaKills = DevSideShots = DevSalvos = DevDronesLaunched = DevBoatsLaunched = 0;
            DevLastWellMultiplier = 1f;
            foreach (var g in _sideGuns) { g.Rounds = 0; g.Cooldown = 1.5f; }

            if (!_indicatorBuilt)
            {
                _indicatorBuilt = true;
                EnemyFireCutout.BuildIndicator(transform, mainBatteryCutouts, cutoutZoneRadius, cutoutZoneMaterial);
            }
            _hull = GetComponent<Collider>();
            _playerHull = player != null ? player.GetComponent<Collider>() : null;
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;

            float maxHp = def != null ? def.MaxHp : 3000f;
            _parts.Clear();
            for (int i = 0; i < _main.Length; i++)
                _main[i].Part = _parts.Add($"주포 {i + 1}", $"{i + 1}번 주포 사격 중지", _main[i].Turret, 4f, maxHp * 0.09f, _main[i].Turret);
            foreach (var g in _sideGuns)
                g.Part = _parts.Add(g.Port ? "좌현 기관포" : "우현 기관포", (g.Port ? "좌현" : "우현") + " 기관포 사격 중지", g.Turret, 2.4f, maxHp * 0.05f, g.Turret);
            int portN = 0, stbdN = 0;
            foreach (var g in _aa)
            {
                string name = g.Port ? $"대공포 좌{++portN}" : $"대공포 우{++stbdN}";
                g.Part = _parts.Add(name, (g.Port ? "좌현" : "우현") + " 요격 확률 감소", g.Turret, 2.2f, maxHp * 0.03f, g.Turret);
                if (g.Part != null) g.Part.Group = "대공포";
            }
            _elevatorPart = _parts.Add("엘리베이터", "드론 발진 중지", _elevatorUpper, 5f, maxHp * 0.10f, _elevatorPivot);
            if (_elevatorPart != null) { _elevatorPart.Shielded = true; _elevatorPart.Priority = true; }
            _wellPart = _parts.Add("함미 웰덱", "자폭 보트 출격 중지", _wellAnchor, 4.5f, maxHp * 0.10f, _gatePivot);
            if (_wellPart != null) { _wellPart.Shielded = true; _wellPart.Priority = true; }
            _radarPart = _parts.Add("레이더", "주포·기관포 조준 흐려짐", _radar, 1.8f, maxHp * 0.04f, _radar);
            GameEvents.RaiseBossPhaseChanged("항공전함 출현 — 현측 기관포 · 가까이 가면 주포 직사 · 미사일은 대공포가 요격");
        }

        /// <summary>모델 소켓을 이름으로 찾아 연결한다(한 번). 좌·우는 이름이 아니라 실제 위치로(FBX 축 보정에 따라 뒤집힐 수 있다).</summary>
        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            var all = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (!all.ContainsKey(t.name)) all[t.name] = t;
                t.gameObject.layer = gameObject.layer;   // 모델 교체로 프리팹의 노드별 레이어 덮어쓰기가 빠진 노드도 적 레이어로
            }
            Transform F(string n) => all.TryGetValue(n, out var t) ? t : null;

            // 주포: MainGun_0n → MainGun_Elevation_0n → MainGun_Muzzle_01~04(포탑 아래에 있는 것)
            for (int i = 1; i <= 4; i++) { var m = F($"MainGun_Muzzle_{i:00}"); if (m != null) _mainMuzzles.Add(m); }
            for (int i = 0; i < _main.Length; i++)
            {
                var g = _main[i];
                g.Turret = F($"MainGun_{i + 1:00}");
                if (g.Turret == null) continue;
                foreach (var m in _mainMuzzles) if (m.IsChildOf(g.Turret)) { g.Muzzle = m; break; }
                g.Weapon.BindArtTransforms(g.Turret, F($"MainGun_Elevation_{i + 1:00}"), g.Muzzle);
                g.Weapon.SetElevationLimits(-5f, 45f);
                if (g.Muzzle != null) g.Weapon.SetFireArc(ShipSectorArc(g.Turret, g.Muzzle, -135f, 135f));
            }

            // 함교 양현 기관포
            foreach (var side in new[] { "BridgePort", "BridgeStarboard" })
            {
                var yaw = F($"AuxGunYaw_{side}");
                var muzzle = F($"AuxGunMuzzle_{side}");
                if (yaw == null || muzzle == null) continue;
                var g = new Gun { Turret = yaw, Muzzle = muzzle, Port = transform.InverseTransformPoint(yaw.position).x < 0f };
                g.Weapon.BindArtTransforms(yaw, F($"AuxGunElevation_{side}"), muzzle);
                g.Weapon.SetElevationLimits(-20f, 35f);
                g.Weapon.SetFireArc(g.Port ? ShipSectorArc(yaw, muzzle, -178f, -4f) : ShipSectorArc(yaw, muzzle, 4f, 178f));
                _sideGuns.Add(g);
            }

            // 비행갑판 대공포 좌3·우3
            foreach (var side in new[] { "Port", "Starboard" })
                for (int i = 1; i <= 3; i++)
                {
                    var yaw = F($"AA_{side}_{i:00}");
                    var muzzle = F($"AA_{side}_Muzzle_{i:00}");
                    if (yaw == null || muzzle == null) continue;
                    var g = new Gun { Turret = yaw, Muzzle = muzzle, Port = transform.InverseTransformPoint(yaw.position).x < 0f };
                    g.Weapon.BindArtTransforms(yaw, F($"AA_{side}_Elevation_{i:00}"), muzzle);
                    g.Weapon.SetElevationLimits(-5f, 85f);
                    _aa.Add(g);
                }

            // 드론 엘리베이터: 판이 Upper → Lower로 내려간다
            _elevatorPivot = F("DroneElevatorPivot");
            _elevatorUpper = F("DroneElevatorUpper");
            var lower = F("DroneElevatorLower");
            _elevatorMount = F("DroneElevatorMount");
            if (_elevatorPivot != null && _elevatorUpper != null && lower != null && _elevatorPivot.parent != null)
            {
                _elevatorRest = _elevatorPivot.localPosition;
                _elevatorTravel = _elevatorPivot.parent.InverseTransformVector(lower.position - _elevatorUpper.position);
            }
            else _elevatorPivot = null;

            // 함미 램프: 선체 가로축으로 openDegrees — 문 몸체가 뒤·아래로 가는 쪽을 연다
            _gatePivot = F("SternGatePivot");
            _boatExit = F("SternBoatExit") ?? F("SternBoatStaging");
            if (_gatePivot != null && _gatePivot.parent != null)
            {
                _gateRest = _gatePivot.localRotation;
                _gateAxis = _gatePivot.parent.InverseTransformDirection(transform.right);
                var mfNode = F("SternGatePivot_Mesh");   // 모델 노드는 빈 피벗 아래 "…_Mesh" 자식에 메시가 있다
                var mf = mfNode != null ? mfNode.GetComponent<MeshFilter>() : _gatePivot.GetComponentInChildren<MeshFilter>();
                Vector3 arm = (mf != null && mf.sharedMesh != null ? mf.transform.TransformPoint(mf.sharedMesh.bounds.center) : _gatePivot.position + Vector3.up) - _gatePivot.position;
                float Score(float sign)
                {
                    Vector3 r = Quaternion.AngleAxis(sign * gateOpenDegrees, transform.right) * arm;
                    return -Vector3.Dot(r, transform.forward) - Vector3.Dot(r, transform.up);
                }
                _gateSign = Score(1f) >= Score(-1f) ? 1f : -1f;
            }

            _radar = F("RadarPivot");

            // 겨냥점: 함수(주포 사이) · 함교 · 함미(웰덱) — 선체 중앙 높이로
            Vector3 Mid(Vector3 w) { var l = transform.InverseTransformPoint(w); l.y = 1.5f; return l; }
            Transform Anchor(string n, Vector3 local)
            {
                var t = new GameObject(n).transform;
                t.SetParent(transform, false);
                t.localPosition = local;
                return t;
            }
            Vector3 bow = _main[0].Turret != null && _main[1].Turret != null ? (_main[0].Turret.position + _main[1].Turret.position) * 0.5f : transform.position + transform.forward * 9f;
            _bowAnchor = Anchor("AimAnchor_Bow", Mid(bow));
            var bridge = F("BridgeAssembly_Mesh") ?? F("BridgeAssembly");
            var bridgeR = bridge != null ? bridge.GetComponentInChildren<Renderer>() : null;
            _bridgeAnchor = Anchor("AimAnchor_Bridge", Mid(bridgeR != null ? bridgeR.bounds.center : transform.position + transform.forward * 3f));
            var well = F("SternWellDeck_Mesh") ?? F("SternWellDeck");
            var wellR = well != null ? well.GetComponentInChildren<Renderer>() : null;
            Vector3 wellPos = wellR != null ? wellR.bounds.center : _boatExit != null ? _boatExit.position : transform.position - transform.forward * 14f;
            var wl = transform.InverseTransformPoint(wellPos);
            _wellAnchor = Anchor("AimAnchor_Well", new Vector3(wl.x, Mathf.Max(0.6f, wl.y), wl.z));
        }

        /// <summary>선체 기준 방위 [minShip, maxShip] 안으로만 선회하는 사격각(포신의 쉬는 방향 기준으로 바꿔 넣는다).</summary>
        private FireArc ShipSectorArc(Transform turret, Transform muzzle, float minShip, float maxShip)
        {
            Vector3 d = transform.InverseTransformDirection(muzzle.position - turret.position);
            float rest = d.x * d.x + d.z * d.z > 1e-6f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
            var noTrain = new bool[FireArc.Steps];
            for (int i = 0; i < noTrain.Length; i++)
            {
                float ship = Mathf.DeltaAngle(0f, (i + 0.5f) * FireArc.StepDeg + rest);
                noTrain[i] = ship < minShip || ship > maxShip;
            }
            return new FireArc(null, noTrain);
        }

        private void ResetPose()
        {
            _elevatorBusy = _gateBusy = false;
            ShowDroneDummies(false);
            SetElevator(0f);
            SetGate(0f);
        }

        private void SetElevator(float t)
        {
            _elevator01 = Mathf.Clamp01(t);
            if (_elevatorPivot != null) _elevatorPivot.localPosition = _elevatorRest + _elevatorTravel * _elevator01;
        }

        private void SetGate(float t)
        {
            _gate01 = Mathf.Clamp01(t);
            if (_gatePivot != null) _gatePivot.localRotation = Quaternion.AngleAxis(_gateSign * gateOpenDegrees * _gate01, _gateAxis) * _gateRest;
        }

        /// <summary>보스는 기함과의 결투로 설계됐다 — 호위함으로 표적을 돌리지 않는다.</summary>
        protected override bool TargetsEscorts => false;

        // ------------------------------------------------------------ 매 프레임

        protected override void UpdateBehaviour(float dt)
        {
            UpdatePhase();

            if (!DevStationary)
            {
                float range = Definition.PreferredRange * (_phase == 2 ? 0.75f : 1f);
                OrbitPlayer(range, _phase == 2 ? 0.9f : 0.6f, dt);
            }

            _parts.Tick(dt);
            UpdateExposure();
            UpdateMainBattery(dt);
            foreach (var g in _sideGuns) UpdateSideGun(g, dt);
            if (_phase >= 1) UpdateWaves(dt);
            UpdateAntiAir(dt);

            if (_radar != null && !RadarDown) _radar.Rotate(Vector3.up, radarRpm * 6f * dt, Space.World);
        }

        private void UpdatePhase()
        {
            int next = HpFraction <= finalPhaseAt ? 2 : HpFraction <= carrierPhaseAt ? 1 : 0;
            if (next <= _phase) return;
            _phase = next;
            _waveTimer = 2f;
            GameEvents.RaiseBossPhaseChanged(_phase == 1
                ? "항모 모드 — 엘리베이터에서 드론 편대(내려간 엘리베이터가 약점)"
                : "총력전 — 드론 + 함미 램프 자폭 보트(열린 함미가 약점)");
        }

        /// <summary>약점: 엘리베이터는 내려가 있을 때만, 웰덱은 램프가 반 넘게 열렸을 때만 피해를 받는다.</summary>
        private void UpdateExposure()
        {
            if (_elevatorPart != null) _elevatorPart.Shielded = _elevator01 < 0.05f;
            if (_wellPart != null) _wellPart.Shielded = _gate01 < 0.5f;
        }

        private Vector3 PlayerVelocity => _playerShip != null ? Player.forward * _playerShip.CurrentSpeed : Vector3.zero;

        // ------------------------------------------------------------ 주포

        private void UpdateMainBattery(float dt)
        {
            float dist = Vector3.Distance(transform.position, Player.position);
            bool inRange = dist <= gunRange;
            float flight = Mathf.Clamp(minFlightTime + dist / 60f, minFlightTime, maxFlightTime);
            Vector3 predicted = RadarDown ? Player.position : Player.position + PlayerVelocity * flight;
            predicted.y = 0f;

            float apex = 0f;
            for (int i = 0; i < _main.Length; i++)
            {
                var g = _main[i];
                _mainAimed[i] = false;
                if (g.Turret == null || g.Muzzle == null) continue;
                var pivot = g.Muzzle.parent != null ? g.Muzzle.parent : g.Turret;
                Vector3 flat = predicted - pivot.position; flat.y = 0f;
                float d = Mathf.Max(1f, flat.magnitude);
                float a = apexBase + apexPerDistSq * d * d;
                apex = Mathf.Max(apex, a);
                // 포물선 첫 기울기 = (4 × 최고점 + 높이 차) / 거리 → 그 앙각으로 포신을 든다
                float theta = Mathf.Atan2(4f * a + (predicted.y - g.Muzzle.position.y), d);
                Vector3 aimPoint = pivot.position + flat / d * 10f + Vector3.up * (10f * Mathf.Tan(theta));
                g.Weapon.AimAt(aimPoint, turretTurnRate, dt);
                g.Weapon.Tick(dt);
                _mainAimed[i] = inRange && !g.Part.Destroyed && g.Weapon.IsInFireArc(predicted) && g.Weapon.IsAimedAt(aimPoint);
            }

            _salvoTimer -= dt;
            if (_salvoTimer > 0f) return;
            if (!inRange || PlayerConcealed) { _salvoTimer = 0.5f; return; }

            var muzzles = new List<(Transform muzzle, Gun gun)>(4);
            for (int i = 0; i < _main.Length; i++)
            {
                if (!_mainAimed[i]) continue;
                foreach (var m in _mainMuzzles) if (m.IsChildOf(_main[i].Turret)) muzzles.Add((m, _main[i]));
            }
            if (muzzles.Count == 0) { _salvoTimer = 0.25f; return; }   // 포탑이 도는 중이거나 선미 사각 — 선회해 현측을 돌린다

            _salvoTimer = salvoInterval[Mathf.Clamp(_phase, 0, salvoInterval.Length - 1)];
            DevLastFlightTime = flight;
            DevSalvos++;
            StartCoroutine(FireSalvo(predicted, flight, apex, muzzles));
        }

        private IEnumerator FireSalvo(Vector3 predicted, float flight, float apex, List<(Transform muzzle, Gun gun)> muzzles)
        {
            if (shellPrefab == null) yield break;
            float spread = salvoSpread * (RadarDown ? 1.7f : 1f);
            for (int i = 0; i < shellsPerSalvo; i++)
            {
                if (!IsAlive || Player == null) yield break;
                var (muzzle, gun) = muzzles[i % muzzles.Count];
                if (muzzle == null || gun.Part.Destroyed) continue;
                Vector2 jitter = i == 0 && !RadarDown ? Vector2.zero : Random.insideUnitCircle * spread;
                Vector3 impact = predicted + new Vector3(jitter.x, 0f, jitter.y);
                var go = PoolManager.Instance?.Spawn(shellPrefab, muzzle.position, muzzle.rotation);
                var shell = go != null ? go.GetComponent<ArtilleryShell>() : null;
                if (shell != null)
                {
                    shell.Launch(muzzle.position, impact, flight, shellDamage, shellBlastRadius, _playerHull, "항공전함 주포");
                    shell.SetApex(apex);
                }
                gun.Weapon.Kick(0.35f);
                PooledEffect.Spawn(muzzleFlash, muzzle.position);
                AudioManager.Play(Game.Data.SfxId.EnemyGunShot, muzzle.position, 1f, 0.75f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        // ------------------------------------------------------------ 함교 양현 기관포

        private void UpdateSideGun(Gun g, float dt)
        {
            if (g.Part == null || g.Part.Destroyed) return;
            g.Weapon.Tick(dt);
            Vector3 target = Player.position + Vector3.up * 0.6f;
            Vector3 aim = RadarDown ? target : Ballistics.PredictIntercept(g.Muzzle.position, target, PlayerVelocity, sideGunSpeed);
            g.Weapon.AimAt(aim, sideGunTurnRate, dt);

            if (g.Cooldown > 0f) { g.Cooldown -= dt; return; }
            bool can = Vector3.Distance(g.Muzzle.position, Player.position) <= sideGunRange && !PlayerConcealed && !PlayerBehindIsland
                       && g.Weapon.IsInFireArc(aim) && g.Weapon.IsAimedAt(aim);
            if (g.Rounds <= 0)
            {
                if (!can) return;
                g.Rounds = sideGunBurst;
                g.ShotTimer = 0f;
            }
            g.ShotTimer -= dt;
            if (g.ShotTimer > 0f) return;
            if (!can) { g.Rounds = 0; g.Cooldown = 0.6f; return; }

            if (gunProjectilePrefab != null)
            {
                float s = sideGunSpread * (RadarDown ? 2f : 1f);
                Vector3 dir = Quaternion.Euler(Random.Range(-s, s), Random.Range(-s, s), 0f) * g.Weapon.FireDirection;
                var shot = PoolManager.Instance?.Spawn(gunProjectilePrefab, g.Muzzle.position, Quaternion.LookRotation(dir));
                shot?.GetComponent<Projectile>()?.Launch(dir, sideGunSpeed, sideGunDamage, DamageSource.Gun);
                PooledEffect.Spawn(muzzleFlash, g.Muzzle.position, 0.35f);
                AudioManager.Play(Game.Data.SfxId.EnemyAutocannonShot, g.Muzzle.position, 0.45f, 1.05f);
                DevSideShots++;
            }
            g.Rounds--;
            g.ShotTimer = sideGunShotInterval;
            if (g.Rounds <= 0) g.Cooldown = sideGunCooldown[Mathf.Clamp(_phase, 0, sideGunCooldown.Length - 1)];
        }

        // ------------------------------------------------------------ 대공포

        /// <summary>
        /// 선체 aaRange 안으로 처음 들어온 아군 유도탄마다 한 번: InterceptChance(그 현). 격추로 정해지면 바로 지친 정도 +1
        /// (같은 순간 들어온 다음 미사일은 깎인 확률로). 지친 정도는 aaRecoverSeconds마다 1씩 돌아온다.
        /// </summary>
        private void UpdateAntiAir(float dt)
        {
            foreach (var g in _aa) { g.Weapon.Tick(dt); if (g.Busy > 0f) g.Busy -= dt; }
            if (_aa.Count == 0) return;
            if (_aaFatigue > 0f) _aaFatigue = Mathf.Max(0f, _aaFatigue - dt / Mathf.Max(0.1f, aaRecoverSeconds));
            _parts.SetGroupNote("대공포", $"대공 {Mathf.Max(InterceptChance(true), InterceptChance(false)) * 100f:0}%");
            var list = Missile.ActiveFriendly;
            if (_aaJudged.Count > 256) _aaJudged.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || !m.IsAlive || _aaJudged.Contains(m.LaunchSerial)) continue;
                Vector3 mp = m.transform.position;
                Vector3 closest = _hull != null ? _hull.ClosestPoint(mp) : transform.position;
                Vector3 flat = mp - closest; flat.y = 0f;
                if (flat.sqrMagnitude > aaRange * aaRange) continue;

                _aaJudged.Add(m.LaunchSerial);
                DevAaJudged++;
                bool port = transform.InverseTransformPoint(mp).x < 0f;
                int alive = 0, total = 0;
                Gun shooter = null;
                float best = float.MaxValue;
                foreach (var g in _aa)
                {
                    if (g.Port != port) continue;
                    total++;
                    if (g.Part != null && g.Part.Destroyed) continue;
                    alive++;
                    float d = (g.Muzzle.position - mp).sqrMagnitude;
                    if (d < best) { best = d; shooter = g; }
                }
                float chance = InterceptChance(port);
                bool kill = shooter != null && Random.value < chance;
                if (kill) _aaFatigue = AaFatigue + 1f;   // 격추마다 −10%p(회복 중이던 몫은 버리고 한 단계 더)
                if (shooter != null) StartCoroutine(AaEngage(shooter, m, m.LaunchSerial, kill));
            }
        }

        private IEnumerator AaEngage(Gun g, Missile m, int serial, bool kill)
        {
            g.Busy = 0.5f;
            for (int shot = 0; shot < 5; shot++)
            {
                if (!IsAlive || m == null || !m.IsAlive || m.LaunchSerial != serial || g.Part.Destroyed) yield break;
                Vector3 aim = Ballistics.PredictIntercept(g.Muzzle.position, m.transform.position, m.Velocity, aaProjectileSpeed);
                if (!kill) aim += Vector3.Cross(Vector3.up, (aim - g.Muzzle.position).normalized) * 2.5f;   // 빗나가는 점사
                g.Weapon.AimAt(aim, 1500f, Time.deltaTime);
                if (aaProjectilePrefab != null)
                {
                    Vector3 dir = (aim - g.Muzzle.position).normalized;
                    var go = PoolManager.Instance?.Spawn(aaProjectilePrefab, g.Muzzle.position, Quaternion.LookRotation(dir));
                    go?.GetComponent<Projectile>()?.Launch(dir, aaProjectileSpeed, 0f, DamageSource.Gun);
                }
                AudioManager.Play(Game.Data.SfxId.EnemyAutocannonShot, g.Muzzle.position, 0.3f, 1.25f);
                if (kill && shot == 1)
                {
                    m.TakeDamage(new DamageInfo(99999f, m.transform.position, Vector3.up, DamageSource.Gun, "항공전함 대공포"));
                    DevAaKills++;
                    yield break;
                }
                yield return new WaitForSeconds(0.05f);
            }
        }

        // ------------------------------------------------------------ 드론 엘리베이터 · 함미 보트

        private void UpdateWaves(float dt)
        {
            if (_elevatorBusy || _gateBusy || !WaveAvailable) return;
            _waveTimer -= dt;
            if (_waveTimer > 0f) return;
            _waveTimer = waveInterval[Mathf.Clamp(_phase - 1, 0, waveInterval.Length - 1)];
            if (DronesAvailable) StartCoroutine(ElevatorCycle());
            if (_phase >= 2 && BoatsAvailable) StartCoroutine(SternCycle());
        }

        /// <summary>엘리베이터: 내려감 → 싣기 → 올라옴 → 드론 2기 이륙, 3번. 내려가 있는 동안 약점. 부서지면 그 자리에 멈춘다.</summary>
        private IEnumerator ElevatorCycle()
        {
            _elevatorBusy = true;
            var spawner = EnemySpawner.Instance;
            EnsureDroneDummies();
            for (int lift = 0; lift < liftsPerWave; lift++)
            {
                for (float t = 0f; t < elevatorMoveTime; t += Time.deltaTime)
                {
                    if (_parts.IsDestroyed(_elevatorPart) || !IsAlive) { StopElevator(); yield break; }
                    SetElevator(t / elevatorMoveTime);
                    yield return null;
                }
                SetElevator(1f);
                ShowDroneDummies(true);   // 격납고에서 판에 싣는다
                for (float t = 0f; t < elevatorLoadTime; t += Time.deltaTime)
                {
                    if (_parts.IsDestroyed(_elevatorPart) || !IsAlive) { StopElevator(); yield break; }
                    yield return null;
                }
                for (float t = 0f; t < elevatorMoveTime; t += Time.deltaTime)
                {
                    if (_parts.IsDestroyed(_elevatorPart) || !IsAlive) { StopElevator(); yield break; }
                    SetElevator(1f - t / elevatorMoveTime);
                    yield return null;
                }
                SetElevator(0f);
                yield return new WaitForSeconds(0.25f);   // 갑판에 올라온 모습을 잠깐 보여 주고
                var origin = _elevatorMount != null ? _elevatorMount : _elevatorUpper;
                Vector3 fwdFlat = transform.forward; fwdFlat.y = 0f; fwdFlat.Normalize();
                if (spawner != null && Definition.LaunchedDrone != null && origin != null)
                    for (int k = 0; k < dronesPerLift; k++)
                    {
                        // 실려 온 사본 자리에서 진짜 드론으로 바꿔 이륙
                        var dummy = k < _droneDummies.Count ? _droneDummies[k] : null;
                        Vector3 at = dummy != null && dummy.activeSelf ? dummy.transform.position : origin.position + transform.right * DroneSide(k) + fwdFlat * DroneStagger(k);
                        if (spawner.SpawnAt(Definition.LaunchedDrone, at + Vector3.up * 0.15f, LaunchRotation()) != null)
                            DevDronesLaunched++;
                    }
                ShowDroneDummies(false);
                AudioManager.Play(Game.Data.SfxId.MissileLaunch, origin != null ? origin.position : transform.position, 0.35f, 1.4f);
                yield return new WaitForSeconds(0.4f);
            }
            _elevatorBusy = false;
        }

        private void StopElevator()
        {
            ShowDroneDummies(false);
            _elevatorBusy = false;
        }

        private float DroneSide(int k) => dronesPerLift > 1 ? Mathf.Lerp(-0.6f, 0.6f, k / (float)(dronesPerLift - 1)) : 0f;
        /// <summary>날개가 겹치지 않게 앞뒤로 엇갈려 싣는다(판 약 2.9 × 3.1m).</summary>
        private float DroneStagger(int k) => dronesPerLift > 1 ? (k % 2 == 0 ? 0.55f : -0.55f) : 0f;

        /// <summary>
        /// 엘리베이터에 실을 드론 겉모습 사본(드론 프리팹의 ModelPivot만 복제 — 동작·충돌 없음, 적 도색)을 한 번 만든다.
        /// 엘리베이터 판(DroneElevatorPivot)의 자식이라 판과 함께 오르내린다. 판이 위에 있을 때 드론 자리(DroneElevatorMount) 양옆에 놓는다.
        /// </summary>
        private void EnsureDroneDummies()
        {
            if (_droneDummies.Count >= dronesPerLift || _elevatorPivot == null || Definition == null || Definition.LaunchedDrone == null) return;
            var prefab = Definition.LaunchedDrone.Prefab;
            var src = prefab != null ? prefab.transform.Find("ModelPivot") : null;
            if (src == null) return;
            var origin = _elevatorMount != null ? _elevatorMount : _elevatorUpper;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            float lift01 = _elevator01;
            SetElevator(0f);   // 위 자세에서 자리를 잡는다
            for (int k = _droneDummies.Count; k < dronesPerLift; k++)
            {
                var go = Instantiate(src.gameObject);
                go.name = "DroneOnElevator";
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
                Game.View.EnemyPaint.Apply(go, true);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
                go.transform.SetPositionAndRotation(origin.position + transform.right * DroneSide(k) + fwd.normalized * DroneStagger(k) + Vector3.up * 0.05f,
                                                    Quaternion.LookRotation(fwd.normalized, Vector3.up) * src.localRotation);
                go.transform.SetParent(_elevatorPivot, true);
                go.SetActive(false);
                _droneDummies.Add(go);
            }
            SetElevator(lift01);
        }

        private void ShowDroneDummies(bool on)
        {
            foreach (var d in _droneDummies) if (d != null) d.SetActive(on);
        }

        /// <summary>함미 램프: 열림 → 자폭 보트 3척 → 잠깐 → 닫힘. 열려 있는 동안 웰덱이 약점(피해 1.5배). 부서지면 열린 채 멈춘다.</summary>
        private IEnumerator SternCycle()
        {
            _gateBusy = true;
            for (float t = 0f; t < gateOpenTime; t += Time.deltaTime)
            {
                if (!IsAlive) { _gateBusy = false; yield break; }
                SetGate(t / gateOpenTime);
                yield return null;
            }
            SetGate(1f);
            var spawner = EnemySpawner.Instance;
            for (int b = 0; b < boatsPerWave; b++)
            {
                if (!IsAlive || _parts.IsDestroyed(_wellPart)) { _gateBusy = false; yield break; }
                if (spawner != null && sternBoat != null)
                {
                    Vector3 back = -transform.forward; back.y = 0f;
                    Vector3 at = _boatExit.position + back.normalized * 1.5f + transform.right * ((b % 3) - 1) * 1.2f;
                    at.y = transform.position.y;
                    if (spawner.SpawnAt(sternBoat, at, Quaternion.LookRotation(back.normalized, Vector3.up)) != null) DevBoatsLaunched++;
                }
                yield return new WaitForSeconds(boatSpacing);
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (!IsAlive || _parts.IsDestroyed(_wellPart)) { _gateBusy = false; yield break; }
                yield return null;
            }
            for (float t = 0f; t < gateOpenTime; t += Time.deltaTime)
            {
                if (!IsAlive || _parts.IsDestroyed(_wellPart)) { _gateBusy = false; yield break; }
                SetGate(1f - t / gateOpenTime);
                yield return null;
            }
            SetGate(0f);
            _gateBusy = false;
        }

        /// <summary>갑판 앞쪽으로, 살짝 들린 채 이륙한다.</summary>
        private Quaternion LaunchRotation()
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            return Quaternion.LookRotation((f.normalized + Vector3.up * 0.35f).normalized, Vector3.up);
        }

        // ------------------------------------------------------------ 겨냥점 · 피해

        /// <summary>
        /// 플레이어 무장이 겨눌 점: 열린 엘리베이터(현측 40~140° 또는 위에서 꽂히는 유도탄) → 열린 함미(뒤쪽 115° 밖 또는 유도탄) →
        /// 그 밖에는 쏘는 쪽에서 가장 가까운 부분(함수 주포 · 함교 · 함미).
        /// </summary>
        public Vector3 AimPointFor(Vector3 shooter, bool topAttack)
        {
            if (!IsAlive || _bowAnchor == null) return transform.position;
            Vector3 local = transform.InverseTransformPoint(shooter);
            float bearing = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
            if (ElevatorExposed && _elevatorUpper != null && (topAttack || (bearing >= 40f && bearing <= 140f))) return _elevatorUpper.position;
            if (GateExposed && (topAttack || bearing >= 115f)) return _wellAnchor.position;
            Transform best = _bridgeAnchor;
            float bestSqr = float.MaxValue;
            foreach (var a in new[] { _bowAnchor, _bridgeAnchor, _wellAnchor })
            {
                if (a == null) continue;
                Vector3 d = a.position - shooter; d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = a; }
            }
            return best != null ? best.position : transform.position;
        }

        public override void TakeDamage(in DamageInfo info)
        {
            var use = info;
            if (GateExposed && info.HitPoint != Vector3.zero)
            {
                Vector3 d = info.HitPoint - _wellAnchor.position; d.y = 0f;
                if (d.sqrMagnitude <= _wellPart.Radius * _wellPart.Radius)
                {
                    use = new DamageInfo(info.Amount * wellDeckBonus, info.HitPoint, info.Direction, info.Source, info.Tag);
                    DevLastWellMultiplier = wellDeckBonus;
                }
            }
            base.TakeDamage(use);
            if (IsAlive) _parts.Hit(use, this);
        }

        public override void OnDespawned()
        {
            StopAllCoroutines();
            ResetPose();
            _parts.Clear();
            base.OnDespawned();
        }

        protected override void Die()
        {
            StopAllCoroutines();
            base.Die();
            GameEvents.RaiseBossDefeated();
        }
    }
}
