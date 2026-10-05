using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 섬 장식(판정 거의 없음). 섬마다 시드로 정해져 같은 섬은 늘 같은 모습이다.
    ///   - 등대: 무인도 40% · 큰 바위섬(반경 7m↑) 25%. 흰 탑 + 붉은 띠 + 등롱. 불빛이 4초마다 한 번 번쩍인다. 가늘어서 가리지 않는다(콜라이더 없음).
    ///   - 난파선: 무인도 30%. 녹슨 선체가 해안에 선수부터 얹혀 기울어 있고 반쯤 잠겨 있다. 선체는 섬의 일부(Terrain 콜라이더) —
    ///     배가 뚫고 지나가면 어색하므로 바위처럼 막는다.
    ///   - 물개: 바위섬 55%(2~4마리, 물가 바위 위) · 무인도 25%(1~3마리, 모래 해안).
    /// 메시는 섬마다 새로 만든다(섬을 치울 때 IslandField가 섬 아래 메시를 모두 지운다).
    /// </summary>
    public static partial class IslandBuilder
    {
        /// <summary>검증용: 켜면 모든 섬에 가능한 장식을 모두 넣는다.</summary>
        public static bool ForceAllDecor;

        private static Material s_white, s_red, s_rust, s_seal, s_glow;

        private static void LoadDecorMaterials()
        {
            if (s_white != null) return;
            s_white = Resources.Load<Material>("Islands/MAT_IslandWhite");
            s_red = Resources.Load<Material>("Islands/MAT_IslandRed");
            s_rust = Resources.Load<Material>("Islands/MAT_IslandRust");
            s_seal = Resources.Load<Material>("Islands/MAT_IslandSeal");
            s_glow = Resources.Load<Material>("Effects/MAT_FxSoft");
        }

        private static (bool lighthouse, bool wreck, int seals) Decorate(Transform root, float radius, float height, bool rocky, System.Random rng)
        {
            LoadDecorMaterials();
            Physics.SyncTransforms();   // 방금 만든 섬 콜라이더에 땅 높이를 물어본다

            bool force = ForceAllDecor;
            bool lighthouse = false, wreck = false;
            int seals = 0;

            if (force || rng.NextDouble() < (rocky ? (radius >= 7f ? 0.25 : 0.0) : 0.4))
                lighthouse = AddLighthouse(root, radius, height, rocky, rng);
            if (!rocky && (force || rng.NextDouble() < 0.3))
                wreck = AddBeachedWreck(root, radius, rng);
            if (force || rng.NextDouble() < (rocky ? 0.55 : 0.25))
                seals = AddSeals(root, radius, rocky, rocky ? rng.Next(2, 5) : rng.Next(1, 4), rng);

            return (lighthouse, wreck, seals);
        }

        // ------------------------------------------------------------ 등대

        private static bool AddLighthouse(Transform root, float radius, float height, bool rocky, System.Random rng)
        {
            // 무인도는 언덕 중턱, 바위섬은 꼭대기 근처 평평한 곳
            Vector3 spot = Vector3.zero;
            bool found = false;
            for (int i = 0; i < 8 && !found; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = radius * (rocky ? Range(0f, 0.25f, rng) : Range(0.15f, 0.4f, rng));
                Vector3 local = new(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (Ground(root, local, out float y, out Vector3 n) && n.y > 0.55f) { spot = new Vector3(local.x, y - 0.2f, local.z); found = true; }
            }
            if (!found) return false;

            float h = rocky ? Range(5f, 6.5f, rng) : Range(7f, 9f, rng);
            var tower = new GameObject("Lighthouse").transform;
            tower.SetParent(root, false);
            tower.localPosition = spot;

            Part(tower, "Tower", Lathe(new[] { (0f, 1f), (h * 0.96f, 0.68f), (h, 0.68f) }, 1.15f, 10, 0f, rng, flatTop: true), s_white, Vector3.zero);
            // 붉은 띠 두 줄
            for (int b = 0; b < 2; b++)
            {
                float y0 = h * (0.3f + b * 0.33f), y1 = y0 + h * 0.12f;
                float r0 = Mathf.Lerp(1f, 0.68f, y0 / h) * 1.15f + 0.03f, r1 = Mathf.Lerp(1f, 0.68f, y1 / h) * 1.15f + 0.03f;
                Part(tower, "Band", Lathe(new[] { (y0, r0), (y1, r1) }, 1f, 10, 0f, rng, flatTop: true), s_red, Vector3.zero);
            }
            // 난간 · 등롱 · 지붕
            Part(tower, "Gallery", Lathe(new[] { (h, 1.25f), (h + 0.25f, 1.25f) }, 1f, 10, 0f, rng, flatTop: true), s_red, Vector3.zero);
            Part(tower, "Lantern", Lathe(new[] { (h + 0.25f, 0.62f), (h + 1.3f, 0.62f) }, 1f, 8, 0f, rng, flatTop: true), s_white, Vector3.zero);
            Part(tower, "Roof", Lathe(new[] { (h + 1.3f, 0.85f), (h + 2.1f, 0f) }, 1f, 8, 0f, rng, flatTop: false), s_red, Vector3.zero);

            // 불빛(부드러운 원 스프라이트를 카메라 쪽으로 돌려 깜박인다)
            if (s_glow != null)
            {
                var glow = new GameObject("Lamp glow", typeof(MeshFilter), typeof(MeshRenderer));
                glow.transform.SetParent(tower, false);
                glow.transform.localPosition = new Vector3(0f, h + 0.8f, 0f);
                glow.GetComponent<MeshFilter>().sharedMesh = GlowQuad(new Color(1f, 0.9f, 0.55f, 1f));
                var r = glow.GetComponent<MeshRenderer>();
                r.sharedMaterial = s_glow;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (!root.TryGetComponent<IslandLights>(out var lights)) lights = root.gameObject.AddComponent<IslandLights>();
                lights.Add(glow.transform, (float)rng.NextDouble() * 4f);
            }
            return true;
        }

        // ------------------------------------------------------------ 해안 난파선

        private static bool AddBeachedWreck(Transform root, float radius, System.Random rng)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float L = Mathf.Clamp(radius * 0.75f, 6f, 11f), B = L * 0.24f, H = L * 0.17f;
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;

            var ship = new GameObject("Beached wreck").transform;
            ship.SetParent(root, false);
            Vector3 fwd = Quaternion.Euler(0f, side * Range(30f, 50f, rng), 0f) * -radial;   // 선수가 섬 쪽으로
            ship.localRotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(-Range(4f, 8f, rng), 0f, side * Range(14f, 22f, rng));
            ship.localPosition = radial * radius * 0.9f + Vector3.up * (SeaY + H * 0.35f);

            var hull = Prism(new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.22f), new Vector2(0f, 0.5f), new Vector2(-0.5f, 0.22f) },
                             new Vector3(B, H, L), keelScale: new Vector2(0.35f, 0.88f));
            var hullGo = Part(ship, "Hull", hull, s_rust, Vector3.zero);
            var mc = hullGo.AddComponent<MeshCollider>();
            mc.sharedMesh = hull;
            mc.convex = true;

            // 부서진 선교(기울어짐)와 부러진 돛대
            var house = Prism(new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) },
                              new Vector3(B * 0.7f, H * 0.9f, L * 0.22f), keelScale: Vector2.one);
            var houseGo = Part(ship, "Wheelhouse", house, s_rust, new Vector3(0f, H * 0.9f, -L * 0.18f));
            houseGo.transform.localRotation = Quaternion.Euler(0f, 0f, side * 6f);
            var mast = Part(ship, "Mast", Lathe(new[] { (0f, 1f), (L * 0.28f, 0.7f) }, 0.12f, 6, 0.05f, rng, flatTop: true), s_rust, new Vector3(0f, 0f, L * 0.12f));
            mast.transform.localRotation = Quaternion.Euler(Range(15f, 35f, rng), 0f, side * Range(10f, 25f, rng));
            return true;
        }

        // ------------------------------------------------------------ 물개

        private static int AddSeals(Transform root, float radius, bool rocky, int count, System.Random rng)
        {
            int placed = 0;
            for (int tries = 0; tries < count * 5 && placed < count; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = radius * (rocky ? Range(0.75f, 1.2f, rng) : Range(0.8f, 0.95f, rng));
                Vector3 local = new(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (!Ground(root, local, out float y, out Vector3 n) || n.y < 0.6f) continue;
                if (y < SeaY + 0.15f || y > 2.6f) continue;   // 물가 낮은 바위·모래 위만

                float len = Range(1.7f, 2.2f, rng);
                var seal = Lathe(new[] { (-len * 0.5f, 0.12f), (-len * 0.32f, 0.55f), (0f, 1f), (len * 0.28f, 0.8f), (len * 0.42f, 0.5f), (len * 0.5f, 0f) },
                                 0.36f, 7, 0.06f, rng, flatTop: false);
                var go = Part(root, "Seal", seal, s_seal, new Vector3(local.x, y + 0.22f, local.z));
                float yaw = a * Mathf.Rad2Deg + Range(40f, 140f, rng);
                // 몸을 눕히고(y축 → 앞), 머리를 살짝 든다
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(80f + Range(-8f, 4f, rng), 0f, 0f);
                placed++;
            }
            return placed;
        }

        // ------------------------------------------------------------ 도구

        /// <summary>섬 root 기준 (x, z)에서 위로부터 이 섬의 땅을 찾는다(root 로컬 높이·법선).</summary>
        private static bool Ground(Transform root, Vector3 local, out float y, out Vector3 normal)
        {
            y = 0f; normal = Vector3.up;
            Vector3 from = root.TransformPoint(new Vector3(local.x, 30f, local.z));
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(root)) continue;
                if (hit.point.y <= best) continue;
                best = hit.point.y;
                normal = hit.normal;
            }
            if (best == float.MinValue) return false;
            y = best - root.position.y;
            return true;
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 local)
        {
            var go = AddPart(parent, name, mesh, mat, collider: false);
            go.transform.localPosition = local;
            return go;
        }

        /// <summary>
        /// 윗면 윤곽(단위 크기, x 좌우 · y 앞뒤)을 아래로 밀어 만든 각기둥. 바닥은 keelScale만큼 좁아진다(선체).
        /// size = (폭, 높이, 길이). 윗면이 y=0, 바닥이 y=-높이. 면마다 정점을 따로 둬 각진 음영.
        /// </summary>
        private static Mesh Prism(Vector2[] outline, Vector3 size, Vector2 keelScale)
        {
            int n = outline.Length;
            var top = new Vector3[n];
            var bottom = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                top[i] = new Vector3(outline[i].x * size.x, 0f, outline[i].y * size.z);
                bottom[i] = new Vector3(outline[i].x * size.x * keelScale.x, -size.y, outline[i].y * size.z * keelScale.y);
            }
            Vector3 center = new(0f, -size.y * 0.5f, 0f);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                // 볼록한 모양이므로 중심에서 바깥을 보게 감는다
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center) < 0f) (b, c) = (c, b);
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(top[i], top[j], bottom[j]);
                Tri(top[i], bottom[j], bottom[i]);
            }
            for (int i = 1; i < n - 1; i++)
            {
                Tri(top[0], top[i], top[i + 1]);
                Tri(bottom[0], bottom[i], bottom[i + 1]);
            }
            var mesh = new Mesh { name = "Island decor" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GlowQuad(Color color)
        {
            const float s = 1.6f;
            var mesh = new Mesh { name = "Lamp glow" };
            mesh.SetVertices(new[] { new Vector3(-s, -s, 0), new Vector3(-s, s, 0), new Vector3(s, s, 0), new Vector3(s, -s, 0) });
            mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetColors(new[] { color, color, color, color });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>등대 불빛: 카메라를 향하게 돌리고 4초마다 한 번 번쩍인다(회전등).</summary>
    public class IslandLights : MonoBehaviour
    {
        private readonly List<(Transform glow, float phase)> _lamps = new();
        private Camera _camera;

        public int LampCount => _lamps.Count;

        public void Add(Transform glow, float phase) => _lamps.Add((glow, phase));

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            float t = Time.time;
            foreach (var (glow, phase) in _lamps)
            {
                if (glow == null) continue;
                glow.rotation = _camera.transform.rotation;
                float beam = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((t + phase) * Mathf.PI * 2f / 4f)), 12f);
                glow.localScale = Vector3.one * (0.35f + 1.4f * beam);
            }
        }
    }
}
