using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 폭발 연출(판정 없음). 미사일 명중·요격, 자폭 보트·자폭 드론의 자폭과 격추에 쓴다.
    ///   섬광 → 불덩이(바깥으로 퍼짐) → 불똥(포물선으로 떨어짐) → 검은 연기(위로 번짐) + 짧은 주황 조명.
    ///   수면 가까이서 터지면 흰 물기둥과 물보라가 함께 솟는다. 공중이면 연기가 적고 불똥이 사방으로.
    /// 파티클은 DecorFx 공용 시스템을 쓰고, 조명은 4개를 돌려 쓴다.
    /// </summary>
    public static class Explosions
    {
        public enum Kind
        {
            /// <summary>함체·표적 명중</summary>
            Impact,
            /// <summary>공중 요격·격추</summary>
            Air,
            /// <summary>수면 명중(빗나감·추락)</summary>
            Water,
            /// <summary>자폭(폭약) — 크고 물기둥이 높다</summary>
            Charge,
        }

        private const float WaterY = -0.9f;
        private const int MaxLights = 4;

        /// <summary>검증용: 지금까지 만든 폭발 수.</summary>
        public static int SpawnCount { get; private set; }

        private static readonly List<Light> s_lights = new();
        private static int s_nextLight;
        private static FlashDriver s_driver;

        /// <summary>폭발 하나. size 1 ≈ 대함미사일 명중(불덩이 지름 약 4m).</summary>
        public static void Spawn(Vector3 position, float size, Kind kind)
        {
            SpawnCount++;
            size = Mathf.Max(0.2f, size);
            bool nearWater = kind == Kind.Water || kind == Kind.Charge || position.y < WaterY + 2.5f && kind != Kind.Air;

            // 섬광
            DecorFx.Emit(DecorFx.Flash, position, Vector3.zero, 0.14f, 4.2f * size, new Color(1f, 0.95f, 0.8f, 0.95f));
            DecorFx.Emit(DecorFx.Flash, position, Vector3.zero, 0.22f, 2.6f * size, new Color(1f, 0.72f, 0.3f, 0.9f));

            // 불덩이
            int fire = Mathf.RoundToInt(10 + 8 * size);
            for (int i = 0; i < fire; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (kind != Kind.Air) dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                DecorFx.Emit(DecorFx.Fire, position + dir * 0.3f * size, dir * Random.Range(2.5f, 6f) * size,
                    Random.Range(0.3f, 0.65f), Random.Range(1.1f, 2.3f) * size,
                    Color.Lerp(new Color(1f, 0.9f, 0.55f, 1f), new Color(1f, 0.36f, 0.08f, 0.9f), Random.value));
            }

            // 불똥
            int sparks = Mathf.RoundToInt(12 + 10 * size);
            for (int i = 0; i < sparks; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (kind != Kind.Air) dir.y = Mathf.Abs(dir.y) + 0.35f;
                DecorFx.Emit(DecorFx.Sparks, position, dir.normalized * Random.Range(7f, 15f) * Mathf.Sqrt(size),
                    Random.Range(0.45f, 1f), Random.Range(0.14f, 0.3f) * Mathf.Sqrt(size),
                    new Color(1f, Random.Range(0.55f, 0.85f), 0.25f, 1f));
            }

            // 검은 연기(공중은 적게)
            int smoke = Mathf.RoundToInt((kind == Kind.Air ? 5 : 9) + 5 * size);
            for (int i = 0; i < smoke; i++)
            {
                Vector3 dir = Random.insideUnitSphere;
                Vector3 v = dir * 1.6f * size + Vector3.up * Random.Range(1.2f, 2.6f) * Mathf.Sqrt(size) + DecorFx.Wind * 0.5f;
                float g = Random.Range(0.1f, 0.22f);
                DecorFx.Emit(DecorFx.WreckSmoke, position + dir * 0.6f * size, v, Random.Range(1.8f, 3.2f),
                    Random.Range(1.2f, 2.2f) * size, new Color(g, g, g, kind == Kind.Air ? 0.45f : 0.6f));
            }

            // 물기둥·물보라
            if (nearWater)
            {
                Vector3 at = new(position.x, WaterY + 0.2f, position.z);
                float column = kind == Kind.Charge ? 1.5f : kind == Kind.Water ? 1.1f : 0.7f;
                int n = Mathf.RoundToInt((14 + 10 * size) * column);
                for (int i = 0; i < n; i++)
                {
                    Vector2 r = Random.insideUnitCircle;
                    Vector3 v = new Vector3(r.x * 2.2f, Random.Range(6f, 12f) * column, r.y * 2.2f) * Mathf.Sqrt(size);
                    DecorFx.Emit(DecorFx.Spray, at + new Vector3(r.x, 0f, r.y) * 0.8f * size, v, Random.Range(0.9f, 1.5f),
                        Random.Range(1.0f, 2.0f) * size, new Color(0.94f, 0.97f, 1f, Random.Range(0.55f, 0.85f)));
                }
                for (int i = 0; i < n / 2; i++)
                {
                    Vector2 r = Random.insideUnitCircle.normalized;
                    Vector3 v = new Vector3(r.x * Random.Range(3f, 6f), Random.Range(1.5f, 3f), r.y * Random.Range(3f, 6f)) * Mathf.Sqrt(size);
                    DecorFx.Emit(DecorFx.Spray, at, v, Random.Range(0.6f, 1f), Random.Range(0.8f, 1.5f) * size,
                        new Color(0.94f, 0.97f, 1f, 0.6f));
                }
                if (kind == Kind.Charge || kind == Kind.Water && size >= 1f) OceanSurface.SpawnSinkingTrace(at);
            }

            FlashLight(position + Vector3.up * 0.5f, size);
        }

        // ------------------------------------------------------------ 조명

        /// <summary>짧은 점광(0.35초에 꺼짐). 폭발 말고도 섬광 연출(기만체 발사·폭발)에 쓴다. 4개를 돌려 쓴다.</summary>
        public static void FlashLight(Vector3 position, float size, Color color)
        {
            FlashLight(position, size);
            var used = s_lights[(s_nextLight + s_lights.Count - 1) % s_lights.Count];
            used.color = color;
        }

        private static void FlashLight(Vector3 position, float size)
        {
            if (s_driver == null)
            {
                s_lights.Clear();
                var root = new GameObject("Explosion lights");
                s_driver = root.AddComponent<FlashDriver>();
                for (int i = 0; i < MaxLights; i++)
                {
                    var go = new GameObject($"Blast light {i}");
                    go.transform.SetParent(root.transform, false);
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.62f, 0.28f);
                    l.shadows = LightShadows.None;
                    l.enabled = false;
                    s_lights.Add(l);
                }
            }
            var light = s_lights[s_nextLight];
            s_nextLight = (s_nextLight + 1) % s_lights.Count;
            light.transform.position = position;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 10f + 8f * size;
            s_driver.Begin(light, 6f + 4f * size);
        }

        /// <summary>조명 세기를 짧게 줄인다.</summary>
        private sealed class FlashDriver : MonoBehaviour
        {
            private readonly Dictionary<Light, (float peak, float t)> _active = new();
            private readonly List<Light> _keys = new();
            private const float Duration = 0.35f;

            public void Begin(Light light, float peak)
            {
                _active[light] = (peak, 0f);
                light.intensity = peak;
                light.enabled = true;
            }

            private void Update()
            {
                _keys.Clear();
                _keys.AddRange(_active.Keys);
                foreach (var l in _keys)
                {
                    var (peak, t) = _active[l];
                    t += Time.deltaTime;
                    if (l == null) { _active.Remove(l); continue; }
                    if (t >= Duration) { l.enabled = false; _active.Remove(l); continue; }
                    float k = 1f - t / Duration;
                    l.intensity = peak * k * k;
                    _active[l] = (peak, t);
                }
            }
        }
    }
}
