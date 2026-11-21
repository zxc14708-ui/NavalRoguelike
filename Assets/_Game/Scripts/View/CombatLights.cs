using System.Collections.Generic;
using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 전투 불빛(2026-11-21 사용자: 야간 전투가 잘 보이게 포구 화염 · 로켓 추진에 광원).
    ///   - 포구 불빛: 쏠 때마다 짧은 점광원(0.06~0.14초). 구경이 클수록 넓고 밝다. 같은 자리에서 연사하면(CIWS · 기관포) 새로 켜지 않고 켜진 불을 다시 밝힌다.
    ///     20개를 돌려 쓴다.
    ///   - 추진 불빛: 날아가는 미사일 · 로켓 꼬리에 깜박이는 주황 점광원. 카메라에서 가까운 12발만(멀리 있는 것은 불빛 없이 불꽃만).
    ///   - 세기: 밤일수록 세다(낮에는 30%) — 낮 화면은 그대로, 밤에는 배 · 수면이 번쩍이며 전투가 보인다. 그림자 없음.
    ///   - 예광탄 불빛(2026-11-21 사용자): 꼬리가 빛나는 탄(기관포 · CIWS 예광탄, 적 포탄 꼬리)에 작은 점광원 — 탄 색 그대로, 카메라에서 가까운 12발만.
    ///     기관포 · CIWS 예광탄이 먼저(거리를 절반으로 쳐서 고른다). 날아가는 탄 아래 수면 · 배가 주황으로 스친다.
    ///   - 설정 "번쩍임 줄이기"를 따른다.
    /// </summary>
    public sealed class CombatLights : MonoBehaviour
    {
        public const int MuzzleLights = 20, ExhaustLights = 12, TracerLights = 12;
        public static readonly Color MuzzleColor = new(1f, 0.72f, 0.38f), ExhaustColor = new(1f, 0.58f, 0.24f);

        private static CombatLights s_instance;
        private readonly Light[] _muzzle = new Light[MuzzleLights];
        private readonly float[] _muzzleT = new float[MuzzleLights], _muzzleLen = new float[MuzzleLights], _muzzlePeak = new float[MuzzleLights];
        private int _next;
        private readonly Light[] _exhaust = new Light[ExhaustLights];
        private static readonly List<Transform> s_emitters = new();
        private readonly List<Transform> _sorted = new();
        private struct Tracer { public Transform T; public Color C; public float Strength; }
        private static readonly List<Tracer> s_tracers = new();
        private readonly Light[] _tracer = new Light[TracerLights];
        private readonly List<int> _tracerOrder = new();
        private readonly List<float> _tracerKey = new();

        /// <summary>검증용: 지금 켜진 포구 · 추진 불빛 수.</summary>
        public static int LitMuzzle { get; private set; }
        public static int LitExhaust { get; private set; }
        public static int Emitters => s_emitters.Count;
        /// <summary>검증용: 지금 켜진 예광탄 불빛 수 · 빛을 받을 수 있는 탄 수.</summary>
        public static int LitTracer { get; private set; }
        public static int Tracers => s_tracers.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_instance = null; s_emitters.Clear(); s_tracers.Clear(); LitMuzzle = LitExhaust = LitTracer = 0; }

        /// <summary>밤일수록 1에 가깝다(낮 0.3).</summary>
        public static float NightBoost => Mathf.Lerp(0.3f, 1f, DayNight.Darkness01);

        private static CombatLights Instance
        {
            get
            {
                if (s_instance != null) return s_instance;
                var go = new GameObject("Combat lights");
                s_instance = go.AddComponent<CombatLights>();
                return s_instance;
            }
        }

        private void Awake()
        {
            for (int i = 0; i < MuzzleLights; i++) _muzzle[i] = Make($"Muzzle light {i}", MuzzleColor);
            for (int i = 0; i < ExhaustLights; i++) _exhaust[i] = Make($"Exhaust light {i}", ExhaustColor);
            for (int i = 0; i < TracerLights; i++) _tracer[i] = Make($"Tracer light {i}", ExhaustColor);
        }

        private Light Make(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            l.enabled = false;
            return l;
        }

        /// <summary>포구 불빛. size = 포구 화염 크기(기관포 약 0.5 · 76mm 약 1.2 · 중포 2).</summary>
        public static void Muzzle(Vector3 position, float size)
        {
            float boost = NightBoost * Game.Core.DisplaySettings.FlashScale;
            if (boost <= 0.01f) return;
            var me = Instance;
            float peak = Mathf.Lerp(2.5f, 9f, Mathf.InverseLerp(0.3f, 2f, size)) * boost;
            float range = Mathf.Lerp(4f, 16f, Mathf.InverseLerp(0.3f, 2f, size));
            float len = Mathf.Lerp(0.06f, 0.14f, Mathf.InverseLerp(0.3f, 2f, size));
            // 같은 자리에서 연사 중이면 켜진 불을 다시 밝힌다
            for (int i = 0; i < MuzzleLights; i++)
            {
                var l = me._muzzle[i];
                if (!l.enabled || (l.transform.position - position).sqrMagnitude > 2.5f * 2.5f) continue;
                me._muzzleT[i] = 0f;
                me._muzzlePeak[i] = Mathf.Max(me._muzzlePeak[i], peak);
                me._muzzleLen[i] = Mathf.Max(me._muzzleLen[i], len);
                return;
            }
            int k = me._next;
            me._next = (me._next + 1) % MuzzleLights;
            var light = me._muzzle[k];
            light.transform.position = position;
            light.range = range;
            light.intensity = peak;
            light.enabled = true;
            me._muzzleT[k] = 0f;
            me._muzzleLen[k] = len;
            me._muzzlePeak[k] = peak;
        }

        /// <summary>추진 불빛을 받을 꼬리(미사일 · 로켓). 꺼질 때 Unregister.</summary>
        public static void Register(Transform exhaust)
        {
            if (exhaust == null) return;
            _ = Instance;
            if (!s_emitters.Contains(exhaust)) s_emitters.Add(exhaust);
        }

        public static void Unregister(Transform exhaust) => s_emitters.Remove(exhaust);

        /// <summary>예광탄 불빛을 받을 탄(꼬리 색 · 세기 — 예광탄 1, 보통 꼬리 0.6). 같은 탄을 다시 부르면 색 · 세기만 바꾼다. 꺼지면 UnregisterTracer.</summary>
        public static void RegisterTracer(Transform round, Color color, float strength)
        {
            if (round == null) return;
            _ = Instance;
            color.a = 1f;
            for (int i = 0; i < s_tracers.Count; i++)
                if (s_tracers[i].T == round) { s_tracers[i] = new Tracer { T = round, C = color, Strength = strength }; return; }
            s_tracers.Add(new Tracer { T = round, C = color, Strength = strength });
        }

        public static void UnregisterTracer(Transform round)
        {
            for (int i = s_tracers.Count - 1; i >= 0; i--) if (s_tracers[i].T == round) s_tracers.RemoveAt(i);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            int lit = 0;
            for (int i = 0; i < MuzzleLights; i++)
            {
                var l = _muzzle[i];
                if (!l.enabled) continue;
                _muzzleT[i] += dt;
                float u = _muzzleT[i] / Mathf.Max(0.01f, _muzzleLen[i]);
                if (u >= 1f) { l.enabled = false; continue; }
                l.intensity = _muzzlePeak[i] * (1f - u) * (1f - u);
                lit++;
            }
            LitMuzzle = lit;

            // 추진 불빛: 카메라에서 가까운 꼬리 12개
            s_emitters.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);
            _sorted.Clear();
            _sorted.AddRange(s_emitters);
            var cam = Camera.main;
            if (cam != null && _sorted.Count > ExhaustLights)
            {
                Vector3 c = cam.transform.position;
                _sorted.Sort((a, b) => (a.position - c).sqrMagnitude.CompareTo((b.position - c).sqrMagnitude));
            }
            float boost = NightBoost * Game.Core.DisplaySettings.FlashScale;
            int ex = 0;
            for (int i = 0; i < ExhaustLights; i++)
            {
                var l = _exhaust[i];
                if (i < _sorted.Count && boost > 0.01f)
                {
                    l.transform.position = _sorted[i].position;
                    float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 18f, i * 3.1f) + 0.15f * Mathf.Sin(Time.time * 41f + i);
                    l.intensity = 3.2f * boost * flicker;
                    l.range = 7f;
                    l.enabled = true;
                    ex++;
                }
                else if (l.enabled) l.enabled = false;
            }
            LitExhaust = ex;

            // 예광탄 불빛: 가까운 12발(예광탄은 거리를 절반으로 쳐서 먼저)
            s_tracers.RemoveAll(t => t.T == null || !t.T.gameObject.activeInHierarchy);
            _tracerOrder.Clear(); _tracerKey.Clear();
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            for (int i = 0; i < s_tracers.Count; i++)
            {
                _tracerOrder.Add(i);
                _tracerKey.Add((s_tracers[i].T.position - cp).sqrMagnitude * (s_tracers[i].Strength >= 1f ? 0.25f : 1f));
            }
            if (_tracerOrder.Count > TracerLights) _tracerOrder.Sort((a, b) => _tracerKey[a].CompareTo(_tracerKey[b]));
            int tl = 0;
            for (int i = 0; i < TracerLights; i++)
            {
                var l = _tracer[i];
                if (i < _tracerOrder.Count && boost > 0.01f)
                {
                    var t = s_tracers[_tracerOrder[i]];
                    l.transform.position = t.T.position;
                    l.color = t.C;
                    l.intensity = 2.6f * t.Strength * boost;
                    l.range = Mathf.Lerp(3f, 5f, t.Strength);
                    l.enabled = true;
                    tl++;
                }
                else if (l.enabled) l.enabled = false;
            }
            LitTracer = tl;
        }
    }
}
