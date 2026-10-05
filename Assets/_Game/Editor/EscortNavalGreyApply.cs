using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Escort-only material update. Keeps existing prefab GUIDs, model mounts and gameplay unchanged.
    /// A one-shot request is consumed only outside Play Mode; no scene is opened or saved.
    /// </summary>
    [InitializeOnLoad]
    public static class EscortNavalGreyApply
    {
        public const string MaterialDir = "Assets/_Game/Art/Models/Materials/TaskForce/NavalGrey";
        private const string ModelDir = "Assets/_Game/Art/Models/TaskForce";
        private const string PrefabDir = "Assets/_Game/Resources/TaskForce/Escorts";
        private const string ManifestPath = ModelDir + "/EscortNavalGreyManifest.json";
        private const string RequestPath = "EscortNavalGreyApply.request";
        private const string ReportPath = "Library/EscortNavalGreyApplyReport.json";
        private static bool running;
        private static double nextCheck;
        private static bool verifyAfterRefresh;

        [Serializable] public sealed class Manifest { public ModelSpec[] models; }
        [Serializable] public sealed class ModelSpec
        {
            public string id;
            public int triangles;
            public string[] sockets;
            public Vector3 size;
            public Vector3 center;
        }
        [Serializable] public sealed class ModelResult
        {
            public string id;
            public string modelGuid;
            public string prefabGuid;
            public bool passed;
            public int triangles;
            public int renderers;
            public Vector3 size;
            public Vector3 center;
            public Vector3 bow;
            public Vector3 support;
            public Vector3 modelMountScale;
            public string[] materials;
            public List<string> errors = new();
        }
        [Serializable] public sealed class Report
        {
            public string utc;
            public string unityVersion;
            public bool allPassed;
            public bool scenesUnchanged = true;
            public bool prefabsUnchanged = true;
            public List<string> errors = new();
            public List<ModelResult> models = new();
        }

        private static readonly Dictionary<string, Color> Palette = new()
        {
            { "Naval Blue Grey", new Color(.30f, .35f, .40f, 1f) },
            { "Naval Superstructure", new Color(.30f, .35f, .40f, 1f) },
            { "Deck Grey", new Color(.14f, .18f, .21f, 1f) },
            { "Gunmetal", new Color(.045f, .060f, .075f, 1f) },
            { "Radar Glass", new Color(.035f, .14f, .17f, 1f) },
            { "Sensor Glass", new Color(.035f, .14f, .17f, 1f) },
            { "Deck Marking White", new Color(.73f, .75f, .75f, 1f) },
            { "Warning Yellow", new Color(.72f, .47f, .085f, 1f) },
        };

        static EscortNavalGreyApply()
        {
            // Resume the one-shot verification after a requested script refresh, even
            // when the previously loaded verifier already consumed the request marker.
            verifyAfterRefresh = File.Exists(ReportPath) &&
                Directory.GetFiles(".", RequestPath + ".failed.*").Length > 0 &&
                !SessionState.GetBool("EscortNavalGrey.ExactBoundsV3Checked", false);
            EditorApplication.update += CheckRequest;
        }

        private static void CheckRequest()
        {
            if (running || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            if ((!File.Exists(RequestPath) && !verifyAfterRefresh) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            verifyAfterRefresh = false;
            SessionState.SetBool("EscortNavalGrey.ExactBoundsV3Checked", true);
            bool ok = ApplyAndVerify();
            // Preserve the marker for audit instead of deleting it. Failures do not loop on each update.
            if (File.Exists(RequestPath))
                File.Move(RequestPath, RequestPath + (ok ? ".completed" : ".failed") + "." + DateTime.UtcNow.Ticks);
        }

        [MenuItem("Naval/Art/Apply And Verify Naval Grey Escorts", priority = 41)]
        public static void ApplyFromMenu() { ApplyAndVerify(); }

        public static void RunBatch()
        {
            if (!ApplyAndVerify()) throw new InvalidOperationException("Escort naval-grey validation failed; see " + ReportPath);
        }

        private static bool ApplyAndVerify()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Escort Naval Grey] Exit Play Mode before applying escort art.");
                return false;
            }
            running = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion };
            try
            {
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                if (manifest?.models == null || manifest.models.Length != 16 ||
                    manifest.models.Select(x => x.id).Distinct().Count() != 16 ||
                    manifest.models.Any(x => !Regex.IsMatch(x.id, @"^ESC_(CAP|ASW|EW|STK)_T[0-3]$")))
                    throw new InvalidDataException("Expected exactly the sixteen CAP/ASW/EW/STK T0-T3 models.");
                CreateMaterials();
                foreach (var spec in manifest.models)
                {
                    var result = new ModelResult { id = spec.id };
                    report.models.Add(result);
                    try
                    {
                        string modelPath = ModelDir + "/" + spec.id + ".fbx";
                        string prefabPath = PrefabDir + "/" + spec.id + ".prefab";
                        result.modelGuid = AssetDatabase.AssetPathToGUID(modelPath);
                        result.prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                        string prefabBefore = File.ReadAllText(prefabPath);
                        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        Remap(modelPath);
                        Verify(spec, result, prefabPath);
                        if (result.modelGuid != AssetDatabase.AssetPathToGUID(modelPath) ||
                            result.prefabGuid != AssetDatabase.AssetPathToGUID(prefabPath))
                            result.errors.Add("Asset GUID changed.");
                        if (prefabBefore != File.ReadAllText(prefabPath))
                        {
                            report.prefabsUnchanged = false;
                            result.errors.Add("Existing prefab file changed unexpectedly.");
                        }
                    }
                    catch (Exception ex) { result.errors.Add(ex.ToString()); }
                    result.passed = result.errors.Count == 0;
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex) { report.errors.Add(ex.ToString()); }
            finally
            {
                report.allPassed = report.errors.Count == 0 && report.models.Count == 16 && report.models.All(x => x.passed);
                Directory.CreateDirectory("Library");
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                running = false;
            }
            if (report.allPassed) Debug.Log("[Escort Naval Grey] PASS: 16 latest escorts; original mounts/GUIDs/sockets retained. " + ReportPath);
            else Debug.LogError("[Escort Naval Grey] Validation failed. " + ReportPath);
            return report.allPassed;
        }

        private static void CreateMaterials()
        {
            EnsureFolder(MaterialDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader missing.");
            foreach (var pair in Palette)
            {
                string path = MaterialDir + "/" + pair.Key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = pair.Key };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetColor("_BaseColor", pair.Value);
                mat.SetFloat("_Metallic", pair.Key == "Gunmetal" ? .20f : .025f);
                mat.SetFloat("_Smoothness", pair.Key.EndsWith("Glass", StringComparison.Ordinal) ? .72f : .28f);
                mat.SetFloat("_Surface", 0);
                mat.SetFloat("_Cull", 2);
                mat.SetFloat("_ZWrite", 1);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_EMISSION");
                mat.renderQueue = -1;
                EditorUtility.SetDirty(mat);
            }
        }

        private static void Remap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || !importer.bakeAxisConversion || Mathf.Abs(importer.globalScale - 1f) > .001f)
                throw new InvalidOperationException("Unexpected importer scale/axis settings: " + path);
            var names = new HashSet<string>(importer.GetExternalObjectMap().Keys.Where(x => x.type == typeof(Material)).Select(x => x.name));
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) names.Add(mat.name);
            foreach (var name in names)
            {
                string canonical = Regex.Replace(name, @"\.\d+$", "");
                if (canonical.StartsWith("Role ", StringComparison.Ordinal)) continue; // Existing role colors remain untouched.
                if (!Palette.ContainsKey(canonical)) throw new InvalidDataException("Unknown escort material: " + name);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/" + canonical + ".mat");
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            }
            importer.SaveAndReimport();
        }

        private static void Verify(ModelSpec spec, ModelResult result, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidDataException("Prefab missing: " + path);
            var inst = Object.Instantiate(prefab);
            inst.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var transforms = inst.GetComponentsInChildren<Transform>(true);
                Transform Find(string name) => transforms.FirstOrDefault(x => x.name == name);
                foreach (var name in spec.sockets)
                    if (transforms.Count(x => x.name == name) != 1) result.errors.Add("Missing/duplicate socket: " + name);
                var bow = Find("RolePlateBow");
                var support = Find("SupportOrigin");
                result.bow = bow != null ? bow.position : Vector3.zero;
                result.support = support != null ? support.position : Vector3.zero;
                if (result.bow.z < 3f || Mathf.Abs(result.bow.x) > .01f || result.bow.y < 0f)
                    result.errors.Add("Bow axis must be +Z.");
                if (result.support.y < 2.8f || new Vector2(result.support.x, result.support.z).magnitude > .01f)
                    result.errors.Add("Up axis must be +Y; preserve the existing X+90 Model mount.");
                var importedModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/" + spec.id + ".fbx");
                foreach (var t in transforms)
                {
                    if (t.name == "Model")
                    {
                        result.modelMountScale = t.localScale;
                        // Unity represents this FBX's centimetre/metre conversion on the
                        // imported root (100); meshes are correspondingly in centimetres.
                        // Keep the original imported basis, not a second prefab scale.
                        // Exact world-space metre bounds below catch 100x size regressions.
                        if (importedModel == null || (t.localScale - importedModel.transform.localScale).sqrMagnitude > .0001f)
                            result.errors.Add("Prefab adds an unexpected scale on the imported model.");
                        continue;
                    }
                    var magnitude = new Vector3(Mathf.Abs(t.localScale.x), Mathf.Abs(t.localScale.y), Mathf.Abs(t.localScale.z));
                    if ((magnitude - Vector3.one).sqrMagnitude > .0001f)
                        result.errors.Add("Non-unit scale: " + t.name + " " + t.localScale);
                }
                var names = new HashSet<string>();
                var meshes = inst.GetComponentsInChildren<MeshRenderer>(true);
                result.renderers = meshes.Length;
                var bounds = new Bounds();
                bool first = true;
                foreach (var renderer in meshes)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) { result.errors.Add("Null material."); continue; }
                        names.Add(mat.name);
                        string materialPath = AssetDatabase.GetAssetPath(mat);
                        if (mat.name.StartsWith("Role ", StringComparison.Ordinal))
                        {
                            if (!materialPath.StartsWith("Assets/_Game/Art/Models/Materials/TaskForce/Role ", StringComparison.Ordinal))
                                result.errors.Add("Role material remap missing: " + mat.name);
                        }
                        else if (!Palette.TryGetValue(mat.name, out var expected) ||
                                 !materialPath.StartsWith(MaterialDir + "/", StringComparison.Ordinal) ||
                                 Vector4.Distance(mat.GetColor("_BaseColor"), expected) > .001f)
                            result.errors.Add("Naval-grey material mismatch: " + mat.name);
                    }
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) { result.errors.Add("Mesh missing: " + renderer.name); continue; }
                    // Renderer.bounds is a transformed local AABB and overestimates rotated
                    // railings/weapons. Compare actual vertices to the Blender manifest instead.
                    foreach (var vertex in mesh.vertices)
                    {
                        var world = renderer.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(world, Vector3.zero); first = false; }
                        else bounds.Encapsulate(world);
                    }
                    for (int i = 0; i < mesh.subMeshCount; i++) result.triangles += (int)mesh.GetIndexCount(i) / 3;
                }
                result.size = bounds.size;
                result.center = bounds.center;
                result.materials = names.OrderBy(x => x).ToArray();
                if (result.triangles != spec.triangles) result.errors.Add("Triangle count changed.");
                if (Vector3.Distance(result.size, spec.size) > .02f || Vector3.Distance(result.center, spec.center) > .02f)
                    result.errors.Add("Size/center mismatch; possible axis or scaling regression.");
            }
            finally { Object.DestroyImmediate(inst); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
