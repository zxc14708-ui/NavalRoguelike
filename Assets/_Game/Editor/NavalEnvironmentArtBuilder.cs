using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Game.World;

namespace Game.EditorTools
{
    /// <summary>
    /// Codex 섬·해안 아트(ArtSource/EnvironmentVarietyPack_v3 · CoastalObjectsPack_v4)를 게임에 연결한다. 씬·기존 프리팹은 건드리지 않는다.
    ///   1. Art/Models/Environment/Islands·Props/*.fbx의 임포트 설정을 맞추고 재질을 이름으로 연결한다
    ///      (MAT_Island* = 기존 섬 재질, Gunmetal·Radar Glass·Warning Yellow = 기존 팔레트, ENV_* = 같은 색의 새 URP Lit 재질).
    ///   2. Resources/Environment/Islands·Props/ENV_*.prefab을 만든다. 프리팹 루트에 EnvironmentArt(해안선·높이·장식·움직임)를 붙이고,
    ///      섬은 지형(TerrainShell)·해안 바위·난파선 정점에서 각도 × 반경 띠마다 볼록 조각을 만들어 "Collision"에 넣는다
    ///      (환초 석호는 둘러싸인 못이라 메운다 — 적이 그 안에 생기지 않게).
    ///      잔교·부잔교·방파제·어선·해식 아치에는 낮은 상자 판정을 준다.
    /// 런타임(IslandBuilder.Art)은 이 프리팹이 없으면 기존 절차형 섬만 만든다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalEnvironmentArtBuilder.RunBatch
    /// </summary>
    public static class NavalEnvironmentArtBuilder
    {
        private const string IslandModelDir = "Assets/_Game/Art/Models/Environment/Islands";
        private const string PropModelDir = "Assets/_Game/Art/Models/Environment/Props";
        private const string CollisionDir = "Assets/_Game/Art/Models/Environment/Collision";
        private const string PaletteDir = "Assets/_Game/Art/Models/Materials";
        private const string IslandMaterialDir = "Assets/_Game/Resources/Islands";
        private const string OwnMaterialDir = "Assets/_Game/Art/Models/Materials/Environment";
        private const string IslandPrefabDir = "Assets/_Game/Resources/Environment/Islands";
        private const string PropPrefabDir = "Assets/_Game/Resources/Environment/Props";

        private const float SeaY = -0.9f, BaseY = -3f;
        private const int Sectors = 20;
        private static readonly float[] Bands = { 0f, 0.45f, 0.75f, 1f };

        private static readonly HashSet<string> PaletteNames = new() { "Gunmetal", "Radar Glass", "Warning Yellow" };

        /// <summary>Codex manifest의 ENV_ 재질색(FBX에 색이 없을 때 쓴다).</summary>
        private static readonly Dictionary<string, Color> EnvColors = new()
        {
            { "ENV_Bark", new Color(0.24f, 0.18f, 0.12f) },
            { "ENV_Concrete", new Color(0.50f, 0.52f, 0.48f) },
            { "ENV_Lamp", new Color(1.00f, 0.73f, 0.27f) },
            { "ENV_Moss", new Color(0.25f, 0.38f, 0.18f) },
            { "ENV_PineHighlight", new Color(0.20f, 0.36f, 0.20f) },
            { "ENV_RockStrata", new Color(0.46f, 0.44f, 0.40f) },
            { "ENV_SandLight", new Color(0.77f, 0.69f, 0.52f) },
            { "ENV_WetRock", new Color(0.16f, 0.20f, 0.20f) },
            { "ENV_Wood", new Color(0.42f, 0.31f, 0.20f) },
            { "ENV_RustLight", new Color(0.52f, 0.26f, 0.11f) },
            { "ENV_SealMuzzle", new Color(0.44f, 0.46f, 0.42f) },
        };

        public static void RunBatch()
        {
            BuildAll();
            var report = Report();
            Debug.Log(report);
            string outPath = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-envReport") outPath = args[i + 1];
            if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, report, Encoding.UTF8);
        }

