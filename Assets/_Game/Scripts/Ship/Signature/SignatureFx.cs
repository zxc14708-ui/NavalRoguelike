using System.Collections.Generic;
using UnityEngine;

namespace Game.Ship
{
    /// <summary>
    /// 고유 스킬 연출 도구(2026-10-11). 프리팹 없이 런타임에 만든다(가산 투명 = Sprites/Default, 불투명 = URP Lit).
    ///   - Ring: 수면에 번지는 원(레이더 펄스·조기경보 탐색).
    ///   - Marker: 표적 위에 도는 마름모 괄호(표적 지시·조준).
    ///   - Shell: 함선을 감싸는 맥박 치는 막(돌격 기동).
    /// 시간은 게임 시간(일시정지·정비 중에는 멈춘다).
    /// </summary>
    public static class SignatureFx
    {
        private static Material s_additive;
        private static readonly Dictionary<Color, Material> s_lit = new();

        /// <summary>가산 투명(정점 색) — 고리·괄호·막에 쓴다.</summary>
        public static Material Additive
        {
            get
            {
                if (s_additive != null) return s_additive;
                var shader = Shader.Find("Sprites/Default");
                s_additive = new Material(shader) { name = "SignatureFx Additive" };
                return s_additive;
            }
        }

        /// <summary>단색 불투명 재질(항공기 동체 등). 같은 색은 하나를 나눠 쓴다.</summary>
        public static Material Lit(Color color, float smoothness = 0.35f, Color? emission = null)
        {
            var key = emission.HasValue ? new Color(color.r, color.g, color.b, 2f + emission.Value.r) : color;
            if (s_lit.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = "SignatureFx Lit", color = color };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            s_lit[key] = m;
            return m;
        }

        // ------------------------------------------------------------ 고리

