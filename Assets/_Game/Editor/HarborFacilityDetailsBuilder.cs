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
                AddMesh(key, PrimitiveMeshes[shape.ToString()], at, size, rotation);
            }
            public void AddMesh(string key, Mesh mesh, Vector3 at, Vector3 size, Quaternion rotation)
            {
                if (!ByMaterial.TryGetValue(key, out var list)) ByMaterial[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0,
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
                         "concrete", "concrete_light", "road", "mark_white", "wall_grey", "roof_blue", "rubber",
                         "hull_black", "hull_red", "box_red", "box_green", "box_orange", "box_teal", "rope" })
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
            PrimitiveMeshes.Add("CargoHull", CreateCargoHull());

            var root = new GameObject("HarborFacilityDetails");
            try
            {
                var research = MakeFacility(root.transform, "Research", new Vector3(-445f, 4f, 330f), 90f, Research());
                var radar = new Parts();
                radar.Beam("steel", new Vector3(-3.8f, 0, 0), new Vector3(3.8f, 0, 0), .35f);
                radar.Box("box_blue", Vector3.zero, new Vector3(5f, 1.4f, .35f));
                MakeFacility(research, "ResearchRadar", new Vector3(0, 21.5f, 1f), 0f, radar, false);
                var records = MakeFacility(root.transform, "Records", new Vector3(110f, 4f, 330f), 0f, Records());
                var fan = new Parts();
                fan.Box("roof_white", Vector3.zero, new Vector3(5f, .18f, .7f));
                fan.Box("roof_white", Vector3.zero, new Vector3(.7f, .18f, 5f));
                MakeFacility(records, "RecordsFan", new Vector3(-31f, 21.7f, 5f), 0f, fan, false);
                var indicator = new Parts();
                indicator.Box("safety_yellow", Vector3.zero, new Vector3(.8f, .28f, .12f));
                MakeFacility(records, "RecordsIndicator", new Vector3(0, 23.65f, 11.95f), 0f, indicator, false);
                var workshop = MakeFacility(root.transform, "Workshop", new Vector3(640f, 0f, 650f), 0f, Workshop());
                MakeCraneTrolley(workshop, "Large", new Vector3(6f, 48f, 22f), 13f, true);
                MakeCraneTrolley(workshop, "Small", new Vector3(-5f, 32.6f, -56f), 7f, false);
                var cargoPort = MakeFacility(root.transform, "CargoPort", new Vector3(-850f, 0f, 550f), 0f, CargoPort());
                MakeFacility(cargoPort, "CargoShip", new Vector3(35f, -3f, 85f), 0f, CargoShip());
                MakeFacility(cargoPort, "CargoSupportTug", new Vector3(-22f, -1f, 50f), 0f, CargoSupportTug());
                MakeFacility(root.transform, "HarborWayfinding", Vector3.zero, 0f, HarborWayfinding());
                Validate(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("시설 프리팹 저장 실패");
                AssetDatabase.SaveAssets();
                Capture(prefab);
                Debug.Log("[HarborFacilityDetails] BUILD_AND_CAPTURE_OK " + PrefabPath);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(PrimitiveMeshes["CargoHull"]); }
        }

        private static Mesh CreateCargoHull()
        {
            // 화물선의 넓고 완만한 선수, 유닛 좌표 y=0~1 / z=-.5~.5.
            var v = new[] {
                new Vector3(-.4f, 0, -.5f), new Vector3(.4f, 0, -.5f), new Vector3(.4f, 0, .32f),
                new Vector3(.18f, 0, .48f), new Vector3(-.18f, 0, .48f), new Vector3(-.4f, 0, .32f),
                new Vector3(-.5f, 1, -.5f), new Vector3(.5f, 1, -.5f), new Vector3(.5f, 1, .32f),
                new Vector3(.23f, 1, .5f), new Vector3(-.23f, 1, .5f), new Vector3(-.5f, 1, .32f)
            };
            var tris = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                tris.AddRange(new[] { i, i + 6, j + 6, i, j + 6, j });
            }
            for (int i = 1; i < 5; i++) tris.AddRange(new[] { 6, 6 + i + 1, 6 + i, 0, i, i + 1 });
            var mesh = new Mesh { name = "CargoHullSource" };
            mesh.vertices = v; mesh.triangles = tris.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
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
            // 기록실 중앙동 옥상 환기 설비. 회전 날개는 별도 메시로 구성한다.
            p.Cylinder("steel_dark", new Vector3(-31f, 21.1f, 5f), 6f, 1f);
            return p;
        }

        private static Parts Workshop()
        {
            var p = new Parts();
            // 섬 안쪽까지 겹쳐 닿는 진입로와 독의 안쪽 끝(z = 565)·옆 작업장을 잇는 매립 안벽.
            // 기존 선박·독의 수면 아래 바닥에는 겹치지 않게 옆과 뒤쪽에만 놓는다.
            p.Box("concrete", new Vector3(35, .1f, -162), new Vector3(114, 7.8f, 80));
            p.Box("concrete_light", new Vector3(35, 4.08f, -162), new Vector3(114, .16f, 80));
            p.Box("concrete", new Vector3(0, .1f, -110), new Vector3(126, 7.8f, 54));
            p.Box("concrete", new Vector3(57, .1f, -7), new Vector3(70, 7.8f, 180));
            p.Box("concrete_light", new Vector3(0, 4.08f, -110), new Vector3(126, .16f, 54));
            p.Box("concrete_light", new Vector3(57, 4.08f, -7), new Vector3(70, .16f, 180));
            // 기존 안벽 간선(x 470, z 455)에서 해안 바깥쪽으로 돌아 독 옆 통로에 이어진다.
            // 주변 연료 탱크와 해안 울타리는 유지한다. 바다 구간은 콘크리트 안벽이 받친다.
            p.Box("concrete", new Vector3(-65f, .1f, -157.5f), new Vector3(216f, 7.8f, 55f));
            p.Box("road", new Vector3(-170f, 4.39f, -170f), new Vector3(18f, .12f, 56f));
            p.Box("road", new Vector3(-61f, 4.39f, -145f), new Vector3(220f, .12f, 18f));
            p.Box("road", new Vector3(52f, 4.39f, -117.5f), new Vector3(13f, .12f, 55f));
            for (int x = -155; x <= 40; x += 18)
                p.Box("mark_white", new Vector3(x, 4.48f, -145f), new Vector3(8f, .04f, .45f));
            for (int z = -188; z <= -160; z += 18)
                p.Box("mark_white", new Vector3(-170f, 4.48f, z), new Vector3(.45f, .04f, 8f));
            for (int z = -128; z <= -100; z += 18)
                p.Box("mark_white", new Vector3(52f, 4.48f, z), new Vector3(.45f, .04f, 8f));
            // 독 옆 작업 차량 통로, 레일과 구획선.
            p.Box("road", new Vector3(52, 4.39f, -5), new Vector3(13, .12f, 170));
            for (int z = -80; z <= 70; z += 20)
                p.Box("mark_white", new Vector3(52, 4.48f, z), new Vector3(.45f, .04f, 9));
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
            // 해안 도로와 독 사이에 소형 작업반·공구 보관동을 배치한다.
            p.Box("wall_grey", new Vector3(-40f, 9.2f, -105f), new Vector3(28f, 10f, 24f));
            p.Box("roof_blue", new Vector3(-40f, 14.4f, -105f), new Vector3(30f, .8f, 26f));
            p.Box("steel_dark", new Vector3(-40f, 8.4f, -117.1f), new Vector3(12f, 7.6f, .2f));
            for (int x = -44; x <= -36; x += 4)
                p.Box("steel", new Vector3(x, 8.4f, -117.25f), new Vector3(.2f, 7.6f, .12f));
            p.Box("safety_yellow", new Vector3(-40f, 12.6f, -117.3f), new Vector3(13f, .35f, .2f));
            p.Box("glass_blue", new Vector3(-50f, 10.7f, -117.15f), new Vector3(4f, 1.4f, .2f));
            // 작업 통로에서 벗어난 지정 주차 구역의 정비 트럭과 이동식 크레인.
            MaintenanceTruck(p, new Vector3(37f, 4.45f, -67f), false);
            MaintenanceTruck(p, new Vector3(37f, 4.45f, 49f), true);
            MaintenanceTruck(p, new Vector3(-120f, 4.45f, -165f), false);
            Forklift(p, new Vector3(-90f, 4.45f, -165f));
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

        private static void MaintenanceTruck(Parts p, Vector3 at, bool crane)
        {
            p.Box("steel_dark", at + new Vector3(0, 1.1f, 0), new Vector3(3.8f, 1.0f, 9.5f));
            p.Box(crane ? "safety_yellow" : "wall_white", at + new Vector3(0, 2.3f, 2.9f), new Vector3(3.6f, 2.8f, 3.2f));
            p.Box("glass_blue", at + new Vector3(0, 2.8f, 4.55f), new Vector3(3.0f, 1.1f, .12f));
            p.Box(crane ? "safety_yellow" : "box_blue", at + new Vector3(0, 2.5f, -1.5f), new Vector3(3.5f, 2.5f, 5.2f));
            foreach (float x in new[] { -2f, 2f })
            foreach (float z in new[] { -3f, 2.8f })
                p.Add("rubber", PrimitiveType.Cylinder, at + new Vector3(x, .85f, z), new Vector3(1.5f, .35f, 1.5f), Quaternion.Euler(0, 0, 90));
            if (!crane) return;
            p.Cylinder("steel_dark", at + new Vector3(0, 4.2f, -1.5f), 2.8f, .5f);
            p.Beam("safety_yellow", at + new Vector3(0, 4.5f, -1.5f), at + new Vector3(0, 10f, -9f), 1.15f);
            p.Beam("steel_dark", at + new Vector3(0, 10f, -9f), at + new Vector3(0, 6.7f, -9f), .18f);
        }

        private static void Forklift(Parts p, Vector3 at)
        {
            p.Box("safety_yellow", at + new Vector3(0, 1.2f, 0), new Vector3(3.3f, 2f, 4.7f));
            p.Box("glass_blue", at + new Vector3(0, 2.9f, -.6f), new Vector3(2.5f, 1.6f, 1.6f));
            p.Box("steel_dark", at + new Vector3(0, 3.8f, -.6f), new Vector3(3f, .25f, 2.1f));
            foreach (float x in new[] { -1.1f, 1.1f })
            {
                p.Box("steel_dark", at + new Vector3(x, 2.5f, 2.6f), new Vector3(.3f, 4.6f, .3f));
                p.Box("steel_dark", at + new Vector3(x * .65f, .45f, 3.9f), new Vector3(.25f, .25f, 2.8f));
            }
            foreach (float x in new[] { -1.6f, 1.6f })
            foreach (float z in new[] { -1.3f, 1.3f })
                p.Add("rubber", PrimitiveType.Cylinder, at + new Vector3(x, .65f, z), new Vector3(1.2f, .3f, 1.2f), Quaternion.Euler(0, 0, 90));
        }

        private static Parts CargoPort()
        {
            var p = new Parts();
            // 군함 선택 정박지 서쪽 바깥의 별도 화물 돌제. 안벽 도로를 연장해 연결한다.
            p.Box("concrete", new Vector3(65f, .1f, -85f), new Vector3(140f, 7.8f, 54f));
            p.Box("concrete", new Vector3(0, .1f, -42f), new Vector3(54f, 7.8f, 86f));
            p.Box("road", new Vector3(73f, 4.39f, -95f), new Vector3(132f, .12f, 18f));
            p.Box("road", new Vector3(0, 4.39f, -48f), new Vector3(14f, .12f, 96f));
            for (int x = 14; x < 140; x += 18)
                p.Box("mark_white", new Vector3(x, 4.48f, -95f), new Vector3(8f, .04f, .45f));
            for (int z = -80; z < -7; z += 18)
                p.Box("mark_white", new Vector3(0, 4.48f, z), new Vector3(.45f, .04f, 8f));
            // 화물 돌제와 기존 안벽 사이를 매립해 사무실·차량 작업장까지 하나의 부지로 잇는다.
            p.Box("concrete", new Vector3(66f, .1f, -29f), new Vector3(132f, 7.8f, 58f));
            p.Box("concrete_light", new Vector3(66f, 4.08f, -29f), new Vector3(132f, .16f, 58f));
            p.Box("road", new Vector3(67f, 4.39f, -50f), new Vector3(120f, .12f, 12f));
            for (int x = 22; x <= 112; x += 18)
                p.Box("mark_white", new Vector3(x, 4.48f, -50f), new Vector3(8f, .04f, .4f));
            // 2층 항만 사무실·출입 차양·창고. 입구와 차량 통로가 안쪽 도로를 향한다.
            p.Box("wall_white", new Vector3(73f, 10.15f, -24f), new Vector3(32f, 12f, 20f));
            p.Box("roof_blue", new Vector3(73f, 16.3f, -24f), new Vector3(33f, .7f, 21f));
            for (int floor = 0; floor < 2; floor++)
            {
                foreach (float z in new[] { -34.08f, -13.92f })
                {
                    p.Box("glass_blue", new Vector3(73f, 7.9f + floor * 4.4f, z), new Vector3(28f, 1.6f, .16f));
                    for (int x = 61; x <= 85; x += 6)
                        p.Box("wall_white", new Vector3(x, 7.9f + floor * 4.4f, z + (z < -24 ? -.12f : .12f)), new Vector3(.22f, 1.8f, .12f));
                }
            }
            p.Box("steel_dark", new Vector3(73f, 5.8f, -34.2f), new Vector3(4f, 3.2f, .18f));
            p.Box("roof_slate", new Vector3(73f, 8.1f, -36.3f), new Vector3(9f, .3f, 4.5f));
            p.Box("flag_navy", new Vector3(73f, 14.6f, -34.24f), new Vector3(11f, 1.4f, .16f));
            p.Box("wall_grey", new Vector3(111f, 8.2f, -23f), new Vector3(22f, 8f, 18f));
            p.Box("roof_slate", new Vector3(111f, 12.4f, -23f), new Vector3(23f, .6f, 19f));
            p.Box("steel_dark", new Vector3(111f, 7.4f, -32.1f), new Vector3(12f, 6f, .18f));
            MaintenanceTruck(p, new Vector3(40f, 4.18f, -23f), false);
            // 돌제 진입로와 갑판을 끊김 없이 맞춘다.
            p.Box("concrete", new Vector3(0, 1f, 90f), new Vector3(28f, 6.8f, 180f));
            p.Box("concrete_light", new Vector3(0, 4.45f, 90f), new Vector3(28f, .1f, 180f));
            p.Box("road", new Vector3(0, 4.54f, 90f), new Vector3(10f, .08f, 180f));
            for (int z = 12; z < 175; z += 18)
                p.Box("mark_white", new Vector3(0, 4.6f, z), new Vector3(.5f, .05f, 8f));
            foreach (float side in new[] { -1f, 1f })
            {
                p.Box("safety_yellow", new Vector3(side * 13.2f, 4.57f, 90f), new Vector3(.4f, .15f, 177f));
                for (int z = 18; z <= 168; z += 30)
                {
                    p.Box("rubber", new Vector3(side * 14.1f, 2f, z), new Vector3(.7f, 3.5f, 3.5f));
                    p.Cylinder("steel_dark", new Vector3(side * 11.7f, 5.1f, z), 1.4f, 1.1f);
                }
            }
            // 안벽 위 문형 다리와 바다 쪽 붐은 선체 상부를 가로지른다.
            foreach (float z in new[] { 65f, 125f })
            {
                foreach (float x in new[] { -10.5f, 10.5f })
                {
                    p.Box("crane_red", new Vector3(x, 22.2f, z), new Vector3(2.4f, 35.4f, 3f));
                    p.Beam("crane_red", new Vector3(x, 4.5f, z - 6f), new Vector3(x, 37f, z), 1f);
                    p.Beam("crane_red", new Vector3(x, 4.5f, z + 6f), new Vector3(x, 37f, z), 1f);
                }
                p.Box("crane_red", new Vector3(15f, 41.2f, z), new Vector3(61f, 3f, 3.8f));
                p.Box("wall_white", new Vector3(-7f, 38.4f, z), new Vector3(6f, 4f, 6f));
                p.Box("glass_blue", new Vector3(-7f, 38.9f, z + 3.05f), new Vector3(4.5f, 1.4f, .15f));
                p.Beam("steel", new Vector3(-5f, 42.5f, z), new Vector3(11f, 55f, z), .5f);
                p.Beam("steel", new Vector3(11f, 55f, z), new Vector3(43f, 42.5f, z), .5f);
                foreach (float dz in new[] { -2.5f, 2.5f })
                    p.Beam("steel_dark", new Vector3(35f, 39.5f, z + dz), new Vector3(35f, 24f, z + dz), .25f);
                p.Box("safety_yellow", new Vector3(35f, 23.7f, z), new Vector3(8.2f, .7f, 11.5f));
            }
            foreach (float z in new[] { 20f, 50f, 130f, 160f })
                p.Beam("rope", new Vector3(12f, 4.9f, z), new Vector3(21f, 9f, z + (z < 90f ? 5f : -5f)), .22f);
            foreach (float z in new[] { 43f, 57f })
                p.Beam("rope", new Vector3(-13.5f, 4.8f, z), new Vector3(-18f, 4f, z), .2f);
            MaintenanceTruck(p, new Vector3(-1f, 4.65f, 94f), false);
            return p;
        }

        private static Parts CargoSupportTug()
        {
            var p = new Parts();
            p.AddMesh("hull_black", PrimitiveMeshes["CargoHull"], Vector3.zero, new Vector3(8f, 5f, 22f), Quaternion.identity);
            p.Box("safety_yellow", new Vector3(0, 5.1f, 0), new Vector3(7.6f, .35f, 15f));
            p.Box("wall_white", new Vector3(0, 7f, -2f), new Vector3(5.6f, 3.6f, 6.5f));
            p.Box("glass_blue", new Vector3(0, 7.4f, 1.28f), new Vector3(4.6f, 1.4f, .16f));
            p.Box("roof_white", new Vector3(0, 8.95f, -2f), new Vector3(6f, .3f, 7f));
            p.Cylinder("steel", new Vector3(0, 11f, -2f), .35f, 4f);
            p.Box("crane_red", new Vector3(0, 13.2f, -2f), new Vector3(.8f, .6f, .8f));
            foreach (float z in new[] { -6f, 5f })
                p.Box("rubber", new Vector3(4f, 3.5f, z), new Vector3(.7f, 1.4f, 2f));
            return p;
        }

        private static Parts HarborWayfinding()
        {
            var p = new Parts();
            // 수로의 외해 측 표식: 군함 선택 위치와 기존 순찰선 경로에서 떨어진다.
            foreach (var at in new[] { new Vector3(-970f, 0f, 760f), new Vector3(505f, 0f, 745f) })
            {
                p.Cylinder("crane_red", at + Vector3.up * .8f, 5f, 2f);
                p.Cylinder("wall_white", at + Vector3.up * 3f, 2f, 3.5f);
                p.Box("safety_yellow", at + Vector3.up * 5f, new Vector3(1.2f, 1.2f, 1.2f));
                p.Cylinder("steel", at + Vector3.up * 6.4f, .3f, 1.7f);
            }
            return p;
        }

        private static Parts CargoShip()
        {
            var p = new Parts();
            p.AddMesh("hull_black", PrimitiveMeshes["CargoHull"], Vector3.zero, new Vector3(30f, 12f, 150f), Quaternion.identity);
            p.Box("steel_dark", new Vector3(0, 12.1f, -15f), new Vector3(29f, .4f, 108f));
            p.Box("wall_white", new Vector3(0, 16f, -58f), new Vector3(19f, 8f, 16f));
            p.Box("glass_blue", new Vector3(0, 19.2f, -49.9f), new Vector3(17f, 1.8f, .16f));
            p.Box("roof_white", new Vector3(0, 20.2f, -58f), new Vector3(20f, .4f, 17f));
            p.Box("crane_red", new Vector3(-5f, 24f, -62f), new Vector3(4f, 7f, 4f));
            p.Box("steel_dark", new Vector3(-5f, 27.8f, -62f), new Vector3(4.2f, .6f, 4.2f));
            p.Cylinder("steel", new Vector3(5f, 24f, -56f), .5f, 8f);
            p.Beam("steel", new Vector3(2f, 27f, -56f), new Vector3(8f, 27f, -56f), .35f);
            string[] colors = { "box_red", "box_blue", "box_orange", "box_green", "box_white", "box_teal" };
            for (int row = 0; row < 7; row++)
            for (int col = 0; col < 8; col++)
            {
                int levels = 2 + ((row + col) % 5 == 0 ? 1 : 0);
                for (int level = 0; level < levels; level++)
                    p.Box(colors[(row * 3 + col * 2 + level) % colors.Length],
                        new Vector3(-11.9f + col * 3.4f, 13.5f + level * 2.7f, -39f + row * 13f),
                        new Vector3(3.25f, 2.55f, 12.2f));
            }
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
                ["Research"] = new(-445, 4, 330), ["Records"] = new(110, 4, 330),
                ["Workshop"] = new(640, 0, 650), ["CargoPort"] = new(-850, 0, 550) };
            foreach (var pair in expected)
            {
                var group = root.transform.Find(pair.Key);
                if (group == null || Vector3.Distance(group.localPosition, pair.Value) > .001f)
                    throw new InvalidOperationException("시설 위치 불일치: " + pair.Key);
                if (group.GetComponentsInChildren<MeshRenderer>().Length == 0)
                    throw new InvalidOperationException("시설 메시 없음: " + pair.Key);
            }
            if (root.GetComponentsInChildren<Collider>().Length != 0) throw new InvalidOperationException("시설 외형에 콜라이더가 있음");
            var cargo = root.transform.Find("CargoPort");
            if (cargo.GetComponentsInChildren<Renderer>().Any(r => r.bounds.max.x > -650f))
                throw new InvalidOperationException("화물 부두가 기존 군함 정박 구역에 너무 가까움");
            var filters = root.GetComponentsInChildren<MeshFilter>();
            int tris = filters.Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
            int renderers = root.GetComponentsInChildren<MeshRenderer>().Length;
            if (renderers > 85 || tris > 25000) throw new InvalidOperationException($"시설 외형 예산 초과: {renderers} renderers / {tris} tris");
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.sharedMaterial == null || r.sharedMaterial.shader.name != "Universal Render Pipeline/Lit")
                    throw new InvalidOperationException("시설 URP 재질 누락: " + r.name);
            File.WriteAllText("ArtSource/HarborFacilityDetails/validation.txt",
                $"PASS: 4 facility anchors, {renderers} combined renderers, {tris} triangles, shared URP materials, no colliders.\n");
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
                ValidateFacilityMotion(details);
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
                    ("workshop_context", new Vector3(665, 10, 620), 260f),
                    ("workshop_access", new Vector3(570, 10, 535), 370f),
                    ("cargo_port", new Vector3(-830, 14, 635), 230f),
                    ("cargo_context", new Vector3(-660, 10, 480), 570f) })
                {
                    Vector3 target = island.transform.TransformPoint(item.Item2);
                    camera.transform.position = island.transform.TransformPoint(item.Item2 + new Vector3(-.45f, .9f, 1.1f) * item.Item3);
                    camera.transform.LookAt(target);
                    details.gameObject.SetActive(false); Render(camera, Output + "/" + item.Item1 + "_before.png");
                    details.gameObject.SetActive(true); Render(camera, Output + "/" + item.Item1 + "_after.png");
                    if (item.Item1 == "research" || item.Item1 == "records")
                    {
                        var motion = new Game.UI.HarborFacilityMotion(details);
                        motion.FocusAt(item.Item2); motion.Tick(1f);
                        Render(camera, Output + "/" + item.Item1 + "_motion.png");
                    }
                }
                camera.fieldOfView = 35f;
                camera.transform.position = island.transform.TransformPoint(new Vector3(-1350f, 1650f, 2000f));
                camera.transform.LookAt(island.transform.TransformPoint(new Vector3(20f, 0f, 130f)));
                Render(camera, Output + "/island_overview_after.png");
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

        private static void ValidateFacilityMotion(Transform details)
        {
            var radar = details.Find("Research/ResearchRadar");
            var fan = details.Find("Records/RecordsFan");
            var indicator = details.Find("Records/RecordsIndicator");
            if (radar == null || fan == null || indicator == null) throw new InvalidOperationException("시설 움직임 계층 누락");
            var radarStart = radar.localRotation; var fanStart = fan.localRotation; var indicatorStart = indicator.localPosition;
            var motion = new Game.UI.HarborFacilityMotion(details);
            motion.FocusAt(new Vector3(640, 15, 650)); motion.Tick(1f);
            if (radar.localRotation != radarStart || fan.localRotation != fanStart || indicator.localPosition != indicatorStart)
                throw new InvalidOperationException("다른 시설 확대 중 연구소·기록실이 움직임");
            motion.FocusAt(new Vector3(-445, 15, 330)); motion.Tick(1f);
            if (radar.localRotation == radarStart || fan.localRotation != fanStart || indicator.localPosition != indicatorStart)
                throw new InvalidOperationException("연구소 선택 동작 불일치");
            var stoppedRadar = radar.localRotation; motion.Tick(0f);
            if (radar.localRotation != stoppedRadar) throw new InvalidOperationException("연구소 정지 실패");
            motion.FocusAt(new Vector3(110, 20, 330)); motion.Tick(1f);
            if (radar.localRotation != stoppedRadar || fan.localRotation == fanStart || indicator.localPosition == indicatorStart)
                throw new InvalidOperationException("기록실 선택 동작 불일치");
            var stoppedFan = fan.localRotation; var stoppedIndicator = indicator.localPosition;
            motion.Tick(0f); motion.Stop(); motion.Tick(1f);
            if (fan.localRotation != stoppedFan || indicator.localPosition != stoppedIndicator)
                throw new InvalidOperationException("기록실 정지 실패");
            radar.localRotation = radarStart; fan.localRotation = fanStart; indicator.localPosition = indicatorStart;
            File.AppendAllText("ArtSource/HarborFacilityDetails/validation.txt",
                "PASS: research radar and records fan/indicator run only at their own focus and stop when paused or unfocused.\n");
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
