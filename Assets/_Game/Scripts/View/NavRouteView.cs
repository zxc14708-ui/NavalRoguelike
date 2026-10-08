using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Ship;

namespace Game.View
{
    /// <summary>
    /// 우클릭 항로 표시(2026-10-07): 예상 항로 = 얇은 초록 점선, 목표 = 초록 앵커(+ 수면 고리, 경유지가 여럿이면 번호).
    /// 마지막 목표 뒤로는 직진 구간이 짧게 이어지다 흐려진다. 앵커 위에 커서를 올리면 고리가 커지고 번호 대신 X(우클릭하면 취소).
    /// 선 굵기·앵커 크기는 화면 픽셀 기준이라 줌과 상관없이 같은 크기로 보인다(1080p 기준, 해상도에 비례).
    /// 판정·충돌이 없는 연출이다. 정비 화면 등 전투 중이 아닐 때는 숨긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavRouteView : MonoBehaviour
    {
        public static readonly Color Green = new(0.17f, 0.88f, 0.44f, 1f);
        private const float LinePixels = 2f, RingPixels = 13f, DashPixels = 14f, WaterY = 0.15f;
        /// <summary>앵커 그림(픽셀 단위로 그린 모양)의 배율.</summary>
        private const float GlyphScale = 1.35f;

        private ShipAutopilot _pilot;
        private LineRenderer _route, _tail;
        private readonly List<Anchor> _anchors = new();
        private static Material s_solid, s_dash;

        public static void Ensure(ShipAutopilot pilot)
        {
            if (pilot == null) return;
            var view = pilot.GetComponent<NavRouteView>();
            if (view == null) view = pilot.gameObject.AddComponent<NavRouteView>();
            view._pilot = pilot;
        }

        private sealed class Anchor
        {
            public Transform Root;
            public LineRenderer Ground, Ring, Shank, Stock, Arms, FlukeL, FlukeR, Digit, CrossA, CrossB;
            public IEnumerable<LineRenderer> Glyph()
            {
                yield return Ring; yield return Shank; yield return Stock; yield return Arms;
                yield return FlukeL; yield return FlukeR;
            }
        }

        // 앵커 모양(픽셀, 위 = +y, 원점 = 수면 위 목표점). 앵커 몸체는 점 위로 12픽셀 솟는다.
        private static readonly Vector2[] Shank = { new(0f, 17.4f), new(0f, 4f) };
        private static readonly Vector2[] Stock = { new(-5f, 14.5f), new(5f, 14.5f) };
        private static readonly Vector2[] FlukeR = { new(9.4f, 11f), new(7.3f, 8.8f), new(4.6f, 9.4f) };
        private static readonly Vector2[] FlukeL = { new(-9.4f, 11f), new(-7.3f, 8.8f), new(-4.6f, 9.4f) };
        private static readonly Vector2[][] Digits =
        {
            new Vector2[] { new(11f, 21f), new(12f, 22f), new(12f, 15f) },
            new Vector2[] { new(10f, 21f), new(11f, 22f), new(13f, 22f), new(14f, 21f), new(14f, 19.5f), new(10f, 15f), new(14f, 15f) },
            new Vector2[] { new(10f, 22f), new(14f, 22f), new(12f, 19f), new(13.5f, 18.5f), new(14f, 17f), new(13f, 15f), new(11f, 15f), new(10f, 16f) },
            new Vector2[] { new(13f, 15f), new(13f, 22f), new(10f, 17f), new(14.5f, 17f) },
            new Vector2[] { new(14f, 22f), new(10.5f, 22f), new(10.3f, 19f), new(12.5f, 19.3f), new(14f, 18f), new(14f, 16f), new(13f, 15f), new(10f, 15f) },
        };
        private static readonly Vector2[] CrossA = { new(10f, 22f), new(15f, 17f) };
        private static readonly Vector2[] CrossB = { new(15f, 22f), new(10f, 17f) };

        private void Awake()
        {
            var root = new GameObject("Nav route (view)").transform;
            root.SetParent(null, false);
            _root = root;
            _route = MakeLine("Route", root, Dash());
            _tail = MakeLine("Route tail", root, Dash());
        }

        private Transform _root;

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            var cam = Camera.main;
            bool show = _pilot != null && cam != null && gm != null && gm.State == GameState.Playing && _pilot.HasRoute;
            _root.gameObject.SetActive(show);
            if (!show) return;

            float scale = Screen.height / 1080f;
            float blink = Time.unscaledTime - _pilot.RejectedAt < 0.6f && Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.5f ? 0.35f : 1f;
            DrawRoute(cam, scale, blink);
            DrawAnchors(cam, scale, blink);
        }

