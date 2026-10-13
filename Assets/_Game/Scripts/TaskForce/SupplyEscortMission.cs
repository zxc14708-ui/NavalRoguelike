using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.View;
using Game.World;

namespace Game.TaskForce
{
    /// <summary>
    /// 선택 임무 "보급선 호위"(2026-10-11 점검 K03, 2026-10-13 수송선단 호위 방식으로 개편). 보스 격침 뒤 항로 선택에서
    /// [3] 임무 항로를 고르면 다음 스테이지 시작에 열린다.
    ///   1. 합류: 스테이지가 시작되고 잠시 뒤 보급선이 기함 앞쪽 약 150m(StagingDistance)에서 기다린다. 기함이 45m 안으로 오면 출발
    ///      (45초 안에 안 오면 혼자 출발). 기다리는 동안 기함에서 보급선까지 초록 선.
    ///   2. 호위: 보급선은 출발지에서 도착지까지 정해진 항로(약 900m, 섬을 피해 75m마다 꺾임, 수로에서는 물길을 따라)를
    ///      순항 7.5m/s로 혼자 간다(약 2분). 수면에 금색 항로 선과 도착지 표시.
    ///   3. 보급선은 아군 수상 표적이라 적이 우선해서 노린다(최대 4척 동시). 아군 탄에는 맞지 않는다.
    ///   4. 성공: 도착지에 닿으면 선체 완전 수리 · 다시 뽑기 +1 · 정비 1회. 보급선은 계속 가서 사라진다.
    ///   5. 실패(보상 없음, 벌칙 없음): 보급선 격침, 또는 기함이 보급선에서 150m(EscortRange) 넘게 10초(LeashTime) 떨어짐.
    ///      100m를 넘으면 경고와 남은 시간, 보급선 둘레에 호위 범위 원.
    /// 항로 중간에 새로 생긴 섬은 보급선이 앞 두 구간을 계속 보고 막히면 남은 항로를 다시 짠다.
    /// 화면 위쪽 가운데에 단계·남은 거리·보급선 선체·기함과의 거리가 나온다. 판이 끝나거나 씬이 바뀌면 함께 사라진다.
    /// </summary>
    public sealed class SupplyEscortMission : MonoBehaviour
    {
        public const float RouteLength = 900f;
        public const float StagingDistance = 150f;
        public const float Cruise = 7.5f;
        public const float JoinRadius = 45f, JoinTimeout = 45f;
        public const float EscortRange = 150f, WarnRange = 100f, LeashTime = 10f;
        public const float ArriveRadius = 15f;
        public const float StartDelay = 3f;
        public const float SupplyHullHp = 140f;
        private const float Step = 75f;
        private const float WaterLine = 0.15f;   // 항로 선을 그리는 높이(NavRouteView와 같게)

        /// <summary>순서를 바꾸지 말고 뒤에만 덧붙인다(Waiting은 2026-10-13 추가).</summary>
        public enum Outcome { Pending, Running, Succeeded, Failed, Waiting }

        /// <summary>지금 진행 중인(또는 막 끝난) 임무. 없으면 null.</summary>
        public static SupplyEscortMission Current { get; private set; }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private SupplyShip _ship;
        private float _delay, _routeLength, _waited, _away;
        private List<Vector3> _route = new();
        private RectTransform _panel, _fill;
        private TMP_Text _label;
        private LineRenderer _routeLine, _joinLine, _rangeRing, _joinRing, _goalRing;
        private Transform _beacon;

        public Outcome State { get; private set; } = Outcome.Pending;
        /// <summary>마지막으로 끝난 임무의 결과(검증·결과 화면용).</summary>
        public static Outcome LastOutcome { get; private set; } = Outcome.Pending;
        /// <summary>마지막 실패 이유("격침"·"이탈"). 성공이면 null.</summary>
        public static string LastFailReason { get; private set; }
        public SupplyShip Ship => _ship;
        public IReadOnlyList<Vector3> Route => _route;
        public Vector3 Destination => _route.Count > 0 ? _route[_route.Count - 1] : Vector3.zero;
        /// <summary>도착지까지 남은 항로 거리(m).</summary>
        public float RemainingDistance => _ship != null ? _ship.RemainingDistance : _routeLength;
        public float DistanceToFlagship => _ship != null && _player != null ? Flat(_ship.transform.position - _player.transform.position).magnitude : 0f;
        /// <summary>호위 범위를 벗어난 채 남은 시간(벗어나 있지 않으면 LeashTime).</summary>
        public float LeashLeft => Mathf.Max(0f, LeashTime - _away);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRun() { Current = null; LastOutcome = Outcome.Pending; LastFailReason = null; }