        /// <summary>수면(y = 높이)에 번지는 원. count개를 stagger초 간격으로 반지름 0 → radius로.</summary>
        public static void Ring(Vector3 center, float radius, float duration, Color color, float width = 0.6f, int count = 1, float stagger = 0.25f)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("SignatureFx Ring");
                go.transform.position = new Vector3(center.x, 0.25f, center.z);
                var ring = go.AddComponent<FxRing>();
                ring.Begin(radius, duration, color, width, i * stagger);
            }
        }

        /// <summary>표적 위에 도는 마름모 괄호. 표적이 사라지면 함께 없어진다.</summary>
        public static FxMarker Marker(Transform target, float duration, Color color, float size = 3.2f)
        {
            if (target == null) return null;
            var go = new GameObject("SignatureFx Marker");
            var m = go.AddComponent<FxMarker>();
            m.Begin(target, duration, color, size);
            return m;
        }

        /// <summary>함선을 감싸는 막(바운드에 맞춘 납작한 구). duration 동안 맥박 치다 사라진다.</summary>
        public static GameObject Shell(Transform ship, Bounds localBounds, float duration, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SignatureFx Shell";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(ship, false);
            go.transform.localPosition = localBounds.center + Vector3.up * 0.4f;
            go.transform.localScale = new Vector3(localBounds.size.x * 1.35f + 1f, Mathf.Max(2.5f, localBounds.size.y * 1.4f), localBounds.size.z * 1.18f + 1.5f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Additive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pulse = go.AddComponent<FxPulse>();
            pulse.Begin(r, duration, color);
            return go;
        }

        /// <summary>원을 그리는 LineRenderer(정점 색으로 칠함).</summary>
        public static LineRenderer Circle(Transform parent, int segments, float width, Color color)
        {
            var lr = parent.gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.widthMultiplier = width;
            lr.sharedMaterial = Additive;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }
            return lr;
        }
    }

    /// <summary>번지는 고리(수면에 눕힌 원). 가장자리가 밝고 커지며 옅어진다.</summary>
    public class FxRing : MonoBehaviour
    {
        private LineRenderer _lr;
        private float _radius, _duration, _delay, _t, _width;
        private Color _color;

        public void Begin(float radius, float duration, Color color, float width, float delay)
        {
            _radius = radius; _duration = Mathf.Max(0.1f, duration); _color = color; _width = width; _delay = delay;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _lr = SignatureFx.Circle(transform, 96, width, new Color(color.r, color.g, color.b, 0f));
            transform.localScale = Vector3.one * 0.01f;
        }

        private void Update()
        {
            if (_delay > 0f) { _delay -= Time.deltaTime; return; }
            _t += Time.deltaTime / _duration;
            if (_t >= 1f) { Destroy(gameObject); return; }
            float r = Mathf.Lerp(0.5f, _radius, 1f - (1f - _t) * (1f - _t));   // 빠르게 번지다 느려짐
            transform.localScale = Vector3.one * r;
            _lr.widthMultiplier = _width / Mathf.Max(0.01f, r) * Mathf.Lerp(1.4f, 0.6f, _t);   // 축척을 상쇄해 화면 두께를 맞춘다
            var c = _color; c.a = _color.a * (1f - _t);
            _lr.startColor = _lr.endColor = c;
        }
    }

    /// <summary>표적 위에 떠서 도는 마름모 괄호(네 꺾쇠). 끝나기 전 1초는 깜빡인다.</summary>
    public class FxMarker : MonoBehaviour
    {
        private Transform _target;
        private float _until, _size;
        private Color _color;
        private LineRenderer[] _corners;

        public void Begin(Transform target, float duration, Color color, float size)
        {
            _target = target; _until = Time.time + duration; _color = color; _size = size;
            _corners = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var c = new GameObject("Corner").transform;
                c.SetParent(transform, false);
                c.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                var lr = c.gameObject.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 3;
                lr.widthMultiplier = 0.22f;
                lr.sharedMaterial = SignatureFx.Additive;
                lr.startColor = lr.endColor = color;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.alignment = LineAlignment.TransformZ;
                float s = size * 0.5f, k = size * 0.22f;
                lr.SetPositions(new[] { new Vector3(s - k, 0f, 0f), new Vector3(s, 0f, 0f), new Vector3(s, k, 0f) });
                _corners[i] = lr;
            }
        }

        public void Extend(float duration) => _until = Mathf.Max(_until, Time.time + duration);

        private void LateUpdate()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy || Time.time >= _until) { Destroy(gameObject); return; }
            transform.position = _target.position + Vector3.up * 3.2f;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up) *
                                                  Quaternion.Euler(0f, 0f, 45f + Time.time * 90f);
            float left = _until - Time.time;
            float a = left < 1f ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 1f : 0.25f) : 1f;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 8f);
            transform.localScale = Vector3.one * pulse;
            var c = _color; c.a *= a;
            foreach (var lr in _corners) lr.startColor = lr.endColor = c;
        }
    }

    /// <summary>맥박 치는 막: 처음엔 밝게 번쩍, 이후 은은히 깜빡이다 마지막에 사그라든다.</summary>
    public class FxPulse : MonoBehaviour
    {
        private Renderer _r;
        private float _start, _duration;
        private Color _color;
        private MaterialPropertyBlock _mpb;

        public void Begin(Renderer r, float duration, Color color)
        {
            _r = r; _duration = duration; _color = color; _start = Time.time;
            _mpb = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void Update()
        {
            float t = (Time.time - _start) / Mathf.Max(0.1f, _duration);
            if (t >= 1f) { Destroy(gameObject); return; }
            Apply(t);
        }

        private void Apply(float t)
        {
            float flash = Mathf.Clamp01(1f - t * 6f);                          // 시작 번쩍임
            float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);          // 깜빡임
            float fade = t > 0.85f ? (1f - t) / 0.15f : 1f;                    // 끝
            float a = (_color.a * (0.6f + 0.4f * shimmer) + flash * 0.35f) * fade;
            _mpb.SetColor("_Color", new Color(_color.r, _color.g, _color.b, a));
            _r.SetPropertyBlock(_mpb);
        }
    }
}
