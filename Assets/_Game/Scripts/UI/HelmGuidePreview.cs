using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 설정 › 조함 › 타륜 표시 미리보기(2026-11-22 사용자: 설정에서 고를 때 인게임에서 어떻게 보이는지 시안도 같이).
    /// 위에서 본 바다와 배 위에 고른 디자인(Game.View.HelmGuide와 같은 규칙 — 링 · 화살표 · 눈금 · 호 · 점선 / 꺾쇠 / 띠 항적 · 유령선)을
    /// 그린다. 타륜이 저절로 좌우로 천천히 돌고(±35°), 20노트로 달리는 것으로 계산한다. "타륜 놓음" 설정이 옅게면 가끔 옅어지는 모습도 보인다.
    /// </summary>
    public class HelmGuidePreview : MaskableGraphic
    {
        public int Style = 4;
        public int IdleMode;
        public TextMeshProUGUI Caption;

        private const float Max = 35f;
        private static readonly Color Sea = new(0.06f, 0.16f, 0.23f, 1f), Wave = new(0.11f, 0.26f, 0.34f, 1f), Hull = new(0.54f, 0.59f, 0.63f, 1f),
            Deck = new(0.27f, 0.33f, 0.36f, 1f), Ring = new(0.5f, 0.83f, 1f, 1f);
        private float _rudder, _alpha = 1f;
        private readonly List<Vector2> _pts = new();
        private readonly List<float> _yaw = new();

        private void Update()
        {
            float t = Time.unscaledTime;
            // 6초 돌리고 → 3초 놓기(놓은 동안은 설정대로 옅게 · 숨김 · 그대로)
            float cycle = Mathf.Repeat(t, 9f);
            bool holding = cycle < 6f;
            if (holding) _rudder = Mathf.Round(Mathf.Sin(t * 0.9f) * Max);
            float target = holding ? 1f : IdleMode == 2 ? 1f : IdleMode == 1 ? 0f : 0.35f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime * 2.5f);
            if (Caption != null)
                Caption.text = Style == 0 ? "타륜 표시 끔" : $"{(_rudder == 0 ? "중앙 0°" : (_rudder > 0 ? "우현 " : "좌현 ") + Mathf.Abs(_rudder) + "°")} · 20노트 · {(holding ? "타륜 잡음" : "타륜 놓음")}";
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float s = Mathf.Min(r.width, r.height) / 300f;   // 시안 좌표(300 × 300) → 화면
            Vector2 o = new(r.center.x - 150f * s, r.center.y + 150f * s);
            Vector2 P(float x, float y) => new(o.x + x * s, o.y - y * s);   // 시안은 위가 0
            Quad(vh, P(0, 0), P(300, 300), Sea);
            for (int i = 0; i < 12; i++) { float x = (i * 61) % 300, y = 15 + (i * 47) % 280; Seg(vh, P(x, y), P(x + 12, y), 1.4f * s, Wave); }

            // 원(2026-11-22): 배(길이 64 — 위 34 · 아래 30) 가운데에서 반지름 = 반 길이 + 12 — 눈금 · 화살표가 선체에 가리지 않게
            const float cx = 150f, cy = 190f, hullMid = 188f, rad = 44f;
            Vector2 RingPt(float deg, float ex = 0f) { float a = (-90f + deg * 1.8f) * Mathf.Deg2Rad; return P(cx + Mathf.Cos(a) * (rad + ex), hullMid + Mathf.Sin(a) * (rad + ex)); }
            Color c = Game.View.HelmGuide.RudderColor(_rudder);
            float A = _alpha;

            // 항적(20노트, 배의 선회와 비슷한 모양)
            _pts.Clear(); _yaw.Clear();
            float v = 20f * 0.514f, yawRate = _rudder / Max * 40f * Mathf.Deg2Rad, h = -Mathf.PI / 2f, px = cx, py = cy - 34f;
            _pts.Add(new Vector2(px, py)); _yaw.Add(h);
            for (int i = 0; i < 50; i++) { h += yawRate * 0.1f; px += Mathf.Cos(h) * v * 0.1f * 1.6f; py += Mathf.Sin(h) * v * 0.1f * 1.6f; _pts.Add(new Vector2(px, py)); _yaw.Add(h); }

            if (Style == 1 || Style == 4) Ellipse(vh, P(cx, hullMid), rad * s, rad * s, (Style == 4 ? 1.2f : 1.5f) * s, Alpha(Ring, A * (Style == 4 ? 0.35f : 0.6f)));
            if (Style == 2)
            {
                for (int k = -35; k <= 35; k += 5)
                {
                    bool major = k % 10 == 0;
                    bool lit = _rudder >= 0 ? k > 0 && k <= _rudder : k < 0 && k >= _rudder;
                    Seg(vh, RingPt(k), RingPt(k, major ? 9f : 5f), (lit || k == 0 ? 3f : 1.4f) * s, Alpha(lit ? c : k == 0 ? Color.white : Ring, A * (lit ? 1f : 0.6f)));
                }
                for (int k = 0; k < 28; k++) Seg(vh, RingPt(-35f + 70f * k / 28f), RingPt(-35f + 70f * (k + 1) / 28f), 1.5f * s, Alpha(Ring, A * 0.7f));
            }
            if (Style == 3 || Style == 5)
            {
                int n = 24;
                for (int k = 0; k < n; k++) Seg(vh, RingPt(_rudder * k / n), RingPt(_rudder * (k + 1) / n), 6f * s, Alpha(c, A * 0.9f));
                Disc(vh, RingPt(0), 3f * s, Alpha(Color.white, A));
                Disc(vh, RingPt(_rudder), 8f * s, Alpha(c, A * 0.35f));
                Disc(vh, RingPt(_rudder), 4.5f * s, Alpha(c, A));
            }
            // 항적 그림
            if (Style == 1)
            {
                for (int i = 0; i + 1 < _pts.Count; i += 2) Seg(vh, P(_pts[i].x, _pts[i].y), P(_pts[i + 1].x, _pts[i + 1].y), 2.5f * s, Alpha(c, A));
                foreach (int k in new[] { 10, 20, 30, 40 }) Disc(vh, P(_pts[k].x, _pts[k].y), 2.8f * s, Alpha(c, A));
                Head(vh, P, _pts[^1], _yaw[^1], 9f, c, A);
            }
            if (Style == 2)
            {
                float flow = Mathf.Repeat(Time.unscaledTime * 1.5f, 1f);
                for (int k = 0; k < 6; k++)
                {
                    int idx = Mathf.Min(_pts.Count - 1, Mathf.RoundToInt((k + flow) * 8f));
                    Chevron(vh, P, _pts[idx], _yaw[idx], 6f, c, A * (1f - k * 0.12f));
                }
            }
            if (Style == 3 || Style == 5)
                for (int i = 1; i < _pts.Count; i++)
                {
                    float f = (float)i / _pts.Count;
                    Seg(vh, P(_pts[i - 1].x, _pts[i - 1].y), P(_pts[i].x, _pts[i].y), (7f * (1f - f) + 1.5f) * s, Alpha(c, A * (0.75f * (1f - f) + 0.1f)));
                }
            if (Style == 4)
                for (int i = 1; i < _pts.Count; i++) Seg(vh, P(_pts[i - 1].x, _pts[i - 1].y), P(_pts[i].x, _pts[i].y), 1.5f * s, Alpha(c, A * 0.7f));
            if (Style == 4 || Style == 5)
            {
                int[] at = { 10, 30, 50 };
                for (int k = 0; k < 3; k++) Ghost(vh, P, _pts[at[k]], _yaw[at[k]], 0.55f, Alpha(c, A * (0.55f - k * 0.12f)), s);
            }

            // 배
            ShipShape(vh, P, cx, cy, s);
            if (Style == 1 || Style == 2 || Style == 4) Arrow(vh, RingPt(_rudder), (-_rudder * 2.6f) * Mathf.Deg2Rad, (Style == 2 ? 0.9f : 1f) * s, Alpha(c, A));
        }

        // ------------------------------------------------------------ 도형

        private static Color Alpha(Color c, float a) { c.a = a; return c; }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x, a.y), c, Vector2.zero); vh.AddVert(new Vector3(b.x, a.y), c, Vector2.zero);
            vh.AddVert(new Vector3(b.x, b.y), c, Vector2.zero); vh.AddVert(new Vector3(a.x, b.y), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void Seg(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
        {
            Vector2 d = b - a; if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero); vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void Disc(VertexHelper vh, Vector2 c0, float r, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(c0, c, Vector2.zero);
            for (int k = 0; k <= 16; k++) { float a = k * Mathf.PI * 2f / 16f; vh.AddVert(c0 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, c, Vector2.zero); }
            for (int k = 0; k < 16; k++) vh.AddTriangle(i, i + 1 + k, i + 2 + k);
        }

        private static void Ellipse(VertexHelper vh, Vector2 c0, float rx, float ry, float w, Color c)
        {
            Vector2 prev = c0 + new Vector2(0f, ry);
            for (int k = 1; k <= 64; k++)
            {
                float a = k * Mathf.PI * 2f / 64f;
                Vector2 p = c0 + new Vector2(Mathf.Sin(a) * rx, Mathf.Cos(a) * ry);
                Seg(vh, prev, p, w, c); prev = p;
            }
        }

        private static void Poly(VertexHelper vh, Vector2[] pts, Color c)
        {
            int i = vh.currentVertCount;
            foreach (var p in pts) vh.AddVert(p, c, Vector2.zero);
            for (int k = 1; k + 1 < pts.Length; k++) vh.AddTriangle(i, i + k, i + k + 1);
        }

        private static void ShipShape(VertexHelper vh, System.Func<float, float, Vector2> P, float cx, float cy, float s)
        {
            Poly(vh, new[] { P(cx, cy - 34), P(cx + 9, cy - 18), P(cx + 9, cy + 30), P(cx - 9, cy + 30), P(cx - 9, cy - 18) }, Hull);
            Quad(vh, P(cx - 5, cy + 8), P(cx + 5, cy - 6), Deck);
        }

        /// <summary>화살표(꼬리 → 머리), rot = 시계 반대 회전(라디안, 화면 기준 — 위가 0).</summary>
        private static void Arrow(VertexHelper vh, Vector2 at, float rot, float s, Color c)
        {
            Vector2 R(float x, float y) { float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot); return at + new Vector2(x * cs - y * sn, x * sn + y * cs) * s; }
            Poly(vh, new[] { R(0, 18), R(13, 2), R(-13, 2) }, c);
            Poly(vh, new[] { R(-6, 2), R(6, 2), R(6, -10), R(-6, -10) }, c);
        }

        private static Vector2 Dir(float yaw) => new(Mathf.Cos(yaw), Mathf.Sin(yaw));

        private static void Head(VertexHelper vh, System.Func<float, float, Vector2> P, Vector2 at, float yaw, float size, Color c, float a)
        {
            Vector2 d = Dir(yaw), n = new(-d.y, d.x);
            Poly(vh, new[] { P(at.x + d.x * size, at.y + d.y * size), P(at.x - d.x * size * 0.5f + n.x * size * 0.75f, at.y - d.y * size * 0.5f + n.y * size * 0.75f),
                             P(at.x - d.x * size * 0.5f - n.x * size * 0.75f, at.y - d.y * size * 0.5f - n.y * size * 0.75f) }, Alpha(c, a));
        }

        private static void Chevron(VertexHelper vh, System.Func<float, float, Vector2> P, Vector2 at, float yaw, float size, Color c, float a)
        {
            Vector2 d = Dir(yaw), n = new(-d.y, d.x);
            Vector2 tip = at + d * (size * 0.5f), l = at - d * (size * 0.7f) + n * size, rr = at - d * (size * 0.7f) - n * size;
            float sc = 1f;
            Seg(vh, P(l.x, l.y), P(tip.x, tip.y), 2.5f * sc, Alpha(c, a));
            Seg(vh, P(tip.x, tip.y), P(rr.x, rr.y), 2.5f * sc, Alpha(c, a));
        }

        private static void Ghost(VertexHelper vh, System.Func<float, float, Vector2> P, Vector2 at, float yaw, float scale, Color c, float s)
        {
            Vector2 d = Dir(yaw), n = new(-d.y, d.x);
            Vector2 Q(float along, float side) { var p = at + (d * along + n * side) * scale; return P(p.x, p.y); }
            var pts = new[] { Q(34, 0), Q(18, 9), Q(-30, 9), Q(-30, -9), Q(18, -9) };
            for (int k = 0; k < pts.Length; k++) Seg(vh, pts[k], pts[(k + 1) % pts.Length], 2.2f * s, c);
        }
    }
}
