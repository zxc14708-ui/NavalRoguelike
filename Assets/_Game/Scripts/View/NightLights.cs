using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 밤 조명 묶음(2026-11-19 낮과 밤). 한 물체에 붙는 작은 등불 여럿(빛나는 구 — HDR 색이라 블룸으로 번진다)을 어두울 때만 켠다.
    ///   - 깜박임: period &gt; 0이면 그 주기에서 onFraction만큼만 켠다(부표 · 조난 신호).
    ///   - 실제 광원은 달지 않는다(가볍게) — 필요한 곳(탐조등 · 등대 · 조명탄)만 따로 단다.
    /// </summary>
    public sealed class NightLampSet : MonoBehaviour
    {
        private struct Lamp { public Renderer R; public Color C; public float Period, On, Phase; }
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Material s_material;
        private static Mesh s_sphere;
        private readonly List<Lamp> _lamps = new();
        private MaterialPropertyBlock _mpb;
        private bool _shown = true;

        public int Count => _lamps.Count;
        /// <summary>검증용: 지금 켜진 등불 수.</summary>
        public int Lit { get; private set; }

        public static NightLampSet On(GameObject go) => go.TryGetComponent<NightLampSet>(out var s) ? s : go.AddComponent<NightLampSet>();

        /// <summary>local 자리에 등불 하나(color는 HDR 배율 전 색).</summary>
        public void Add(Vector3 local, Color color, float size, float period = 0f, float onFraction = 0.5f, float phase = 0f)
        {
            if (s_material == null)
            {
                s_material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Night lamp (Runtime)", enableInstancing = true };
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s_sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
            var go = new GameObject("Night lamp", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var lossy = transform.lossyScale;
            go.transform.localScale = new Vector3(size / Mathf.Max(0.01f, lossy.x), size / Mathf.Max(0.01f, lossy.y), size / Mathf.Max(0.01f, lossy.z));
            go.GetComponent<MeshFilter>().sharedMesh = s_sphere;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = s_material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color * 2.6f);
            r.SetPropertyBlock(_mpb);
            r.enabled = false;
            _lamps.Add(new Lamp { R = r, C = color, Period = period, On = onFraction, Phase = phase });
            _shown = true;
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
                if (on) lit++;
            }
            Lit = lit;
        }
    }

    /// <summary>
    /// 배 항해등(2026-11-19): 좌현 빨강 · 우현 초록 · 돛대 흰색 · 선미 흰색. 선체 메시 범위로 자리를 잡는다.
    /// 적 수상함 · 기함 · 아군 호위함에 붙는다(반잠수정 · 잠수함 · 항공기는 없음). 밤에 어두워도 배 위치와 방향이 보이게.
    /// </summary>
    public static class NavLights
    {
        public static readonly Color Port = new(1f, 0.12f, 0.08f), Starboard = new(0.15f, 1f, 0.3f), Mast = new(1f, 0.95f, 0.85f);
        /// <summary>현측등 지름(m) — 항로 부표 · 등부표 등불과 같다. 돛대등은 0.9배, 선미등은 7.5%.</summary>
        public const float BulbSize = 0.45f;

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
            var set = NightLampSet.On(ship);
            float midZ = (min.z + max.z) * 0.5f, deckY = Mathf.Lerp(min.y, max.y, 0.45f);
            // 등 크기는 부표 등불(0.45m)과 같게(2026-11-20) — 예전엔 배 길이에 따라 0.55~1.1m라 너무 컸다
            float size = BulbSize * scale;
            set.Add(new Vector3(min.x, deckY, midZ + (max.z - min.z) * 0.1f), Port, size);
            set.Add(new Vector3(max.x, deckY, midZ + (max.z - min.z) * 0.1f), Starboard, size);
            set.Add(new Vector3((min.x + max.x) * 0.5f, max.y + 0.2f, Mathf.Lerp(midZ, max.z, 0.35f)), Mast, size * 0.9f);
            set.Add(new Vector3((min.x + max.x) * 0.5f, Mathf.Lerp(min.y, max.y, 0.35f), min.z), Mast, size * 0.075f);
            return set;
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
