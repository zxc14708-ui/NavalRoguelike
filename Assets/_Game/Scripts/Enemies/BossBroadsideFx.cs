using System.Collections.Generic;
using UnityEngine;
using Game.Ship;

namespace Game.Enemies
{
    /// <summary>
    /// 연안 경비정 현측 연출(2026-11-26). 수면 부채꼴과 발사관 불빛을 한 메시로 그린다(정점 색 · 투명, 매 프레임 다시 씀).
    ///   - 경고(붉은색): 일제사격 직전 — 그 현측 바깥으로 옅은 부채꼴 + 밝은 붉은 테두리 + 바깥으로 밀려 나가는 줄무늬 2개,
    ///     발사관마다 둥근 붉은 불(쏠 때가 가까울수록 크고 밝고 빠르게 맥박).
    ///   - 기회(금색): 쏜 직후 재장전 중 — 그 현측 부채꼴(여기서 쏘면 열린 발사관을 노린다) + 금색 테두리 · 양옆 가장자리,
    ///     달아오른 발사관(주황, 식어 감), 닫히기 직전 깜빡임.
    /// 색 약속: 위협 = 붉은색, 기회 = 금색(이후 보스도 같은 말을 쓴다).
    /// </summary>
    public sealed class BossBroadsideFx : MonoBehaviour
    {
        public enum Mode { Off, Warn, Open }

        public static readonly Color WarnColor = new(1f, 0.16f, 0.08f, 1f);
        public static readonly Color OpenColor = new(1f, 0.78f, 0.18f, 1f);

        private const int Segs = 28;
        private Mesh _mesh;
        private readonly List<Vector3> _v = new(512);
        private readonly List<Color> _c = new(512);
        private readonly List<int> _t = new(1024);
        private Transform[] _sockets = new Transform[0];
        private Mode _mode;
        private float _side, _start, _length, _radius, _halfAngle;

        public Mode Current => _mode;
        /// <summary>검증용: 지금 부채꼴 방향(배 기준, +1 우현 / −1 좌현).</summary>
        public float Side => _side;

        public static BossBroadsideFx Ensure(Transform boss)
        {
            var t = boss.Find("BroadsideFx");
            if (t != null && t.TryGetComponent<BossBroadsideFx>(out var fx)) return fx;
            var go = new GameObject("BroadsideFx");
            go.transform.SetParent(boss, false);
            go.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            fx = go.AddComponent<BossBroadsideFx>();
            fx._mesh = new Mesh { name = "BossBroadsideFx" };
            fx._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = fx._mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SignatureFx.Additive;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return fx;
        }

        /// <summary>side: +1 우현 · −1 좌현. length: 경고면 쏠 때까지 초, 기회면 열린 시간.</summary>
        public void Show(Mode mode, float side, float length, Transform[] sockets, float radius = 30f, float halfAngle = 40f)
        {
            _mode = mode; _side = Mathf.Sign(side); _start = Time.time; _length = Mathf.Max(0.1f, length);
            _sockets = sockets ?? new Transform[0];
            _radius = radius; _halfAngle = halfAngle;
            Refresh();
        }

        public void Hide() { _mode = Mode.Off; Refresh(); }

        private void LateUpdate() => Refresh();