        private void DrawRoute(Camera cam, float scale, float alpha)
        {
            var pts = _pilot.Predicted;
            int tailStart = Mathf.Clamp(_pilot.PredictedTailStart, 0, pts.Count);
            int mainCount = Mathf.Min(pts.Count, tailStart + 1);
            float wpp = WorldPerPixel(cam, transform.position) * scale;

            SetPath(_route, pts, 0, mainCount, wpp, alpha, alpha);
            SetPath(_tail, pts, tailStart, pts.Count - tailStart, wpp, alpha, 0f);
        }

        private static void SetPath(LineRenderer line, List<Vector3> pts, int start, int count, float wpp, float a0, float a1)
        {
            if (count < 2) { line.enabled = false; return; }
            line.enabled = true;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                var p = pts[start + i];
                line.SetPosition(i, new Vector3(p.x, WaterY, p.z));
            }
            line.startWidth = line.endWidth = LinePixels * wpp;
            line.textureScale = new Vector2(1f / Mathf.Max(0.01f, DashPixels * wpp), 1f);
            line.startColor = new Color(Green.r, Green.g, Green.b, a0);
            line.endColor = new Color(Green.r, Green.g, Green.b, a1);
        }

        private void DrawAnchors(Camera cam, float scale, float alpha)
        {
            var route = _pilot.Route;
            while (_anchors.Count < route.Count) _anchors.Add(MakeAnchor(_anchors.Count));
            int hover = HoveredAnchor(cam, route);
            Vector3 right = cam.transform.right, up = Vector3.ProjectOnPlane(cam.transform.up, cam.transform.forward).normalized;

            for (int i = 0; i < _anchors.Count; i++)
            {
                var a = _anchors[i];
                bool on = i < route.Count;
                a.Root.gameObject.SetActive(on);
                if (!on) continue;

                Vector3 basePos = new(route[i].x, WaterY, route[i].z);
                float wpp = WorldPerPixel(cam, basePos) * scale;
                float gpp = wpp * GlyphScale;
                bool hot = i == hover;
                var color = new Color(Green.r, Green.g, Green.b, alpha);
                float width = LinePixels * wpp;

                // 수면 고리(쿼터뷰라 타원으로 보인다)
                float r = (hot ? RingPixels + 2f : RingPixels) * gpp;
                for (int k = 0; k < a.Ground.positionCount; k++)
                {
                    float ang = k / (float)a.Ground.positionCount * Mathf.PI * 2f;
                    a.Ground.SetPosition(k, basePos + new Vector3(Mathf.Sin(ang) * r, 0f, Mathf.Cos(ang) * r));
                }
                Style(a.Ground, (hot ? 1.5f : 1f) * wpp, color);

                // 앵커 몸체: 화면을 향해 서 있다
                Vector3 W(Vector2 px) => basePos + (right * px.x + up * px.y) * gpp;
                for (int k = 0; k < a.Ring.positionCount; k++)
                {
                    float ang = k / (float)a.Ring.positionCount * Mathf.PI * 2f;
                    a.Ring.SetPosition(k, W(new Vector2(Mathf.Cos(ang) * 2.6f, 20f + Mathf.Sin(ang) * 2.6f)));
                }
                SetGlyph(a.Shank, Shank, W);
                SetGlyph(a.Stock, Stock, W);
                for (int k = 0; k < a.Arms.positionCount; k++)
                {
                    float ang = Mathf.Lerp(0.15f, 0.85f, k / (float)(a.Arms.positionCount - 1)) * Mathf.PI;
                    a.Arms.SetPosition(k, W(new Vector2(Mathf.Cos(ang) * 7.5f, 11f - Mathf.Sin(ang) * 7.5f)));
                }
                SetGlyph(a.FlukeL, FlukeL, W);
                SetGlyph(a.FlukeR, FlukeR, W);
                foreach (var line in a.Glyph()) Style(line, width, color);

                // 번호(경유지가 여럿일 때) 또는 커서를 올렸을 때 X
                bool number = !hot && route.Count > 1 && i < Digits.Length;
                a.Digit.enabled = number;
                if (number) { SetGlyph(a.Digit, Digits[i], W); Style(a.Digit, width, color); }
                a.CrossA.enabled = a.CrossB.enabled = hot;
                if (hot)
                {
                    SetGlyph(a.CrossA, CrossA, W); Style(a.CrossA, width, color);
                    SetGlyph(a.CrossB, CrossB, W); Style(a.CrossB, width, color);
                }
            }
        }

        private static int HoveredAnchor(Camera cam, IReadOnlyList<Vector3> route)
        {
            var mouse = Mouse.current;
            if (mouse == null) return -1;
            Vector2 m = mouse.position.ReadValue();
            int best = -1;
            float bestSq = 22f * 22f * (Screen.height / 1080f) * (Screen.height / 1080f);
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 s = cam.WorldToScreenPoint(route[i]);
                if (s.z <= 0f) continue;
                float d = (m - new Vector2(s.x, s.y + 12f)).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = i; }
            }
            return best;
        }

        private static void SetGlyph(LineRenderer line, Vector2[] px, System.Func<Vector2, Vector3> toWorld)
        {
            line.positionCount = px.Length;
            for (int i = 0; i < px.Length; i++) line.SetPosition(i, toWorld(px[i]));
        }

        private static void Style(LineRenderer line, float width, Color color)
        {
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
        }

        /// <summary>그 지점에서 화면 1픽셀이 몇 m인가.</summary>
        private static float WorldPerPixel(Camera cam, Vector3 at)
        {
            float h = Mathf.Max(1, Screen.height);
            if (cam.orthographic) return 2f * cam.orthographicSize / h;
            float dist = Vector3.Distance(cam.transform.position, at);
            return 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / h;
        }

        private Anchor MakeAnchor(int index)
        {
            var root = new GameObject($"Route anchor {index + 1}").transform;
            root.SetParent(_root, false);
            var a = new Anchor
            {
                Root = root,
                Ground = MakeLine("Water ring", root, Solid(), 40, true),
                Ring = MakeLine("Anchor ring", root, Solid(), 14, true),
                Shank = MakeLine("Shank", root, Solid()),
                Stock = MakeLine("Stock", root, Solid()),
                Arms = MakeLine("Arms", root, Solid(), 12),
                FlukeL = MakeLine("Fluke L", root, Solid()),
                FlukeR = MakeLine("Fluke R", root, Solid()),
                Digit = MakeLine("Number", root, Solid()),
                CrossA = MakeLine("Remove A", root, Solid()),
                CrossB = MakeLine("Remove B", root, Solid()),
            };
            return a;
        }

        private static LineRenderer MakeLine(string name, Transform parent, Material material, int points = 2, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.numCornerVertices = 1;
            line.numCapVertices = 1;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static Shader LineShader()
        {
            var shader = Shader.Find("Sprites/Default");
            return shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
        }

        private static Material Solid()
        {
            if (s_solid == null) s_solid = new Material(LineShader()) { name = "Nav route solid (Runtime)" };
            return s_solid;
        }

        /// <summary>점선: 한 칸의 앞 55%만 칠한 반복 텍스처(선 길이 기준으로 반복).</summary>
        private static Material Dash()
        {
            if (s_dash != null) return s_dash;
            const int w = 16;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Nav route dash" };
            for (int x = 0; x < w; x++) tex.SetPixel(x, 0, x < 9 ? Color.white : new Color(1f, 1f, 1f, 0f));
            tex.Apply(false, true);
            s_dash = new Material(LineShader()) { name = "Nav route dash (Runtime)", mainTexture = tex };
            return s_dash;
        }
    }
}
