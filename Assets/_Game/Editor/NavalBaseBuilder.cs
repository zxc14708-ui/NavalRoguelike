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
    /// 해군 기지 섬 씬 빌더(2026-10-07 2차: 파스텔 로우폴리 섬 + 실제 비례, 2026-10-09 3차: 실제 기지 배치·밀도·대비).
    /// 축척: 호위함 T3(명목 9m × 1.08 = 9.7 단위)를 길이 120m 호위함으로 보고 1 단위 = 12.5m(K = 0.08).
    /// 모든 시설은 실제 크기(m)로 적고, 루트 "NavalBaseIsland"의 배율 K가 게임 단위로 줄인다. 루트 원점 = 수면.
    /// 배치(NavalBaseBuilder.Layout.cs)는 실제 해군기지·해군항공기지 구성을 따른다:
    ///   북쪽 해안 = 군항(돌제 4개, 안벽 크레인·지원 설비, 항만 지원정 정박지·항무청·연료 부두, 부유식 독),
    ///   그 뒤 = 함정 정비창·보급 창고 / 사령부·행정 / 숙소·식당·체육 시설,
    ///   남쪽 = 해군 항공대(활주로·평행 유도로·계류장, 격납고 줄, 관제탑·소방대, 엄체호, 헬기 계류장,
    ///          항공유 저장소·탄약고는 비행장 가장자리에 떨어뜨림, 레이더 기지).
    /// 모델 키트는 NavalBaseBuilder.Kit.cs. 같은 재질끼리 메시를 합쳐(CombineStatic) 시작 화면에서 실시간으로 그려도 가볍게 한다.
    /// 결과: Scenes/NavalBase.unity(낮), Scenes/NavalBase_Title.unity(석양), Logs/NavalBase/*.png
    /// 시작 화면 프리팹은 이 씬에서 NavalBaseMenuBuilder(Naval/Art/Prepare Harbor Menu)가 만든다.
    /// </summary>
    internal static partial class NavalBaseBuilder
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
            BuildLayout();
            CombineStatic();

            var cams = SetupCameras();
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "NavalBase");
            Directory.CreateDirectory(outDir);
            foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);

            ApplyDay();
            UseCamera(cams, 0);
            EditorSceneManager.SaveScene(scene, DayScene);
            Capture(cams[0], Path.Combine(outDir, "1_island_overview.png"));
            Capture(cams[1], Path.Combine(outDir, "2_harbor_closeup.png"));
            CaptureMenuView(cams[0], Path.Combine(outDir, "0_start_screen_view.png"));
            ApplyDay();

            ApplySunset();
            UseCamera(cams, 2);
            EditorSceneManager.SaveScene(scene, TitleScene, true);
            Capture(cams[2], Path.Combine(outDir, "3_title_sunset.png"));

            // 바다 재질은 두 씬이 함께 쓴다 — 시작 화면 프리팹(낮 씬에서 만듦)이 석양 바다색을 물려받지 않게 낮 색으로 되돌린다
            ApplyDay();

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
            // 동쪽 끝 레이더 언덕(레이더 기지 자리)
            Sph("RadarHill", _root, new Vector3(830f, Ground - 14f, 20f), new Vector3(190f, 46f, 150f), M("grass_dark"));
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
            SetWater(new Color(0.18f, 0.5f, 0.74f), new Color(0.34f, 0.68f, 0.8f));
            SetNightLights(false);
        }

        /// <summary>
        /// 시작 화면(NavalBaseMenu)과 같은 조명·카메라로 섬 전체를 찍는다: 평면 주변광 (0.63, 0.71, 0.77),
        /// 해 (40°, 150°), 배경색 단색. 시작 화면 프리팹은 이 씬에서 만들어지므로 결과가 거의 같다.
        /// </summary>
        private static void CaptureMenuView(Camera cam, string file)
        {
            var sun = Object.FindFirstObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadowStrength = 1f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.63f, 0.71f, 0.77f);
            var flags = cam.clearFlags;
            var bg = cam.backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.65f, 0.77f);
            Capture(cam, file);
            cam.clearFlags = flags;
            cam.backgroundColor = bg;
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
            SetWater(new Color(0.36f, 0.42f, 0.55f), new Color(0.5f, 0.5f, 0.58f), 0.05f);
            SetNightLights(true);
        }

        /// <summary>
        /// 바다색. 시작 화면은 평면 주변광이라 물이 회보라색으로 죽는다 — 약한 자체 발광을 더해 파란색을 유지한다.
        /// </summary>
        private static void SetWater(Color deep, Color shallow, float glow = 0.28f)
        {
            foreach (var (name, c) in new[] { ("water", deep), ("water_shallow", shallow) })
            {
                var m = M(name);
                m.SetColor("_BaseColor", c);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(m);
            }
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
            Color c = Palette.TryGetValue(name, out var pc) ? pc : Color.magenta;
            float smooth = name switch
            {
                "glass" or "glass_blue" or "cockpit" => 0.85f,
                "water" or "water_shallow" => 0.55f,
                _ when name.StartsWith("aircraft") || name.StartsWith("car_") => 0.45f,
                _ when name.StartsWith("steel") || name.StartsWith("roof_metal") => 0.3f,
                _ => 0.12f,
            };
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
