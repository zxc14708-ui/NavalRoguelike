using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// 군항 세 기능 시설의 추가 외형. 기존 통합 기지 메시·씬을 다시 만들지 않는다.
    /// 좌표와 치수는 NavalBaseBuilder의 축소 전 섬 좌표(m)이다.
    /// </summary>
    public static class HarborFacilityDetailsBuilder
    {
        private const string Art = "Assets/_Game/Art/HarborFacilityDetails";
        private const string PrefabPath = "Assets/_Game/Resources/Environment/HarborFacilityDetails.prefab";
        private const string ExistingIsland = "Assets/_Game/Resources/Environment/NavalBaseMenu.prefab";
        private const string Palette = "Assets/_Game/Art/NavalBase/MAT_base_";
        private const string Output = "ArtSource/HarborFacilityDetails/Preview";
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly Dictionary<string, Mesh> PrimitiveMeshes = new();

        private sealed class Parts
        {
            public readonly Dictionary<string, List<CombineInstance>> ByMaterial = new();
            public void Add(string key, PrimitiveType shape, Vector3 at, Vector3 size, Quaternion rotation)
            {
                if (!ByMaterial.TryGetValue(key, out var list)) ByMaterial[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = PrimitiveMeshes[shape.ToString()], subMeshIndex = 0,
                    transform = Matrix4x4.TRS(at, rotation, size) });
            }
            public void Box(string mat, Vector3 at, Vector3 size) => Add(mat, PrimitiveType.Cube, at, size, Quaternion.identity);
            public void Cylinder(string mat, Vector3 at, float diameter, float height)
                => Add(mat, PrimitiveType.Cylinder, at, new Vector3(diameter, height * .5f, diameter), Quaternion.identity);
            public void Sphere(string mat, Vector3 at, Vector3 size) => Add(mat, PrimitiveType.Sphere, at, size, Quaternion.identity);
            public void Beam(string mat, Vector3 a, Vector3 b, float width)
            {
                Vector3 delta = b - a;
                Add(mat, PrimitiveType.Cube, (a + b) * .5f, new Vector3(width, delta.magnitude, width),
                    Quaternion.FromToRotation(Vector3.up, delta.normalized));
            }
        }

        [MenuItem("Naval/Art/Build Interactive Harbor Facility Details")]
        public static void Build()
        {
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Materials.Clear(); PrimitiveMeshes.Clear();
            foreach (string key in new[] { "wall_white", "wall_beige", "roof_slate", "glass_blue", "steel", "steel_dark",
                         "safety_yellow", "dock_grey", "roof_white", "box_blue", "box_white", "crane_red", "flag_navy",
                         "concrete", "concrete_light", "road", "mark_white", "wall_grey", "roof_blue" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Palette + key + ".mat");
                if (material == null) throw new InvalidOperationException("군항 공용 재질 누락: " + key);
                Materials.Add(key, material);
            }
            foreach (var shape in new[] { PrimitiveType.Cube, PrimitiveType.Cylinder, PrimitiveType.Sphere })
            {
                var primitive = GameObject.CreatePrimitive(shape);
                PrimitiveMeshes.Add(shape.ToString(), primitive.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(primitive);
            }

            var root = new GameObject("HarborFacilityDetails");
            try
            {
                MakeFacility(root.transform, "Research", new Vector3(-445f, 4f, 330f), 90f, Research());
                MakeFacility(root.transform, "Records", new Vector3(110f, 4f, 330f), 0f, Records());
                var workshop = MakeFacility(root.transform, "Workshop", new Vector3(640f, 0f, 650f), 0f, Workshop());
                MakeCraneTrolley(workshop, "Large", new Vector3(6f, 48f, 22f), 13f, true);
                MakeCraneTrolley(workshop, "Small", new Vector3(-5f, 32.6f, -56f), 7f, false);
                Validate(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("시설 프리팹 저장 실패");
                AssetDatabase.SaveAssets();
                Capture(prefab);
                Debug.Log("[HarborFacilityDetails] BUILD_AND_CAPTURE_OK " + PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Parts Research()
        {
            var p = new Parts();
            // 건물 본체는 기존 EngineeringOffice 그대로. 남서쪽 정면 창 띠에 수직 창틀과 현관 사인을 얹는다.
            for (int floor = 0; floor < 3; floor++)
                for (int x = -26; x <= 26; x += 4)
                    p.Box("wall_white", new Vector3(x, 2.2f + floor * 3.8f, -9.18f), new Vector3(.18f, 1.55f, .12f));
            p.Box("flag_navy", new Vector3(0, 10.55f, -9.32f), new Vector3(11f, 1.5f, .28f));
            p.Box("roof_white", new Vector3(0, 10.55f, -9.50f), new Vector3(7.6f, .16f, .08f));
            p.Box("glass_blue", new Vector3(0, 2f, -9.22f), new Vector3(4.1f, 2.6f, .14f));
            p.Box("wall_white", new Vector3(0, 3.36f, -9.32f), new Vector3(4.6f, .18f, .18f));
            // 실험실 상부: 낮은 유리 연구동, 난간, 장비 박스와 관측 안테나.
            p.Box("roof_slate", new Vector3(0, 13.12f, 1f), new Vector3(16f, .38f, 9f));
            p.Box("wall_white", new Vector3(0, 14.83f, 1f), new Vector3(15f, 3.05f, 8f));
            p.Box("glass_blue", new Vector3(0, 15.15f, -3.05f), new Vector3(13.7f, 1.35f, .13f));
            p.Box("glass_blue", new Vector3(0, 15.15f, 5.05f), new Vector3(13.7f, 1.35f, .13f));
            for (int x = -6; x <= 6; x += 3)
                foreach (float z in new[] { -3.14f, 5.14f })
                    p.Box("wall_white", new Vector3(x, 15.15f, z), new Vector3(.18f, 1.45f, .12f));
            p.Box("roof_slate", new Vector3(0, 16.48f, 1f), new Vector3(15.8f, .35f, 8.8f));
            foreach (float x in new[] { -23f, 23f })
            {
                p.Box("steel_dark", new Vector3(x, 13.15f, 1f), new Vector3(7f, .28f, 5f));
                p.Box("glass_blue", new Vector3(x, 13.33f, 1f), new Vector3(6.6f, .12f, 4.6f));
                for (int i = -1; i <= 1; i++) p.Box("steel", new Vector3(x + i * 1.6f, 13.42f, 1f), new Vector3(.12f, .08f, 4.6f));
            }
            p.Cylinder("steel_dark", new Vector3(0, 16.85f, 1f), 2.5f, .4f);
            p.Cylinder("steel", new Vector3(0, 20.1f, 1f), .45f, 6.3f);
            p.Beam("steel", new Vector3(-3, 21.5f, 1f), new Vector3(3, 21.5f, 1f), .35f);
            p.Box("box_blue", new Vector3(0, 21.5f, 1f), new Vector3(3.6f, 1.4f, .35f));
            p.Sphere("roof_white", new Vector3(0, 23.9f, 1f), new Vector3(1.6f, 1.6f, 1.6f));
            return p;
        }

        private static Parts Records()
        {
            var p = new Parts();
            // 기존 5층 사령부 중앙 유리탑의 양면에 규칙적인 창틀과 기록실 표식을 더한다.
            foreach (float z in new[] { -13.57f, 11.57f })
            {
                for (int x = -8; x <= 8; x += 4)
                    p.Box("wall_white", new Vector3(x, 15.5f, z), new Vector3(.24f, 24.5f, .15f));
                for (int i = 0; i < 5; i++)
                    p.Box("wall_white", new Vector3(0, 5.5f + i * 4.5f, z), new Vector3(19.5f, .22f, .15f));
                p.Box("flag_navy", new Vector3(0, 24.8f, z * 1.012f), new Vector3(7.6f, 3.4f, .28f));
                // 나란한 세 가로줄은 기록 서가를 암시하는 조형물이다.
                for (int i = -1; i <= 1; i++)
                    p.Box("roof_white", new Vector3(0, 24.8f + i * .75f, z * 1.025f), new Vector3(4.8f, .22f, .12f));
            }
            // 정면 진입구·차양과 중앙 탑의 상부 보강.
            p.Box("roof_slate", new Vector3(0, 9.65f, -17.5f), new Vector3(28.5f, .35f, 7f));
            p.Box("roof_white", new Vector3(0, 10f, -21.05f), new Vector3(27.5f, .22f, .16f));
            p.Box("flag_navy", new Vector3(0, 29.03f, -1), new Vector3(12f, .22f, 12f));
            p.Box("roof_white", new Vector3(0, 29.18f, -1), new Vector3(8f, .08f, 8f));
            foreach (float x in new[] { -7.5f, 7.5f })
            {
                p.Box("steel", new Vector3(x, 29.7f, -1), new Vector3(.45f, 1.1f, 10f));
                p.Box("steel", new Vector3(x, 30.2f, -1), new Vector3(.5f, .2f, 10f));
            }
            return p;
        }

        private static Parts Workshop()
        {
            var p = new Parts();
            // 섬 안쪽까지 겹쳐 닿는 진입로와 독의 안쪽 끝(z = 565)·옆 작업장을 잇는 매립 안벽.
            // 기존 선박·독의 수면 아래 바닥에는 겹치지 않게 옆과 뒤쪽에만 놓는다.
            p.Box("concrete", new Vector3(35, .1f, -162), new Vector3(114, 7.8f, 80));
            p.Box("concrete_light", new Vector3(35, 4.08f, -162), new Vector3(114, .16f, 80));
            p.Box("road", new Vector3(0, 4.1f, -162), new Vector3(28, .18f, 80));
            p.Box("concrete", new Vector3(0, .1f, -110), new Vector3(126, 7.8f, 54));
            p.Box("concrete", new Vector3(57, .1f, -7), new Vector3(70, 7.8f, 180));
            p.Box("concrete_light", new Vector3(0, 4.08f, -110), new Vector3(126, .16f, 54));
            p.Box("concrete_light", new Vector3(57, 4.08f, -7), new Vector3(70, .16f, 180));
            // 독 옆 작업 차량 통로, 레일과 구획선.
            p.Box("road", new Vector3(52, 4.2f, -5), new Vector3(13, .12f, 170));
            for (int z = -80; z <= 70; z += 20)
                p.Box("mark_white", new Vector3(52, 4.28f, z), new Vector3(.45f, .04f, 9));
            foreach (float x in new[] { 27f, 38f })
                p.Box("steel_dark", new Vector3(x, 4.22f, -5), new Vector3(.4f, .16f, 166));
            p.Box("safety_yellow", new Vector3(22.35f, 4.22f, -5), new Vector3(.45f, .12f, 166));
            // 선박 블록·장비를 다루는 육상 정비동. 큰 출입문이 작업장 쪽을 향한다.
            p.Box("wall_grey", new Vector3(72, 12.2f, -17), new Vector3(30, 16, 58));
            p.Box("roof_blue", new Vector3(72, 20.5f, -17), new Vector3(32, 1.0f, 60));
            p.Box("steel_dark", new Vector3(56.92f, 10.5f, -17), new Vector3(.18f, 12, 26));
            for (int z = -29; z <= -5; z += 6)
                p.Box("steel", new Vector3(56.78f, 10.5f, z), new Vector3(.18f, 12, .25f));
            p.Box("flag_navy", new Vector3(56.75f, 18.15f, -17), new Vector3(.24f, 2.0f, 17));
            p.Box("roof_white", new Vector3(56.58f, 18.15f, -17), new Vector3(.1f, .2f, 11));
            for (int z = -39; z <= 5; z += 11)
                p.Box("glass_blue", new Vector3(56.82f, 16.2f, z), new Vector3(.2f, 1.3f, 6));
            // 육상 작업물과 작은 안전 표식은 사용 중인 조선소의 크기감을 만든다.
            foreach (float z in new[] { 28f, 44f, 60f })
            {
                p.Box("steel_dark", new Vector3(72, 5.1f, z), new Vector3(13, 1.8f, 8));
                p.Box("box_blue", new Vector3(72, 7.4f, z), new Vector3(11, 3.0f, 6));
            }
            foreach (float x in new[] { -42f, -22f, -2f, 18f })
                p.Box("safety_yellow", new Vector3(x, 4.26f, -94), new Vector3(9, .08f, .4f));
            // 대형 문형 크레인: 독의 양 벽에 다리를 두고 선체 위로 작업한다.
            foreach (float x in new[] { -20f, 20f })
            {
                p.Box("crane_red", new Vector3(x, 34f, 22f), new Vector3(2.8f, 31f, 3f));
                p.Beam("crane_red", new Vector3(x, 18.5f, 14f), new Vector3(x, 47f, 22f), 1.4f);
                p.Beam("crane_red", new Vector3(x, 18.5f, 30f), new Vector3(x, 47f, 22f), 1.4f);
            }
            p.Box("crane_red", new Vector3(0, 50f, 22f), new Vector3(44f, 3f, 4f));
            // 부유 독 기존 선박의 공간을 비워 두고 양현 상부에 안전 난간과 장비를 올린다.
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 18.5f;
                p.Box("steel_dark", new Vector3(x, 18.16f, 0), new Vector3(5.2f, .28f, 163f));
                p.Box("safety_yellow", new Vector3(x - side * 2.7f, 18.34f, 0), new Vector3(.35f, .09f, 160f));
                foreach (float z in new[] { -75f, -55f, -35f, -15f, 5f, 25f, 45f, 65f, 80f })
                    p.Box("steel", new Vector3(x + side * 2.8f, 19.75f, z), new Vector3(.34f, 2.9f, .34f));
                p.Box("steel", new Vector3(x + side * 2.8f, 21.1f, 0), new Vector3(.32f, .3f, 162f));
                p.Box("steel", new Vector3(x + side * 2.8f, 19.85f, 0), new Vector3(.24f, .22f, 162f));
                foreach (float z in new[] { -63f, 63f })
                {
                    p.Box("box_blue", new Vector3(x, 20.1f, z), new Vector3(3.4f, 3.5f, 5f));
                    p.Box("roof_white", new Vector3(x, 21.95f, z), new Vector3(3.6f, .25f, 5.2f));
                    p.Box("glass_blue", new Vector3(x - side * 1.73f, 20.7f, z), new Vector3(.12f, 1.0f, 3.2f));
                }
                for (int step = 0; step < 6; step++)
                    p.Box("safety_yellow", new Vector3(x + side * 3.58f, 2.9f + step * 2.2f, 76f), new Vector3(.18f, .3f, 2.6f));
            }
            // 横断門型クレーン: 船体に触れない上空の梁、走行台車とフック。
            foreach (float x in new[] { -20f, 20f })
            {
                p.Box("safety_yellow", new Vector3(x, 25.8f, -56f), new Vector3(2.4f, 15.4f, 2.4f));
                p.Beam("safety_yellow", new Vector3(x, 18.3f, -62f), new Vector3(x, 30.5f, -56f), 1.0f);
                p.Beam("safety_yellow", new Vector3(x, 18.3f, -50f), new Vector3(x, 30.5f, -56f), 1.0f);
            }
            p.Box("safety_yellow", new Vector3(0, 34f, -56f), new Vector3(43f, 2.1f, 3f));
            return p;
        }

        private static void MakeCraneTrolley(Transform workshop, string prefix, Vector3 position, float cableLength, bool large)
        {
            var trolley = new Parts();
            trolley.Box("steel_dark", Vector3.zero, large ? new Vector3(5f, 1.3f, 4.5f) : new Vector3(5f, .9f, 3.8f));
            var carrier = MakeFacility(workshop, prefix + "CraneTrolley", position, 0f, trolley, false);
            var cable = new Parts();
            cable.Box("steel_dark", new Vector3(0, -cableLength * .5f, 0), new Vector3(.35f, cableLength, .35f));
            MakeFacility(carrier, prefix + "CraneCable", new Vector3(0, -.5f, 0), 0f, cable, false);
            var hook = new Parts();
            hook.Box(large ? "safety_yellow" : "crane_red", Vector3.zero, new Vector3(1.5f, 1f, 1.5f));
            MakeFacility(carrier, prefix + "CraneHook", new Vector3(0, -.5f - cableLength, 0), 0f, hook, false);
        }

        private static Transform MakeFacility(Transform root, string name, Vector3 position, float yaw, Parts parts, bool staticMeshes = true)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false); group.localPosition = position; group.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var pair in parts.ByMaterial)
            {
                string path = $"{Art}/MESH_{name}_{pair.Key}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
                mesh.Clear(); mesh.name = $"MESH_{name}_{pair.Key}";
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                var child = new GameObject(pair.Key, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(group, false); child.isStatic = staticMeshes;
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                child.GetComponent<MeshRenderer>().sharedMaterial = Materials[pair.Key];
            }
            return group;
        }

        private static void Validate(GameObject root)
        {
            var expected = new Dictionary<string, Vector3> {
                ["Research"] = new(-445, 4, 330), ["Records"] = new(110, 4, 330), ["Workshop"] = new(640, 0, 650) };
            foreach (var pair in expected)
            {
                var group = root.transform.Find(pair.Key);
                if (group == null || Vector3.Distance(group.localPosition, pair.Value) > .001f)
                    throw new InvalidOperationException("시설 위치 불일치: " + pair.Key);
                if (group.GetComponentsInChildren<MeshRenderer>().Length == 0)
                    throw new InvalidOperationException("시설 메시 없음: " + pair.Key);
            }
            if (root.GetComponentsInChildren<Collider>().Length != 0) throw new InvalidOperationException("시설 외형에 콜라이더가 있음");
            var filters = root.GetComponentsInChildren<MeshFilter>();
            int tris = filters.Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
            int renderers = root.GetComponentsInChildren<MeshRenderer>().Length;
            if (renderers > 36 || tris > 25000) throw new InvalidOperationException($"시설 외형 예산 초과: {renderers} renderers / {tris} tris");
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.sharedMaterial == null || r.sharedMaterial.shader.name != "Universal Render Pipeline/Lit")
                    throw new InvalidOperationException("시설 URP 재질 누락: " + r.name);
            File.WriteAllText("ArtSource/HarborFacilityDetails/validation.txt",
                $"PASS: 3 facility anchors, {renderers} combined renderers, {tris} triangles, shared URP materials, no colliders.\n");
        }

        private static void Capture(GameObject detailsPrefab)
        {
            var islandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExistingIsland);
            if (islandPrefab == null) throw new InvalidOperationException("군항 배경 프리팹 없음: " + ExistingIsland);
            Scene previous = SceneManager.GetActiveScene();
            bool single = Application.isBatchMode && string.IsNullOrEmpty(previous.path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, single ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var oldAmbientMode = RenderSettings.ambientMode;
            var oldAmbient = RenderSettings.ambientLight;
            bool oldFog = RenderSettings.fog;
            var disabledLights = new List<Light>();
            try
            {
                var island = (GameObject)PrefabUtility.InstantiatePrefab(islandPrefab, scene);
                island.transform.localPosition = new Vector3(0, -.9f, 0);
                // 시작 화면과 동일한 Resources.Load / 부착 경로를 검증한다.
                if (Resources.Load<GameObject>("Environment/HarborFacilityDetails") != detailsPrefab)
                    throw new InvalidOperationException("시설 Resources 프리팹 연결 실패");
                Game.UI.HarborFacilityArt.Attach(island.transform);
                Game.UI.HarborFacilityArt.Attach(island.transform);
                var details = island.transform.Find("HarborFacilityDetails");
                if (details == null || island.GetComponentsInChildren<Transform>(true)
                    .Count(t => t.name == "HarborFacilityDetails") != 1)
                    throw new InvalidOperationException("시설 프리팹 로드 또는 중복 방지 실패");
                ValidateCraneMotion(details);
                foreach (var t in island.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.enabled) { light.enabled = false; disabledLights.Add(light); }
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.63f, .71f, .77f);
                RenderSettings.fog = false;
                var sun = new GameObject("Harbor daylight").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1f, .97f, .92f);
                sun.transform.rotation = Quaternion.Euler(40, 150, 0); sun.cullingMask = 1 << 30;
                sun.shadows = LightShadows.Soft;
                var camera = new GameObject("Harbor focus capture").AddComponent<Camera>();
                camera.enabled = false; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.42f, .65f, .77f); camera.fieldOfView = 34;
                camera.nearClipPlane = .1f; camera.farClipPlane = 1200; camera.allowHDR = false;
                foreach (var item in new[] {
                    ("research", new Vector3(-445, 15, 330), 150f),
                    ("records", new Vector3(110, 20, 330), 160f),
                    ("workshop", new Vector3(640, 15, 650), 170f),
                    ("workshop_context", new Vector3(665, 10, 620), 260f) })
                {
                    Vector3 target = island.transform.TransformPoint(item.Item2);
                    camera.transform.position = island.transform.TransformPoint(item.Item2 + new Vector3(-.45f, .9f, 1.1f) * item.Item3);
                    camera.transform.LookAt(target);
                    details.gameObject.SetActive(false); Render(camera, Output + "/" + item.Item1 + "_before.png");
                    details.gameObject.SetActive(true); Render(camera, Output + "/" + item.Item1 + "_after.png");
                }
                File.AppendAllText("ArtSource/HarborFacilityDetails/validation.txt",
                    "PASS: Resources.Load, one-time attachment, existing harbor prefab, three focus cameras and before/after renders.\n");
            }
            finally
            {
                RenderSettings.ambientMode = oldAmbientMode;
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.fog = oldFog;
                foreach (var light in disabledLights) if (light != null) light.enabled = true;
                if (!single) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void ValidateCraneMotion(Transform details)
        {
            var workshop = details.Find("Workshop");
            var large = workshop.Find("LargeCraneTrolley");
            var small = workshop.Find("SmallCraneTrolley");
            var cable = large != null ? large.Find("LargeCraneCable") : null;
            var hook = large != null ? large.Find("LargeCraneHook") : null;
            if (large == null || small == null || cable == null || hook == null)
                throw new InvalidOperationException("크레인 움직임 계층 누락");
            var largeStart = large.localPosition; var smallStart = small.localPosition;
            var cableStart = cable.localScale; var hookStart = hook.localPosition;
            var smallCable = small.Find("SmallCraneCable"); var smallHook = small.Find("SmallCraneHook");
            if (smallCable == null || smallHook == null) throw new InvalidOperationException("소형 크레인 고리 누락");
            var smallScale = smallCable.localScale; var smallHookStart = smallHook.localPosition;
            var motion = new Game.UI.HarborCraneMotion(details);
            motion.FocusAt(new Vector3(-445, 15, 330)); motion.Tick(2f);
            if (large.localPosition != largeStart || small.localPosition != smallStart)
                throw new InvalidOperationException("다른 시설에서 크레인이 움직임");
            motion.FocusAt(new Vector3(640, 15, 650)); motion.Tick(2f);
            if (large.localPosition == largeStart || small.localPosition == smallStart || cable.localScale == cableStart || hook.localPosition == hookStart)
                throw new InvalidOperationException("정비창 확대 시 크레인 움직임 누락");
            var paused = large.localPosition; motion.Tick(0f);
            if (large.localPosition != paused) throw new InvalidOperationException("정지 상태에서 크레인이 움직임");
            motion.Stop(); motion.Tick(2f);
            if (large.localPosition != paused) throw new InvalidOperationException("정비창을 벗어난 뒤 크레인이 움직임");
            large.localPosition = largeStart; small.localPosition = smallStart;
            cable.localScale = cableStart; hook.localPosition = hookStart;
            smallCable.localScale = smallScale; smallHook.localPosition = smallHookStart;
            File.AppendAllText("ArtSource/HarborFacilityDetails/validation.txt",
                "PASS: both crane trolleys and hoists move on workshop focus, remain still elsewhere and when paused.\n");
        }

        private static void Render(Camera camera, string path)
        {
            const int w = 1600, h = 900;
            var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var old = RenderTexture.active; var oldTarget = camera.targetTexture;
            Texture2D pixels = null;
            try
            {
                target.Create(); camera.targetTexture = target; camera.aspect = w / (float)h;
                camera.Render(); camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = old;
                if (pixels != null) Object.DestroyImmediate(pixels);
                target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