        public static SupplyEscortMission Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp,
                                                float routeLength = RouteLength, float delay = StartDelay)
        {
            if (Current != null) Destroy(Current.gameObject);
            var mission = new GameObject("Supply escort mission").AddComponent<SupplyEscortMission>();
            mission._player = player;
            mission._xp = xp;
            mission._routeLength = routeLength;
            mission._delay = delay;
            Current = mission;
            GameEvents.RaiseBanner($"임무 항로 — 앞쪽에서 보급선이 기다립니다. 도착지까지 약 {routeLength:0}m 호위하면 선체 완전 수리 · 다시 뽑기 +1 · 정비 1회");
            return mission;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_panel != null) Destroy(_panel.gameObject);
            if (_ship != null && (State == Outcome.Running || State == Outcome.Waiting)) Destroy(_ship.gameObject);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsRunOver) { if (State == Outcome.Running || State == Outcome.Waiting) Fail(null); return; }
            if (gm.State != GameState.Playing) return;
            float dt = Time.deltaTime;

            if (State == Outcome.Pending)
            {
                _delay -= dt;
                if (_delay <= 0f) Spawn();
                return;
            }
            if (State != Outcome.Running && State != Outcome.Waiting) return;
            if (_ship == null || !_ship.IsAlive) { Fail("격침"); return; }
            if (_player == null) { Fail(null); return; }

            float dist = DistanceToFlagship;
            if (State == Outcome.Waiting)
            {
                _waited += dt;
                if (dist <= JoinRadius) Depart(true);
                else if (_waited >= JoinTimeout) Depart(false);
            }
            else
            {
                // 호위 범위: 넘어선 시간이 쌓이면 실패, 돌아오면 다시 처음부터
                _away = dist > EscortRange ? _away + dt : 0f;
                if (_away >= LeashTime) { Fail("이탈"); return; }
                if (_ship.Arrived) { Succeed(); return; }
            }
            RefreshPanel();
            RefreshMarks(dist);
        }

        // ------------------------------------------------------------ 항로

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>그 구간을 배가 지나갈 수 있는가: 끝점 둘레에 섬이 없고, 사이를 섬이 가리지 않고, 물길 안.</summary>
        public static bool SegmentClear(Vector3 a, Vector3 b)
            => Islands.IsClear(b, 14f) && !Islands.BlocksShipLine(a, b) && SeaRegion.InWater(b, 18f);

        /// <summary>
        /// start에서 dir 쪽으로 length만큼 가는 항로(Step 간격 꺾은선). 각 점은 펄린 잡음으로 ±20° 흔들리고, 막히면 옆으로
        /// 25·50·75m 비켜 본다. 수로에서는 물길 방향을 따라 가운데 쪽으로 둔다. 첫 점은 start.
        /// </summary>
        public static List<Vector3> PlanRoute(Vector3 start, Vector3 dir, float length, int seed)
        {
            dir = Flat(dir).sqrMagnitude > 1e-4f ? Flat(dir).normalized : Vector3.forward;
            if (SeaRegion.IsStrait) dir = Vector3.Dot(dir, SeaRegion.ChannelAxis) >= 0f ? SeaRegion.ChannelAxis : -SeaRegion.ChannelAxis;
            var right = new Vector3(dir.z, 0f, -dir.x);
            var route = new List<Vector3> { Flat(start) };
            var prev = Flat(start);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / Step));
            float step = length / count;
            float[] tries = { 0f, 25f, -25f, 50f, -50f, 75f, -75f };
            for (int i = 1; i <= count; i++)
            {
                float swing = (Mathf.PerlinNoise(seed * 0.013f + 3.1f, i * 0.37f) - 0.5f) * 40f;
                var forward = Quaternion.Euler(0f, swing, 0f) * dir;
                Vector3 chosen = prev + forward * step;
                foreach (float side in tries)
                {
                    var p = prev + forward * step + right * side;
                    if (SeaRegion.IsStrait)
                    {
                        float lat = Mathf.Clamp(SeaRegion.Lateral(p), -(SeaRegion.ChannelHalfWidth - 22f), SeaRegion.ChannelHalfWidth - 22f);
                        p = SeaRegion.ChannelPoint(SeaRegion.Along(p), lat);
                        p.y = 0f;
                    }
                    if (SegmentClear(prev, p)) { chosen = p; break; }
                }
                route.Add(chosen);
                prev = chosen;
            }
            return route;
        }

        private void Spawn()
        {
            if (_player == null) { Destroy(gameObject); return; }
            var t = _player.transform;
            var dir = Flat(t.forward).normalized;
            // 출발지: 앞쪽 150m, 막혔으면 옆으로 비켜 본다
            var right = new Vector3(dir.z, 0f, -dir.x);
            Vector3 start = Flat(t.position) + dir * StagingDistance;
            foreach (float side in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
            {
                var p = Flat(t.position) + dir * StagingDistance + right * side;
                if (SeaRegion.IsStrait)
                {
                    p = SeaRegion.ChannelPoint(SeaRegion.Along(p), Mathf.Clamp(SeaRegion.Lateral(p), -30f, 30f));
                    p.y = 0f;
                }
                if (Islands.IsClear(p, 16f) && SeaRegion.InWater(p, 18f)) { start = p; break; }
            }
            _route = PlanRoute(start, dir, _routeLength, RunSeed.Current + Mathf.RoundToInt(start.x * 7f + start.z * 13f));
            var first = _route.Count > 1 ? Flat(_route[1] - _route[0]) : dir;
            _ship = SupplyShip.Create(start, Mathf.Atan2(first.x, first.z) * Mathf.Rad2Deg, this);
            _ship.SetRoute(_route);
            State = Outcome.Waiting;
            BuildPanel();
            BuildMarks();
            RefreshPanel();
            UiSound.Play(UiSound.Cue.Confirm);
            GameEvents.RaiseBanner($"보급선 대기 — 앞쪽 {StagingDistance:0}m, 초록 선을 따라 가서 합류하세요(45m 안 → 출발)");
        }

        /// <summary>합류(또는 기다리다 혼자) 출발.</summary>
        private void Depart(bool joined)
        {
            State = Outcome.Running;
            _ship.Go();
            UiSound.Play(UiSound.Cue.Confirm);
            GameEvents.RaiseBanner(joined
                ? $"보급선 출발 — 금색 항로를 따라 도착지까지 호위(약 {_routeLength:0}m). 보급선에서 {EscortRange:0}m 넘게 {LeashTime:0}초 떨어지면 실패"
                : $"보급선이 혼자 출발했습니다 — 따라잡으세요. {EscortRange:0}m 넘게 {LeashTime:0}초 떨어지면 실패");
        }

        /// <summary>항로가 바뀌었다(앞에 새 섬). 도착지 표시를 옮긴다.</summary>
        internal void OnRouteChanged(List<Vector3> route)
        {
            _route = route;
            if (_beacon != null) _beacon.position = new Vector3(Destination.x, 0f, Destination.z);
            if (_goalRing != null) Ring(_goalRing, Destination, ArriveRadius);
        }

        private void Succeed()
        {
            State = LastOutcome = Outcome.Succeeded;
            LastFailReason = null;
            _ship.Depart();
            if (_player != null && _player.IsAlive) _player.RepairHull(_player.HullMaxHp);
            Game.Refit.RefitDraft.GrantReroll();
            PlayerProfile.Unlock("supply");
            UiSound.Play(UiSound.Cue.Victory);
            GameEvents.RaiseBanner("보급선 도착 — 선체 완전 수리 · 다시 뽑기 +1 · 정비 1회");
            // 정비 1회: 다음 레벨까지 남은 경험치를 채워 레벨업 정비를 연다
            if (_xp != null) _xp.AddXp(Mathf.Max(1, _xp.XpToNext - _xp.Xp));
            Close();
        }

        /// <summary>reason: "격침" · "이탈" · null(판이 끝남 — 알리지 않음).</summary>
        private void Fail(string reason)
        {
            State = LastOutcome = Outcome.Failed;
            LastFailReason = reason;
            if (reason != null)
            {
                UiSound.Play(UiSound.Cue.Fail);
                GameEvents.RaiseBanner(reason == "이탈"
                    ? $"보급선 호위 이탈 — {EscortRange:0}m 넘게 {LeashTime:0}초 떨어져 임무 실패(보상 없음)"
                    : "보급선 격침 — 임무 실패(보상 없음)");
            }
            if (reason == "이탈" && _ship != null) _ship.Depart();   // 혼자 남은 보급선은 물러난다
            Close();
        }

        private void Close()
        {
            if (_panel != null) Destroy(_panel.gameObject);
            _panel = null;
            foreach (var line in new[] { _routeLine, _joinLine, _rangeRing, _joinRing, _goalRing })
                if (line != null) Destroy(line.gameObject);
            if (_beacon != null) Destroy(_beacon.gameObject);
            Destroy(gameObject, 0.1f);
        }

        // ------------------------------------------------------------ 수면 표시(항로 · 도착지 · 합류 · 호위 범위)

        private static Material s_line, s_beacon;

        private static Material LineMaterial()
        {
            if (s_line != null) return s_line;
            var shader = Shader.Find("Sprites/Default");
            s_line = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "Supply route (Runtime)" };
            return s_line;
        }

        private LineRenderer Line(string name, Color color, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = LineMaterial();
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.loop = loop;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 수면에 눕힌다(위에서 보는 굵기 그대로)
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void Ring(LineRenderer line, Vector3 center, float radius)
        {
            const int n = 48;
            line.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                line.SetPosition(i, new Vector3(center.x + Mathf.Sin(a) * radius, WaterLine, center.z + Mathf.Cos(a) * radius));
            }
        }

        private void BuildMarks()
        {
            _routeLine = Line("Supply route", new Color(1f, 0.82f, 0.4f, 0.75f), 1.1f, false);
            _joinLine = Line("Supply join line", new Color(0.35f, 0.95f, 0.55f, 0.8f), 0.8f, false);
            _joinRing = Line("Supply join ring", new Color(0.35f, 0.95f, 0.55f, 0.55f), 0.7f, true);
            _rangeRing = Line("Supply escort range", new Color(1f, 0.35f, 0.25f, 0.7f), 1.2f, true);
            _goalRing = Line("Supply destination ring", new Color(1f, 0.82f, 0.4f, 0.9f), 1.3f, true);
            Ring(_goalRing, Destination, ArriveRadius);
            Ring(_rangeRing, Vector3.zero, EscortRange);

            // 도착지 등대: 멀리서도 보이는 금색 기둥
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Supply destination beacon";
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(transform, true);
            beacon.transform.position = new Vector3(Destination.x, 0f, Destination.z);
            beacon.transform.localScale = new Vector3(1.6f, 9f, 1.6f);
            if (s_beacon == null) s_beacon = new Material(LineMaterial()) { name = "Supply beacon (Runtime)", color = new Color(1f, 0.82f, 0.4f, 0.55f) };
            var r = beacon.GetComponent<MeshRenderer>();
            r.sharedMaterial = s_beacon;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beacon = beacon.transform;
        }

        private void RefreshMarks(float dist)
        {
            if (_routeLine == null || _ship == null) return;
            // 항로: 보급선 자리 → 남은 꺾은 점들 → 도착지
            var pts = new List<Vector3> { _ship.transform.position };
            for (int i = _ship.RouteIndex; i < _route.Count; i++) pts.Add(_route[i]);
            _routeLine.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) _routeLine.SetPosition(i, new Vector3(pts[i].x, WaterLine, pts[i].z));

            bool waiting = State == Outcome.Waiting;
            _joinLine.enabled = _joinRing.enabled = waiting;
            if (waiting && _player != null)
            {
                _joinLine.positionCount = 2;
                _joinLine.SetPosition(0, new Vector3(_player.transform.position.x, WaterLine, _player.transform.position.z));
                _joinLine.SetPosition(1, new Vector3(_ship.transform.position.x, WaterLine, _ship.transform.position.z));
                Ring(_joinRing, _ship.transform.position, JoinRadius);
            }
            bool warn = !waiting && dist > WarnRange;
            _rangeRing.enabled = warn;
            if (warn) Ring(_rangeRing, _ship.transform.position, EscortRange);
        }

        // ------------------------------------------------------------ 화면 위 막대

        private void BuildPanel()
        {
            var hud = FindFirstObjectByType<Game.UI.HUDView>();
            if (hud == null) return;
            var go = new GameObject("Supply escort panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            _panel = (RectTransform)go.transform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 1f);
            _panel.anchoredPosition = new Vector2(0f, -212f);
            _panel.sizeDelta = new Vector2(560f, 52f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.07f, 0.09f, 0.82f);
            bg.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(_panel, false);
            _label = label.GetComponent<TextMeshProUGUI>();
            _label.font = hud.Font;
            _label.fontSize = 18;
            _label.color = new Color(0.66f, 1f, 0.75f);
            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;
            _label.richText = true;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            var lr = _label.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = new Vector2(0f, -4f);
            lr.sizeDelta = new Vector2(540f, 28f);

            var back = new GameObject("Hull back", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(_panel, false);
            var br = (RectTransform)back.transform;
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 0f);
            br.anchoredPosition = new Vector2(14f, 9f);
            br.sizeDelta = new Vector2(532f, 9f);
            back.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            back.GetComponent<Image>().raycastTarget = false;
            var fill = new GameObject("Hull fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(br, false);
            _fill = (RectTransform)fill.transform;
            _fill.anchorMin = _fill.anchorMax = _fill.pivot = Vector2.zero;
            _fill.sizeDelta = new Vector2(532f, 9f);
            fill.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>검증용: 화면 위 막대 글.</summary>
        public string PanelText => _label != null ? _label.text : "";

        private void RefreshPanel()
        {
            if (_panel == null || _ship == null) return;
            float hp = _ship.Hull01;
            float dist = DistanceToFlagship;
            string head = "<color=#FFD166>임무</color>  보급선 호위";
            if (State == Outcome.Waiting)
                _label.text = $"{head}   ·   <b>합류하러 가기</b> {dist:0}m   ·   {Mathf.CeilToInt(JoinTimeout - _waited)}초 뒤 혼자 출발";
            else
            {
                string range = dist > WarnRange
                    ? $"<color=#FF7A6A>거리 {dist:0}m · 이탈 {LeashLeft:0.0}초</color>"
                    : $"거리 {dist:0}m";
                _label.text = $"{head}   도착까지 <b>{RemainingDistance:0}m</b>   ·   선체 {hp * 100f:0}%   ·   {range}";
            }
            _fill.sizeDelta = new Vector2(532f * hp, 9f);
            _fill.GetComponent<Image>().color = hp > 0.5f ? new Color(0.35f, 0.9f, 0.55f) : hp > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.4f, 0.3f);
        }

        // ------------------------------------------------------------ 보급선

        /// <summary>
        /// 보급선 한 척: 아군 수상 표적(ITargetable)이자 피해 대상(IDamageable). 콜라이더는 PlayerShip 레이어라 적 탄에만 맞는다.
        /// 출발 전에는 제자리, 출발하면 항로 꺾은 점을 차례로 배처럼(뱃머리 방향으로만, 선회율 한계) 따라간다.
        /// 앞 두 구간에 새 섬이 생기면 남은 항로를 다시 짠다. 격침되면 기울며 가라앉는다.
        /// </summary>
        public sealed class SupplyShip : MonoBehaviour, ITargetable, IDamageable, ITargetPriority
        {
            public const float MaxSpeed = 12.5f;
            private const float TurnRate = 30f, Accel = 2.5f, WaypointRadius = 12f;
            private const float VisualScale = 0.75f;

            private SupplyEscortMission _mission;
            private List<Vector3> _route = new();
            private int _index = 1;
            private float _hp, _heading, _speed, _sinkAge, _nextCheck, _nextReplan;
            private bool _going, _departing, _sinking;
            private BoxCollider _hull;
            private Transform _visual;

            public Transform Transform => transform;
            public TargetKind Kind => TargetKind.Surface;
            public CombatFaction Faction => CombatFaction.Player;
            public bool IsAlive => this != null && isActiveAndEnabled && !_sinking && !_departing && _hp > 0f;
            public bool IsRevealed => true;
            /// <summary>호위함(0.4)보다 먼저 노린다. 기함은 1.</summary>
            public float TargetPriority => 0.8f;
            public int MaxAttackers => 4;
            public float Hull01 => Mathf.Clamp01(_hp / SupplyHullHp);
            public int RouteIndex => _index;
            public bool Going => _going;
            public bool Arrived => _route.Count > 0 && _index >= _route.Count;
            public float Speed => _speed;

            public float RemainingDistance
            {
                get
                {
                    if (Arrived || _route.Count == 0) return 0f;
                    float d = Flat(_route[_index] - transform.position).magnitude;
                    for (int i = _index; i < _route.Count - 1; i++) d += Flat(_route[i + 1] - _route[i]).magnitude;
                    return d;
                }
            }

            public static SupplyShip Create(Vector3 position, float heading, SupplyEscortMission mission)
            {
                var root = new GameObject("Supply ship");
                root.layer = Factions.PlayerShipLayer;
                root.SetActive(false);
                root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
                var ship = root.AddComponent<SupplyShip>();
                ship._mission = mission;
                ship._heading = heading;
                ship._hp = SupplyHullHp;
                ship._hull = root.AddComponent<BoxCollider>();
                // 기함(약 3×10m)보다 조금 큰 화물선: 모델 15.5m를 0.75배
                ship._hull.size = new Vector3(4.4f, 3f, 16f) * VisualScale;
                ship._hull.center = new Vector3(0f, 1f, 0f) * VisualScale;
                ship._visual = BuildModel(root.transform);
                ship._visual.localScale = Vector3.one * VisualScale;
                Game.View.ShipWake.Ensure(root, () => GameManager.Instance != null && GameManager.Instance.State == GameState.Playing ? 1f : 0f);
                root.SetActive(true);
                Waterline.Settle(root.transform, ship._visual);   // 수면선(점검 G06)
                return ship;
            }

            public void SetRoute(List<Vector3> route) { _route = route; _index = Mathf.Min(1, route.Count); }
            public void Go() => _going = true;

            private void OnEnable() { if (IsAlive) TargetRegistry.Register(this); }
            private void OnDisable() => Release();

            private void Release()
            {
                TargetRegistry.Unregister(this);
                TargetAllocator.ReleaseTarget(transform);
            }

            public void TakeDamage(in DamageInfo info)
            {
                if (!IsAlive) return;
                _hp -= info.Amount;
                if (_hp > 0f) return;
                _hp = 0f;
                _sinking = true;
                if (_hull != null) _hull.enabled = false;
                Release();
                Explosions.Spawn(transform.position + Vector3.up * 1.5f, 1.6f, Explosions.Kind.Impact);
                AudioManager.Play(Game.Data.SfxId.Explosion, transform.position);
            }

            /// <summary>임무가 끝남: 표적에서 빠지고 지금 방향으로 계속 가서 사라진다.</summary>
            public void Depart()
            {
                _departing = true;
                if (_hull != null) _hull.enabled = false;
                Release();
                Destroy(gameObject, 12f);
            }

            /// <summary>앞 두 구간이 새 섬에 막혔으면 남은 항로를 지금 자리에서 다시 짠다(남은 거리 그대로).</summary>
            private void CheckAhead()
            {
                if (Arrived) return;
                Vector3 from = transform.position;
                bool blocked = false;
                for (int i = _index; i < _route.Count && i < _index + 2; i++)
                {
                    if (!SegmentClear(from, _route[i])) { blocked = true; break; }
                    from = _route[i];
                }
                if (!blocked || Time.time < _nextReplan) return;
                _nextReplan = Time.time + 3f;   // 섬이 빽빽한 곳에서 매번 다시 짜지 않게(그 사이는 바로 앞 섬 비켜 돌기)
                var goal = _route[_route.Count - 1];
                float remaining = RemainingDistance;
                var dir = Flat(goal - transform.position);
                var fresh = PlanRoute(transform.position, dir.sqrMagnitude > 1f ? dir : transform.forward, Mathf.Max(Step, remaining),
                                      Mathf.RoundToInt(transform.position.x * 3f + transform.position.z * 5f));
                _route = fresh;
                _index = Mathf.Min(1, fresh.Count);
                if (_mission != null) _mission.OnRouteChanged(fresh);
            }

            private void Update()
            {
                if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing && !_sinking) return;
                float dt = Time.deltaTime;
                if (_sinking)
                {
                    _sinkAge += dt;
                    transform.position += Vector3.down * (0.7f * dt);
                    if (_visual != null) _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Min(24f, _sinkAge * 6f));
                    if (_sinkAge > 6f) Destroy(gameObject);
                    return;
                }

                float desired = _heading, wantSpeed = 0f;
                if (_departing)
                {
                    wantSpeed = MaxSpeed;
                }
                else if (_going && !Arrived)
                {
                    if (Time.time >= _nextCheck) { _nextCheck = Time.time + 0.5f; CheckAhead(); }
                    var to = Flat(_route[_index] - transform.position);
                    if (to.magnitude <= WaypointRadius && _index < _route.Count) { _index++; if (Arrived) return; to = Flat(_route[_index] - transform.position); }
                    desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    // 바로 앞의 섬은 비켜 돈다(항로를 다시 짜기 전 틈)
                    var fwd = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
                    if (Islands.ProbeAhead(transform.position, fwd, 4f, 22f, out var hit))
                    {
                        var away = Flat(hit.normal);
                        if (away.sqrMagnitude > 1e-4f) desired = Mathf.Atan2(away.x + fwd.x, away.z + fwd.z) * Mathf.Rad2Deg;
                    }
                    // 크게 꺾는 동안은 느리게
                    float turn = Mathf.Abs(Mathf.DeltaAngle(_heading, desired));
                    wantSpeed = Cruise * Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((turn - 15f) / 60f));
                }
                _heading = Mathf.MoveTowardsAngle(_heading, desired, TurnRate * dt * Mathf.Lerp(0.4f, 1f, _speed / Cruise));
                _speed = Mathf.MoveTowards(_speed, wantSpeed, Accel * dt);
                var rot = Quaternion.Euler(0f, _heading, 0f);
                Vector3 p = transform.position + rot * Vector3.forward * (_speed * dt);
                p.y = 0f;
                if (_hull != null && Islands.ResolveOverlap(_hull, ref p, rot, out _)) _speed *= 0.6f;
                if (SeaRegion.ClampToChannel(ref p, -2f)) _speed *= 0.6f;
                transform.SetPositionAndRotation(p, rot);
            }

            /// <summary>회색박스 화물선: 붉은 선체, 흰 선교(뒤쪽), 갑판의 컨테이너 줄.</summary>
            private static Transform BuildModel(Transform root)
            {
                var visual = new GameObject("Supply visual").transform;
                visual.SetParent(root, false);
                Box(visual, "Hull", new Vector3(0f, 0.2f, 0f), new Vector3(4.2f, 2.2f, 15.5f), new Color(0.42f, 0.13f, 0.1f));
                Box(visual, "Deck", new Vector3(0f, 1.32f, 0f), new Vector3(4f, 0.08f, 15f), new Color(0.32f, 0.33f, 0.31f));
                Box(visual, "Bow", new Vector3(0f, 0.6f, 7.9f), new Vector3(2.4f, 1.6f, 1.2f), new Color(0.42f, 0.13f, 0.1f));
                Box(visual, "Bridge", new Vector3(0f, 2.6f, -5.6f), new Vector3(3.6f, 2.5f, 2.6f), new Color(0.88f, 0.88f, 0.84f));
                Box(visual, "Bridge windows", new Vector3(0f, 3.3f, -4.28f), new Vector3(3.2f, 0.45f, 0.06f), new Color(0.08f, 0.12f, 0.16f));
                Box(visual, "Funnel", new Vector3(0f, 4.3f, -6.4f), new Vector3(1f, 1.4f, 1f), new Color(0.15f, 0.15f, 0.16f));
                Color[] colors = { new(0.15f, 0.35f, 0.6f), new(0.75f, 0.45f, 0.12f), new(0.2f, 0.45f, 0.3f), new(0.6f, 0.18f, 0.16f) };
                int k = 0;
                for (int row = 0; row < 4; row++)
                    for (int col = -1; col <= 1; col += 2)
                        Box(visual, "Container", new Vector3(col * 0.95f, 1.95f, -2.6f + row * 2.6f), new Vector3(1.75f, 1.2f, 2.4f), colors[k++ % colors.Length]);
                Box(visual, "Container top", new Vector3(0f, 3.15f, 0f), new Vector3(1.75f, 1.2f, 2.4f), colors[1]);
                return visual;
            }

            private static Material s_lit;

            private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Color color)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Destroy(go.GetComponent<Collider>());
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                go.transform.localScale = size;
                var r = go.GetComponent<MeshRenderer>();
                if (s_lit == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    s_lit = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Supply ship" };
                    if (s_lit.HasProperty("_Smoothness")) s_lit.SetFloat("_Smoothness", 0.25f);
                }
                r.sharedMaterial = s_lit;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
