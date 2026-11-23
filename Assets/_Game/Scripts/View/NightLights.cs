using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 밤 조명 묶음(2026-11-19 낮과 밤). 한 물체에 붙는 작은 등불 여럿(빛나는 구 — HDR 색이라 블룸으로 번진다)을 어두울 때만 켠다.
    ///   - 깜박임: period &gt; 0이면 그 주기에서 onFraction만큼만 켠다(부표 · 조난 신호).
    ///   - 모양: 기본은 구. 배 항해등은 횃불 모양(2026-11-21 사용자: 둥글지 않게, 블록 게임 횃불 느낌의 직사각형 육면체) —
    ///     빛나는 네모 머리(0.5 × 0.6 × 0.5) 아래 가는 네모 막대(0.24 × 0.9 × 0.24, 어두운 나무색).
    ///   - 광원: lightRange &gt; 0인 등은 실제 점광원을 받는다(NightLampLights — 카메라에서 가까운 24개만, 그림자 없음).
    /// </summary>
    public sealed class NightLampSet : MonoBehaviour
    {
        public enum Shape { Sphere, Torch }

        private struct Lamp { public Renderer R, Stick; public Color C; public float Period, On, Phase; }
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Material s_material;
        private static Mesh s_sphere, s_cube;
        private static readonly Color StickColor = new(0.28f, 0.18f, 0.09f);
        private readonly List<Lamp> _lamps = new();
        private MaterialPropertyBlock _mpb;
        private bool _shown = true;

        public int Count => _lamps.Count;
        /// <summary>검증용: 지금 켜진 등불 수.</summary>
        public int Lit { get; private set; }

        public static NightLampSet On(GameObject go) => go.TryGetComponent<NightLampSet>(out var s) ? s : go.AddComponent<NightLampSet>();

        /// <summary>local 자리에 등불 하나(color는 HDR 배율 전 색).</summary>
        public void Add(Vector3 local, Color color, float size, float period = 0f, float onFraction = 0.5f, float phase = 0f)
            => Add(local, color, size, Shape.Sphere, 0f, period, onFraction, phase);

        /// <summary>모양 · 광원 거리를 정해 등불 하나. Torch면 local이 빛나는 머리 가운데, 막대는 그 아래로.</summary>
        public void Add(Vector3 local, Color color, float size, Shape shape, float lightRange, float period = 0f, float onFraction = 0.5f, float phase = 0f)
            => Add(local, color, size, shape, lightRange, null, period, onFraction, phase);

        /// <summary>parent(없으면 이 물체) 기준 local 자리에 등불 하나 — 배는 흔들리는 모델에 붙여 함께 움직이게(2026-11-22).</summary>
        public void Add(Vector3 local, Color color, float size, Shape shape, float lightRange, Transform parent, float period = 0f, float onFraction = 0.5f, float phase = 0f)
        {
            if (s_material == null)
            {
                s_material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Night lamp (Runtime)", enableInstancing = true };
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s_sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
                tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s_cube = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
            var go = new GameObject("Night lamp", typeof(MeshFilter), typeof(MeshRenderer));
            var holder = parent != null ? parent : transform;
            go.transform.SetParent(holder, false);
            go.transform.localPosition = local;
            var lossy = holder.lossyScale;
            Vector3 world = shape == Shape.Torch ? new Vector3(0.5f, 0.6f, 0.5f) * size : Vector3.one * size;
            go.transform.localScale = new Vector3(world.x / Mathf.Max(0.01f, lossy.x), world.y / Mathf.Max(0.01f, lossy.y), world.z / Mathf.Max(0.01f, lossy.z));
            go.GetComponent<MeshFilter>().sharedMesh = shape == Shape.Torch ? s_cube : s_sphere;
            var r = go.GetComponent<MeshRenderer>();
            Setup(r, color * 2.6f);

            Renderer stick = null;
            if (shape == Shape.Torch)
            {
                // 막대: 머리 바로 아래(머리 크기 기준 좌표 — 머리 0.5 × 0.6 → 막대 0.24 × 0.9)
                var s = new GameObject("Night lamp", typeof(MeshFilter), typeof(MeshRenderer));   // 이름을 같게 — 피격 번쩍임 · 격침 · 잔상이 등불을 건너뛴다
                s.transform.SetParent(go.transform, false);
                s.transform.localScale = new Vector3(0.24f / 0.5f, 0.9f / 0.6f, 0.24f / 0.5f);
                s.transform.localPosition = new Vector3(0f, -0.5f - 0.5f * (0.9f / 0.6f), 0f);
                s.GetComponent<MeshFilter>().sharedMesh = s_cube;
                stick = s.GetComponent<MeshRenderer>();
                Setup(stick, StickColor);
            }
            _lamps.Add(new Lamp { R = r, Stick = stick, C = color, Period = period, On = onFraction, Phase = phase });
            if (lightRange > 0f) NightLampLights.Register(go.transform, color, lightRange);
            _shown = true;
        }

        private void Setup(Renderer r, Color c)
        {
            r.sharedMaterial = s_material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
            r.enabled = false;
        }

        /// <summary>등불을 모두 지운다(선체가 바뀌어 다시 달 때 — NavLights.Rebuild).</summary>
        public void Clear()
        {
            foreach (var l in _lamps) if (l.R != null) { l.R.enabled = false; if (l.Stick != null) l.Stick.enabled = false; Destroy(l.R.gameObject); }
            _lamps.Clear();
            Lit = 0;
        }

        /// <summary>등불을 모두 끄고 다시 켜지 않는다(격침 등) — Show(true)로 되살린다.</summary>
        public void Show(bool shown) => _shown = shown;

        private void LateUpdate()
        {
            bool night = _shown && DayNight.LightsOn;
            float t = Time.time;
            int lit = 0;
            for (int i = 0; i < _lamps.Count; i++)
            {
                var l = _lamps[i];
                if (l.R == null) continue;
                bool on = night && (l.Period <= 0f || Mathf.Repeat(t / l.Period + l.Phase, 1f) < l.On);
                if (l.R.enabled != on) l.R.enabled = on;
                if (l.Stick != null && l.Stick.enabled != on) l.Stick.enabled = on;
                if (on) lit++;
            }
            Lit = lit;
        }
    }

    /// <summary>
    /// 등불 광원(2026-11-21 사용자: 항해등에 광원 효과). 광원을 받기로 한 등불(항해등) 가운데 켜져 있고 카메라에서 가까운 24개에만
    /// 실제 점광원을 옮겨 단다 — 배 수십 척 × 등 4개에 하나씩 달면 무거우므로. 등 색 그대로, 어두울수록 밝고 횃불처럼 살짝 일렁인다.
    /// </summary>
    public sealed class NightLampLights : MonoBehaviour
    {
        public const int MaxLights = 24;
        private struct Entry { public Transform T; public Renderer R; public Color C; public float Range; }
        private static readonly List<Entry> s_entries = new();
        private static NightLampLights s_instance;
        private readonly Light[] _lights = new Light[MaxLights];
        private readonly List<int> _lit = new();
        private readonly List<float> _dist = new();
        private readonly List<int> _order = new();

        /// <summary>검증용: 지금 켜진 등불 광원 수 · 등록된 등 수.</summary>
        public static int LitCount { get; private set; }
        public static int Registered => s_entries.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_instance = null; s_entries.Clear(); LitCount = 0; }

        public static void Register(Transform lamp, Color color, float range)
        {
            if (lamp == null) return;
            if (s_instance == null) s_instance = new GameObject("Night lamp lights").AddComponent<NightLampLights>();
            s_entries.Add(new Entry { T = lamp, R = lamp.GetComponent<Renderer>(), C = color, Range = range });
        }

        private void Awake()
        {
            for (int i = 0; i < MaxLights; i++)
            {
                var go = new GameObject($"Lamp light {i}");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _lights[i] = l;
            }
        }

        private void OnDestroy() { if (s_instance == this) s_instance = null; }

        private void LateUpdate()
        {
            s_entries.RemoveAll(e => e.T == null);
            _lit.Clear(); _dist.Clear();
            float k = Mathf.InverseLerp(DayNight.LightsOnDarkness, 1f, DayNight.Darkness01);
            var cam = Camera.main;
            Vector3 c = cam != null ? cam.transform.position : Vector3.zero;
            if (k > 0.01f)
                for (int i = 0; i < s_entries.Count; i++)
                {
                    var e = s_entries[i];
                    if (e.R == null || !e.R.enabled || !e.T.gameObject.activeInHierarchy) continue;
                    _lit.Add(i); _dist.Add((e.T.position - c).sqrMagnitude);
                }
            if (_lit.Count > MaxLights)
            {
                // 가까운 순으로 24개
                _order.Clear();
                for (int i = 0; i < _lit.Count; i++) _order.Add(i);
                _order.Sort((a, b) => _dist[a].CompareTo(_dist[b]));
                for (int i = 0; i < MaxLights; i++) _order[i] = _lit[_order[i]];
                _lit.Clear();
                for (int i = 0; i < MaxLights; i++) _lit.Add(_order[i]);
            }
            int n = 0;
            float t = Time.time;
            for (int i = 0; i < MaxLights; i++)
            {
                var l = _lights[i];
                if (i < _lit.Count)
                {
                    var e = s_entries[_lit[i]];
                    l.transform.position = e.T.position + Vector3.up * 0.15f;
                    l.color = e.C;
                    l.range = e.Range;
                    float flicker = 0.9f + 0.1f * Mathf.PerlinNoise(t * 6f, _lit[i] * 0.37f);   // 횃불처럼 살짝 일렁인다
                    l.intensity = 2.4f * k * flicker * Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(2f, 6f, e.Range));
                    if (!l.enabled) l.enabled = true;
                    n++;
                }
                else if (l.enabled) l.enabled = false;
            }
            LitCount = n;
        }
    }

    /// <summary>
    /// 배 항해등(2026-11-19): 좌현 빨강 · 우현 초록 · 돛대 흰색 · 선미 흰색. 선체 메시 범위로 자리를 잡는다.
    /// 2026-11-21: 둥근 구 대신 횃불 모양 직육면체(빛나는 머리 + 막대) · 실제 점광원(카메라에서 가까운 24개).
    /// 적 수상함 · 기함 · 아군 호위함에 붙는다(반잠수정 · 잠수함 · 항공기는 없음). 밤에 어두워도 배 위치와 방향이 보이게.
    /// </summary>
    public static class NavLights
    {
        public static readonly Color Port = new(1f, 0.12f, 0.08f), Starboard = new(0.15f, 1f, 0.3f), Mast = new(1f, 0.95f, 0.85f);
        /// <summary>
        /// 등 머리 크기 기준(m). 2026-11-22 사용자: 배에 비해 너무 크다(0.45 → 25% 수준) — 0.13(머리 약 7cm × 8cm, 막대 포함 높이 약 20cm).
        /// 밤에는 빛나는 색과 실제 점광원으로 읽힌다. 돛대등 0.9배, 선미등 SternRatio.
        /// </summary>
        public const float BulbSize = 0.13f;

        /// <summary>
        /// 선미등 크기(현측등 대비). 2026-11-16 사용자 요청으로 7.5%(그때 현측등 0.45 → 선미등 머리 약 1.7cm)였는데,
        /// 2026-11-22 전체를 0.13으로 줄이자 머리 약 0.5cm가 되어 보이지 않았다 — 0.5배(머리 약 3cm)로 다시 잡는다.
        /// </summary>
        public const float SternRatio = 0.5f;

        public static NightLampSet Ensure(GameObject ship, float scale = 1f)
        {
            if (ship == null) return null;
            if (ship.TryGetComponent<NightLampSet>(out var existing) && existing.Count > 0) return existing;
            var mfs = ship.GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) return null;
            // 선체 범위(배 자기 좌표). 렌더러 bounds 대신 메시 bounds × 변환 행렬 — 꺼진 상태에서 만들어도(아군 호위함) 맞는다
            var inv = ship.transform.worldToLocalMatrix;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var mf in mfs)
            {
                if (mf == null || mf.sharedMesh == null || mf.name == "Night lamp" || !mf.TryGetComponent<MeshRenderer>(out _)) continue;
                var b = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
            }
            if (!float.IsFinite(min.x)) return null;
            // 블록 선체로 만든 배(기함)는 좌우 폭을 선체 조각(갑판 · 뱃전)만으로 — 헬기 날개 · 포신처럼 뱃전 밖으로 나온 것이
            // 현측등을 바다로 밀어냈다(2026-11-21 지휘함)
            var hull = ship.GetComponentInChildren<Game.Ship.ShipHullBuilder>();
            float hullTop = float.NegativeInfinity;
            if (hull != null && hull.HullRoot != null)
            {
                float hxMin = float.PositiveInfinity, hxMax = float.NegativeInfinity;
                foreach (var mf in hull.HullRoot.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf == null || mf.sharedMesh == null || mf.name == "Night lamp") continue;
                    var b = mf.sharedMesh.bounds;
                    var m = inv * mf.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        hxMin = Mathf.Min(hxMin, p.x); hxMax = Mathf.Max(hxMax, p.x); hullTop = Mathf.Max(hullTop, p.y);
                    }
                }
                if (float.IsFinite(hxMin) && hxMax - hxMin > 0.5f) { min.x = hxMin; max.x = hxMax; }
            }
            var set = NightLampSet.On(ship);
            float midZ = (min.z + max.z) * 0.5f, deckY = Mathf.Lerp(min.y, max.y, 0.35f);
            if (hull != null && float.IsFinite(hullTop)) deckY = hullTop;   // 블록 선체: 갑판 · 뱃전 꼭대기
            // 등 크기는 부표 등불(0.45m)과 같게(2026-11-20) — 예전엔 배 길이에 따라 0.55~1.1m라 너무 컸다
            float size = BulbSize * scale;
            // 2026-11-21: 횃불 모양(네모 머리 + 막대) · 실제 광원(현측등 5m · 돛대등 6m · 선미등 2.5m). 머리를 막대 길이만큼 올려 막대가 갑판 위에 선다.
            float lift = size * 0.9f * 0.6f;
            var torch = NightLampSet.Shape.Torch;
            float reach = Mathf.Max(1f, scale);
            // 돛대등: 가장 높은 부품(돛대 · 레이더) 꼭대기에 막대가 닿게 — 예전엔 배 전체 상자 위 0.2m 허공(가운데)이었다
            Vector3 topPoint = new((min.x + max.x) * 0.5f, max.y, Mathf.Lerp(midZ, max.z, 0.35f));
            float bestTop = float.NegativeInfinity;
            foreach (var mf in mfs)
            {
                if (mf == null || mf.sharedMesh == null || mf.name == "Night lamp" || !mf.TryGetComponent<MeshRenderer>(out _)) continue;
                var b = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                float top = float.NegativeInfinity;
                for (int i = 0; i < 8; i++)
                    top = Mathf.Max(top, m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))).y);
                if (top <= bestTop) continue;
                bestTop = top;
                var c = m.MultiplyPoint3x4(b.center);
                topPoint = new Vector3(c.x, top, c.z);
            }

            // 흔들리는 모델(ShipVisualSway)에 붙여 배와 함께 오르내리고 기울게 — 예전엔 배 뿌리에 붙어 모델이 파도에 내려가면 등만 허공에 떴다.
            // 자리는 흔들리지 않은 기본 자세 기준으로 바꾼다
            var sway = ship.GetComponentInChildren<ShipVisualSway>(true);
            Transform anchor = sway != null ? sway.transform : null;
            Matrix4x4 toAnchor = sway != null ? sway.RestLocalToWorld.inverse * ship.transform.localToWorldMatrix : Matrix4x4.identity;
            Vector3 A(Vector3 shipLocal) => anchor != null ? toAnchor.MultiplyPoint3x4(shipLocal) : shipLocal;

            float inset = size * 0.25f;   // 막대가 뱃전 위에 서게(바깥 허공 말고)
            float sideZ = midZ + (max.z - min.z) * 0.1f;
            set.Add(A(new Vector3(min.x + inset, deckY + lift, sideZ)), Port, size, torch, 5f * reach, anchor);
            set.Add(A(new Vector3(max.x - inset, deckY + lift, sideZ)), Starboard, size, torch, 5f * reach, anchor);
            set.Add(A(new Vector3(topPoint.x, topPoint.y + lift * 0.9f, topPoint.z)), Mast, size * 0.9f, torch, 6f * reach, anchor);
            set.Add(A(new Vector3((min.x + max.x) * 0.5f, deckY + lift * SternRatio, min.z + inset)), Mast, size * SternRatio, torch, 2.5f, anchor);
            return set;
        }

        /// <summary>선체가 바뀐 배(기함 — 시작 함정 선택 · 정비)의 항해등을 지우고 새 선체에 맞게 다시 단다.</summary>
        public static NightLampSet Rebuild(GameObject ship, float scale = 1f)
        {
            if (ship == null) return null;
            if (ship.TryGetComponent<NightLampSet>(out var set)) set.Clear();
            return Ensure(ship, scale);
        }

        /// <summary>배 하나의 항해등 수(좌현 · 우현 · 돛대 · 선미) — 검증용.</summary>
        public const int PerShip = 4;
    }

    /// <summary>
    /// 기함 탐조등(2026-11-19): 밤에 함교 높이에서 앞쪽 바다를 비추며 좌우로 천천히 훑는다(실제 스포트라이트 — 배 · 수면을 비춘다).
    /// 빛 원뿔 안의 반잠수정(ILowObservable)은 드러난다.
    /// </summary>
    public sealed class Searchlight : MonoBehaviour
    {
        public const float Range = 38f, HalfAngle = 17f, Sweep = 22f, SweepSeconds = 7f;
        private Light _light;
        private float _scan;

        /// <summary>검증용: 이번 판에 탐조등이 드러낸 횟수.</summary>
        public int Revealed { get; private set; }
        public Light Light => _light;
        public static Searchlight Active { get; private set; }

        public static Searchlight Ensure(GameObject ship)
        {
            if (ship == null) return null;
            var s = ship.GetComponentInChildren<Searchlight>();
            if (s != null) return s;
            var go = new GameObject("Searchlight");
            go.transform.SetParent(ship.transform, false);
            go.transform.localPosition = new Vector3(0f, 4.5f, 2f);
            return go.AddComponent<Searchlight>();
        }

        private void Awake()
        {
            Active = this;
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.range = Range * 1.4f;
            _light.spotAngle = HalfAngle * 2f;
            _light.innerSpotAngle = HalfAngle;
            _light.color = new Color(0.92f, 0.96f, 1f);
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
        }

        private void OnDestroy() { if (Active == this) Active = null; }

        private void Update()
        {
            float dark = DayNight.Darkness01;
            bool on = DayNight.LightsOn;
            if (_light.enabled != on) _light.enabled = on;
            if (!on) return;
            float yaw = Mathf.Sin(Time.time * Mathf.PI * 2f / SweepSeconds) * Sweep;
            transform.localRotation = Quaternion.Euler(14f, yaw, 0f);
            _light.intensity = 900f * Mathf.InverseLerp(DayNight.LightsOnDarkness, 1f, dark);

            _scan -= Time.deltaTime;
            if (_scan > 0f) return;
            _scan = 0.25f;
            Vector3 origin = transform.position; origin.y = 0f;
            Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
            {
                if (t is not ILowObservable low || !t.IsAlive || t.Transform == null) continue;
                Vector3 d = t.Transform.position - origin; d.y = 0f;
                if (d.sqrMagnitude > Range * Range || Vector3.Angle(fwd, d) > HalfAngle) continue;
                if (low.Stealthy) Revealed++;
                low.MarkDetected(1f);
            }
        }
    }
}
