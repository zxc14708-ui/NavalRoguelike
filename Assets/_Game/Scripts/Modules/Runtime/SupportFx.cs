using UnityEngine;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 지원 블록 연출 도구(2026-11-22). 프리팹 없이 런타임에 만든다.
    ///   - Tracer: 짧은 예광선 · 낙하 포탄 줄기.
    ///   - WarnRing: 수면의 경고 원(포격 지원 낙탄 지역).
    ///   - 임시 모델(사용자: 임시 모델 OK): 보급선 Lv1 · 2 · 3, 고속정, 정찰 드론 — 기본 도형을 조합한다. 정식 모델이 오면 바꾼다.
    /// </summary>
    public static class SupportFx
    {
        private static Material s_line;

        private static Material LineMaterial
        {
            get
            {
                if (s_line != null) return s_line;
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                s_line = new Material(shader) { name = "SupportFx line (Runtime)" };
                return s_line;
            }
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color, float life, float width)
        {
            var go = new GameObject("SupportFx tracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = LineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.AddComponent<FadeLine>().Begin(line, color, life);
        }

        /// <summary>수면 경고 원: 바깥 원은 그대로, 안쪽 원이 duration 동안 바깥까지 차오른다(낙탄 시각). 끝나면 사라진다.</summary>
        public static GameObject WarnRing(Vector3 center, float radius, float duration, Color color)
        {
            var go = new GameObject("SupportFx warn ring");
            go.transform.position = new Vector3(center.x, 0.22f, center.z);
            go.AddComponent<WarnRingFx>().Begin(radius, duration, color);
            return go;
        }

        public static LineRenderer Circle(Transform parent, string name, float width, Color color, int segments = 48)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = LineMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            return line;
        }

        // ------------------------------------------------------------ 임시 모델

        private static readonly Color HullGrey = new(0.42f, 0.47f, 0.5f);
        private static readonly Color DeckGrey = new(0.28f, 0.32f, 0.34f);
        private static readonly Color Dark = new(0.12f, 0.15f, 0.17f);
        private static readonly Color White = new(0.82f, 0.86f, 0.86f);
        private static readonly Color Supply = new(0.86f, 0.62f, 0.18f);   // 보급선 표시(주황 띠)
        private static readonly Color Friendly = new(0.3f, 0.85f, 0.95f);   // 아군 표시(청록 띠)

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, Vector3? euler = null, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = SignatureFx.Lit(color);
            return go.transform;
        }

        /// <summary>선체: 상자 + 뾰족한 선수(45° 돌린 상자). length · beam은 m.</summary>
        private static void Hull(Transform root, float length, float beam, float freeboard, Color stripe)
        {
            Part(PrimitiveType.Cube, root, new Vector3(0f, freeboard * 0.5f - 0.35f, -length * 0.06f), new Vector3(beam, freeboard, length * 0.82f), HullGrey, null, "Hull");
            float bow = beam * 0.7071f;
            Part(PrimitiveType.Cube, root, new Vector3(0f, freeboard * 0.5f - 0.35f, length * 0.35f), new Vector3(bow, freeboard * 0.98f, bow), HullGrey, new Vector3(0f, 45f, 0f), "Bow");
            Part(PrimitiveType.Cube, root, new Vector3(0f, freeboard - 0.33f, -length * 0.06f), new Vector3(beam * 0.96f, 0.06f, length * 0.8f), DeckGrey, null, "Deck");
            Part(PrimitiveType.Cube, root, new Vector3(0f, freeboard * 0.55f - 0.35f, -length * 0.06f), new Vector3(beam * 1.01f, 0.12f, length * 0.7f), stripe, null, "Stripe");
        }

        /// <summary>
        /// 보급선(전투단 통신 중계가 부른다). Lv1 = 소형 군수 지원정(약 11m · 화물 갑판 · 기중기 1),
        /// Lv2 = 중형 보급함(약 15m · 함교 · 보급 기둥 1쌍), Lv3 = 대형 군수 지원함(약 19m · 보급 기둥 2쌍 · 헬기 갑판 · 연돌).
        /// 우현(+x)에 보급 호스가 나가는 소켓 "TransferPoint".
        /// </summary>
        public static Transform BuildSupplyShip(Transform parent, int level)
        {
            var root = new GameObject($"Supply ship Lv{level}").transform;
            root.SetParent(parent, false);
            level = Mathf.Clamp(level, 1, 3);
            float length = level == 1 ? 11f : level == 2 ? 15f : 19f;
            float beam = level == 1 ? 3.2f : level == 2 ? 4.0f : 4.8f;
            float fb = level == 1 ? 1.3f : 1.6f;
            Hull(root, length, beam, fb, Supply);
            float deck = fb - 0.3f;

            // 함교(뒤쪽)
            float bridgeZ = -length * 0.32f;
            Part(PrimitiveType.Cube, root, new Vector3(0f, deck + 0.75f, bridgeZ), new Vector3(beam * 0.8f, 1.5f, length * 0.16f), White, null, "Bridge");
            Part(PrimitiveType.Cube, root, new Vector3(0f, deck + 1.35f, bridgeZ + length * 0.08f + 0.02f), new Vector3(beam * 0.7f, 0.22f, 0.05f), Dark, null, "Windows");
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, deck + 2.3f, bridgeZ), new Vector3(0.12f, 0.8f, 0.12f), Dark, null, "Mast");

            if (level == 1)
            {
                // 화물 갑판 + 기중기
                for (int i = 0; i < 3; i++)
                    Part(PrimitiveType.Cube, root, new Vector3((i - 1) * beam * 0.26f, deck + 0.3f, length * 0.12f), new Vector3(beam * 0.22f, 0.6f, length * 0.2f), i == 1 ? Supply : DeckGrey, null, "Cargo");
                Part(PrimitiveType.Cylinder, root, new Vector3(beam * 0.3f, deck + 0.9f, -length * 0.1f), new Vector3(0.14f, 0.9f, 0.14f), Dark, null, "Crane post");
                Part(PrimitiveType.Cube, root, new Vector3(beam * 0.3f, deck + 1.7f, length * 0.02f), new Vector3(0.1f, 0.1f, length * 0.28f), Dark, new Vector3(-18f, 0f, 0f), "Crane jib");
            }
            else
            {
                // 보급 기둥(킹포스트)과 가로대 — Lv2 1쌍, Lv3 2쌍
                int pairs = level == 2 ? 1 : 2;
                for (int p = 0; p < pairs; p++)
                {
                    float z = length * (pairs == 1 ? 0.08f : (p == 0 ? 0.2f : -0.02f));
                    for (int s = -1; s <= 1; s += 2)
                        Part(PrimitiveType.Cylinder, root, new Vector3(s * beam * 0.3f, deck + 1.4f, z), new Vector3(0.18f, 1.4f, 0.18f), White, null, "Kingpost");
                    Part(PrimitiveType.Cube, root, new Vector3(0f, deck + 2.7f, z), new Vector3(beam * 0.9f, 0.14f, 0.14f), Dark, null, "Crossbar");
                    Part(PrimitiveType.Cylinder, root, new Vector3(beam * 0.48f, deck + 2.0f, z), new Vector3(0.08f, 0.7f, 0.08f), Supply, new Vector3(0f, 0f, 35f), "Hose boom");
                }
                // 화물 · 연료 탱크
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, deck + 0.45f, length * (level == 2 ? 0.25f : 0.33f)), new Vector3(beam * 0.5f, length * 0.08f, beam * 0.5f), DeckGrey, new Vector3(90f, 0f, 0f), "Tank");
                if (level == 3)
                {
                    Part(PrimitiveType.Cube, root, new Vector3(0f, deck + 0.08f, -length * 0.44f), new Vector3(beam * 0.9f, 0.08f, length * 0.12f), DeckGrey, null, "Helideck");
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, deck + 0.13f, -length * 0.44f), new Vector3(beam * 0.5f, 0.01f, beam * 0.5f), White, null, "H ring");
                    Part(PrimitiveType.Cube, root, new Vector3(0f, deck + 2.0f, bridgeZ + length * 0.12f), new Vector3(beam * 0.35f, 1.2f, length * 0.06f), Dark, null, "Funnel");
                }
            }
            var tp = new GameObject("TransferPoint").transform;
            tp.SetParent(root, false);
            tp.localPosition = new Vector3(-beam * 0.5f, deck + 1.6f, length * 0.08f);   // 기함을 오른쪽(+x)에 두고 왼현으로 보급
            return root;
        }

        /// <summary>고속정(약 6m): 낮은 선체 · 조타실 · 앞 갑판 기관포. 포구 소켓 "Muzzle".</summary>
        public static Transform BuildFastBoat(Transform parent)
        {
            var root = new GameObject("Fast boat").transform;
            root.SetParent(parent, false);
            Hull(root, 6.2f, 1.9f, 0.9f, Friendly);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.95f, -0.7f), new Vector3(1.3f, 0.75f, 1.6f), White, null, "Wheelhouse");
            Part(PrimitiveType.Cube, root, new Vector3(0f, 1.15f, 0.12f), new Vector3(1.1f, 0.22f, 0.04f), Dark, null, "Windows");
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.65f, -0.8f), new Vector3(0.06f, 0.35f, 0.06f), Dark, null, "Whip");
            var mount = Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.72f, 1.35f), new Vector3(0.5f, 0.12f, 0.5f), Dark, null, "Gun mount");
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.85f, 1.85f), new Vector3(0.07f, 0.45f, 0.07f), Dark, new Vector3(90f, 0f, 0f), "Barrel");
            var mz = new GameObject("Muzzle").transform;
            mz.SetParent(root, false);
            mz.localPosition = new Vector3(0f, 0.85f, 2.3f);
            return root;
        }

        /// <summary>정찰 드론(날개 폭 약 2m 고정익 무인기).</summary>
        public static Transform BuildDrone(Transform parent)
        {
            var root = new GameObject("Recon drone").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * 1.5f;   // 쿼터뷰에서 보이게(2026-11-22)
            Part(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.28f, 0.6f, 0.28f), HullGrey, new Vector3(90f, 0f, 0f), "Fuselage");
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.04f, 0.1f), new Vector3(2.1f, 0.04f, 0.32f), DeckGrey, null, "Wing");
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.04f, -0.55f), new Vector3(0.7f, 0.03f, 0.2f), DeckGrey, null, "Tailplane");
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.18f, -0.55f), new Vector3(0.03f, 0.28f, 0.2f), DeckGrey, null, "Fin");
            Part(PrimitiveType.Sphere, root, new Vector3(0f, -0.12f, 0.32f), new Vector3(0.16f, 0.16f, 0.16f), Dark, null, "Sensor ball");
            var lamp = Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.16f, 0f), new Vector3(0.08f, 0.08f, 0.08f), new Color(1f, 0.2f, 0.15f), null, "Beacon");
            lamp.GetComponent<Renderer>().sharedMaterial = SignatureFx.Lit(new Color(1f, 0.2f, 0.15f), 0.3f, new Color(3f, 0.4f, 0.3f));
            return root;
        }

        // ------------------------------------------------------------ 내부 연출

        private sealed class FadeLine : MonoBehaviour
        {
            private LineRenderer _line;
            private Color _color;
            private float _life, _age;

            public void Begin(LineRenderer line, Color color, float life)
            {
                _line = line; _color = color; _life = Mathf.Max(0.02f, life);
                _line.startColor = _line.endColor = color;
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float k = 1f - _age / _life;
                if (k <= 0f) { Destroy(gameObject); return; }
                var c = _color; c.a *= k;
                _line.startColor = c;
                _line.endColor = new Color(c.r, c.g, c.b, c.a * 0.4f);
            }
        }

        /// <summary>
        /// 포격 경고(2026-11-22 다듬음 — 넓은 화면에서 작게 보이던 것): 굵은 바깥 원이 1.6배에서 조여 들어와 자리 잡고,
        /// 안이 반투명하게 차오르며(낙탄 시각) 가운데 십자선과 하늘로 솟은 빛기둥이 멀리서도 위치를 알린다.
        /// </summary>
        private sealed class WarnRingFx : MonoBehaviour
        {
            private LineRenderer _outer, _inner, _beam;
            private readonly LineRenderer[] _ticks = new LineRenderer[4];
            private Renderer _disc;
            private MaterialPropertyBlock _mpb;
            private float _radius, _duration, _age;
            private Color _color;

            public void Begin(float radius, float duration, Color color)
            {
                _radius = radius; _duration = Mathf.Max(0.1f, duration); _color = color;
                _outer = Circle(transform, "Outer", 0.75f, color, 64);
                _inner = Circle(transform, "Inner", 0.4f, color, 64);
                _outer.transform.localScale = Vector3.one * radius * 1.6f;
                _inner.transform.localScale = Vector3.one * 0.1f;

                // 반투명 원판(가산)
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Fill";
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(transform, false);
                disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
                _disc = disc.GetComponent<Renderer>();
                _disc.sharedMaterial = SignatureFx.Additive;
                _disc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _disc.receiveShadows = false;
                _mpb = new MaterialPropertyBlock();

                // 십자선 4개(원 안쪽 0.35~0.95)
                for (int k = 0; k < 4; k++)
                {
                    var go = new GameObject("Tick");
                    go.transform.SetParent(transform, false);
                    var l = go.AddComponent<LineRenderer>();
                    l.sharedMaterial = LineMaterial; l.useWorldSpace = false; l.positionCount = 2; l.widthMultiplier = 0.45f;
                    l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Vector3 d = Quaternion.Euler(0f, k * 90f + 45f, 0f) * Vector3.forward;
                    l.SetPosition(0, d * radius * 0.35f); l.SetPosition(1, d * radius * 0.95f);
                    _ticks[k] = l;
                }

                // 빛기둥(가운데에서 하늘로)
                var bgo = new GameObject("Beam");
                bgo.transform.SetParent(transform, false);
                _beam = bgo.AddComponent<LineRenderer>();
                _beam.sharedMaterial = LineMaterial; _beam.useWorldSpace = false; _beam.positionCount = 2;
                _beam.SetPosition(0, Vector3.zero); _beam.SetPosition(1, Vector3.up * 28f);
                _beam.startWidth = 1.2f; _beam.endWidth = 0.15f;
                _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float t = _age / _duration;
                if (t >= 1.15f) { Destroy(gameObject); return; }
                float k = Mathf.Clamp01(t);
                float lockIn = Mathf.Clamp01(_age / 0.35f);
                _outer.transform.localScale = Vector3.one * _radius * Mathf.Lerp(1.6f, 1f, 1f - (1f - lockIn) * (1f - lockIn));
                _inner.transform.localScale = Vector3.one * Mathf.Max(0.1f, _radius * k);
                float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_age * Mathf.Lerp(6f, 16f, k)));
                float fade = t > 1f ? (1.15f - t) / 0.15f : 1f;
                var c = _color; c.a *= blink * fade;
                _outer.startColor = _outer.endColor = c;
                _inner.startColor = _inner.endColor = new Color(c.r, c.g, c.b, c.a * 0.75f);
                foreach (var l in _ticks) l.startColor = l.endColor = new Color(c.r, c.g, c.b, c.a * 0.85f);
                _beam.startColor = new Color(c.r, c.g, c.b, 0.65f * fade * blink);
                _beam.endColor = new Color(c.r, c.g, c.b, 0f);
                _mpb.SetColor("_Color", new Color(_color.r, _color.g, _color.b, (0.08f + 0.22f * k) * fade * (0.7f + 0.3f * blink)));
                _disc.SetPropertyBlock(_mpb);
            }
        }
    }
}
