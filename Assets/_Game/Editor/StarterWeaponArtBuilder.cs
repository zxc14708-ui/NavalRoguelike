using System;
using System.IO;
using System.Linq;
using System.Text;
using Game.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Import and review the two L1 art proposals. Does not edit loadouts or module definitions.</summary>
    public static class StarterWeaponArtBuilder
    {
        const string Models = "Assets/_Game/Art/Models/StarterWeapons_v1";
        const string Folder = "Assets/_Game/Art/StarterWeapons_v1";
        const string Output = "ArtSource/StarterWeapons_v1/Preview";
        static readonly string[] Names = { "MOD_BasicNavalGun_L1", "MOD_MultipurposeMG_L1" };

        [MenuItem("Naval/Art/Build Starter Weapons L1 Preview")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var palette = new[] { "Deck Grey", "Naval Blue Grey", "Gunmetal" }
                .Select(n => (name: n, mat: AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Game/Art/Models/Materials/" + n + ".mat"))).ToArray();
            if (palette.Any(p => p.mat == null)) throw new Exception("Missing shared naval palette material.");
            var report = new StringBuilder("Starter weapons L1: real imported FBX validation\n");
            var prefabs = new GameObject[2];
            for (int i = 0; i < Names.Length; i++)
            {
                var asset = NavalEditorUtil.ImportModel(Models + "/" + Names[i] + ".fbx", palette);
                if (asset == null) throw new Exception("FBX import failed: " + Names[i]);
                var root = new GameObject(Names[i] + "_Visual");
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    model.name = "Model";
                    model.transform.SetParent(root.transform, false);
                    var muzzle = Find(root, "Muzzle");
                    var pitch = Find(root, "ElevationPivot");
                    // Normalize presentation forward in the wrapper, retaining the source FBX convention.
                    Vector3 forward = muzzle.position - pitch.position;
                    model.transform.localRotation = Quaternion.AngleAxis(
                        Vector3.SignedAngle(Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.forward, Vector3.up), Vector3.up)
                        * model.transform.localRotation;
                    Validate(root, report);
                    prefabs[i] = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + root.name + ".prefab");
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("ArtSource/StarterWeapons_v1/UnityValidation.txt", report.ToString());
            Capture(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[StarterWeapons] BUILD_AND_CAPTURE_OK\n" + report);
        }

        static Transform Find(GameObject root, string name)
            => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("No renderers: " + go.name);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        static void Validate(GameObject root, StringBuilder report)
        {
            var yaw = Find(root, "TurretPivot");
            var pitch = Find(root, "ElevationPivot");
            var muzzle = Find(root, "Muzzle");
            if (!pitch.IsChildOf(yaw) || !muzzle.IsChildOf(pitch)) throw new Exception("Socket hierarchy invalid.");
            var bounds = BoundsOf(root);
            if (Mathf.Abs(bounds.size.x - 1.7f) > .025f || bounds.size.y < 1f || bounds.size.y > 1.5f
                || Mathf.Abs(bounds.min.y) > .025f || bounds.size.z > 2.5f)
                throw new Exception("Wrong imported bounds: " + root.name + " " + bounds);
            int tris = root.GetComponentsInChildren<MeshFilter>().Sum(f =>
                Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(s => (int)f.sharedMesh.GetIndexCount(s) / 3));
            if (tris > 900) throw new Exception("Cell triangle budget exceeded: " + tris);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader.name != "Universal Render Pipeline/Lit")
                        throw new Exception("Missing URP palette material: " + renderer.name);
            if (root.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Art must not add colliders.");
            var yawRotation = yaw.localRotation;
            var pitchRotation = pitch.localRotation;
            var pitchPosition = pitch.localPosition;
            // Exercise the actual existing game controller against imported FBX pivots.
            var weapon = new WeaponController();
            weapon.BindArtTransforms(yaw, pitch, muzzle);
            foreach (var offset in new[] { new Vector3(4, 0, 7), new Vector3(-4, 3, 7) })
            {
                var target = pitch.position + offset;
                for (int i = 0; i < 120; i++) weapon.AimAt(target, 720f, 1f / 60f);
                float error = Vector3.Angle(weapon.FireDirection, (target - muzzle.position).normalized);
                // AimAt calibrates around muzzle/pivot; allow parallax at this close test distance.
                if (error > 8f) throw new Exception("Imported aiming failed: " + error);
            }
            yaw.localRotation = yawRotation;
            pitch.localRotation = pitchRotation;
            pitch.localPosition = pitchPosition;
            report.AppendLine($"{root.name}: {tris} tris, {root.GetComponentsInChildren<MeshFilter>().Length} meshes, " +
                $"bounds={bounds.size:F3}, minY={bounds.min.y:F4}; URP, sockets, forward, yaw/elevation PASS.");
        }

        static void Capture(GameObject[] prefabs)
        {
            Scene previous = SceneManager.GetActiveScene();
            bool replaceBlankBatchScene = Application.isBatchMode && string.IsNullOrEmpty(previous.path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceBlankBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.60f, .68f, .73f);
                RenderSettings.ambientEquatorColor = new Color(.38f, .43f, .47f);
                RenderSettings.ambientGroundColor = new Color(.20f, .25f, .29f);
                RenderSettings.fog = false;
                var key = new GameObject("Key light").AddComponent<Light>();
                key.type = LightType.Directional; key.intensity = 1.35f;
                key.transform.rotation = Quaternion.Euler(40, -35, 0);
                key.shadows = LightShadows.Soft;
                var fill = new GameObject("Fill light").AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = .6f;
                fill.transform.rotation = Quaternion.Euler(55, 130, 0);
                var camera = new GameObject("Review camera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .12f, .15f);
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.allowHDR = false; camera.allowMSAA = true;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Review surface";
                ground.transform.position = new Vector3(0, -.09f, 0);
                ground.transform.localScale = new Vector3(30, .16f, 30);
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Game/Art/Models/Materials/Deck Grey.mat");
                var models = prefabs.Select(p => (GameObject)PrefabUtility.InstantiatePrefab(p, scene)).ToArray();
                foreach (var sceneRoot in scene.GetRootGameObjects())
                    foreach (var transform in sceneRoot.GetComponentsInChildren<Transform>(true))
                        transform.gameObject.layer = 31;
                camera.cullingMask = 1 << 31;
                key.cullingMask = fill.cullingMask = 1 << 31;
                for (int i = 0; i < models.Length; i++)
                {
                    foreach (var m in models) m.SetActive(false);
                    models[i].SetActive(true);
                    var target = BoundsOf(models[i]).center;
                    camera.transform.position = target + new Vector3(3.8f, 4.8f, 6f);
                    camera.transform.LookAt(target);
                    camera.orthographicSize = 1.75f;
                    Render(camera, 1024, 1024, Output + "/" + Names[i] + "_Unity.png");
                }
                models[0].SetActive(true); models[1].SetActive(true);
                models[0].transform.position = new Vector3(-1.45f, 0, 0);
                models[1].transform.position = new Vector3(1.45f, 0, 0);
                camera.transform.position = new Vector3(0, 7.2f, 9f);
                camera.transform.LookAt(new Vector3(0, .48f, .2f));
                camera.orthographicSize = 2.65f;
                Render(camera, 1600, 1000, Output + "/StarterWeapons_L1_Unity.png");
                camera.transform.position = new Vector3(0, 12f, 9f);
                camera.transform.LookAt(new Vector3(0, .48f, .2f));
                camera.orthographicSize = 3.5f;
                Render(camera, 1200, 750, Output + "/StarterWeapons_L1_GameAngle.png");
                // Leave an editable art-only review scene with the actual prefabs.
                camera.enabled = true;
                EditorSceneManager.SaveScene(scene, Folder + "/StarterWeapons_L1_Review.unity");
            }
            finally
            {
                if (!replaceBlankBatchScene) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static void Render(Camera camera, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var old = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            Texture2D pixels = null;
            try
            {
                rt.Create(); camera.targetTexture = rt; camera.aspect = width / (float)height;
                camera.Render(); camera.Render();
                RenderTexture.active = rt;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = old;
                if (pixels != null) Object.DestroyImmediate(pixels);
                rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }
}