        [MenuItem("Naval/Art/Build Environment Prefabs", priority = 41)]
        public static void BuildAll()
        {
            EnsureFolder(OwnMaterialDir);
            EnsureFolder(CollisionDir);
            EnsureFolder(IslandPrefabDir);
            EnsureFolder(PropPrefabDir);

            foreach (var path in Fbx(IslandModelDir)) ImportAndRemap(path);
            foreach (var path in Fbx(PropModelDir)) ImportAndRemap(path);
            AssetDatabase.SaveAssets();

            foreach (var path in Fbx(IslandModelDir)) MakeIsland(path);
            foreach (var path in Fbx(PropModelDir)) MakeProp(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Env Art] 섬·해안 소품 프리팹 생성 완료");
        }

        private static IEnumerable<string> Fbx(string dir)
        {
            if (!Directory.Exists(dir)) yield break;
            foreach (var f in Directory.GetFiles(dir, "*.fbx", SearchOption.TopDirectoryOnly))
                yield return f.Replace('\\', '/');
        }

        // ------------------------------------------------------------ 임포트 · 재질

        private static void ImportAndRemap(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning($"[Env Art] 임포터 없음: {path}"); return; }

            bool dirty = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                apply(wanted);
                dirty = true;
            }
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportStandard, v => importer.materialImportMode = v);
            Set(importer.materialLocation, ModelImporterMaterialLocation.InPrefab, v => importer.materialLocation = v);
            Set(importer.bakeAxisConversion, true, v => importer.bakeAxisConversion = v);
            Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.importBlendShapes, false, v => importer.importBlendShapes = v);
            Set(importer.addCollider, false, v => importer.addCollider = v);
            Set(importer.isReadable, false, v => importer.isReadable = v);
            if (dirty) importer.SaveAndReimport();

            var embedded = new Dictionary<string, Material>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m) embedded[m.name] = m;
            var names = new HashSet<string>(embedded.Keys);
            foreach (var kv in importer.GetExternalObjectMap())
                if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);

            bool remapped = false;
            foreach (var name in names)
            {
                embedded.TryGetValue(name, out var source);
                var target = Resolve(name, source);
                if (target == null) { Debug.LogWarning($"[Env Art] {Path.GetFileName(path)}: 연결할 재질 없음 '{name}'"); continue; }
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (importer.GetExternalObjectMap().TryGetValue(id, out var existing) && existing == target) continue;
                importer.AddRemap(id, target);
                remapped = true;
            }
            if (remapped) importer.SaveAndReimport();
        }

        private static Material Resolve(string fbxName, Material source)
        {
            string name = Regex.Replace(fbxName, @"\.\d+$", "");
            if (name.StartsWith("MAT_Island"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{IslandMaterialDir}/{name}.mat");
                if (m != null) return m;
            }
            if (PaletteNames.Contains(name))
                return AssetDatabase.LoadAssetAtPath<Material>($"{PaletteDir}/{name}.mat");
            if (name.StartsWith("ENV_") || name.StartsWith("MAT_Island"))
            {
                Color color = EnvColors.TryGetValue(name, out var c) ? c
                            : source != null && source.HasProperty("_Color") ? source.color : new Color(0.5f, 0.5f, 0.5f);
                return OwnMaterial(name, color, name == "ENV_Lamp");
            }
            return null;
        }

        /// <summary>기존 섬 재질과 같은 무광 URP Lit(등불만 약하게 빛난다).</summary>
        private static Material OwnMaterial(string name, Color color, bool emissive)
        {
            string path = $"{OwnMaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 1f));
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", name.Contains("Wet") ? 0.35f : 0.1f);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.4f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------ 섬

        private static void MakeIsland(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return;
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            var root = new GameObject(name);
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                SetShadows(inst);

                // 판정용 정점: 지형 셸 + 해안 바위(Decor_Visual) + 난파선 선체
                var points = new List<Vector3>();
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                {
                    string n = mf.name;
                    if (!(n.StartsWith("TerrainShell") || n.StartsWith("Decor_Visual") || n.StartsWith("BeachedWreck_Visual"))) continue;
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    foreach (var v in mesh.vertices) points.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
                }
                if (points.Count == 0) { Debug.LogWarning($"[Env Art] {name}: TerrainShell 없음"); return; }

                var art = root.AddComponent<EnvironmentArt>();
                art.island = true;
                art.rocky = name.Contains("Rock");
                BakeShape(name, root.transform, points, art);

                // 잔교: 낮은 상자 판정 + 배를 대는 끝
                var ends = new List<Transform>();
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    // 잔교 묶음: "Jetty" 또는 "ENV_Jetty_3"(자식에 …_Visual 메시)
                    if (!Regex.IsMatch(Regex.Replace(t.name, @"\.\d+$", ""), @"^(ENV_)?Jetty(_\d+)?$")) continue;
                    var vis = FindPrefix(t, t.name + "_Visual") ?? FindPrefix(t, "Jetty_Visual") ?? t.GetComponentInChildren<MeshFilter>(true);
                    if (vis != null) AddMeshBox(vis);
                    var end = new GameObject("JettyEnd").transform;
                    end.SetParent(t, false);
                    end.localPosition = LocalFarEnd(t, vis);
                    ends.Add(end);
                }
                art.jettyEnds = ends.ToArray();

                art.lighthouse = FindPrefix(inst.transform, "Lighthouse") != null;
                art.wreck = FindPrefix(inst.transform, "BeachedWreck") != null;
                art.seals = CountPrefix(inst.transform, "ENV_Seal") + CountPrefix(inst.transform, "Seal_");
                CollectMotion(inst.transform, art);

                SetLayer(root, Islands.Layer);
                PrefabUtility.SaveAsPrefabAsset(root, $"{IslandPrefabDir}/{name}.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// 각도 Sectors칸 × 반경 띠(0~45~75~100%)마다 볼록 조각. 경계 각도마다 수면 위 정점의 최대 반경(해안)을 재고,
        /// 띠의 안쪽·바깥쪽 높이는 그 자리 정점의 최고 높이로 둔다.
        /// </summary>
        private static void BakeShape(string name, Transform root, List<Vector3> points, EnvironmentArt art)
        {
            float step = Mathf.PI * 2f / Sectors;
            var land = new List<(float a, float r, float y)>();
            foreach (var p in points)
                if (p.y > SeaY - 0.1f) land.Add((Mathf.Repeat(Mathf.Atan2(p.z, p.x), Mathf.PI * 2f), new Vector2(p.x, p.z).magnitude, p.y));

            static float AngDist(float a, float b) { float d = Mathf.Abs(a - b) % (Mathf.PI * 2f); return d > Mathf.PI ? Mathf.PI * 2f - d : d; }

            var rOut = new float[Sectors];
            var rIn = new float[Sectors];
            for (int k = 0; k < Sectors; k++)
            {
                float a = k * step, max = 0f, min = float.MaxValue;
                foreach (var q in land)
                {
                    if (AngDist(q.a, a) > step * 0.75f) continue;
                    max = Mathf.Max(max, q.r);
                    min = Mathf.Min(min, q.r);
                }
                rOut[k] = Mathf.Max(max, 1f);
                // 늘 가운데까지 채운다(쐐기). 환초 석호는 둘러싸인 못이라 배가 들어갈 수 없고, 메워 두면 적이 그 안에 생기지 않는다.
                rIn[k] = 0f;
            }
            art.rimRadii = rOut;
            float shore = 0f, top = 0f;
            foreach (var r in rOut) shore = Mathf.Max(shore, r);
            foreach (var q in land) top = Mathf.Max(top, q.y);
            art.shoreRadius = shore;
            art.height = top;

            var collision = new GameObject("Collision").transform;
            collision.SetParent(root, false);
            string assetPath = $"{CollisionDir}/{name}_Collision.asset";
            AssetDatabase.DeleteAsset(assetPath);
            Mesh container = null;
            int count = 0;
            for (int k = 0; k < Sectors; k++)
            {
                int k1 = (k + 1) % Sectors;
                float a0 = k * step, a1 = (k + 1) * step;
                for (int b = 0; b < Bands.Length - 1; b++)
                {
                    float i0 = Mathf.Lerp(rIn[k], rOut[k], Bands[b]), o0 = Mathf.Lerp(rIn[k], rOut[k], Bands[b + 1]);
                    float i1 = Mathf.Lerp(rIn[k1], rOut[k1], Bands[b]), o1 = Mathf.Lerp(rIn[k1], rOut[k1], Bands[b + 1]);
                    float mid = (Bands[b] + Bands[b + 1]) * 0.5f;
                    float hIn = float.MinValue, hOut = float.MinValue;
                    foreach (var q in land)
                    {
                        float d = AngDist(q.a, (a0 + a1) * 0.5f);
                        if (d > step * 0.6f) continue;
                        float t = Mathf.InverseLerp(a0, a1, a0 + d);
                        float rin = Mathf.Lerp(rIn[k], rIn[k1], t), rout = Mathf.Lerp(rOut[k], rOut[k1], t);
                        if (rout - rin < 0.1f) continue;
                        float f = (q.r - rin) / (rout - rin);
                        if (f < Bands[b] - 0.05f || f > Bands[b + 1] + 0.05f) continue;
                        if (f < mid) hIn = Mathf.Max(hIn, q.y); else hOut = Mathf.Max(hOut, q.y);
                    }
                    if (hIn == float.MinValue && hOut == float.MinValue) continue;
                    if (hIn == float.MinValue) hIn = hOut;
                    if (hOut == float.MinValue) hOut = hIn;
                    hIn = Mathf.Max(hIn, SeaY + 0.3f);
                    hOut = Mathf.Max(hOut, SeaY + 0.3f);

                    var mesh = Prism(a0, a1, i0, i1, o0, o1, hIn, hOut);
                    mesh.name = $"{name} collision {k}-{b}";
                    if (container == null) { AssetDatabase.CreateAsset(mesh, assetPath); container = mesh; }
                    else AssetDatabase.AddObjectToAsset(mesh, container);
                    var go = new GameObject($"Piece {k}-{b}");
                    go.transform.SetParent(collision, false);
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = true;
                    count++;
                }
            }
            Debug.Log($"[Env Art] {name}: 해안 반경 {shore:F1}m · 높이 {top:F1}m · 판정 조각 {count}개");
        }

        /// <summary>두 경계 각도 사이 띠 하나: 안쪽(i)·바깥쪽(o) 반경, 바닥 BaseY, 윗면은 안쪽 hIn · 바깥쪽 hOut.</summary>
        private static Mesh Prism(float a0, float a1, float i0, float i1, float o0, float o1, float hIn, float hOut)
        {
            Vector3 P(float a, float r, float y) => new(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            var verts = new List<Vector3>();
            bool wedge = i0 < 0.05f && i1 < 0.05f;
            if (wedge)
            {
                verts.Add(new Vector3(0f, BaseY, 0f)); verts.Add(new Vector3(0f, hIn, 0f));
            }
            else
            {
                verts.Add(P(a0, i0, BaseY)); verts.Add(P(a0, i0, hIn));
                verts.Add(P(a1, i1, BaseY)); verts.Add(P(a1, i1, hIn));
            }
            verts.Add(P(a0, o0, BaseY)); verts.Add(P(a0, o0, hOut));
            verts.Add(P(a1, o1, BaseY)); verts.Add(P(a1, o1, hOut));
            // 볼록 판정은 정점의 껍질로 만들어지므로 면은 정점을 모두 쓰는 부채꼴이면 된다
            var tris = new List<int>();
            for (int i = 1; i < verts.Count - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------ 소품

        private static void MakeProp(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return;
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            var root = new GameObject(name);
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                SetShadows(inst);
                var art = root.AddComponent<EnvironmentArt>();

                switch (name)
                {
                    case "ENV_FishingBoat":
                        // 선체만 막는다(수면 위 1m — 갑판 높이 사선은 넘어간다)
                        AddRootBox(root.transform, inst, 1.0f);
                        art.bobbers = new[] { inst.transform };
                        break;
                    case "ENV_FloatingPontoon":
                    case "ENV_Jetty":
                        AddRootBox(root.transform, inst, 1.0f);
                        break;
                    case "ENV_Breakwater":
                    case "ENV_BeachedWreck":
                        AddRootBox(root.transform, inst, 99f);
                        break;
                    case "ENV_SeaRockArch":
                        AddArchBoxes(root.transform, inst);
                        break;
                    case "ENV_NavigationBuoy":
                        art.bobbers = new[] { inst.transform };
                        break;
                }
                CollectMotion(inst.transform, art, keepBobbers: true);
                art.lighthouse = name == "ENV_Lighthouse";
                art.wreck = name == "ENV_BeachedWreck";
                SetLayer(root, Islands.Layer);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PropPrefabDir}/{name}.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>모델 전체 정점(루트 기준)을 감싼 상자. 윗면은 수면 + maxAboveSea까지만.</summary>
        private static void AddRootBox(Transform root, GameObject inst, float maxAboveSea)
        {
            var b = RootBounds(root, inst, _ => true);
            if (b.size == Vector3.zero) return;
            float top = Mathf.Min(b.max.y, maxAboveSea);
            float bottom = Mathf.Min(b.min.y, -0.8f);
            var go = new GameObject("Collision");
            go.transform.SetParent(root, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(b.center.x, (top + bottom) * 0.5f, b.center.z);
            box.size = new Vector3(b.size.x, top - bottom, b.size.z);
        }

        /// <summary>해식 아치: 양쪽 기둥과 윗부분만 막고 가운데 통로는 연다.</summary>
        private static void AddArchBoxes(Transform root, GameObject inst)
        {
            var all = RootBounds(root, inst, _ => true);
            if (all.size == Vector3.zero) return;
            bool alongX = all.size.x >= all.size.z;
            float span = alongX ? all.size.x : all.size.z;
            float lo = (alongX ? all.min.x : all.min.z) + span * 0.3f, hi = (alongX ? all.max.x : all.max.z) - span * 0.3f;
            float Along(Vector3 p) => alongX ? p.x : p.z;
            var left = RootBounds(root, inst, p => Along(p) <= lo);
            var right = RootBounds(root, inst, p => Along(p) >= hi);
            var holder = new GameObject("Collision");
            holder.transform.SetParent(root, false);
            foreach (var b in new[] { left, right })
            {
                if (b.size == Vector3.zero) continue;
                var box = holder.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            // 윗부분(통로 위): 아치 높이의 60%부터 — 함정 사선(수면 위 2.1~2.4m)은 통로로 지나간다
            float topStart = all.min.y + (all.max.y - all.min.y) * 0.6f;
            var topBounds = RootBounds(root, inst, p => p.y >= topStart);
            if (topBounds.size != Vector3.zero)
            {
                var box = holder.AddComponent<BoxCollider>();
                box.center = topBounds.center;
                box.size = topBounds.size;
            }
        }

        private static Bounds RootBounds(Transform root, GameObject inst, System.Func<Vector3, bool> keep)
        {
            var b = new Bounds();
            bool has = false;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = root.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (!keep(p)) continue;
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        // ------------------------------------------------------------ 공통

        /// <summary>회전·흔들림·출렁임·등불 자리를 이름으로 모은다.</summary>
        private static void CollectMotion(Transform model, EnvironmentArt art, bool keepBobbers = false)
        {
            var spin = new List<Transform>();
            var sweep = new List<Transform>();
            var bob = keepBobbers && art.bobbers != null ? new List<Transform>(art.bobbers) : new List<Transform>();
            var lamps = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = Regex.Replace(t.name, @"\.\d+$", "");
                if (n == "RadarPivot" || n == "AnemometerPivot") spin.Add(t);
                else if (n == "SearchlightPivot" || n == "WindVanePivot") sweep.Add(t);
                else if (n.StartsWith("ENV_NavigationBuoy") && !n.EndsWith("_Visual") && t != model) bob.Add(t);
                else if (n == "LampOrigin" && (HasAncestor(t, "Lighthouse") || HasAncestor(t, "BeaconMount") || HasAncestor(t, "ENV_Lighthouse"))) lamps.Add(t);
            }
            art.spinners = spin.ToArray();
            art.sweepers = sweep.ToArray();
            art.bobbers = bob.ToArray();
            art.lamps = lamps.ToArray();
        }

        private static bool HasAncestor(Transform t, string prefix)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name.StartsWith(prefix)) return true;
            return false;
        }

        /// <summary>잔교 끝: 잔교 메시에서 기준점(해안 연결)에서 가장 먼 쪽의 가운데.</summary>
        private static Vector3 LocalFarEnd(Transform jetty, MeshFilter vis)
        {
            if (vis == null || vis.sharedMesh == null) return Vector3.zero;
            Vector3 far = Vector3.zero;
            float best = -1f;
            foreach (var v in vis.sharedMesh.vertices)
            {
                var p = jetty.InverseTransformPoint(vis.transform.TransformPoint(v));
                float d = new Vector2(p.x, p.z).sqrMagnitude;
                if (d > best) { best = d; far = p; }
            }
            var dir = new Vector3(far.x, 0f, far.z);
            float len = dir.magnitude;
            if (len < 0.01f) return Vector3.zero;
            // 가장 먼 꼭짓점은 모서리이므로 잔교 축(원점 → 먼 쪽) 위로 끌어온다
            var axis = AxisOf(dir);
            return axis * Vector3.Dot(far, axis);
        }

        private static Vector3 AxisOf(Vector3 d)
        {
            if (Mathf.Abs(d.x) > Mathf.Abs(d.z)) return new Vector3(Mathf.Sign(d.x), 0f, 0f);
            return new Vector3(0f, 0f, Mathf.Sign(d.z));
        }

        private static void AddMeshBox(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) return;
            var b = mesh.bounds;
            var box = mf.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }

        private static MeshFilter FindPrefix(Transform t, string prefix)
        {
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.StartsWith(prefix)) return mf;
            return null;
        }

        private static int CountPrefix(Transform t, string prefix)
        {
            int n = 0;
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                if (c.name.StartsWith(prefix) && !c.name.Contains("_Visual") && c.GetComponent<MeshFilter>() == null) n++;
            return n;
        }

        private static void SetShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        /// <summary>검증용: 프리팹마다 크기·삼각형·판정·움직임·재질 연결 상태.</summary>
        public static string Report()
        {
            var sb = new StringBuilder("# Environment art report\n");
            foreach (var dir in new[] { IslandPrefabDir, PropPrefabDir })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.prefab"))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/'));
                    if (go == null) continue;
                    var b = new Bounds(); bool has = false; int tris = 0; var missing = new HashSet<string>(); var mats = new HashSet<string>();
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;
                        var wb = TransformBounds(go.transform, r.transform, mf.sharedMesh.bounds);
                        if (!has) { b = wb; has = true; } else b.Encapsulate(wb);
                        tris += mf.sharedMesh.triangles.Length / 3;
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) { missing.Add("(null)"); continue; }
                            string p = AssetDatabase.GetAssetPath(m);
                            if (p.EndsWith(".fbx")) missing.Add(m.name); else mats.Add(m.name);
                        }
                    }
                    var art = go.GetComponent<EnvironmentArt>();
                    int colliders = go.GetComponentsInChildren<Collider>(true).Length;
                    string shape = art != null && art.island
                        ? $" shore {art.shoreRadius:F1} height {art.height:F1} rocky {art.rocky} lighthouse {art.lighthouse} wreck {art.wreck} seals {art.seals} jetties {art.jettyEnds?.Length ?? 0}"
                        : "";
                    string motion = art != null ? $" spin {art.spinners?.Length ?? 0} sweep {art.sweepers?.Length ?? 0} bob {art.bobbers?.Length ?? 0} lamps {art.lamps?.Length ?? 0}" : "";
                    sb.AppendLine($"- {go.name}: size {b.size:F1} center {b.center:F1} tris {tris} colliders {colliders}{shape}{motion} mats {mats.Count}" +
                                  (missing.Count > 0 ? $" UNMAPPED [{string.Join(", ", missing)}]" : ""));
                }
            }
            return sb.ToString();
        }

        private static Bounds TransformBounds(Transform root, Transform t, Bounds local)
        {
            var b = new Bounds(root.InverseTransformPoint(t.TransformPoint(local.center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(root.InverseTransformPoint(t.TransformPoint(c)));
            }
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
