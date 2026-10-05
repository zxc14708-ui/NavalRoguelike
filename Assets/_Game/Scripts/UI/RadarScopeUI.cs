using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules.Runtime;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 왼쪽 아래 레이더 화면(PPI). 자함이 가운데, 화면 위가 북(카메라와 같은 방향)인 상대 운동 표시.
    ///
    /// 실제 항해 레이더처럼 동작한다.
    ///   - 스윕은 함교 레이더 안테나가 실제로 바라보는 방위를 따라 돈다(함교가 부서지면 전용 레이더, 둘 다 없으면 NO RADAR).
    ///   - 에코는 빔이 표적 방위를 지나가는 순간에만 그 자리에 찍히고, 형광면 잔광처럼 한 바퀴 동안 흐려진다.
    ///     표적이 움직여도 다음 스윕 전까지 에코는 옛 위치에 남는다.
    ///   - 빔 폭 때문에 먼 에코일수록 방위 방향으로 길게 번진다. 자함 주변에는 해면 클러터가 깔린다.
    ///   - 사격통제가 추적 중인 표적(TargetingSystem 목록)만 속도 벡터(ARPA)를 단다. 레이더 범위 안이라도 추적 수 밖이면 에코만.
    ///   - 잠수함은 수면에 드러났을 때만, 채프(기만체)는 넓고 흐린 에코로, 아군 미사일은 작은 에코로 보인다.
    ///   - 적 대함미사일이 찍히면 상태 줄에 VAMPIRE 경고.
    /// 거리 눈금은 현재 함선 탐지거리(함교 32, 전용 레이더 52)를 4등분한다.
    /// </summary>
    public class RadarScopeUI : MonoBehaviour
    {
        // HUD 기준 해상도(1920×1080) 픽셀
        private const float PanelWidth = 340f, PanelHeight = 372f;
        private const float CenterX = 170f, CenterY = 166f;
        private const float FaceRadius = 136f;      // 방위 눈금 바깥 테두리
        private const float DisplayRadius = 128f;   // 거리 눈금 끝
        private const float LabelRadius = 152f;
        private const int EchoCapacity = 200;

        [SerializeField] private float beamWidthDegrees = 2.5f;
        [SerializeField] private float clutterPerRevolution = 70f;
        [Tooltip("속도 벡터 길이 = 이 시간 동안 움직일 거리")]
        [SerializeField] private float vectorSeconds = 3f;

        private static readonly Color Phosphor = new(0.36f, 1f, 0.46f, 1f);
        private static readonly Color VampireColor = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color OfflineColor = new(1f, 0.62f, 0.25f, 1f);

        private enum EchoType { Clutter, Ship, Boss, Submarine, Suspected, Aircraft, Missile, FriendlyMissile, Chaff, Land }

        private sealed class Echo
        {
            public RectTransform Root;
            public RawImage Dot;
            public RawImage Vector;
            public Vector2 OffsetWorld;    // 찍힌 순간 자함 기준 위치(x, z)
            public Vector2 VelocityWorld;
            public EchoType Type;
            public float Born, Life, Peak;
            public bool HasVector;
            public bool Active;
            public bool Elite;   // 엘리트 적: 황금색으로 더 크게
        }

        private readonly List<Echo> _echoes = new(EchoCapacity);
        private int _nextEcho;
        private readonly Dictionary<Transform, (Vector3 pos, float time)> _lastPaint = new();
        private readonly Dictionary<Transform, float> _missilePaint = new();
        private readonly List<Transform> _scratch = new();

        private RectTransform _contacts;
        private RadarSweepGraphic _sweep;
        private RectTransform _heading;
        private TMP_Text _status, _rings;

        private ShipController _player;
        private TargetingSystem _targeting;
        private IRadarSource _source;
        private float _sourceTimer;
        private bool _hasPrev;
        private float _prevBearing;
        private float _range = 32f, _layoutRange = -1f;
        private float _clutterAccumulator;
        private float _sonarPaintTimer;

        private static Texture2D s_face, s_dot;

        // --- 개발 검증용 조회
        public static RadarScopeUI Instance { get; private set; }
        public bool Online => _player != null && _source != null && !(_source is Object o && o == null) && _source.RadarOperational;
        public IRadarSource Source => _source;
        public float DisplayRange => _range;
        /// <summary>화면에 그려진 스윕 방위(도).</summary>
        public float SweepBearing => Mathf.Repeat(-_sweep.rectTransform.localEulerAngles.z, 360f);
        public int CountEchoes(bool includeClutter)
        {
            int n = 0;
            foreach (var e in _echoes) if (e.Active && (includeClutter || (e.Type != EchoType.Clutter && e.Type != EchoType.Land))) n++;   // 육지(섬) 반사는 표적이 아니다
            return n;
        }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>HUD 아래에 레이더 화면을 만든다. position은 부모 왼쪽 아래 기준.</summary>
        /// <param name="width">표시 폭(HUD 픽셀). 기본 설계 폭 340을 기준으로 전체를 비례 축소한다.</param>
        public static RadarScopeUI Create(Transform parent, TMP_FontAsset font, Color panelColor, Color headerColor, Vector2 position,
                                          float width = PanelWidth)
        {
            var panel = NewRect("Radar scope", parent);
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = position;
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.localScale = Vector3.one * (width / PanelWidth);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = panelColor;
            bg.raycastTarget = false;
            // 에코 알파가 매 프레임 바뀌므로 HUD 전체가 다시 그려지지 않게 캔버스를 나눈다
            panel.gameObject.AddComponent<Canvas>();

            var ui = panel.gameObject.AddComponent<RadarScopeUI>();
            ui.Build(panel, font, headerColor);
            return ui;
        }

        private void Build(RectTransform panel, TMP_FontAsset font, Color headerColor)
        {
            if (s_face == null) s_face = BuildFaceTexture();
            if (s_dot == null) s_dot = BuildDotTexture();

            Label(panel, font, "Title", "RADAR  /  레이더", new Vector2(18f, 340f), new Vector2(170f, 24f), 18f, headerColor, TextAlignmentOptions.Left);
            _status = Label(panel, font, "Status", "", new Vector2(150f, 340f), new Vector2(174f, 24f), 15f, Phosphor, TextAlignmentOptions.Right);

            var center = new Vector2(CenterX, CenterY);

            var face = NewRect("Face", panel);
            Place(face, center, new Vector2(FaceRadius * 2f, FaceRadius * 2f));
            var faceImage = face.gameObject.AddComponent<RawImage>();
            faceImage.texture = s_face;
            faceImage.raycastTarget = false;

            _contacts = NewRect("Echoes", panel);
            Place(_contacts, center, Vector2.zero);

            _heading = NewRect("Heading marker", panel);
            Place(_heading, center, new Vector2(1.3f, DisplayRadius));
            _heading.pivot = new Vector2(0.5f, 0f);
            _heading.anchoredPosition = center;
            var headingImage = _heading.gameObject.AddComponent<RawImage>();
            headingImage.texture = Texture2D.whiteTexture;
            headingImage.color = new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.5f);
            headingImage.raycastTarget = false;

            var sweepRect = NewRect("Sweep", panel);
            Place(sweepRect, center, new Vector2(DisplayRadius * 2f, DisplayRadius * 2f));
            _sweep = sweepRect.gameObject.AddComponent<RadarSweepGraphic>();
            _sweep.color = Phosphor;
            _sweep.raycastTarget = false;

            var own = NewRect("Own ship", panel);
            Place(own, center, new Vector2(8f, 8f));
            var ownImage = own.gameObject.AddComponent<RawImage>();
            ownImage.texture = s_dot;
            ownImage.color = Phosphor;
            ownImage.raycastTarget = false;

            for (int b = 0; b < 360; b += 30)
            {
                float rad = b * Mathf.Deg2Rad;
                var pos = center + new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * LabelRadius;
                var t = Label(panel, font, $"Bearing {b}", b.ToString(), pos, new Vector2(40f, 18f), 13f,
                              new Color(Phosphor.r, Phosphor.g, Phosphor.b, b == 0 ? 1f : 0.75f), TextAlignmentOptions.Center);
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.rectTransform.anchoredPosition = pos;
            }

            Label(panel, font, "Mode", "N-UP  RM", new Vector2(8f, 6f), new Vector2(80f, 16f), 12f,
                  new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.7f), TextAlignmentOptions.Left);
            _rings = Label(panel, font, "Rings", "", new Vector2(PanelWidth - 98f, 6f), new Vector2(90f, 16f), 12f,
                           new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.7f), TextAlignmentOptions.Right);

            for (int i = 0; i < EchoCapacity; i++) _echoes.Add(CreateEcho(i));
        }

        // ------------------------------------------------------------ 갱신

        private void LateUpdate()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player != _player)
            {
                _player = player;
                _targeting = player != null ? player.GetComponentInChildren<TargetingSystem>() : null;
                _source = null;
                _sourceTimer = 0f;
                _hasPrev = false;
                ClearEchoes();
            }

            _sourceTimer -= Time.unscaledDeltaTime;
            if (_sourceTimer <= 0f)
            {
                _sourceTimer = 0.5f;
                var found = FindSource(_player);
                if (found != _source) _hasPrev = false;
                _source = found;
            }

            // 철거된 레이더(파괴된 컴포넌트)를 붙잡고 있지 않게
            if (_source is Object removed && removed == null) { _source = null; _sourceTimer = 0f; }
            bool online = _player != null && _source != null && _source.RadarOperational;
            if (online)
            {
                float systemRange = _player.Systems != null ? _player.Systems.DetectionRange : 0f;
                float tacticalRange = _targeting != null ? _targeting.RadarRange : 0f;
                _range = Mathf.Max(1f, Mathf.Max(tacticalRange, Mathf.Max(systemRange, _source.RadarRange)));
                Sweep();
            }
            else _hasPrev = false;

            _sonarPaintTimer -= Time.deltaTime;
            if (_sonarPaintTimer <= 0f && _player != null && _targeting != null)
            {
                _sonarPaintTimer = 0.6f;
                PaintSonarContacts();
            }

            if (_sweep.enabled != online) _sweep.enabled = online;
            if (_player != null) _heading.localRotation = Quaternion.Euler(0f, 0f, -_player.transform.eulerAngles.y);

            UpdateEchoes();
            UpdateStatus(online);
        }

        /// <summary>함교 레이더 우선. 함교가 부서졌으면 살아 있는 전용 레이더 중 탐지거리가 긴 것.</summary>
        private static IRadarSource FindSource(ShipController player)
        {
            if (player == null || player.Grid == null) return null;

            IRadarSource best = null;
            foreach (var m in player.Grid.Modules)
            {
                if (m?.Runtime is not IRadarSource radar || !radar.RadarOperational) continue;
                if (radar is BridgeModule) return radar;
                if (best == null || radar.RadarRange > best.RadarRange) best = radar;
            }
            return best;
        }

        private void Sweep()
        {
            float current = _source.AntennaBearing;
            _sweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -current);

            if (!_hasPrev)
            {
                _prevBearing = current;
                _hasPrev = true;
                return;
            }

            float from = _prevBearing;
            float delta = Mathf.DeltaAngle(from, current);
            _prevBearing = current;
            if (delta <= 0f || delta > 120f) return;   // 역회전·순간 이동(재배치, 프레임 급락)은 건너뛴다

            Vector3 self = _player.transform.position;
            float period = 60f / Mathf.Max(1f, _source.AntennaRpm);

            PaintKind(TargetKind.Surface, self, from, delta, period);
            PaintKind(TargetKind.Aircraft, self, from, delta, period);
            PaintKind(TargetKind.Missile, self, from, delta, period);
            PaintKind(TargetKind.Decoy, self, from, delta, period);
            PaintLand(self, from, delta, period);

            var friendly = Missile.ActiveFriendly;
            for (int i = 0; i < friendly.Count; i++)
            {
                var m = friendly[i];
                if (m == null || !m.isActiveAndEnabled) continue;
                TryPaint(m.transform, EchoType.FriendlyMissile, false, m, self, from, delta, period);
            }

            // 해면 클러터: 자함 가까이, 스윕이 지나간 방위에만
            _clutterAccumulator += delta / 360f * clutterPerRevolution;
            while (_clutterAccumulator >= 1f)
            {
                _clutterAccumulator -= 1f;
                float bearing = (from + Random.value * delta) * Mathf.Deg2Rad;
                float radiusPx = 5f + DisplayRadius * 0.2f * Mathf.Pow(Random.value, 1.8f);
                float worldRadius = radiusPx / DisplayRadius * _range;
                var offset = new Vector2(Mathf.Sin(bearing), Mathf.Cos(bearing)) * worldRadius;
                Spawn(offset, Vector2.zero, false, EchoType.Clutter, period * 0.5f, Random.Range(0.1f, 0.32f));
            }
        }

        /// <summary>섬: 해안선 표본점마다 넓고 흐린 육지 반사. 레이더 화면에서 섬 모양이 드러난다.</summary>
        private void PaintLand(Vector3 self, float from, float delta, float period)
        {
            var islands = Game.World.Islands.All;
            for (int i = 0; i < islands.Count; i++)
            {
                var isl = islands[i];
                var c = new Vector2(isl.Center.x - self.x, isl.Center.z - self.z);
                if (c.magnitude - isl.Radius > _range) continue;
                PaintLandPoint(c, from, delta, period);
                foreach (var r in isl.Rim) PaintLandPoint(new Vector2(r.x - self.x, r.y - self.z), from, delta, period);
            }
        }

        private void PaintLandPoint(Vector2 offset, float from, float delta, float period)
        {
            float dist = offset.magnitude;
            if (dist > _range || dist < 0.5f) return;
            float bearing = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
            if (Mathf.Repeat(bearing - from, 360f) >= delta) return;
            Spawn(offset, Vector2.zero, false, EchoType.Land, period * 0.98f, 0.55f);
        }

        private void PaintKind(TargetKind kind, Vector3 self, float from, float delta, float period)
        {
            // 적 반사만(자함·아군 호위함은 그리지 않는다). 기만체는 내가 뿌린 채프라 아군 진영 것을 그린다.
            var list = kind == TargetKind.Decoy ? TargetRegistry.Of(CombatFaction.Player, kind) : TargetRegistry.HostileTo(CombatFaction.Player, kind);
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.IsAlive || t.Transform == null) continue;

                EchoType type;
                switch (kind)
                {
                    case TargetKind.Submarine:
                        if (!t.IsRevealed) continue;   // 잠항 중인 잠수함은 레이더에 반사되지 않는다
                        type = EchoType.Submarine;
                        break;
                    case TargetKind.Aircraft: type = EchoType.Aircraft; break;
                    case TargetKind.Missile: type = EchoType.Missile; break;
                    case TargetKind.Decoy: type = EchoType.Chaff; break;
                    default: type = t is BossShip or HybridBattleshipBoss ? EchoType.Boss : EchoType.Ship; break;
                }

                bool tracked = IsTracked(t, kind);
                if (TryPaint(t.Transform, type, tracked, t, self, from, delta, period) && kind == TargetKind.Missile)
                    _missilePaint[t.Transform] = Time.time;
            }
        }

        private void PaintSonarContacts()
        {
            Vector3 self = _player.transform.position;
            foreach (var suspected in _targeting.SuspectedSonar)
            {
                Vector3 bearing = suspected - self;
                if (bearing.sqrMagnitude <= _range * _range)
                    Spawn(new Vector2(bearing.x, bearing.z), Vector2.zero, false, EchoType.Suspected, 0.9f, 0.55f);
            }
            foreach (var t in _targeting.DetectedSubmarines)
            {
                if (t == null || !t.IsAlive || t.Transform == null) continue;
                Vector3 reported = t is SubmarineBase sub && !sub.IsRevealed
                    ? sub.LastKnownPosition : t.Transform.position;
                Vector3 delta = reported - self;
                if (delta.sqrMagnitude > _range * _range) continue;
                Spawn(new Vector2(delta.x, delta.z), Vector2.zero, false, EchoType.Submarine, 0.9f, 0.95f, IsElite(t));
            }
            if (_targeting.TryGetLaunchCue(self, _range, out Vector3 cue))
            {
                Vector3 delta = cue - self;
                Spawn(new Vector2(delta.x, delta.z), Vector2.zero, false, EchoType.Suspected, 1.2f, 0.65f);
            }
        }

        private bool IsTracked(ITargetable t, TargetKind kind)
        {
            if (_targeting == null) return false;
            IReadOnlyList<ITargetable> list = kind switch
            {
                TargetKind.Surface => _targeting.DetectedSurface,
                TargetKind.Submarine => _targeting.DetectedSubmarines,
                TargetKind.Aircraft => _targeting.DetectedAircraft,
                TargetKind.Missile => _targeting.IncomingMissiles,
                _ => null,
            };
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], t)) return true;
            return false;
        }

        /// <summary>빔이 이번 프레임에 표적 방위를 지났고 탐지거리 안이면 에코를 찍는다.</summary>
        private bool TryPaint(Transform target, EchoType type, bool tracked, object source,
                              Vector3 self, float from, float delta, float period)
        {
            Vector3 pos = target.position;
            var offset = new Vector2(pos.x - self.x, pos.z - self.z);
            float dist = offset.magnitude;
            if (dist > _range || dist < 0.5f) return false;

            float bearing = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
            if (Mathf.Repeat(bearing - from, 360f) >= delta) return false;

            // 레이더 그림자: 안테나(약 6m)에서 섬에 가린 수상함·저공 표적은 찍히지 않는다
            if (type is EchoType.Ship or EchoType.Boss or EchoType.Submarine or EchoType.Aircraft &&
                Game.World.Islands.BlocksLine(self + Vector3.up * 6f, pos + Vector3.up * 1f))
                return false;

            // 속도: 사격통제 데이터가 있으면 그것을, 없으면 직전 스윕과의 위치 차이로(ARPA)
            Vector2 velocity = Vector2.zero;
            bool hasVelocity = false;
            if (tracked)
            {
                if (source is IHasVelocity v)
                {
                    velocity = new Vector2(v.Velocity.x, v.Velocity.z);
                    hasVelocity = true;
                }
                else if (_lastPaint.TryGetValue(target, out var last))
                {
                    float dt = Time.time - last.time;
                    if (dt > period * 0.5f && dt < period * 2.5f)
                    {
                        velocity = new Vector2(pos.x - last.pos.x, pos.z - last.pos.z) / dt;
                        hasVelocity = velocity.sqrMagnitude < 60f * 60f;   // 풀에서 재사용된 개체의 순간 이동은 버린다
                    }
                }
            }
            _lastPaint[target] = (pos, Time.time);

            float peak = type switch
            {
                EchoType.Chaff => 0.45f,
                EchoType.FriendlyMissile => 0.6f,
                _ => 1f,
            };
            Spawn(offset, velocity, hasVelocity && velocity.sqrMagnitude > 0.64f, type, period * 0.95f, peak, IsElite(source));
            return true;
        }

        /// <summary>엘리트 등급 적인가(레이더에서 황금색으로 구별).</summary>
        private static bool IsElite(object source)
            => source is EnemyController { Definition: { } def } && def.Rank == Game.Data.EnemyRank.Elite;

        private void Spawn(Vector2 offsetWorld, Vector2 velocityWorld, bool hasVector, EchoType type, float life, float peak, bool elite = false)
        {
            var e = _echoes[_nextEcho];
            _nextEcho = (_nextEcho + 1) % _echoes.Count;

            e.OffsetWorld = offsetWorld;
            e.VelocityWorld = velocityWorld;
            e.HasVector = hasVector;
            e.Type = type;
            e.Elite = elite;
            e.Born = Time.time;
            e.Life = Mathf.Max(0.1f, life);
            e.Peak = peak;
            e.Active = true;
            e.Dot.enabled = true;
            e.Vector.enabled = hasVector;
            Layout(e);
        }

        /// <summary>에코 위치·크기. 빔 폭 때문에 먼 에코는 방위 방향으로 번진다.</summary>
        private void Layout(Echo e)
        {
            float scale = DisplayRadius / _range;
            Vector2 px = e.OffsetWorld * scale;
            float radiusPx = px.magnitude;
            e.Root.anchoredPosition = px;

            (float radial, float tangential) = e.Type switch
            {
                EchoType.Boss => (9f, 12f),
                EchoType.Ship => (5.5f, 6.5f),
                EchoType.Submarine => (4.5f, 5f),
                EchoType.Suspected => (11f, 12f),
                EchoType.Aircraft => (4f, 4.2f),
                EchoType.Missile => (3.6f, 3.8f),
                EchoType.FriendlyMissile => (3f, 3f),
                EchoType.Chaff => (10f, 11f),
                EchoType.Land => (8f, 10f),
                _ => (2.6f, 3f),
            };
            if (e.Elite) { radial *= 1.45f; tangential *= 1.45f; }
            tangential += radiusPx * beamWidthDegrees * Mathf.Deg2Rad;

            var dot = e.Dot.rectTransform;
            dot.sizeDelta = new Vector2(tangential, radial) * 2f;   // 부드러운 점 텍스처라 보이는 크기보다 크게
            float angle = radiusPx > 0.01f ? Mathf.Atan2(px.y, px.x) * Mathf.Rad2Deg - 90f : 0f;
            dot.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (e.HasVector)
            {
                float length = Mathf.Min(e.VelocityWorld.magnitude * vectorSeconds * scale, 48f);
                var vec = e.Vector.rectTransform;
                vec.sizeDelta = new Vector2(1.1f, length);
                vec.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(e.VelocityWorld.y, e.VelocityWorld.x) * Mathf.Rad2Deg - 90f);
            }
        }

        private void UpdateEchoes()
        {
            bool relayout = !Mathf.Approximately(_layoutRange, _range);
            _layoutRange = _range;
            float now = Time.time;

            for (int i = 0; i < _echoes.Count; i++)
            {
                var e = _echoes[i];
                if (!e.Active) continue;

                float t = (now - e.Born) / e.Life;
                if (t >= 1f || t < 0f)
                {
                    e.Active = false;
                    e.Dot.enabled = false;
                    e.Vector.enabled = false;
                    continue;
                }
                if (relayout) Layout(e);

                // 형광면 잔광: 찍힌 직후 밝고 다음 스윕 직전에 거의 사라진다
                float a = e.Peak * Mathf.Pow(1f - t, 1.25f);
                e.Dot.color = e.Elite ? new Color(Game.View.EliteMarker.Gold.r, Game.View.EliteMarker.Gold.g, Game.View.EliteMarker.Gold.b, a)
                    : e.Type is EchoType.Submarine or EchoType.Suspected
                    ? new Color(0.35f, 0.87f, 1f, a) : new Color(Phosphor.r, Phosphor.g, Phosphor.b, a);
                if (e.HasVector) e.Vector.color = new Color(Phosphor.r, Phosphor.g, Phosphor.b, a * 0.55f);
            }
        }

        private void UpdateStatus(bool online)
        {
            if (!online)
            {
                _status.text = "NO RADAR";
                _status.color = OfflineColor;
                _rings.text = "";
                return;
            }

            float period = 60f / Mathf.Max(1f, _source.AntennaRpm);
            int vampires = 0;
            _scratch.Clear();
            foreach (var kv in _missilePaint)
            {
                bool alive = kv.Key != null && kv.Key.gameObject.activeInHierarchy;
                if (alive && Time.time - kv.Value <= period * 1.05f) vampires++;
                else if (!alive || Time.time - kv.Value > period * 3f) _scratch.Add(kv.Key);
            }
            foreach (var k in _scratch) _missilePaint.Remove(k);

            if (vampires > 0)
            {
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.42f;
                _status.text = $"VAMPIRE ×{vampires}";
                _status.color = blink ? VampireColor : new Color(VampireColor.r, VampireColor.g, VampireColor.b, 0.45f);
            }
            else
            {
                _status.text = $"RNG {_range:0}  ·  {_source.AntennaRpm:0} RPM";
                _status.color = Phosphor;
            }
            _rings.text = $"RINGS {_range / 4f:0.#}";

            // 오래된 추적 기록 정리(한 바퀴에 한 번 정도면 충분)
            if (Time.frameCount % 120 == 0)
            {
                _scratch.Clear();
                foreach (var kv in _lastPaint)
                    if (kv.Key == null || Time.time - kv.Value.time > period * 3f) _scratch.Add(kv.Key);
                foreach (var k in _scratch) _lastPaint.Remove(k);
            }
        }

        private void ClearEchoes()
        {
            foreach (var e in _echoes)
            {
                e.Active = false;
                e.Dot.enabled = false;
                e.Vector.enabled = false;
            }
            _lastPaint.Clear();
            _missilePaint.Clear();
        }

        // ------------------------------------------------------------ 만들기

        private Echo CreateEcho(int index)
        {
            var root = NewRect($"Echo {index}", _contacts);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;

            var dotRect = NewRect("Dot", root);
            dotRect.anchorMin = dotRect.anchorMax = dotRect.pivot = new Vector2(0.5f, 0.5f);
            var dot = dotRect.gameObject.AddComponent<RawImage>();
            dot.texture = s_dot;
            dot.raycastTarget = false;
            dot.enabled = false;

            var vecRect = NewRect("Vector", root);
            vecRect.anchorMin = vecRect.anchorMax = new Vector2(0.5f, 0.5f);
            vecRect.pivot = new Vector2(0.5f, 0f);
            var vec = vecRect.gameObject.AddComponent<RawImage>();
            vec.texture = Texture2D.whiteTexture;
            vec.raycastTarget = false;
            vec.enabled = false;

            return new Echo { Root = root, Dot = dot, Vector = vec };
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string value, Vector2 position,
                                      Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        /// <summary>
        /// 형광면: 어두운 녹색 원판, 거리 링 4개(사이 보조 링), 30°마다 방위선, 테두리 방위 눈금(5°·10°·30°).
        /// 한 번만 만들어 모든 레이더 화면이 공유한다.
        /// </summary>
        private static Texture2D BuildFaceTexture()
        {
            const int N = 512;
            float half = N * 0.5f;
            float texPerUi = half / FaceRadius;
            float rDisplay = DisplayRadius * texPerUi;
            float rFace = half - 1.5f;
            var fill = new Color(0.012f, 0.075f, 0.035f, 0.94f);

            static float Line(float distance, float width) => Mathf.Clamp01(width * 0.5f + 0.5f - Mathf.Abs(distance));

            var pixels = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > rFace + 2f) { pixels[y * N + x] = new Color32(0, 0, 0, 0); continue; }

                    float inside = Mathf.Clamp01(rFace - d + 0.5f);
                    float line = 0f;

                    for (int k = 1; k <= 4; k++)
                    {
                        line = Mathf.Max(line, Line(d - rDisplay * k / 4f, (k == 4 ? 1.5f : 1f) * texPerUi) * (k == 4 ? 0.7f : 0.36f));
                        line = Mathf.Max(line, Line(d - rDisplay * (k - 0.5f) / 4f, 0.8f * texPerUi) * 0.1f);
                    }
                    line = Mathf.Max(line, Line(d - (rFace - 1f), 1.4f * texPerUi) * 0.75f);

                    float bearing = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);
                    if (d < rDisplay)
                    {
                        float nearest = Mathf.Round(bearing / 30f) * 30f;
                        float perp = d * Mathf.Abs(Mathf.Sin((bearing - nearest) * Mathf.Deg2Rad));
                        line = Mathf.Max(line, Line(perp, 0.9f * texPerUi) * 0.13f);
                    }
                    else
                    {
                        float nearest = Mathf.Round(bearing / 5f) * 5f;
                        int tick = ((int)nearest % 360 + 360) % 360;
                        bool major = tick % 30 == 0, mid = tick % 10 == 0;
                        float band = rFace - rDisplay;
                        float length = major ? band : mid ? band * 0.6f : band * 0.35f;
                        if (d >= rFace - length)
                        {
                            float perp = d * Mathf.Abs(Mathf.Sin((bearing - nearest) * Mathf.Deg2Rad));
                            line = Mathf.Max(line, Line(perp, (major ? 1.6f : 1f) * texPerUi) * (major ? 0.9f : 0.55f));
                        }
                    }

                    float baseAlpha = fill.a * inside;
                    var c = Color.Lerp(fill, Phosphor, line);
                    c.a = line + baseAlpha * (1f - line);
                    pixels[y * N + x] = c;
                }
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                name = "RadarFace",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildDotTexture()
        {
            const int N = 32;
            var pixels = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    // 밝은 심과 부드러운 가장자리
                    float a = Mathf.Exp(-(u * u + v * v) / (2f * 0.42f * 0.42f));
                    a = Mathf.Clamp01((a - 0.03f) * 1.35f);
                    pixels[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                name = "RadarEcho",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
