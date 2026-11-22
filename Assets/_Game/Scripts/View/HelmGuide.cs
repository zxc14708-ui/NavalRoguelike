using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Game.Core;
using Game.Ship;

namespace Game.View
{
    /// <summary>
    /// 타륜 표시(2026-11-22 사용자: 처음 하는 사람도 지금 타각으로 배가 어디로 가는지 알게 — 시안 A 예상 항적 + B 선체 링을 합친 5가지).
    /// 설정 › 조함 › 타륜 표시(DisplaySettings.HelmGuide)로 고른다. 모두 수면에 눕혀 그리고, 색은 타각이 작으면 초록 · 중간 노랑 · 크면 주황.
    ///   1 깔끔한 HUD    : 배를 감싼 얇은 하늘색 타원 + 타각만큼 옆으로 옮겨 기운 화살표 + 1초마다 점이 찍힌 점선 항적(끝 화살촉).
    ///   2 항해 나침반   : 뱃머리 쪽 반원에만 5° 눈금(10°마다 숫자) — 돌린 각도까지 불이 들어옴 + 화살표 + 앞으로 흐르는 꺾쇠 항적.
    ///   3 빛나는 호     : 링 대신 0°(흰 점)에서 타각까지 채워지는 굵은 호 + 끝 점 + 앞으로 갈수록 가늘고 옅어지는 띠 항적.
    ///   4 유령선 예측   : 옅은 링 + 화살표 + 가는 항적 위 1 · 3 · 5초 뒤 배 실루엣(갈수록 옅게).
    ///   5 호 + 유령선   : 3의 호와 띠 항적 + 4의 유령선.
    /// 항적은 배의 실제 선회 식(ShipController.StepHelm)을 지금 전령기 · 타각 그대로 5초 앞까지 돌려 그린다(정지하면 항적 없음).
    /// 타륜을 돌리는 동안과 놓은 뒤 2초는 진하게, 그 뒤는 설정(옅게 35% · 숨김 · 그대로). 전투 중일 때만(정비 · 메뉴에서는 숨김).
    /// </summary>
    public sealed class HelmGuide : MonoBehaviour
    {
        public const float Horizon = 5f, ActiveHold = 2f, IdleAlpha = 0.35f, RingPad = 3f, RingDegPerRudder = 1.8f;
        private const int RingPoints = 72, PathSteps = 50, Ticks = 15, Chevrons = 6;
        private static readonly Color RingColor = new(0.5f, 0.83f, 1f, 1f);
        private static Material s_mat, s_dash;
        private static TMP_FontAsset s_font;

        private ShipController _ship;
        private Transform _root;
        private LineRenderer _ring, _arc, _arcEnd, _arcZero, _path, _head;
        private readonly List<LineRenderer> _ticks = new(), _dots = new(), _chev = new(), _ghosts = new();
        private readonly List<TextMeshPro> _labels = new();
        private MeshRenderer _arrowR;
        private MeshFilter _arrowF;
        private Mesh _arrowMesh;
        private float _lastRudder, _activeUntil = -99f, _dimsAt = -99f, _len = 12f, _beam = 4f;
        private Vector2 _center;   // 선체 가운데(배 좌표 x · z) — 원의 중심
        private readonly Vector3[] _pathPts = new Vector3[PathSteps + 1];
        private readonly float[] _pathYaw = new float[PathSteps + 1];

        /// <summary>원 반지름(m): 배 길이 · 폭 중 긴 쪽의 절반 + 3m — 선체 끝에서 3m 떨어져 눈금 · 화살표가 선체에 가리지 않는다.</summary>
        public float RingRadius => Mathf.Max(_len, _beam) * 0.5f + RingPad;

        /// <summary>검증용: 지금 그리는 디자인 · 진하기 · 항적 끝까지 거리(m).</summary>
        public int Style { get; private set; }
        public float Alpha { get; private set; }
        public float PathLength { get; private set; }
        public int GhostsShown { get; private set; }

        public static HelmGuide Ensure(ShipController ship)
        {
            if (ship == null) return null;
            var g = ship.GetComponent<HelmGuide>();
            return g != null ? g : ship.gameObject.AddComponent<HelmGuide>();
        }

