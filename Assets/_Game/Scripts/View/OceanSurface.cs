using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 끝없는 바다. 씬의 "Ocean" 평면(600m, 정적)은 배가 멀리 가면 끝이 보이므로,
    /// 같은 머티리얼로 큰 수면(1200m)을 새로 만들어 카메라를 따라 옮기고 원래 평면은 숨긴다.
    /// 무늬는 셰이더가 월드 좌표로 그리므로(Naval/Ocean) 수면을 옮겨도 물결은 제자리에 있다.
    /// 씬을 다시 만들지 않아도 되게 씬이 열린 뒤 스스로 붙는다.
    /// </summary>
    public class OceanSurface : MonoBehaviour
    {
        private const string SceneOceanName = "Ocean";
        private const float Size = 1200f;

        private Camera _camera;
        private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
        private readonly List<SinkingTrace> _sinkingTraces = new();

        private sealed class SinkingRing
        {
            public GameObject Object;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Properties;
            public float Delay, Duration, StartRadius, EndRadius, Alpha;
        }

        private sealed class SinkingTrace
        {
            public float Age;
            public readonly SinkingRing[] Rings = new SinkingRing[3];
        }

        /// <summary>잠수함이 격침된 정확한 수면 위치에 포말을 남긴다. 적 풀과 독립적으로 수명이 끝난다.</summary>
        public static void SpawnSinkingTrace(Vector3 position)
        {
            var ocean = FindFirstObjectByType<OceanSurface>();
            if (ocean != null) ocean.AddSinkingTrace(position);
        }

        private void AddSinkingTrace(Vector3 position)
        {
            var material = Resources.Load<Material>("Water/MAT_WakeKelvin");
            if (material == null) return;

            position.y = transform.position.y + 0.08f;
            var trace = new SinkingTrace();
            trace.Rings[0] = CreateSinkingRing(position, material, 0, 0f, 3.2f, 0.7f, 5f, 0.95f);
            trace.Rings[1] = CreateSinkingRing(position, material, 1, 0.35f, 4.4f, 0.9f, 8f, 0.75f);
            trace.Rings[2] = CreateSinkingRing(position, material, 2, 0.8f, 7f, 1f, 3.5f, 0.42f);
            _sinkingTraces.Add(trace);
        }

        private static SinkingRing CreateSinkingRing(Vector3 position, Material material, int index,
            float delay, float duration, float startRadius, float endRadius, float alpha)
        {
            var go = new GameObject("Submarine sinking foam", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = position + Vector3.up * (index * 0.012f);
            var mesh = BuildSinkingRing(index);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return new SinkingRing
            {
                Object = go, Mesh = mesh, Renderer = renderer, Properties = new MaterialPropertyBlock(),
                Delay = delay, Duration = duration, StartRadius = startRadius, EndRadius = endRadius, Alpha = alpha
            };
        }

        private static Mesh BuildSinkingRing(int seed)
        {
            const int segments = 48;
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float uneven = 1f + 0.07f * Mathf.Sin(angle * 5f + seed * 1.3f)
                                   + 0.04f * Mathf.Sin(angle * 9f - seed * 0.7f);
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * (uneven * 0.89f);
                vertices[i * 2 + 1] = direction * (uneven * 1.11f);
                uvs[i * 2] = new Vector2(0f, i * 0.32f);
                uvs[i * 2 + 1] = new Vector2(1f, i * 0.32f);
                colors[i * 2] = colors[i * 2 + 1] = new Color32(255, 255, 255, 255);
                if (i == segments) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Submarine sinking foam" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdateSinkingTraces(float dt)
        {
            for (int i = _sinkingTraces.Count - 1; i >= 0; i--)
            {
                var trace = _sinkingTraces[i];
                trace.Age += dt;
                foreach (var ring in trace.Rings)
                {
                    float elapsed = trace.Age - ring.Delay;
                    ring.Renderer.enabled = elapsed >= 0f && elapsed < ring.Duration;
                    if (!ring.Renderer.enabled) continue;
                    float phase = Mathf.Clamp01(elapsed / ring.Duration);
                    float radius = Mathf.Lerp(ring.StartRadius, ring.EndRadius, phase);
                    ring.Object.transform.localScale = new Vector3(radius, 1f, radius);
                    float alpha = ring.Alpha * Mathf.Pow(1f - phase, 1.4f);
                    ring.Properties.SetColor(FoamColorId, new Color(0.88f, 0.96f, 1f, alpha));
                    ring.Renderer.SetPropertyBlock(ring.Properties);
                }
                if (trace.Age < 7.8f) continue;
                ReleaseTrace(trace);
                _sinkingTraces.RemoveAt(i);
            }
        }

        private static void ReleaseTrace(SinkingTrace trace)
        {
            foreach (var ring in trace.Rings)
            {
                if (ring.Mesh != null) Destroy(ring.Mesh);
                if (ring.Object != null) Destroy(ring.Object);
            }
        }

        private void OnDestroy()
        {
            foreach (var trace in _sinkingTraces) ReleaseTrace(trace);
            _sinkingTraces.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<OceanSurface>() != null) return;
            var sceneOcean = GameObject.Find(SceneOceanName);
            if (sceneOcean == null || !sceneOcean.TryGetComponent<MeshRenderer>(out var sceneRenderer)) return;

            var go = new GameObject("Ocean Surface", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = sceneOcean.transform.position;
            go.GetComponent<MeshFilter>().sharedMesh = BuildQuad();

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = sceneRenderer.sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;

            sceneRenderer.enabled = false;
            go.AddComponent<OceanSurface>();
        }

        private static Mesh BuildQuad()
        {
            float h = Size * 0.5f;
            var mesh = new Mesh { name = "Ocean surface" };
            mesh.SetVertices(new[] { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(Size, 1f, Size));
            return mesh;
        }

        private void LateUpdate()
        {
            UpdateSinkingTraces(Time.deltaTime);
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // 카메라가 내려다보는 곳을 가운데로(수평 위치만)
            Vector3 c = _camera.transform.position;
            Vector3 f = _camera.transform.forward;
            float y = transform.position.y;
            Vector3 focus = f.y < -0.01f ? c + f * ((y - c.y) / f.y) : c;
            transform.position = new Vector3(focus.x, y, focus.z);
        }
    }
}
