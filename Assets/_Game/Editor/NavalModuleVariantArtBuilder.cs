using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 위치별 소나·폭뢰 모델(Codex SonarPositionPack · DepthChargePositionPack, 원본 ArtSource)을 게임에 연결한다.
    ///   1. Art/Models/Variants/*.fbx 임포트 설정을 기존 블록 모델과 맞추고(축 변환은 모델 루트에 두고 부모 피벗에서 선수 보정)
    ///      재질을 이름으로 기존 팔레트(Art/Models/Materials)에 연결한다.
    ///   2. Resources/Modules/Variants/MOD_*.prefab: 루트 → ModelPivot(Y 180°, 다른 블록과 같은 선수 보정) → 모델.
    /// 런타임(ModuleVariantVisual)이 형태에 맞는 프리팹을 블록 중심에 붙이고 ForwardMarker/OutboardDirection 소켓으로 방향을 맞춘다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalModuleVariantArtBuilder.RunBatch
    /// </summary>
    public static class NavalModuleVariantArtBuilder
    {
        private const string ModelDir = "Assets/_Game/Art/Models/Variants";
        private const string PaletteDir = "Assets/_Game/Art/Models/Materials";
        private const string PrefabDir = "Assets/_Game/Resources/Modules/Variants";
        private static readonly Quaternion ArtForwardFix = Quaternion.Euler(0f, 180f, 0f);

        public static void RunBatch()
        {
            BuildAll();
            var report = Report();
            Debug.Log(report);
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-variantReport") File.WriteAllText(args[i + 1], report, Encoding.UTF8);
        }

        [MenuItem("Naval/Art/Build Sonar & Depth Charge Variant Prefabs", priority = 42)]
        public static void BuildAll()
        {
            EnsureFolder(PrefabDir);
            foreach (var path in Fbx()) ImportAndRemap(path);
            AssetDatabase.SaveAssets();
            foreach (var path in Fbx()) MakePrefab(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Variant Art] 위치별 소나·폭뢰 프리팹 생성 완료");
        }

        private static IEnumerable<string> Fbx()
        {
            if (!Directory.Exists(ModelDir)) yield break;
            foreach (var f in Directory.GetFiles(ModelDir, "*.fbx")) yield return f.Replace('\\', '/');
        }

        private static void ImportAndRemap(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            bool dirty = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                apply(wanted);
                dirty = true;
            }
            // 기존 블록 모델(MOD_Sonar.fbx 등)과 같은 축 처리: 축 변환은 모델 루트에, 선수 보정은 프리팹 피벗에
            Set(importer.bakeAxisConversion, false, v => importer.bakeAxisConversion = v);
            Set(importer.useFileScale, true, v => importer.useFileScale = v);
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportStandard, v => importer.materialImportMode = v);
            Set(importer.materialLocation, ModelImporterMaterialLocation.InPrefab, v => importer.materialLocation = v);
            Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.addCollider, false, v => importer.addCollider = v);
            Set(importer.isReadable, false, v => importer.isReadable = v);
            if (dirty) importer.SaveAndReimport();

            var names = new HashSet<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Material m) names.Add(m.name);
            foreach (var kv in importer.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            bool remapped = false;
            foreach (var name in names)
            {
                string clean = Regex.Replace(name, @"\.\d+$", "");
                var target = AssetDatabase.LoadAssetAtPath<Material>($"{PaletteDir}/{clean}.mat");
                if (target == null) { Debug.LogWarning($"[Variant Art] {Path.GetFileName(path)}: 연결할 재질 없음 '{name}'"); continue; }
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (importer.GetExternalObjectMap().TryGetValue(id, out var existing) && existing == target) continue;
                importer.AddRemap(id, target);
                remapped = true;
            }
            if (remapped) importer.SaveAndReimport();
        }

        private static void MakePrefab(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return;
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            var root = new GameObject(name);
            try
            {
                var pivot = new GameObject("ModelPivot").transform;
                pivot.SetParent(root.transform, false);
                pivot.localRotation = ArtForwardFix;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(pivot, false);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>검증용: 크기·소켓 방향(선수 +Z 기준)·재질.</summary>
        public static string Report()
        {
            var sb = new StringBuilder("# Module variant art report\n");
            if (!Directory.Exists(PrefabDir)) return sb.ToString();
            foreach (var f in Directory.GetFiles(PrefabDir, "*.prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/'));
                if (go == null) continue;
                var inst = Object.Instantiate(go);
                var b = new Bounds(); bool has = false; int tris = 0; var missing = new HashSet<string>();
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                    foreach (var m in r.sharedMaterials) if (m == null || AssetDatabase.GetAssetPath(m).EndsWith(".fbx")) missing.Add(m != null ? m.name : "(null)");
                }
                string Sock(string n) { var s = Game.Modules.ModuleVariantVisual.Find(inst.transform, n); return s != null ? s.position.ToString("F2") : "-"; }
                sb.AppendLine($"- {go.name}: size {b.size:F2} center {b.center:F2} tris {tris} forward {Sock("ForwardMarker")} outboard {Sock("OutboardDirection")} " +
                              $"drop {Sock("DropPoint_01")} launch {Sock("LaunchPoint_01")} cable {Sock("CableExit")}" +
                              (missing.Count > 0 ? $" UNMAPPED [{string.Join(", ", missing)}]" : ""));
                Object.DestroyImmediate(inst);
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
