using System.IO;
using UnityEditor;
using UnityEngine;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 모듈 프리팹을 한 장의 아이콘 이미지로 구워낸다.
    ///
    /// 정비 카드에 3D 미리보기를 넣으려면 카드마다 카메라를 띄우는 방법도 있지만,
    /// 카드는 잠깐 보이고 마는 UI라 매번 렌더할 이유가 없다.
    /// 셋업할 때 한 번 구워 스프라이트로 저장해두고 카드가 그것을 쓴다.
    /// </summary>
    public static class NavalIconBaker
    {
        private const int Size = 384;   // 카드 그림이 커져 256은 흐리다
        [InitializeOnLoadMethod]
        private static void ScheduleMissingModuleIcons()
        {
            EditorApplication.delayCall += BakeMissingModuleIcons;
        }

        [MenuItem("Naval/Art/Bake Missing Module Icons")]
        private static void BakeMissingModuleIcons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool changed = false;
            foreach (var guid in AssetDatabase.FindAssets("t:ModuleDefinition", new[] { Root + "/Data/Modules" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<Game.Modules.ModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.Icon != null || def.Prefab == null) continue;
                var icon = Bake(def.Prefab, def.name);
                if (icon == null) continue;
                var so = new SerializedObject(def);
                so.FindProperty("icon").objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
        }
        // 배경은 투명 — 카드의 그림 틀 색이 그대로 보이게
        private static readonly Color Background = new(0.10f, 0.12f, 0.15f, 0f);

        /// <summary>
        /// 모든 모듈 아이콘을 지금 프리팹(모델)으로 다시 굽고, 블록 강화 단계(U1·U2) 아이콘도 구워 강화 데이터에 연결한다.
        /// 기존 아이콘 파일을 같은 경로에 덮어써 ModuleDefinition의 참조는 그대로 유지된다.
        /// 배치 실행: -executeMethod Game.EditorTools.NavalIconBaker.RebakeAllModuleIcons
        /// </summary>
        [MenuItem("Naval/Art/Rebake All Module Icons")]
        public static void RebakeAllModuleIcons()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ModuleDefinition", new[] { Root + "/Data/Modules" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<Game.Modules.ModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.Prefab == null) continue;
                var icon = Bake(def.Prefab, def.name);
                if (icon == null || def.Icon == icon) continue;
                var so = new SerializedObject(def);
                so.FindProperty("icon").objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);
            }

            // 블록 강화 단계 아이콘: 프리팹 안 Visual_Level2(U1)·Visual_Level3(U2)를 켜고 찍는다
            foreach (var guid in AssetDatabase.FindAssets("t:ModuleUpgradeProfile", new[] { Root + "/Resources/Upgrades" }))
            {
                var profile = AssetDatabase.LoadAssetAtPath<Game.Modules.ModuleUpgradeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                var def = profile != null ? profile.Module : null;
                if (def == null || def.Prefab == null) continue;
                var so = new SerializedObject(profile);
                var stages = so.FindProperty("stages");
                for (int level = 1; level <= profile.MaxLevel && level <= stages.arraySize; level++)
                {
                    var icon = Bake(def.Prefab, $"{def.name}_U{level}", level + 1);
                    if (icon != null) stages.GetArrayElementAtIndex(level - 1).FindPropertyRelative("icon").objectReferenceValue = icon;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Icons] 모든 모듈·강화 단계 아이콘을 다시 구웠습니다.");
        }

        /// <summary>
        /// 기관포·76mm·CIWS 아이콘(기본 + 강화 단계)만 다시 굽는다 — 무장 강화 팩(v8)처럼 이 셋의 모델만 바뀌었을 때.
        /// 같은 경로에 덮어써 참조는 그대로다. 배치 실행: -executeMethod Game.EditorTools.NavalIconBaker.RebakeWeaponIcons (그래픽 필요)
        /// </summary>
        [MenuItem("Naval/Art/Rebake Weapon Icons (CIWS · 76mm · Autocannon)")]
        public static void RebakeWeaponIcons() => RebakeIcons("mod_autocannon", "mod_gun76", "mod_ciws");

        /// <summary>함대공·전자전·레이더·기만체·손상 통제반 아이콘만 다시 굽는다(NavalGreyPack v7 적용 뒤). 그래픽 필요.</summary>
        [MenuItem("Naval/Art/Rebake Support Module Icons (SAM · EW · Radar · Decoy · Repair)")]
        public static void RebakeSupportModuleIcons() => RebakeIcons("mod_sam", "mod_ew", "mod_radar", "mod_decoy", "mod_repairbay");

        /// <summary>지정한 모듈 아이콘(강화 단계 포함)만 같은 경로에 다시 굽는다.</summary>
        public static void RebakeIcons(params string[] ids)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ModuleDefinition", new[] { Root + "/Data/Modules" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<Game.Modules.ModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.Prefab == null || System.Array.IndexOf(ids, def.Id) < 0) continue;
                var icon = Bake(def.Prefab, def.name);
                if (icon == null || def.Icon == icon) continue;
                var so = new SerializedObject(def);
                so.FindProperty("icon").objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ModuleUpgradeProfile", new[] { Root + "/Resources/Upgrades" }))
            {
                var profile = AssetDatabase.LoadAssetAtPath<Game.Modules.ModuleUpgradeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                var def = profile != null ? profile.Module : null;
                if (def == null || def.Prefab == null || System.Array.IndexOf(ids, def.Id) < 0) continue;
                var so = new SerializedObject(profile);
                var stages = so.FindProperty("stages");
                for (int level = 1; level <= profile.MaxLevel && level <= stages.arraySize; level++)
                {
                    var icon = Bake(def.Prefab, $"{def.name}_U{level}", level + 1);
                    if (icon != null) stages.GetArrayElementAtIndex(level - 1).FindPropertyRelative("icon").objectReferenceValue = icon;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Icons] {string.Join(", ", ids)} 아이콘(기본·강화)을 다시 구웠습니다.");
        }

        /// <summary>모듈 아이콘을 굽고 스프라이트를 돌려준다. 실패하면 null.</summary>
        /// <param name="visualLevel">블록 강화 외형 단계(1 = 기본형, 2 = U1, 3 = U2). 프리팹에 Visual_LevelN이 없으면 무시.</param>
        public static Sprite Bake(GameObject prefab, string id, int visualLevel = 1)
        {
            if (prefab == null) return null;

            string dir = $"{Root}/Art/Icons";
            EnsureFolder(dir);
            string path = $"{dir}/ICON_{id}.png";

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            GameObject camGo = null, lightGo = null, instance = null;

            try
            {
                // 열려 있는 씬과 겹치지 않도록 멀리 떨어진 곳에서 찍는다
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = new Vector3(5000f, 5000f, 5000f);
                instance.transform.rotation = Quaternion.identity;

                // 강화 외형 단계만 켠다(프리팹에 단계 외형이 없으면 그대로)
                var wanted = instance.transform.Find($"Visual_Level{visualLevel}");
                if (visualLevel > 1 && wanted == null) return null;
                if (wanted != null)
                    for (int i = 1; i <= 3; i++)
                    {
                        var v = instance.transform.Find($"Visual_Level{i}");
                        if (v != null) v.gameObject.SetActive(i == visualLevel);
                    }

                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;

                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                camGo = new GameObject("IconCamera", typeof(Camera));
                var cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;
                cam.orthographic = true;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 200f;
                cam.targetTexture = rt;

                // 실루엣이 가장 잘 읽히는 살짝 위에서 본 대각선 각도.
                // 경계 상자 8꼭짓점을 화면 평면에 투영해 모델이 가운데에 오고 가장자리에 12% 여백이 남게 맞춘다.
                var rot = Quaternion.Euler(24f, 38f, 0f);
                var inv = Quaternion.Inverse(rot);
                Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
                foreach (var r in renderers)
                {
                    var b = r.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        var local = inv * (corner - bounds.center);
                        min = Vector2.Min(min, local);
                        max = Vector2.Max(max, local);
                    }
                }
                Vector2 mid = (min + max) * 0.5f;
                float half = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f;
                cam.orthographicSize = Mathf.Max(0.3f, half * 1.12f);
                Vector3 focus = bounds.center + rot * new Vector3(mid.x, mid.y, 0f);
                camGo.transform.SetPositionAndRotation(focus - rot * Vector3.forward * 50f, rot);

                lightGo = new GameObject("IconLight", typeof(Light));
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                lightGo.transform.rotation = Quaternion.Euler(40f, 25f, 0f);

                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;

                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                RenderTexture.active = previous;

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (lightGo != null) Object.DestroyImmediate(lightGo);

                rt.Release();
                Object.DestroyImmediate(rt);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