        private static Color A(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

        private Vector3 Dir(float u)   // u 0~1: 부채꼴 한쪽 끝 → 다른 끝
        {
            float yaw = ((_side > 0 ? 90f : -90f) - _halfAngle + _halfAngle * 2f * u) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }

        /// <summary>반지름 r0 → r1 띠(호). 안 · 밖 색을 따로.</summary>
        private void Band(float r0, float r1, Color c0, Color c1, float fadeEdges = 0f)
        {
            int baseV = _v.Count;
            for (int i = 0; i <= Segs; i++)
            {
                float u = i / (float)Segs;
                Vector3 d = Dir(u);
                float edge = fadeEdges > 0f ? Mathf.Clamp01((0.5f - Mathf.Abs(u - 0.5f)) / fadeEdges) : 1f;
                _v.Add(d * r0); _c.Add(A(c0, c0.a * edge));
                _v.Add(d * r1); _c.Add(A(c1, c1.a * edge));
                if (i == 0) continue;
                int a = baseV + (i - 1) * 2, b = a + 2;
                _t.Add(a); _t.Add(b); _t.Add(a + 1); _t.Add(a + 1); _t.Add(b); _t.Add(b + 1);
            }
        }

        /// <summary>부채꼴 가장자리(반지름 방향) 선.</summary>
        private void Edge(float u, float r0, float r1, float width, Color c)
        {
            Vector3 d = Dir(u), side = Vector3.Cross(Vector3.up, d) * (width * 0.5f);
            int b = _v.Count;
            _v.Add(d * r0 - side); _v.Add(d * r0 + side); _v.Add(d * r1 + side); _v.Add(d * r1 - side);
            for (int k = 0; k < 4; k++) _c.Add(c);
            _t.Add(b); _t.Add(b + 1); _t.Add(b + 2); _t.Add(b); _t.Add(b + 2); _t.Add(b + 3);
        }

        /// <summary>둥근 불빛(가운데 밝고 가장자리 0), 카메라를 본다.</summary>
        private void Glow(Vector3 p, float size, Color c, Vector3 camRight, Vector3 camUp)
        {
            int b = _v.Count;
            _v.Add(p); _c.Add(c);
            const int n = 10;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                _v.Add(p + (camRight * Mathf.Cos(a) + camUp * Mathf.Sin(a)) * size);
                _c.Add(A(c, 0f));
            }
            for (int k = 0; k < n; k++) { _t.Add(b); _t.Add(b + 1 + k); _t.Add(b + 1 + (k + 1) % n); }
        }

        private void Refresh()
        {
            if (_mesh == null) return;
            _v.Clear(); _c.Clear(); _t.Clear();
            if (_mode != Mode.Off)
            {
                float age = Time.time - _start;
                float t = Mathf.Clamp01(age / _length);
                bool warn = _mode == Mode.Warn;
                Color col = warn ? WarnColor : OpenColor;
                float pulse = warn
                    ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(age * Mathf.Lerp(5f, 14f, t)))
                    : (t > 0.75f ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 1f : 0.35f) : 1f);
                float r = _radius * (warn ? Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t * 2.5f)) : 1f);
                // 바탕(안쪽 옅게 → 바깥 조금 진하게)
                Band(2.5f, r, A(col, 0.04f * pulse), A(col, (warn ? 0.22f : 0.16f) * pulse), 0.18f);
                // 테두리
                Band(r - 0.7f, r, A(col, 0.75f * pulse), A(col, 0.95f * pulse), 0.08f);
                if (warn)
                {
                    // 바깥으로 밀려 나가는 줄무늬 2개(쏘는 방향)
                    for (int k = 0; k < 2; k++)
                    {
                        float ph = Mathf.Repeat(age / 0.55f + k * 0.5f, 1f);
                        float rr = Mathf.Lerp(4f, r - 1f, ph);
                        Band(rr - 0.45f, rr, A(col, 0.6f * (1f - ph) * pulse), A(col, 0.6f * (1f - ph) * pulse), 0.15f);
                    }
                }
                else
                {
                    Edge(0f, 2.5f, r, 0.35f, A(col, 0.8f * pulse));
                    Edge(1f, 2.5f, r, 0.35f, A(col, 0.8f * pulse));
                }
                // 발사관 불빛
                var cam = Camera.main;
                Vector3 camRight = cam != null ? transform.InverseTransformDirection(cam.transform.right) : Vector3.right;
                Vector3 camUp = cam != null ? transform.InverseTransformDirection(cam.transform.up) : Vector3.up;
                float gs = warn ? Mathf.Lerp(0.45f, 0.95f, t) * (0.85f + 0.15f * pulse) : Mathf.Lerp(0.8f, 0.35f, t);
                Color gc = warn ? new Color(1f, 0.25f, 0.1f, Mathf.Lerp(0.6f, 1f, t) * pulse) : new Color(1f, 0.62f, 0.2f, Mathf.Lerp(0.95f, 0.25f, t));
                foreach (var s in _sockets)
                    if (s != null) Glow(transform.InverseTransformPoint(s.position) + Vector3.up * 0.15f, gs, gc, camRight, camUp);
            }
            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
        }

        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
