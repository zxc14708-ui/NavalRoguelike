using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// 해군 기지 섬 씬 빌더(2026-10-07, 2차: 파스텔 로우폴리 섬 + 실제 비례).
    /// 축척: 호위함 T3(명목 9m × 1.08 = 9.7 단위)를 길이 120m 호위함으로 보고 1 단위 = 12.5m(K = 0.08).
    /// 모든 시설은 실제 크기(m)로 적고, 루트 "NavalBaseIsland"의 배율 K가 게임 단위로 줄인다. 루트 원점 = 수면.
    /// - 군항: 북쪽 안벽에서 뻗은 돌제 3개, 호위함 5척 돌격 계류(함미 = 안벽, 함수 = 외해)
    /// - 해군 항공대: 활주로 1,500m, 유도로·계류장, 격납고, 관제탑, 초계기·조기경보기·전투기·헬기
    /// 결과: Scenes/NavalBase.unity(낮), Scenes/NavalBase_Title.unity(석양), Logs/NavalBase/*.png
    /// </summary>
    internal static class NavalBaseBuilder
    {
        private const string MatFolder = NavalEditorUtil.Root + "/Art/NavalBase";
        private const string DayScene = NavalEditorUtil.Root + "/Scenes/NavalBase.unity";
        private const string TitleScene = NavalEditorUtil.Root + "/Scenes/NavalBase_Title.unity";
        private const string EscortFolder = NavalEditorUtil.Root + "/Resources/TaskForce/Escorts/";
        private const string PropFolder = NavalEditorUtil.Root + "/Resources/Environment/Props/";

        private const float K = 0.08f;          // 게임 단위 / m
        private const float WaterY = -0.9f;     // 전투 씬 수면 높이(게임 단위)
        private const float Ground = 4f;        // 섬 윗면(수면 위 m)
        private const float PierDeck = 3.5f;    // 돌제 갑판(m)

        private static readonly Dictionary<string, Material> Mats = new();
        private static System.Random _rng;
        private static Transform _root;

        [MenuItem("Naval/Art/Build Naval Base Scene")]
        public static void BuildMenu() => BuildAndCapture();

        public static void BuildAndCapture()
        {
            Mats.Clear();
            _rng = new System.Random(20261007);
            AssetDatabase.DeleteAsset(MatFolder);   // 이 빌더만 쓰는 폴더 — 매번 새로 만든다
            NavalEditorUtil.EnsureFolder(MatFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            _root = new GameObject("NavalBaseIsland").transform;
            _root.position = new Vector3(0f, WaterY, 0f);
            _root.localScale = Vector3.one * K;

            BuildWater();
            BuildIsland();
            BuildRoads();
            BuildHarbor();
            BuildBaseFacilities();
            BuildAirStation();
            BuildPerimeter();
            BuildExtras();

            var cams = SetupCameras();
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "NavalBase");
            Directory.CreateDirectory(outDir);
            foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);

            ApplyDay();
            UseCamera(cams, 0);
            EditorSceneManager.SaveScene(scene, DayScene);
            Capture(cams[0], Path.Combine(outDir, "1_island_overview.png"));
            Capture(cams[1], Path.Combine(outDir, "2_harbor_closeup.png"));

            ApplySunset();
            UseCamera(cams, 2);
            EditorSceneManager.SaveScene(scene, TitleScene, true);
            Capture(cams[2], Path.Combine(outDir, "3_title_sunset.png"));

            AssetDatabase.SaveAssets();
            Debug.Log($"[NavalBase] 완료: {DayScene}, {TitleScene}, {outDir}");
        }

        // ================================================================ 바다·섬

        private static void BuildWater()
        {
            var sea = NavalEditorUtil.Primitive("Water", PrimitiveType.Plane, Vector3.one * 900f, M("water"), _root);
            sea.transform.localPosition = Vector3.zero;
            sea.isStatic = true;
            // 섬 둘레 얕은 물(밝은 띠)
            var shallow = IslandMesh("Shallows", 1.12f, -0.4f, -0.4f, M("water_shallow"));
            shallow.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        }

        /// <summary>둥근 사각형(초타원) 섬: 잔디 윗면 + 모래 띠 + 물속 경사면.</summary>
        private static void BuildIsland()
        {
            IslandMesh("Grass", 0.97f, Ground, Ground, M("grass"));
            IslandSand();
            // 동쪽 끝 레이더 언덕
            Sph("RadarHill", _root, new Vector3(830f, Ground - 14f, -60f), new Vector3(200f, 50f, 150f), M("grass_light"));
        }

        private static Vector3[] Outline(float scale, float y)
        {
            const int n = 96;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2f;
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / 4.2f) * 1000f;
                float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / 3.2f) * 520f;
                float wobble = 1f + 0.035f * Mathf.Sin(3f * t + 0.6f) + 0.02f * Mathf.Sin(7f * t + 2f);
                pts[i] = new Vector3(x * wobble * scale, y, z * wobble * scale - 10f);
            }
            return pts;
        }

        private static GameObject IslandMesh(string name, float scale, float yIn, float yOut, Material mat)
        {
            var ring = Outline(scale, yOut);
            var verts = new List<Vector3> { new Vector3(0f, yIn, -10f) };
            verts.AddRange(ring);
            var tris = new List<int>();
            for (int i = 0; i < ring.Length; i++)
            {
                tris.Add(0); tris.Add(1 + (i + 1) % ring.Length); tris.Add(1 + i);
            }
            return MeshObject(name, verts, tris, mat);
        }

        private static void IslandSand()
        {
            var grass = Outline(0.97f, Ground);
            var top = Outline(1f, Ground - 1.2f);
            var foot = Outline(1.045f, -6f);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Strip(Vector3[] a, Vector3[] b)
            {
                int o = verts.Count;
                verts.AddRange(a); verts.AddRange(b);
                int n = a.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    tris.Add(o + i); tris.Add(o + j); tris.Add(o + n + i);
                    tris.Add(o + j); tris.Add(o + n + j); tris.Add(o + n + i);
                }
            }
            Strip(grass, top);
            MeshObject("SandBeach", verts, tris, M("sand"));
            verts.Clear(); tris.Clear();
            Strip(top, foot);
            MeshObject("SandSlope", verts, tris, M("sand_wet"));
        }

        private static GameObject MeshObject(string name, List<Vector3> verts, List<int> tris, Material mat)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            // 위에서 보이는 면이 위를 향하도록(감기 방향이 반대면 뒤집는다)
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[0].y < 0f)
            {
                for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            string path = $"{MatFolder}/MESH_base_{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        // ================================================================ 도로

        private static void BuildRoads()
        {
            var roads = Group("Roads");
            Road(roads, new Vector3(-715f, 0f, 455f), new Vector3(135f, 0f, 455f), 24f);     // 안벽 도로
            Road(roads, new Vector3(-850f, 0f, 120f), new Vector3(760f, 0f, 120f), 24f);     // 기지 동서 도로
            Road(roads, new Vector3(-80f, 0f, 455f), new Vector3(-80f, 0f, -190f), 24f);     // 남북 주 도로
            Road(roads, new Vector3(-560f, 0f, 455f), new Vector3(-560f, 0f, 120f), 18f);
            Road(roads, new Vector3(380f, 0f, 120f), new Vector3(380f, 0f, -190f), 18f);
            foreach (var p in new[] { new Vector3(-80f, 0f, 455f), new Vector3(-80f, 0f, 120f), new Vector3(-560f, 0f, 455f),
                                      new Vector3(-560f, 0f, 120f), new Vector3(380f, 0f, 120f) })
                Box("Junction", roads, p + Vector3.up * (Ground + 0.25f), new Vector3(26f, 0.5f, 26f), M("road"));
        }

        private static void Road(Transform parent, Vector3 a, Vector3 b, float width)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var rot = Quaternion.LookRotation(d.normalized);
            var road = Box("Road", parent, (a + b) * 0.5f + Vector3.up * (Ground + 0.2f), new Vector3(width, 0.4f, len), M("road"));
            road.transform.localRotation = rot;
            for (float t = 20f; t < len - 20f; t += 32f)
            {
                var dash = Box("Dash", parent, a + d.normalized * t + Vector3.up * (Ground + 0.45f), new Vector3(1f, 0.1f, 12f), M("mark_white"));
                dash.transform.localRotation = rot;
            }
        }

        // ================================================================ 군항

        private static void BuildHarbor()
        {
            var harbor = Group("Harbor");
            // 안벽(콘크리트 매립지): 북쪽 해안을 곧게 덮는다
            Box("Quay", harbor, new Vector3(-290f, Ground * 0.5f - 3f, 470f), new Vector3(860f, Ground + 6f, 180f), M("concrete"));
            Box("QuayApron", harbor, new Vector3(-290f, Ground + 0.15f, 530f), new Vector3(860f, 0.3f, 60f), M("concrete_light"));
            Box("QuayEdge", harbor, new Vector3(-290f, Ground + 0.35f, 558.5f), new Vector3(860f, 0.4f, 1.5f), M("mark_yellow"));

            // 돌제 3개(파일 위 갑판). 호위함은 돌제 옆에 함미를 안벽 쪽으로 붙이고 함수는 외해(북)로 — 돌격 계류
            float[] piers = { -620f, -400f, -180f };
            foreach (float px in piers) Pier(harbor, px);

            var berths = Group("Berths", harbor);
            Berth(berths, "ESC_CAP_T3", piers[0], -1);
            Berth(berths, "ESC_ASW_T3", piers[0], +1);
            Berth(berths, "ESC_STK_T3", piers[1], -1);
            Berth(berths, "ESC_EW_T2", piers[1], +1);
            Berth(berths, "ESC_CAP_T2", piers[2], -1);

            // 입항 중인 고속정(약 40m)
            var pb = Place(EscortFolder + "ESC_PB_T0.prefab", harbor, new Vector3(-60f, 0f, 760f), 200f, 0.36f / K);
            SitOnWater(pb);

            // 부두 크레인(높이 약 35m) 2기, 가로등
            DockCrane(harbor, new Vector3(-510f, Ground, 535f));
            DockCrane(harbor, new Vector3(-290f, Ground, 535f));
            for (float x = -700f; x <= 120f; x += 80f) LampPost(harbor, new Vector3(x, Ground, 548f));
            for (int i = 0; i < 3; i++)
                Box("ShorePower", harbor, new Vector3(piers[i] + 26f, Ground + 2f, 548f), new Vector3(6f, 4f, 4f), M("building_light"));
        }

        private static void Pier(Transform parent, float x)
        {
            var p = Group("Pier", parent);
            const float len = 170f, width = 26f, z0 = 556f;
            Box("Deck", p, new Vector3(x, PierDeck - 0.75f, z0 + len * 0.5f), new Vector3(width, 1.5f, len), M("concrete_light"));
            Box("Lane", p, new Vector3(x, PierDeck + 0.05f, z0 + len * 0.5f), new Vector3(1f, 0.1f, len - 8f), M("mark_yellow"));
            Box("Cap", p, new Vector3(x, PierDeck - 2.2f, z0 + len * 0.5f), new Vector3(width - 2f, 1.4f, len), M("concrete"));
            for (float z = z0 + 12f; z < z0 + len; z += 24f)
                foreach (float s in new[] { -1f, 1f })
                    Box("Pile", p, new Vector3(x + s * (width * 0.5f - 3f), -2f, z), new Vector3(3f, 9f, 3f), M("pile"));
            for (float z = z0 + 20f; z < z0 + len; z += 40f)
                foreach (float s in new[] { -1f, 1f })
                    Cyl("Bollard", p, new Vector3(x + s * (width * 0.5f - 1.5f), PierDeck + 0.6f, z), new Vector3(1.2f, 0.6f, 1.2f), M("bollard"));
        }

        /// <summary>돌제 옆 계류: 함미를 안벽 쪽, 함수를 외해로. 홋줄 4가닥 + 현문 사다리 + 방현재.</summary>
        private static void Berth(Transform parent, string escort, float pierX, int side)
        {
            float tierScale = escort.EndsWith("T3") ? 1.08f : 0.92f;
            float beam = 2.9f * tierScale / K;           // m
            float length = 9f * tierScale / K;
            float x = pierX + side * (13f + 2.5f + beam * 0.5f);
            float sternZ = 566f;
            var ship = Place(EscortFolder + escort + ".prefab", parent, new Vector3(x, 0f, sternZ + length * 0.5f), 0f, tierScale / K);
            if (ship == null) return;
            ship.name = $"Berth_{escort}";
            SitOnWater(ship);

            float deck = 9f;
            float hull = x - side * beam * 0.45f;   // 돌제 쪽 현측
            float pierEdge = pierX + side * 11.5f;
            var lines = Group("Mooring", parent);
            Line("BowLine", lines, new Vector3(hull, deck, sternZ + length * 0.88f), new Vector3(pierEdge, PierDeck + 1f, sternZ + length * 0.88f + 22f), 0.8f, M("rope"));
            Line("SternLine", lines, new Vector3(hull, deck, sternZ + 6f), new Vector3(pierEdge, PierDeck + 1f, sternZ - 10f), 0.8f, M("rope"));
            Line("SpringFwd", lines, new Vector3(hull, deck, sternZ + length * 0.55f), new Vector3(pierEdge, PierDeck + 1f, sternZ + length * 0.35f), 0.8f, M("rope"));
            Line("SpringAft", lines, new Vector3(hull, deck, sternZ + length * 0.4f), new Vector3(pierEdge, PierDeck + 1f, sternZ + length * 0.6f), 0.8f, M("rope"));
            Line("Brow", lines, new Vector3(pierEdge - side * 1f, PierDeck + 0.3f, sternZ + length * 0.47f),
                 new Vector3(hull, deck + 0.5f, sternZ + length * 0.47f), 3f, M("gangway"), flat: true);
            for (int i = 0; i < 3; i++)
                Cap("Fender", lines, new Vector3(pierEdge + side * 1.2f, 1.2f, sternZ + length * (0.25f + i * 0.25f)), new Vector3(2.4f, 2f, 2.4f), M("rubber"));
        }

        private static void DockCrane(Transform parent, Vector3 at)
        {
            var c = Group("DockCrane", parent);
            c.localPosition = at;
            Box("Base", c, new Vector3(0f, 2f, 0f), new Vector3(10f, 4f, 10f), M("building"));
            Cyl("Tower", c, new Vector3(0f, 14f, 0f), new Vector3(5f, 10f, 5f), M("building"));
            Box("Cab", c, new Vector3(0f, 26f, 0f), new Vector3(7f, 5f, 7f), M("building_light"));
            Box("Glass", c, new Vector3(0f, 26.5f, 3.55f), new Vector3(5f, 2f, 0.2f), M("glass"));
            var jib = Box("Jib", c, new Vector3(0f, 30f, 14f), new Vector3(3f, 2.4f, 34f), M("crane"));
            jib.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Box("Counter", c, new Vector3(0f, 28f, -6f), new Vector3(5f, 4f, 6f), M("building_dark"));
            Line("Hoist", c, new Vector3(0f, 35.5f, 29f), new Vector3(0f, 14f, 29f), 0.4f, M("bollard"));
            Box("Hook", c, new Vector3(0f, 13f, 29f), new Vector3(2f, 2f, 2f), M("mark_yellow"));
        }

        private static void LampPost(Transform parent, Vector3 at)
        {
            var lp = Group("LampPost", parent);
            lp.localPosition = at;
            Cyl("Pole", lp, new Vector3(0f, 6f, 0f), new Vector3(0.5f, 6f, 0.5f), M("pole"));
            Box("Head", lp, new Vector3(0f, 12f, 1f), new Vector3(1.2f, 0.6f, 2.5f), M("lamp", emission: 2f));
        }

        // ================================================================ 기지 시설

        private static void BuildBaseFacilities()
        {
            var b = Group("BaseFacilities");

            // 군항 지원: 창고·정비 공장(안벽 도로 남쪽)
            Shed(b, new Vector3(-820f, 0f, 395f), 70f, 45f, 16f, 0f, "building", "roof");
            Shed(b, new Vector3(-730f, 0f, 395f), 70f, 45f, 16f, 0f, "building", "roof");
            Shed(b, new Vector3(-640f, 0f, 395f), 60f, 40f, 14f, 0f, "building_light", "roof");
            Shed(b, new Vector3(-470f, 0f, 400f), 55f, 50f, 18f, 0f, "building", "roof_dark");
            Workshop(b, new Vector3(-380f, 0f, 395f), 45f, 35f);
            Workshop(b, new Vector3(-310f, 0f, 395f), 45f, 35f);

            // 컨테이너 야적장
            var yard = Group("ContainerYard", b);
            Box("YardPad", yard, new Vector3(-180f, Ground + 0.12f, 300f), new Vector3(160f, 0.25f, 200f), M("concrete"));
            string[] cols = { "box_sage", "box_slate", "box_sage_dark", "box_slate_dark" };
            for (int r = 0; r < 6; r++)
            for (int c = 0; c < 4; c++)
            {
                int stack = 1 + (int)R(0f, 2.99f);
                for (int s = 0; s < stack; s++)
                    Box("Container", yard, new Vector3(-235f + c * 38f, Ground + 1.5f + s * 2.6f, 220f + r * 30f),
                        new Vector3(24f, 2.6f, 5f), M(cols[(r + c) % cols.Length]));
            }

            // 차량 주차장(군용 트럭)
            var motor = Group("MotorPool", b);
            Box("Lot", motor, new Vector3(-700f, Ground + 0.12f, 250f), new Vector3(120f, 0.25f, 70f), M("concrete"));
            for (int i = 0; i < 8; i++)
                Truck(motor, new Vector3(-745f + (i % 4) * 26f, Ground + 0.3f, 235f + (i / 4) * 30f), 0f);
            Workshop(b, new Vector3(-620f, 0f, 250f), 40f, 30f);

            // 사령부·막사(동쪽 블록)
            Office(b, new Vector3(-420f, 0f, 220f), 60f, 26f, 3, "Headquarters");
            Office(b, new Vector3(-420f, 0f, 170f), 40f, 18f, 2, "Admin");
            FlagPole(b, new Vector3(-420f, Ground, 248f));
            for (int i = 0; i < 6; i++)
                Office(b, new Vector3(480f + (i % 3) * 80f, 0f, 260f - (i / 3) * 70f), 52f, 16f, 3, "Barracks");
            Office(b, new Vector3(720f, 0f, 30f), 60f, 30f, 2, "Mess");
            Shed(b, new Vector3(600f, 0f, -20f), 70f, 40f, 15f, 90f, "building", "roof");

            // 연료 저장소
            var fuel = Group("FuelFarm", b);
            Box("Bund", fuel, new Vector3(150f, Ground + 0.8f, 270f), new Vector3(110f, 1.6f, 80f), M("concrete"));
            for (int i = 0; i < 4; i++)
                Cyl("Tank", fuel, new Vector3(120f + (i % 2) * 55f, Ground + 6f, 250f + (i / 2) * 40f), new Vector3(28f, 6f, 28f), M("tank"));

            // 헬기장(군항 동쪽)
            var heli = AssetDatabase.LoadAssetAtPath<GameObject>(NavalEditorUtil.Root + "/Prefabs/Projectiles/HEL_Asw.prefab");
            for (int i = 0; i < 3; i++)
            {
                Vector3 pad = new Vector3(180f + i * 55f, Ground, 420f);
                Helipad(b, pad);
                if (heli != null && i != 1) PlaceStripped(heli, b, pad + Vector3.up * 0.6f, 200f + i * 20f, 1f / K, $"AswHelicopter{i}");
            }

            // 수목(시설을 피해서)
            var pine = AssetDatabase.LoadAssetAtPath<GameObject>(PropFolder + "ENV_CoastalPine.prefab");
            if (pine == null) return;
            var trees = Group("Trees", b);
            for (int i = 0; i < 420; i++)
            {
                float x = R(-900f, 900f), z = R(-480f, 420f);
                float ex = x / 940f, ez = (z + 10f) / 500f;
                if (ex * ex * ex * ex + Mathf.Abs(ez * ez * ez) > 0.82f) continue;   // 해안 밖
                if (Occupied(x, z)) continue;
                PlaceStripped(pine, trees, new Vector3(x, Ground, z), R(0f, 360f), R(0.24f, 0.34f) / K, "Pine");
            }
        }

        /// <summary>빈 땅 채우기: 탄약고(흙 덮은 반원통), 연병장·운동장, 통신 안테나, 의무대.</summary>
        private static void BuildExtras()
        {
            var e = Group("Extras");
            // 탄약고 — 활주로 동쪽 끝 북편, 흙을 덮은 반원통 4동
            for (int i = 0; i < 4; i++)
            {
                var bunker = Group("AmmoBunker", e);
                bunker.localPosition = new Vector3(520f + i * 65f, Ground, -175f);
                Rot(Cyl("Mound", bunker, Vector3.zero, new Vector3(36f, 22f, 16f), M("grass_light")), 90f, 0f, 0f);
                Box("Face", bunker, new Vector3(0f, 4f, -22f), new Vector3(30f, 8f, 2f), M("concrete"));
                Box("Door", bunker, new Vector3(0f, 3f, -23.2f), new Vector3(10f, 6f, 0.5f), M("door"));
            }
            Box("BunkerRoad", e, new Vector3(617f, Ground + 0.2f, -215f), new Vector3(280f, 0.4f, 16f), M("road"));

            // 연병장 + 육상 트랙
            var field = Group("ParadeGround", e);
            Box("Parade", field, new Vector3(-300f, Ground + 0.15f, 260f), new Vector3(120f, 0.3f, 70f), M("concrete_light"));
            Cyl("Track", field, new Vector3(230f, Ground + 0.15f, 30f), new Vector3(170f, 0.15f, 95f), M("track"));
            Cyl("Infield", field, new Vector3(230f, Ground + 0.3f, 30f), new Vector3(140f, 0.15f, 70f), M("grass_light"));

            // 통신 안테나 단지(서쪽)
            var comms = Group("CommsSite", e);
            Box("Pad", comms, new Vector3(-700f, Ground + 0.15f, -40f), new Vector3(150f, 0.3f, 110f), M("concrete"));
            for (int i = 0; i < 6; i++)
            {
                var mast = Group("Mast", comms);
                mast.localPosition = new Vector3(-750f + (i % 3) * 50f, Ground, -70f + (i / 3) * 60f);
                Box("Lattice", mast, new Vector3(0f, 18f, 0f), new Vector3(1.6f, 36f, 1.6f), M("pole"));
                Box("Top", mast, new Vector3(0f, 36.5f, 0f), new Vector3(3f, 1f, 3f), M("box_orange"));
            }
            Office(e, new Vector3(-640f, 0f, -40f), 30f, 18f, 1, "CommsBuilding");
            Sph("Satcom", e, new Vector3(-640f, Ground + 9f, -20f), new Vector3(10f, 10f, 10f), M("dome"));

            // 의무대·정비 지원(서쪽 도로 남쪽)
            Office(e, new Vector3(-700f, 0f, 80f), 50f, 20f, 2, "Medical");
            Shed(e, new Vector3(-460f, 0f, 30f), 60f, 40f, 12f, 0f, "building_light", "roof");
            Shed(e, new Vector3(-380f, 0f, 30f), 60f, 40f, 12f, 0f, "building", "roof");
            Office(e, new Vector3(-250f, 0f, 40f), 50f, 18f, 2, "Admin2");
        }

        /// <summary>시설·도로가 있는 곳(나무를 심지 않는다).</summary>
        private static bool Occupied(float x, float z)
        {
            if (z > 160f && x < 130f) return true;                          // 군항·기지 블록
            if (z > 160f && x > 420f && x < 800f) return true;              // 막사
            if (z > 200f && x > 80f && x < 360f) return true;               // 연료·헬기장
            if (z > -380f && z < -230f) return true;                          // 활주로·유도로
            if (z > -260f && z < 0f && x > -320f && x < 380f) return true;     // 계류장·격납고·관제탑
            if (z > -220f && z < -130f && x > 480f && x < 820f) return true;   // 탄약고
            if (z > -30f && z < 100f && x > 120f && x < 340f) return true;     // 운동장
            if (z > -10f && z < 60f && x > -500f && x < -210f) return true;    // 정비 지원
            if (x > 560f && x < 640f && z > -50f && z < 20f) return true;
            if (Mathf.Abs(z - 120f) < 30f || Mathf.Abs(x + 80f) < 30f || Mathf.Abs(x - 380f) < 25f || Mathf.Abs(x + 560f) < 25f) return true;
            if (x > 680f && z < 120f) return true;                          // 식당·창고·레이더
            if (x < -600f && z > -110f && z < 120f) return true;            // 통신·의무대
            return false;
        }

        // ================================================================ 해군 항공대

        private static void BuildAirStation()
        {
            var air = Group("NavalAirStation");
            const float rwZ = -330f;
            Box("Runway", air, new Vector3(-50f, Ground + 0.25f, rwZ), new Vector3(1500f, 0.5f, 45f), M("road"));
            for (float x = -740f; x < 640f; x += 50f)
                Box("CenterLine", air, new Vector3(x, Ground + 0.55f, rwZ), new Vector3(30f, 0.1f, 1.2f), M("mark_white"));
            foreach (float end in new[] { -785f, 685f })
                for (int i = 0; i < 8; i++)
                    Box("Threshold", air, new Vector3(end, Ground + 0.55f, rwZ - 17.5f + i * 5f), new Vector3(36f, 0.1f, 2.4f), M("mark_white"));
            Box("EdgeN", air, new Vector3(-50f, Ground + 0.55f, rwZ + 21f), new Vector3(1500f, 0.1f, 1f), M("mark_white"));
            Box("EdgeS", air, new Vector3(-50f, Ground + 0.55f, rwZ - 21f), new Vector3(1500f, 0.1f, 1f), M("mark_white"));

            // 유도로·연결로
            Box("Taxiway", air, new Vector3(-50f, Ground + 0.22f, -260f), new Vector3(1400f, 0.45f, 23f), M("road"));
            Box("TaxiLine", air, new Vector3(-50f, Ground + 0.5f, -260f), new Vector3(1400f, 0.1f, 0.8f), M("mark_yellow"));
            foreach (float x in new[] { -720f, -300f, 200f, 620f })
                Box("Connector", air, new Vector3(x, Ground + 0.23f, -295f), new Vector3(23f, 0.45f, 60f), M("road"));

            // 계류장
            Box("Apron", air, new Vector3(30f, Ground + 0.2f, -160f), new Vector3(560f, 0.4f, 170f), M("concrete_light"));
            for (int i = 0; i < 6; i++)
            {
                Cyl("StandMark", air, new Vector3(-150f + i * 70f, Ground + 0.45f, -185f), new Vector3(30f, 0.05f, 30f), M("mark_yellow"));
                Cyl("StandInner", air, new Vector3(-150f + i * 70f, Ground + 0.5f, -185f), new Vector3(28f, 0.05f, 28f), M("concrete_light"));
            }

            // 격납고(70×50×18m, 문은 계류장 쪽)
            Hangar(air, new Vector3(-200f, 0f, -40f));
            Hangar(air, new Vector3(-110f, 0f, -40f));
            Hangar(air, new Vector3(-20f, 0f, -40f));
            Shed(air, new Vector3(90f, 0f, -45f), 55f, 40f, 14f, 0f, "building_light", "roof");

            // 관제탑(약 30m)·소방대
            ControlTower(air, new Vector3(250f, Ground, -70f));
            Shed(air, new Vector3(330f, 0f, -60f), 45f, 30f, 10f, 0f, "building", "roof_dark");

            // 항공기: 해상초계기 3, 조기경보기 1, 전투기 4, 헬기 1
            PatrolAircraft(air, new Vector3(-150f, Ground, -185f), 180f, "PatrolAircraft1", rotodome: false);
            PatrolAircraft(air, new Vector3(-80f, Ground, -185f), 180f, "PatrolAircraft2", rotodome: false);
            PatrolAircraft(air, new Vector3(-10f, Ground, -185f), 180f, "AewAircraft", rotodome: true);
            PatrolAircraft(air, new Vector3(-420f, Ground, -260f), 90f, "PatrolAircraft3_Taxi", rotodome: false);
            Fighter(air, new Vector3(60f, Ground, -185f), 180f, "Fighter1");
            Fighter(air, new Vector3(130f, Ground, -185f), 180f, "Fighter2");
            Fighter(air, new Vector3(190f, Ground, -140f), 210f, "Fighter3");
            Fighter(air, new Vector3(220f, Ground, -170f), 210f, "Fighter4");
            var heli = AssetDatabase.LoadAssetAtPath<GameObject>(NavalEditorUtil.Root + "/Prefabs/Projectiles/HEL_Asw.prefab");
            if (heli != null)
            {
                Helipad(air, new Vector3(-280f, Ground, -150f));
                PlaceStripped(heli, air, new Vector3(-280f, Ground + 0.6f, -150f), 150f, 1f / K, "AswHelicopter_Apron");
            }
            Truck(air, new Vector3(20f, Ground + 0.4f, -130f), 90f, "fuel");
            Truck(air, new Vector3(-120f, Ground + 0.4f, -125f), 0f, "box_orange");

            // 레이더 돔(언덕 위)
            var radar = Group("RadarDome", air);
            radar.localPosition = new Vector3(830f, Ground + 10f, -60f);
            Box("Base", radar, new Vector3(0f, 4f, 0f), new Vector3(24f, 8f, 24f), M("building_light"));
            Cyl("Neck", radar, new Vector3(0f, 10f, 0f), new Vector3(10f, 3f, 10f), M("building"));
            Sph("Dome", radar, new Vector3(0f, 18f, 0f), new Vector3(20f, 18f, 20f), M("dome"));
        }

        private static void Hangar(Transform parent, Vector3 at)
        {
            var h = Group("Hangar", parent);
            h.localPosition = at;
            Shed(h, Vector3.zero, 70f, 50f, 18f, 0f, "building", "roof", withDoor: false);
            Box("Door", h, new Vector3(0f, Ground + 7.5f, -25.1f), new Vector3(56f, 15f, 0.6f), M("door"));
            for (int i = -3; i <= 3; i++)
                Box("Seam", h, new Vector3(i * 8f, Ground + 7.5f, -25.5f), new Vector3(0.4f, 15f, 0.3f), M("building_dark"));
            for (int i = -2; i <= 2; i++)
                Box("Skylight", h, new Vector3(i * 12f, Ground + 18f + 4.6f, 0f), new Vector3(3f, 0.4f, 48f), M("building_light"));
        }

        private static void ControlTower(Transform parent, Vector3 at)
        {
            var t = Group("ControlTower", parent);
            t.localPosition = at;
            Box("Base", t, new Vector3(0f, 4f, 0f), new Vector3(26f, 8f, 20f), M("building_light"));
            Box("Shaft", t, new Vector3(0f, 16f, 0f), new Vector3(9f, 18f, 9f), M("building"));
            Box("CabFloor", t, new Vector3(0f, 25.5f, 0f), new Vector3(15f, 1f, 15f), M("building_dark"));
            Box("Cab", t, new Vector3(0f, 28f, 0f), new Vector3(13f, 4f, 13f), M("glass"));
            Box("Roof", t, new Vector3(0f, 30.5f, 0f), new Vector3(16f, 1f, 16f), M("building_dark"));
            Cyl("Mast", t, new Vector3(0f, 34f, 0f), new Vector3(0.6f, 3f, 0.6f), M("pole"));
        }

        private static void Helipad(Transform parent, Vector3 at)
        {
            var p = Group("Helipad", parent);
            p.localPosition = at;
            Cyl("Pad", p, new Vector3(0f, 0.3f, 0f), new Vector3(36f, 0.3f, 36f), M("concrete_light"));
            Cyl("Ring", p, new Vector3(0f, 0.62f, 0f), new Vector3(30f, 0.02f, 30f), M("mark_yellow"));
            Cyl("RingIn", p, new Vector3(0f, 0.64f, 0f), new Vector3(28f, 0.02f, 28f), M("concrete_light"));
            Box("H_L", p, new Vector3(-3.5f, 0.68f, 0f), new Vector3(1.6f, 0.05f, 11f), M("mark_yellow"));
            Box("H_R", p, new Vector3(3.5f, 0.68f, 0f), new Vector3(1.6f, 0.05f, 11f), M("mark_yellow"));
            Box("H_M", p, new Vector3(0f, 0.68f, 0f), new Vector3(7f, 0.05f, 1.6f), M("mark_yellow"));
        }

        /// <summary>4발 터보프롭 해상초계기(날개폭 약 30m) / 조기경보기(로토돔). 기수 = +Z.</summary>
        private static void PatrolAircraft(Transform parent, Vector3 at, float yaw, string name, bool rotodome)
        {
            var a = Group(name, parent);
            a.localPosition = at;
            a.localRotation = Quaternion.Euler(0f, yaw, 0f);
            a.localScale = Vector3.one * 3.2f;   // 아래 치수(폭 9.6) × 3.2 ≈ 30m
            var body = M("aircraft");
            const float y = 1.05f;
            Rot(Cap("Fuselage", a, new Vector3(0f, y, 0f), new Vector3(1.05f, 4.1f, 1.05f), body), 90f, 0f, 0f);
            Rot(Cap("Nose", a, new Vector3(0f, y - 0.05f, 3.65f), new Vector3(0.8f, 0.6f, 0.8f), M("aircraft_dark")), 90f, 0f, 0f);
            Box("Cockpit", a, new Vector3(0f, y + 0.32f, 3.1f), new Vector3(0.7f, 0.25f, 0.6f), M("glass"));
            Box("Wing", a, new Vector3(0f, y + 0.25f, 0.5f), new Vector3(9.6f, 0.14f, 1.35f), body);
            foreach (float ex in new[] { -3.2f, -1.6f, 1.6f, 3.2f })
            {
                Rot(Cap("Nacelle", a, new Vector3(ex, y + 0.12f, 1.1f), new Vector3(0.38f, 0.75f, 0.38f), body), 90f, 0f, 0f);
                Rot(Cyl("Prop", a, new Vector3(ex, y + 0.12f, 1.88f), new Vector3(1.3f, 0.02f, 1.3f), M("prop_disc")), 90f, 0f, 0f);
            }
            Box("Fin", a, new Vector3(0f, y + 1.05f, -3.5f), new Vector3(0.12f, 1.6f, 1.2f), body);
            Box("Stabilizer", a, new Vector3(0f, y + 0.35f, -3.7f), new Vector3(3.6f, 0.1f, 0.85f), body);
            if (rotodome)
            {
                Box("DomeStrut", a, new Vector3(0f, y + 0.85f, -0.6f), new Vector3(0.2f, 0.6f, 0.5f), body);
                Cyl("Rotodome", a, new Vector3(0f, y + 1.25f, -0.6f), new Vector3(3f, 0.18f, 3f), M("aircraft_dark"));
            }
            else Rot(Cyl("MadBoom", a, new Vector3(0f, y + 0.1f, -4.6f), new Vector3(0.18f, 0.6f, 0.18f), M("aircraft_dark")), 90f, 0f, 0f);
            Gear(a, new Vector3(0f, 0f, 2.6f), 0.6f);
            Gear(a, new Vector3(-1.6f, 0f, 0.6f), 0.6f);
            Gear(a, new Vector3(1.6f, 0f, 0.6f), 0.6f);
        }

        /// <summary>함재 전투기(길이 약 17m). 기수 = +Z.</summary>
        private static void Fighter(Transform parent, Vector3 at, float yaw, string name)
        {
            var a = Group(name, parent);
            a.localPosition = at;
            a.localRotation = Quaternion.Euler(0f, yaw, 0f);
            a.localScale = Vector3.one * 3.4f;   // 길이 약 5 × 3.4 ≈ 17m
            var body = M("aircraft_fighter");
            const float y = 0.62f;
            Rot(Cap("Fuselage", a, new Vector3(0f, y, 0f), new Vector3(0.62f, 2.3f, 0.6f), body), 90f, 0f, 0f);
            Rot(Cap("Nose", a, new Vector3(0f, y, 2.15f), new Vector3(0.36f, 0.5f, 0.36f), M("aircraft_dark")), 90f, 0f, 0f);
            Sph("Canopy", a, new Vector3(0f, y + 0.3f, 1.05f), new Vector3(0.38f, 0.3f, 0.95f), M("glass"));
            foreach (float s in new[] { -1f, 1f })
            {
                Rot(Box("Wing", a, new Vector3(s * 1.25f, y - 0.05f, -0.35f), new Vector3(2.2f, 0.08f, 1.5f), body), 0f, s * 28f, 0f);
                Rot(Box("Tail", a, new Vector3(s * 0.45f, y + 0.55f, -1.75f), new Vector3(0.07f, 0.95f, 0.85f), body), 0f, 0f, s * -18f);
                Rot(Box("Stab", a, new Vector3(s * 0.85f, y - 0.05f, -2f), new Vector3(1.1f, 0.06f, 0.7f), body), 0f, s * 25f, 0f);
                Box("Intake", a, new Vector3(s * 0.42f, y - 0.05f, 0.5f), new Vector3(0.28f, 0.38f, 1.2f), M("aircraft_dark"));
            }
            Gear(a, new Vector3(0f, 0f, 1.5f), 0.35f);
            Gear(a, new Vector3(-0.6f, 0f, -0.4f), 0.35f);
            Gear(a, new Vector3(0.6f, 0f, -0.4f), 0.35f);
        }

        private static void Gear(Transform parent, Vector3 at, float h)
        {
            Cyl("Strut", parent, at + new Vector3(0f, h * 0.6f, 0f), new Vector3(0.08f, h * 0.4f, 0.08f), M("bollard"));
            Rot(Cyl("Wheel", parent, at + new Vector3(0f, h * 0.22f, 0f), new Vector3(h * 0.44f, 0.06f, h * 0.44f), M("rubber")), 0f, 0f, 90f);
        }

        // ================================================================ 해안 경계

        private static void BuildPerimeter()
        {
            var per = Group("Perimeter");
            var ring = Outline(0.93f, Ground);
            for (int i = 4; i < ring.Length; i += 12)
            {
                var p = ring[i];
                if (p.z > 380f && p.x < 140f) continue;   // 안벽 쪽은 생략
                WatchTower(per, p);
            }
            Shed(per, new Vector3(-880f, 0f, -150f), 16f, 12f, 4f, 30f, "building_light", "roof_dark");
            Shed(per, new Vector3(860f, 0f, 200f), 16f, 12f, 4f, -40f, "building_light", "roof_dark");
        }

        private static void WatchTower(Transform parent, Vector3 at)
        {
            var t = Group("WatchTower", parent);
            t.localPosition = at;
            foreach (float x in new[] { -2.2f, 2.2f })
            foreach (float z in new[] { -2.2f, 2.2f })
                Box("Leg", t, new Vector3(x, 5f, z), new Vector3(0.5f, 10f, 0.5f), M("pole"));
            Box("Cabin", t, new Vector3(0f, 11.5f, 0f), new Vector3(6f, 3f, 6f), M("building_light"));
            Box("Window", t, new Vector3(0f, 12f, 0f), new Vector3(6.1f, 1f, 6.1f), M("glass"));
            Box("Roof", t, new Vector3(0f, 13.3f, 0f), new Vector3(7f, 0.6f, 7f), M("roof_dark"));
        }

        // ================================================================ 건물 도우미

        /// <summary>박공지붕 창고: 벽 상자 + 납작한 마름모 프리즘 지붕(부모 Y 배율로 경사를 만든다).</summary>
        private static void Shed(Transform parent, Vector3 at, float w, float d, float h, float yaw, string wall, string roof,
                                 bool withDoor = true)
        {
            var s = Group("Shed", parent);
            s.localPosition = new Vector3(at.x, 0f, at.z);
            s.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Walls", s, new Vector3(0f, Ground + h * 0.5f, 0f), new Vector3(w, h, d), M(wall));
            var pitch = Group("RoofPitch", s);
            pitch.localPosition = new Vector3(0f, Ground + h, 0f);
            pitch.localScale = new Vector3(1f, Mathf.Tan(14f * Mathf.Deg2Rad), 1f);
            float side = (w + 2.5f) / Mathf.Sqrt(2f);
            var prism = Box("Roof", pitch, Vector3.zero, new Vector3(side, side, d + 2.5f), M(roof));
            prism.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            if (withDoor)
            {
                Box("Door", s, new Vector3(0f, Ground + h * 0.35f, -d * 0.5f - 0.2f), new Vector3(w * 0.35f, h * 0.7f, 0.4f), M("door"));
                Box("Vent", s, new Vector3(w * 0.36f, Ground + 1.5f, -d * 0.5f - 1.2f), new Vector3(3f, 3f, 2f), M("building_light"));
            }
        }

        /// <summary>정비동: 셔터 문이 달린 낮은 건물.</summary>
        private static void Workshop(Transform parent, Vector3 at, float w, float d)
        {
            Shed(parent, at, w, d, 9f, 0f, "building_light", "roof", withDoor: false);
            var g = Group("Shutters", parent);
            g.localPosition = new Vector3(at.x, 0f, at.z);
            for (int i = 0; i < 2; i++)
            {
                Box("Shutter", g, new Vector3(-w * 0.2f + i * w * 0.4f, Ground + 3.2f, -d * 0.5f - 0.2f), new Vector3(w * 0.28f, 6.4f, 0.4f), M("door"));
                for (int k = 0; k < 5; k++)
                    Box("Slat", g, new Vector3(-w * 0.2f + i * w * 0.4f, Ground + 1f + k * 1.2f, -d * 0.5f - 0.45f), new Vector3(w * 0.28f, 0.2f, 0.1f), M("building_dark"));
            }
        }

        /// <summary>층수만큼 창 띠가 있는 평지붕 건물(막사·사령부).</summary>
        private static void Office(Transform parent, Vector3 at, float w, float d, int floors, string name)
        {
            var o = Group(name, parent);
            o.localPosition = new Vector3(at.x, 0f, at.z);
            float h = floors * 4f;
            Box("Body", o, new Vector3(0f, Ground + h * 0.5f, 0f), new Vector3(w, h, d), M("building_light"));
            Box("Parapet", o, new Vector3(0f, Ground + h + 0.4f, 0f), new Vector3(w + 0.8f, 0.8f, d + 0.8f), M("roof"));
            Box("RoofUnit", o, new Vector3(w * 0.25f, Ground + h + 1.5f, 0f), new Vector3(6f, 2f, 4f), M("building"));
            for (int f = 0; f < floors; f++)
                foreach (float s in new[] { -1f, 1f })
                    Box("Windows", o, new Vector3(0f, Ground + 2.2f + f * 4f, s * (d * 0.5f + 0.05f)), new Vector3(w - 4f, 1.4f, 0.2f), M("glass"));
            Box("Entrance", o, new Vector3(0f, Ground + 1.6f, -d * 0.5f - 1.5f), new Vector3(6f, 3.2f, 3f), M("building"));
        }

        private static void Truck(Transform parent, Vector3 at, float yaw, string paint = "olive")
        {
            var v = Group("Truck", parent);
            v.localPosition = at;
            v.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Cargo", v, new Vector3(0f, 1.8f, -1.2f), new Vector3(2.5f, 2.6f, 5f), M(paint));
            Box("Cab", v, new Vector3(0f, 1.5f, 2.6f), new Vector3(2.4f, 2.2f, 2.2f), M(paint == "olive" ? "olive_dark" : "building_light"));
            Box("Window", v, new Vector3(0f, 2f, 3.71f), new Vector3(2f, 0.8f, 0.05f), M("glass"));
            foreach (float wx in new[] { -1.2f, 1.2f })
            foreach (float wz in new[] { -2.5f, 0f, 2.6f })
                Rot(Cyl("Wheel", v, new Vector3(wx, 0.5f, wz), new Vector3(1f, 0.2f, 1f), M("rubber")), 0f, 0f, 90f);
        }

        private static void FlagPole(Transform parent, Vector3 at)
        {
            var f = Group("FlagPole", parent);
            f.localPosition = at;
            Cyl("Pole", f, new Vector3(0f, 7f, 0f), new Vector3(0.4f, 7f, 0.4f), M("mark_white"));
            Box("Flag", f, new Vector3(2f, 12.5f, 0f), new Vector3(4f, 2.6f, 0.1f), M("flag_navy"));
        }

        // ================================================================ 조명·카메라·캡처

        private static Camera[] SetupCameras()
        {
            var day = Camera.main;
            day.name = "Cam_Overview";
            var close = new GameObject("Cam_HarborCloseup", typeof(Camera)).GetComponent<Camera>();
            var title = new GameObject("Cam_Title", typeof(Camera)).GetComponent<Camera>();
            var cams = new[] { day, close, title };
            foreach (var c in cams) { c.farClipPlane = 1500f; c.nearClipPlane = 0.3f; c.clearFlags = CameraClearFlags.Skybox; }

            // 섬 전체(참고 이미지 2): 북서 바다 위 높은 경사 시점
            Aim(day, new Vector3(-1350f, 1650f, 2000f), new Vector3(20f, 0f, 60f), 25f);
            // 군항 근경(참고 이미지 1): 돌제와 계류 함정, 뒤로 기지 블록
            Aim(close, new Vector3(-760f, 380f, 1020f), new Vector3(-330f, 0f, 470f), 38f);
            // 시작 화면: 외해 낮은 시점에서 출격 대기 중인 함수들
            Aim(title, new Vector3(-250f, 140f, 960f), new Vector3(-420f, 75f, 520f), 42f);
            return cams;
        }

        private static void Aim(Camera cam, Vector3 posM, Vector3 targetM, float fov)
        {
            cam.transform.position = _root.TransformPoint(posM);
            cam.transform.LookAt(_root.TransformPoint(targetM));
            cam.fieldOfView = fov;
        }

        private static void UseCamera(Camera[] cams, int index)
        {
            for (int i = 0; i < cams.Length; i++)
            {
                cams[i].enabled = i == index;
                cams[i].tag = i == index ? "MainCamera" : "Untagged";
                var listener = cams[i].GetComponent<AudioListener>();
                if (listener != null) listener.enabled = i == index;
            }
        }

        private static void ApplyDay()
        {
            var sun = Object.FindFirstObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            RenderSettings.skybox = Sky(false);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.85f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.74f, 0.78f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.5f, 0.5f);
            RenderSettings.fog = false;
            RenderSettings.sun = sun;
            SetWater(new Color(0.42f, 0.65f, 0.77f), new Color(0.55f, 0.76f, 0.84f));
            SetNightLights(false);
        }

        private static void ApplySunset()
        {
            var sun = Object.FindFirstObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(6f, 40f, 0f);
            sun.color = new Color(1f, 0.6f, 0.34f);
            sun.intensity = 1.6f;
            sun.shadowStrength = 0.7f;
            RenderSettings.skybox = Sky(true);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.55f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.65f, 0.45f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.9f, 0.62f, 0.42f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 380f;
            RenderSettings.sun = sun;
            SetWater(new Color(0.36f, 0.42f, 0.55f), new Color(0.5f, 0.5f, 0.58f));
            SetNightLights(true);
        }

        private static void SetWater(Color deep, Color shallow)
        {
            M("water").SetColor("_BaseColor", deep);
            M("water_shallow").SetColor("_BaseColor", shallow);
        }

        private static Material Sky(bool sunset)
        {
            string path = $"{MatFolder}/SKY_base_{(sunset ? "sunset" : "day")}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(mat, path);
            }
            // 절차 하늘의 Sky Tint는 산란 색을 반대로 거른다 — 석양은 회색 틴트 + 두꺼운 대기로 낸다
            mat.SetColor("_SkyTint", sunset ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.55f, 0.6f, 0.7f));
            mat.SetFloat("_Exposure", sunset ? 1.35f : 1.2f);
            mat.SetFloat("_AtmosphereThickness", sunset ? 2.1f : 0.75f);
            mat.SetFloat("_SunSize", 0.05f);
            mat.SetColor("_GroundColor", new Color(0.45f, 0.62f, 0.72f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetNightLights(bool on)
        {
            var holder = _root.Find("NightLights");
            if (holder == null)
            {
                holder = Group("NightLights");
                for (float x = -680f; x <= 100f; x += 130f)
                {
                    var l = new GameObject("QuayLight", typeof(Light)).GetComponent<Light>();
                    l.transform.SetParent(holder, false);
                    l.transform.localPosition = new Vector3(x, Ground + 11f, 545f);
                    l.type = LightType.Point;
                    l.range = 6f;
                    l.intensity = 4f;
                    l.color = new Color(1f, 0.78f, 0.5f);
                }
            }
            holder.gameObject.SetActive(on);
        }

        private static void Capture(Camera cam, string file)
        {
            const int w = 2560, h = 1440;
            // 섬 전체 시점은 기본 그림자 거리 밖이라 캡처 동안만 늘린다(프로젝트 설정은 되돌린다)
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float oldShadow = urp != null ? urp.shadowDistance : 0f;
            if (urp != null) urp.shadowDistance = 400f;

            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { msaaSamples = 8, sRGB = true };
            var rt = new RenderTexture(desc);
            bool wasEnabled = cam.enabled;
            cam.enabled = true;
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = null;
            cam.enabled = wasEnabled;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            if (urp != null) urp.shadowDistance = oldShadow;
            Debug.Log($"[NavalBase] 캡처: {file}");
        }

        // ================================================================ 공통 도우미

        private static Material M(string name, float emission = 0f)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            Color c = name switch
            {
                "water" => new Color(0.42f, 0.65f, 0.77f),
                "water_shallow" => new Color(0.55f, 0.76f, 0.84f),
                "grass" => new Color(0.6f, 0.72f, 0.52f),
                "grass_light" => new Color(0.64f, 0.76f, 0.56f),
                "sand" => new Color(0.89f, 0.86f, 0.76f),
                "sand_wet" => new Color(0.42f, 0.44f, 0.42f),
                "road" => new Color(0.55f, 0.58f, 0.61f),
                "concrete" => new Color(0.66f, 0.69f, 0.7f),
                "concrete_light" => new Color(0.8f, 0.82f, 0.82f),
                "mark_white" => new Color(0.95f, 0.95f, 0.93f),
                "mark_yellow" => new Color(0.9f, 0.74f, 0.25f),
                "pile" => new Color(0.25f, 0.27f, 0.29f),
                "bollard" => new Color(0.22f, 0.24f, 0.26f),
                "pole" => new Color(0.4f, 0.44f, 0.48f),
                "rope" => new Color(0.85f, 0.8f, 0.64f),
                "gangway" => new Color(0.62f, 0.65f, 0.68f),
                "rubber" => new Color(0.12f, 0.12f, 0.13f),
                "crane" => new Color(0.92f, 0.76f, 0.3f),
                "building" => new Color(0.62f, 0.68f, 0.74f),
                "building_light" => new Color(0.74f, 0.78f, 0.82f),
                "building_dark" => new Color(0.4f, 0.45f, 0.5f),
                "roof" => new Color(0.5f, 0.57f, 0.65f),
                "roof_dark" => new Color(0.4f, 0.47f, 0.55f),
                "door" => new Color(0.46f, 0.52f, 0.58f),
                "glass" => new Color(0.28f, 0.38f, 0.48f),
                "tank" => new Color(0.86f, 0.88f, 0.88f),
                "dome" => new Color(0.93f, 0.94f, 0.94f),
                "box_sage" => new Color(0.6f, 0.68f, 0.6f),
                "box_sage_dark" => new Color(0.5f, 0.58f, 0.52f),
                "box_slate" => new Color(0.5f, 0.6f, 0.68f),
                "box_slate_dark" => new Color(0.42f, 0.5f, 0.6f),
                "box_orange" => new Color(0.9f, 0.6f, 0.25f),
                "olive" => new Color(0.42f, 0.5f, 0.34f),
                "olive_dark" => new Color(0.34f, 0.41f, 0.28f),
                "fuel" => new Color(0.9f, 0.8f, 0.3f),
                "aircraft" => new Color(0.8f, 0.83f, 0.86f),
                "aircraft_fighter" => new Color(0.66f, 0.71f, 0.76f),
                "aircraft_dark" => new Color(0.36f, 0.4f, 0.45f),
                "prop_disc" => new Color(0.3f, 0.3f, 0.32f),
                "flag_navy" => new Color(0.15f, 0.25f, 0.5f),
                "lamp" => new Color(1f, 0.86f, 0.62f),
                "track" => new Color(0.78f, 0.5f, 0.42f),
                _ => Color.magenta,
            };
            float smooth = name switch { "glass" => 0.85f, "water" or "water_shallow" => 0.55f, _ when name.StartsWith("aircraft") => 0.4f, _ => 0.12f };
            string path = $"{MatFolder}/MAT_base_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", smooth);
            mat.SetFloat("_Metallic", 0f);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            Mats[name] = mat;
            return mat;
        }

        private static float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        private static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : _root, false);
            return t;
        }

        private static GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = NavalEditorUtil.Primitive(name, type, scale, mat, parent);
            go.transform.localPosition = pos;
            return go;
        }

        private static GameObject Box(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(n, PrimitiveType.Cube, p, pos, s, m);
        private static GameObject Cyl(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(n, PrimitiveType.Cylinder, p, pos, s, m);
        private static GameObject Cap(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(n, PrimitiveType.Capsule, p, pos, s, m);
        private static GameObject Sph(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(n, PrimitiveType.Sphere, p, pos, s, m);

        private static GameObject Rot(GameObject go, float x, float y, float z)
        {
            go.transform.localRotation = Quaternion.Euler(x, y, z);
            return go;
        }

        /// <summary>두 점(부모 기준)을 잇는 막대. flat이면 폭 = thickness, 두께 0.4.</summary>
        private static void Line(string n, Transform p, Vector3 a, Vector3 b, float thickness, Material m, bool flat = false)
        {
            var go = Box(n, p, (a + b) * 0.5f, Vector3.one, m);
            Vector3 d = b - a;
            go.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            go.transform.localScale = new Vector3(thickness, flat ? 0.4f : thickness, d.magnitude);
        }

        private static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[NavalBase] 프리팹 없음: {path}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>렌더러 경계의 바닥이 흘수(약 3.5m)만큼 물에 잠기도록 높이를 맞춘다.</summary>
        private static void SitOnWater(GameObject ship)
        {
            if (ship == null) return;
            var rs = ship.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            float minY = float.MaxValue;
            foreach (var r in rs) minY = Mathf.Min(minY, r.bounds.min.y);
            float draft = 3.5f * K;
            ship.transform.position += Vector3.up * (_root.position.y - draft - minY);
        }

        private static void PlaceStripped(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale, string name)
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
        }
    }
}