        private void Awake()
        {
            _ship = GetComponent<ShipController>();
            if (s_mat == null) s_mat = new Material(Shader.Find("Sprites/Default")) { name = "Helm guide" };
            if (s_dash == null)
            {
                var tex = new Texture2D(8, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Helm dash" };
                for (int x = 0; x < 8; x++) for (int y = 0; y < 2; y++) tex.SetPixel(x, y, x < 5 ? Color.white : Color.clear);
                tex.Apply();
                s_dash = new Material(Shader.Find("Sprites/Default")) { name = "Helm guide dash", mainTexture = tex };
            }
            _root = new GameObject("Helm guide").transform;   // 배를 따라가지 않는 세계 공간(매 프레임 다시 그림)
            _ring = Line("Ring", 0.22f, true);
            _arc = Line("Arc", 0.85f, false);
            _arcEnd = Dot("Arc end");
            _arcZero = Dot("Arc zero");
            _path = Line("Path", 0.35f, false);
            _head = Line("Path head", 0.35f, false);
            for (int i = 0; i < Ticks; i++) _ticks.Add(Line($"Tick {i}", 0.2f, false));
            for (int i = 0; i < 4; i++) _dots.Add(Dot($"Dot {i}"));
            for (int i = 0; i < Chevrons; i++) _chev.Add(Line($"Chevron {i}", 0.32f, false));
            for (int i = 0; i < 3; i++) _ghosts.Add(Line($"Ghost {i}", 0.2f, true));
            var a = new GameObject("Arrow", typeof(MeshFilter), typeof(MeshRenderer));
            a.transform.SetParent(_root, false);
            _arrowF = a.GetComponent<MeshFilter>();
            _arrowR = a.GetComponent<MeshRenderer>();
            _arrowR.sharedMaterial = s_mat;
            _arrowR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _arrowR.receiveShadows = false;
            _arrowMesh = new Mesh { name = "Helm arrow" };
            _arrowF.sharedMesh = _arrowMesh;
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
            if (_arrowMesh != null) Destroy(_arrowMesh);
        }

        private LineRenderer Line(string name, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = s_mat;
            l.useWorldSpace = true;
            l.loop = loop;
            l.widthMultiplier = width;
            l.alignment = LineAlignment.TransformZ;
            l.numCapVertices = 2;
            l.positionCount = 0;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            return l;
        }

        private LineRenderer Dot(string name)
        {
            var l = Line(name, 0.7f, false);
            l.numCapVertices = 6;
            return l;
        }

        private void LateUpdate()
        {
            Style = DisplaySettings.HelmGuide;
            var gm = GameManager.Instance;
            bool show = Style > 0 && _ship != null && _ship.IsAlive && gm != null && gm.State == GameState.Playing;
            float rudder = _ship != null ? _ship.RudderInput : 0f;
            if (Mathf.Abs(rudder - _lastRudder) > 0.001f) _activeUntil = Time.time + ActiveHold;
            _lastRudder = rudder;
            bool active = Time.time < _activeUntil;
            int idle = DisplaySettings.HelmGuideIdle;
            Alpha = active || idle == 2 ? 1f : idle == 1 ? 0f : IdleAlpha;
            if (!show || Alpha <= 0.001f) { HideAll(); return; }
            if (Time.time >= _dimsAt) { _dimsAt = Time.time + 1f; MeasureHull(); }
            Draw(Style, rudder * _ship.MaxRudderAngle, Alpha);
        }

        // ------------------------------------------------------------ 그리기

        private void Draw(int style, float rudderDeg, float alpha)
        {
            HideAll();
            Color c = RudderColor(rudderDeg);
            Vector3 pos = _ship.transform.position;
            Vector3 fwd = _ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            // 원(2026-11-22 사용자: 타원 말고 원 — 지름은 배보다 조금 크게, 눈금 · 화살표가 선체에 가리지 않게)
            float rad = RingRadius;
            Vector3 hc = pos + right * _center.x + fwd * _center.y;
            Vector3 RingPt(float deg) { float a = deg * RingDegPerRudder * Mathf.Deg2Rad; return W(hc + right * (Mathf.Sin(a) * rad) + fwd * (Mathf.Cos(a) * rad)); }

            Predict(pos, fwd);
            bool moving = PathLength > 1f;

            // 링
            if (style == 1 || style == 4)
            {
                _ring.positionCount = RingPoints;
                for (int i = 0; i < RingPoints; i++) { float a = i * Mathf.PI * 2f / RingPoints; _ring.SetPosition(i, W(hc + right * (Mathf.Sin(a) * rad) + fwd * (Mathf.Cos(a) * rad))); }
                Tint(_ring, RingColor, alpha * (style == 4 ? 0.35f : 0.6f));
            }
            if (style == 2)
            {
                // 뱃머리 쪽 반원(±35°) + 5° 눈금(10°마다 길게 · 숫자), 돌린 각도까지 불
                _ring.loop = false;
                _ring.positionCount = 29;
                for (int i = 0; i <= 28; i++) _ring.SetPosition(i, RingPt(-35f + 70f * i / 28f));
                Tint(_ring, RingColor, alpha * 0.7f);
                EnsureLabels();
                int li = 0;
                for (int k = 0; k < Ticks; k++)
                {
                    float deg = -35f + 5f * k;
                    bool major = Mathf.RoundToInt(deg) % 10 == 0;
                    bool lit = rudderDeg >= 0f ? deg > 0f && deg <= rudderDeg + 0.5f : deg < 0f && deg >= rudderDeg - 0.5f;
                    Vector3 p0 = RingPt(deg), dir = (p0 - W(hc)); dir.y = 0f; dir.Normalize();
                    var t = _ticks[k];
                    t.positionCount = 2;
                    t.SetPosition(0, p0); t.SetPosition(1, p0 + dir * (major ? 1.6f : 1f));
                    t.widthMultiplier = lit || Mathf.Approximately(deg, 0f) ? 0.32f : 0.18f;
                    Tint(t, lit ? c : Mathf.Approximately(deg, 0f) ? Color.white : RingColor, alpha * (lit ? 1f : 0.6f));
                    if (major && li < _labels.Count)
                    {
                        var lb = _labels[li++];
                        lb.gameObject.SetActive(true);
                        lb.transform.position = p0 + dir * 3f + Vector3.up * 0.05f;
                        lb.text = Mathf.Abs(Mathf.RoundToInt(deg)).ToString();
                        var lc = lit ? c : new Color(0.62f, 0.78f, 0.87f); lc.a = alpha;
                        lb.color = lc;
                    }
                }
            }
            // 화살표
            if (style == 1 || style == 2 || style == 4)
            {
                Vector3 at = RingPt(rudderDeg);
                Vector3 adir = Quaternion.AngleAxis(rudderDeg * 2.6f, Vector3.up) * fwd;
                BuildArrow(at, adir, style == 2 ? 2.4f : 2.7f, c, alpha);
            }
            // 호
            if (style == 3 || style == 5)
            {
                int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(rudderDeg) / 2f) + 1);
                if (Mathf.Abs(rudderDeg) > 0.5f)
                {
                    _arc.positionCount = n;
                    for (int i = 0; i < n; i++) _arc.SetPosition(i, RingPt(rudderDeg * i / (n - 1)));
                    Tint(_arc, c, alpha * 0.9f);
                }
                SetDot(_arcZero, RingPt(0f), 0.6f, Color.white, alpha);
                SetDot(_arcEnd, RingPt(rudderDeg), 1.1f, c, alpha);
            }
            // 항적
            if (moving)
            {
                switch (style)
                {
                    case 1:
                        _path.sharedMaterial = s_dash; _path.textureMode = LineTextureMode.Tile; _path.textureScale = new Vector2(0.45f, 1f);
                        SetPath(_path, 0.35f, 0.35f); Tint(_path, c, alpha);
                        for (int k = 0; k < 4; k++) SetDot(_dots[k], _pathPts[(k + 1) * PathSteps / 5], 0.75f, c, alpha);
                        Vector3 e = _pathPts[PathSteps], ed = Dir(_pathYaw[PathSteps]), es = Vector3.Cross(Vector3.up, ed);
                        _head.positionCount = 3;
                        _head.SetPosition(0, e - ed * 1.4f + es * 1.1f); _head.SetPosition(1, e + ed * 0.4f); _head.SetPosition(2, e - ed * 1.4f - es * 1.1f);
                        Tint(_head, c, alpha);
                        break;
                    case 2:
                        float flow = Mathf.Repeat(Time.time * 1.5f, 1f);
                        for (int k = 0; k < Chevrons; k++)
                        {
                            int idx = Mathf.Min(PathSteps, Mathf.RoundToInt((k + flow) * PathSteps / Chevrons));
                            Vector3 p = _pathPts[idx], d = Dir(_pathYaw[idx]), sd = Vector3.Cross(Vector3.up, d);
                            var l = _chev[k];
                            l.positionCount = 3;
                            l.SetPosition(0, p - d * 0.9f + sd * 1f); l.SetPosition(1, p + d * 0.3f); l.SetPosition(2, p - d * 0.9f - sd * 1f);
                            Tint(l, c, alpha * (1f - k * 0.12f));
                        }
                        break;
                    case 3:
                    case 5:
                        _path.sharedMaterial = s_mat; _path.textureMode = LineTextureMode.Stretch;
                        SetPath(_path, 1.3f, 0.2f);
                        var gr = new Gradient();
                        gr.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                                   new[] { new GradientAlphaKey(0.8f * alpha, 0f), new GradientAlphaKey(0.1f * alpha, 1f) });
                        _path.colorGradient = gr;
                        break;
                    case 4:
                        _path.sharedMaterial = s_mat; _path.textureMode = LineTextureMode.Stretch;
                        SetPath(_path, 0.22f, 0.22f); Tint(_path, c, alpha * 0.7f);
                        break;
                }
                if (style == 4 || style == 5)
                {
                    int[] at = { PathSteps / 5, PathSteps * 3 / 5, PathSteps };
                    for (int k = 0; k < 3; k++) GhostAt(_ghosts[k], _pathPts[at[k]], Dir(_pathYaw[at[k]]), c, alpha * (0.6f - k * 0.13f));
                    GhostsShown = 3;
                }
            }
        }

        private void HideAll()
        {
            GhostsShown = 0;
            _ring.positionCount = 0; _ring.loop = true;
            _arc.positionCount = 0; _arcEnd.positionCount = 0; _arcZero.positionCount = 0;
            _path.positionCount = 0; _head.positionCount = 0;
            foreach (var l in _ticks) l.positionCount = 0;
            foreach (var l in _dots) l.positionCount = 0;
            foreach (var l in _chev) l.positionCount = 0;
            foreach (var l in _ghosts) l.positionCount = 0;
            foreach (var t in _labels) if (t != null) t.gameObject.SetActive(false);
            if (_arrowR != null) _arrowR.enabled = false;
        }

        /// <summary>지금 전령기 · 타각으로 Horizon초 앞까지(배의 실제 선회 식).</summary>
        private void Predict(Vector3 pos, Vector3 fwd)
        {
            float speed = _ship.CurrentSpeed, yawRate = _ship.YawRate;
            float throttle = _ship.ThrottleInput, rudder = _ship.RudderInput;
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            float dt = Horizon / PathSteps;
            Vector3 rt = Vector3.Cross(Vector3.up, fwd);
            Vector3 p = pos + rt * _center.x + fwd * (_center.y + _len * 0.5f + 0.5f);   // 뱃머리 앞에서 시작(선체 가운데 기준)
            _pathPts[0] = W(p); _pathYaw[0] = yaw;
            PathLength = 0f;
            for (int i = 1; i <= PathSteps; i++)
            {
                _ship.StepHelm(throttle, rudder, ref speed, ref yawRate, dt);
                yaw += yawRate * dt;
                Vector3 step = Dir(yaw) * (speed * dt);
                p += step;
                PathLength += step.magnitude;
                _pathPts[i] = W(p); _pathYaw[i] = yaw;
            }
        }

        private void SetPath(LineRenderer l, float w0, float w1)
        {
            l.positionCount = PathSteps + 1;
            l.SetPositions(_pathPts);
            l.widthCurve = AnimationCurve.Linear(0f, w0, 1f, w1);
            l.widthMultiplier = 1f;
        }

        private void GhostAt(LineRenderer l, Vector3 at, Vector3 dir, Color c, float a)
        {
            Vector3 s = Vector3.Cross(Vector3.up, dir);
            float hl = _len * 0.5f, hb = _beam * 0.5f;
            Vector3 back = at - dir * hl;   // at = 그때 뱃머리 앞 → 배 가운데는 그 뒤로 반 길이
            Vector3 mid = back;
            l.positionCount = 5;
            l.SetPosition(0, W(mid + dir * hl));
            l.SetPosition(1, W(mid + dir * (hl * 0.45f) + s * hb));
            l.SetPosition(2, W(mid - dir * hl + s * hb));
            l.SetPosition(3, W(mid - dir * hl - s * hb));
            l.SetPosition(4, W(mid + dir * (hl * 0.45f) - s * hb));
            Tint(l, c, a);
        }

        private void SetDot(LineRenderer l, Vector3 at, float size, Color c, float a)
        {
            l.positionCount = 2;
            l.SetPosition(0, at - Vector3.forward * 0.01f);
            l.SetPosition(1, at + Vector3.forward * 0.01f);
            l.widthMultiplier = size;
            Tint(l, c, a);
        }

        private void BuildArrow(Vector3 at, Vector3 dir, float size, Color c, float a)
        {
            Vector3 s = Vector3.Cross(Vector3.up, dir);
            float h = size;
            // 화살표 모양(꼬리 → 머리): 가는 몸통 + 넓은 머리
            Vector3[] v =
            {
                at - dir * (0.55f * h) - s * (0.23f * h), at - dir * (0.55f * h) + s * (0.23f * h),
                at + dir * (0.1f * h) + s * (0.23f * h), at + dir * (0.1f * h) - s * (0.23f * h),
                at + dir * (0.1f * h) - s * (0.5f * h), at + dir * (0.1f * h) + s * (0.5f * h), at + dir * (0.75f * h),
            };
            for (int i = 0; i < v.Length; i++) v[i] = W(v[i]) + Vector3.up * 0.02f;
            var col = new Color(c.r, c.g, c.b, a);
            _arrowMesh.Clear();
            _arrowMesh.vertices = v;
            _arrowMesh.colors = new[] { col, col, col, col, col, col, col };
            _arrowMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5 };
            _arrowMesh.RecalculateBounds();
            _arrowR.enabled = true;
        }

        private void EnsureLabels()
        {
            if (_labels.Count > 0) return;
            if (s_font == null) { var any = Object.FindFirstObjectByType<TMP_Text>(); s_font = any != null ? any.font : TMP_Settings.defaultFontAsset; }
            if (s_font == null) return;
            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject($"Tick label {i}");
                go.transform.SetParent(_root, false);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var t = go.AddComponent<TextMeshPro>();
                t.font = s_font; t.fontSize = 9f; t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.rectTransform.sizeDelta = new Vector2(3f, 1.5f);
                t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _labels.Add(t);
            }
        }

        /// <summary>선체 길이 · 폭: 블록 선체 조각(뱃전 · 함수 · 함미)만으로 — 헬기 날개 · 포신 제외. 없으면 메시 전체.</summary>
        private void MeasureHull()
        {
            Transform root = null;
            var hb = _ship.GetComponentInChildren<ShipHullBuilder>();
            if (hb != null) root = hb.HullRoot;
            if (root == null) root = _ship.transform;
            var inv = _ship.transform.worldToLocalMatrix;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.name == "Night lamp") continue;
                var b = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
            }
            if (!float.IsFinite(min.x)) return;
            _len = Mathf.Clamp(max.z - min.z, 6f, 60f);
            _beam = Mathf.Clamp(max.x - min.x, 2f, 20f);
            _center = new Vector2((min.x + max.x) * 0.5f, (min.z + max.z) * 0.5f);
        }

        // ------------------------------------------------------------ 도움

        /// <summary>타각 색: 작음(35% 아래) 초록 · 중간(75% 아래) 노랑 · 큼 주황.</summary>
        public static Color RudderColor(float rudderDeg)
        {
            float a = Mathf.Abs(rudderDeg) / 35f;
            return a < 0.35f ? new Color(0.36f, 0.89f, 0.48f) : a < 0.75f ? new Color(1f, 0.82f, 0.29f) : new Color(1f, 0.6f, 0.24f);
        }

        private static Vector3 Dir(float yawDeg) { float r = yawDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); }
        private static Vector3 W(Vector3 p) => new(p.x, 0.22f, p.z);

        private static void Tint(LineRenderer l, Color c, float a)
        {
            c.a = a;
            l.startColor = l.endColor = c;
        }
    }
}
