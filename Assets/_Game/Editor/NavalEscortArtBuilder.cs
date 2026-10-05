using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Codex 전투단 아트(ArtSource/TaskForceTacticalDraft)를 게임에 연결한다. 씬·기존 프리팹은 건드리지 않는다.
    ///   1. Art/Models/TaskForce/*.fbx(호위함 16척)와 Effects/*.fbx(전술 표시 5종)의 임포트 설정을 맞추고
    ///      재질을 이름으로 프로젝트 팔레트에 연결한다(Deck Grey·Naval Blue Grey·Gunmetal 등은 기존 재질, 역할색·FX는 새 재질).
    ///   2. Resources/TaskForce/Escorts/ESC_&lt;역할&gt;_T&lt;단계&gt;.prefab, Resources/TaskForce/Effects/FX_&lt;코드&gt;_Tactical.prefab을 만든다.
    ///      선수가 +Z를 향하도록(RolePlateBow / NorthBearing 소켓으로 판정) 모델을 돌려 넣는다.
    /// 런타임(TaskForceEscortFormation·TaskForceWorldFeedback)은 이 프리팹이 없으면 기존 회색박스·선 표시로 돌아간다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalEscortArtBuilder.RunBatch
    /// </summary>
    public static class NavalEscortArtBuilder
    {
        private const string ModelDir = "Assets/_Game/Art/Models/TaskForce";
        private const string EffectModelDir = "Assets/_Game/Art/Models/TaskForce/Effects";
        private const string PaletteDir = "Assets/_Game/Art/Models/Materials";
        private const string OwnMaterialDir = "Assets/_Game/Art/Models/Materials/TaskForce";
        private const string EscortPrefabDir = "Assets/_Game/Resources/TaskForce/Escorts";
        private const string EffectPrefabDir = "Assets/_Game/Resources/TaskForce/Effects";

        /// <summary>Codex 팔레트 이름 → 프로젝트의 기존 재질(기함 모듈과 같은 톤).</summary>
        private static readonly Dictionary<string, string> Palette = new()
        {
            { "Naval Blue Grey", "Naval Blue Grey" },
            { "Deck Grey", "Deck Grey" },
            { "Naval Superstructure", "Naval cool grey" },
            { "Gunmetal", "Gunmetal" },
            { "Radar Glass", "Radar Glass" },
            { "Sensor Glass", "Sensor Glass" },
            { "Warning Yellow", "Warning Yellow" },
            { "Deck Marking White", "Medical White" },
        };

        /// <summary>역할색(TaskForceEscortFormation.RoleColor, Codex manifest와 같은 값).</summary>
        private static readonly Dictionary<string, Color> RoleColors = new()
        {
            { "CAP", new Color(0.35f, 1.00f, 0.58f) },
            { "ASW", new Color(0.22f, 0.68f, 1.00f) },
            { "EW", new Color(0.83f, 0.37f, 1.00f) },
            { "STK", new Color(1.00f, 0.52f, 0.16f) },
            { "AEW", new Color(0.20f, 0.95f, 1.00f) },
        };

        public static void RunBatch()
        {
            BuildAll();
            var report = Report();
            Debug.Log(report);
            string outPath = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-escortReport") outPath = args[i + 1];
            if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, report, Encoding.UTF8);
        }

        [MenuItem("Naval/Art/Build Task Force Escort Prefabs", priority = 40)]
        public static void BuildAll()
        {
            EnsureFolder(OwnMaterialDir);
            EnsureFolder(EscortPrefabDir);
            EnsureFolder(EffectPrefabDir);

            foreach (var path in Fbx(ModelDir)) ImportAndRemap(path, false);
            foreach (var path in Fbx(EffectModelDir)) ImportAndRemap(path, true);
            AssetDatabase.SaveAssets();

            foreach (var path in Fbx(ModelDir)) MakePrefab(path, EscortPrefabDir, "RolePlateBow");
            foreach (var path in Fbx(EffectModelDir)) MakePrefab(path, EffectPrefabDir, "NorthBearing");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Escort Art] 호위함·전술 이펙트 프리팹 생성 완료");
        }

        private static IEnumerable<string> Fbx(string dir)
        {
            if (!Directory.Exists(dir)) yield break;
            foreach (var f in Directory.GetFiles(dir, "*.fbx", SearchOption.TopDirectoryOnly))
                yield return f.Replace('\\', '/');
        }

        private static void ImportAndRemap(string path, bool effect)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning($"[Escort Art] 임포터 없음: {path}"); return; }

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

            // FBX 안 재질 이름(.001 등 접미사 제외)으로 연결한다
            var names = new HashSet<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m) names.Add(m.name);
            foreach (var kv in importer.GetExternalObjectMap())
                if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);

            bool remapped = false;
            foreach (var name in names)
            {
                var target = Resolve(name, effect);
                if (target == null) { Debug.LogWarning($"[Escort Art] {Path.GetFileName(path)}: 연결할 재질 없음 '{name}'"); continue; }
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (importer.GetExternalObjectMap().TryGetValue(id, out var existing) && existing == target) continue;
                importer.AddRemap(id, target);
                remapped = true;
            }
            if (remapped) importer.SaveAndReimport();
        }

        private static Material Resolve(string fbxName, bool effect)
        {
            string name = Regex.Replace(fbxName, @"\.\d+$", "");
            // Escort recolors are isolated from the player palette and tactical FX.
            if (!effect)
            {
                var escortGrey = AssetDatabase.LoadAssetAtPath<Material>($"{EscortNavalGreyApply.MaterialDir}/{name}.mat");
                if (escortGrey != null) return escortGrey;
            }
            if (Palette.TryGetValue(name, out var palette))
                return AssetDatabase.LoadAssetAtPath<Material>($"{PaletteDir}/{palette}.mat");

            var role = Regex.Match(name, @"^Role (\w+)$");
            if (role.Success && RoleColors.TryGetValue(role.Groups[1].Value, out var rc))
                return RoleMaterial(name, rc);

            var fx = Regex.Match(name, @"^FX (\w+)( Faint)?$");
            if (fx.Success && RoleColors.TryGetValue(fx.Groups[1].Value, out var fc))
                return FxMaterial(name, fc, fx.Groups[2].Success ? 0.32f : 0.9f);
            return null;
        }

        /// <summary>역할색 표식(URP Lit, 약한 자체발광 — 원거리에서 역할이 보이게).</summary>
        private static Material RoleMaterial(string name, Color color)
        {
            string path = $"{OwnMaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", 0.15f);
            m.SetFloat("_Smoothness", 0.55f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.65f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>전술 표시(URP Unlit, 가산 투명, 양면). 런타임이 _BaseColor 알파로 서서히 지운다.</summary>
        private static Material FxMaterial(string name, Color color, float alpha)
        {
            string path = $"{OwnMaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);                                               // Additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>모델을 감싼 프리팹. 선수 소켓이 -Z 쪽이면 180° 돌려 넣는다(게임은 +Z = 선수).</summary>
        private static void MakePrefab(string fbxPath, string outDir, string bowSocket)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return;
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            var previous = AssetDatabase.LoadAssetAtPath<GameObject>($"{outDir}/{name}.prefab");
            var previousMount = previous != null ? previous.transform.Find("Model") : null;
            var previousRotation = previousMount != null ? previousMount.localRotation : (Quaternion?)null;
            var root = new GameObject(name);
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                if (outDir == EscortPrefabDir && previousRotation.HasValue)
                    inst.transform.localRotation = previousRotation.Value;
                var bow = Find(inst.transform, bowSocket);
                if (bow != null && root.transform.InverseTransformPoint(bow.position).z < 0f)
                    inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = outDir == EffectPrefabDir
                        ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = outDir != EffectPrefabDir;
                }
                PrefabUtility.SaveAsPrefabAsset(root, $"{outDir}/{name}.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }

        /// <summary>검증용: 프리팹마다 크기·선수 방향·소켓 위치·재질 연결 상태.</summary>
        public static string Report()
        {
            var sb = new StringBuilder("# Escort art report\n");
            foreach (var dir in new[] { EscortPrefabDir, EffectPrefabDir })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.prefab"))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/'));
                    if (go == null) continue;
                    var inst = Object.Instantiate(go);
                    var b = new Bounds(); bool has = false; int tris = 0; var missing = new HashSet<string>(); var mats = new HashSet<string>();
                    foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) { missing.Add("(null)"); continue; }
                            string p = AssetDatabase.GetAssetPath(m);
                            if (p.EndsWith(".fbx")) missing.Add(m.name); else mats.Add(m.name);
                        }
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                    }
                    string Sock(string n) { var s = Find(inst.transform, n); return s != null ? s.position.ToString("F2") : "-"; }
                    sb.AppendLine($"- {inst.name.Replace("(Clone)", "")}: size {b.size:F2} center {b.center:F2} tris {tris} " +
                                  $"bow {Sock("RolePlateBow")} north {Sock("NorthBearing")} support {Sock("SupportOrigin")} " +
                                  $"heli {Sock("HelicopterLaunch")} mats [{string.Join(", ", mats)}]" +
                                  (missing.Count > 0 ? $" UNMAPPED [{string.Join(", ", missing)}]" : ""));
                    Object.DestroyImmediate(inst);
                }
            }
            return sb.ToString();
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
