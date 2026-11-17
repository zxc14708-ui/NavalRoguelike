using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Modules;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 노봉·곡사포·경어뢰 발사관·충각 함수 강화(2026-10-11). 이 네 블록만 바꾼다 — 다른 프리팹·데이터·씬은 건드리지 않는다.
    ///   1. Resources/Upgrades/UPG_Nobong·Howitzer·TorpedoTube·RamBow(이미 있으면 그대로) — NavalUpgradeBuilder.AddWeaponPackProfiles
    ///   2. Prefabs/Modules/MOD_*.prefab: 예전 그레이박스 외형을 지우고 Codex 강화 외형(Art/WeaponConcepts/{이름}_T0~T2)을
    ///      Visual_Level1~3으로 넣는다(ModuleUpgradeVisuals). 프리팹 연결은 풀어서(복사본) 넣는다 — 시안 생성기를 다시 돌려도
    ///      실제 무기 프리팹의 조준·발사 참조가 깨지지 않게. 곡사포 고각부는 높은 포물선 자세(−45°)로 둔다.
    ///   3. mod_nobong: 좌우 번갈아 한 발씩 쏘므로 한 번에 쓰는 탄 2 → 1, 설명 문구.
    ///   4. 아이콘: 기본 + 강화 I·II(그래픽 필요).
    /// 배치 실행: -executeMethod Game.EditorTools.NavalWeaponUpgradeVisuals.Run
    /// </summary>
    internal static class NavalWeaponUpgradeVisuals
    {
        private const string ArtDir = Root + "/Art/WeaponConcepts";
        private const string ModuleDefDir = Root + "/Resources/Modules";
        private const float HowitzerElevation = -45f;   // 예전 곡사포 프리팹과 같은 고각

        [MenuItem("Naval/Art/Apply Weapon Pack Upgrades (Nobong · Howitzer · Torpedo · Ram)", priority = 42)]
        public static void Run()
        {
            NavalUpgradeBuilder.AddWeaponPackProfiles();
            FixNobongData();

            ApplyNobong();
            ApplyHowitzer();
            ApplyTorpedo();
            Apply<RamBowModule>("MOD_RamBow", "RamBow", null);

            AssetDatabase.SaveAssets();
            BakeIcons("mod_nobong", "mod_howitzer", "mod_torpedotube", "mod_rambow");
            Debug.Log("[Weapon Pack Upgrades] 노봉·곡사포·경어뢰 발사관·충각 함수 강화 외형·데이터·아이콘 적용 완료.");
        }

        // 2026-10-11: Codex 노봉 v22 모델(Art/Models/Nobong)이 있으면 그것을 쓴다(NavalPlayerAviationArt). 없으면 예전 시안 외형.
        private static void ApplyNobong()
        {
            if (!NavalPlayerAviationArt.ApplyNobongV22()) ApplyNobongConcept();
        }

        private static void ApplyNobongConcept() => Apply<NobongModule>("MOD_Nobong", "Nobong", (c, v) => Configure(c, so =>
        {
            Set(so, "weapon.turret", Need(v, "TurretPivot"));
            Set(so, "weapon.elevationPivot", Need(v, "ElevationPivot"));
            Set(so, "weapon.muzzle", Need(v, "Muzzle"));
        }));

        private static void ApplyHowitzer() => Apply<HowitzerModule>("MOD_Howitzer", "Howitzer", (c, v) => Configure(c, so =>
        {
            Set(so, "turret", Need(v, "TurretPivot"));
            Set(so, "barrel", Need(v, "Barrel"));
            Set(so, "muzzle", Need(v, "Muzzle"));
        }), level =>
        {
            var elevation = FindDeep(level, "ElevationPivot");
            if (elevation != null) elevation.localRotation = Quaternion.Euler(HowitzerElevation, 0f, 0f);
        });

        private static void ApplyTorpedo() => Apply<TorpedoTubeModule>("MOD_TorpedoTube", "TorpedoTube", (c, v) => Configure(c, so =>
        {
            Set(so, "mount", Need(v, "TubeMount"));
            SetArray(so, "launchPoints", Need(v, "LaunchPoint_01"), Need(v, "LaunchPoint_02"), Need(v, "LaunchPoint_03"));
        }));

        /// <summary>
        /// 블록 생성기(Naval/Add Nobong 40mm 등)가 프리팹을 그레이박스로 다시 만든 뒤 부른다 — 그 블록만 강화 외형을 다시 넣고 아이콘을 굽는다.
        /// 2026-10-11: 노봉 생성기가 다시 실행돼 강화 외형이 지워진 일이 있었다.
        /// </summary>
        public static void ReapplyAfterRebuild(string moduleId)
        {
            switch (moduleId)
            {
                case "mod_nobong": ApplyNobong(); break;
                case "mod_howitzer": ApplyHowitzer(); break;
                case "mod_torpedotube": ApplyTorpedo(); break;
                case "mod_rambow": Apply<RamBowModule>("MOD_RamBow", "RamBow", null); break;
                default: return;
            }
            AssetDatabase.SaveAssets();
            BakeIcons(moduleId);
        }

        /// <summary>좌우 번갈아 한 발씩: 한 번에 쓰는 탄 1, 설명 문구(ScriptableObject 원본 데이터 — 에디터에서만 고친다).</summary>
        private static void FixNobongData()
        {
            var def = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{ModuleDefDir}/mod_nobong.asset");
            if (def == null) { Debug.LogError("[Weapon Pack Upgrades] mod_nobong을 찾지 못했습니다."); return; }
            Configure(def, so =>
            {
                Set(so, "stats.AmmoPerShot", 1);
                Set(so, "description", NavalNobongBuilder.Description);
            });
        }

        private static void Apply<T>(string prefabName, string artName, Action<T, Transform> bindBase,
                                     Action<Transform> adjustLevel = null) where T : Component
        {
            string path = $"{Root}/Prefabs/Modules/{prefabName}.prefab";
            var levels = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                levels[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtDir}/{artName}_T{i}.prefab");
                if (levels[i] == null) { Debug.LogError($"[Weapon Pack Upgrades] {artName}_T{i} 외형이 없어 {prefabName}을 건너뜁니다."); return; }
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var module = root.GetComponent<T>();
                if (module == null) { Debug.LogError($"[Weapon Pack Upgrades] {prefabName}에 {typeof(T).Name}이 없습니다."); return; }

                // 예전 외형(그레이박스)을 지운다 — 루트에는 모듈 런타임만 있고 자식은 모두 외형이다
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                var visuals = new GameObject[3];
                for (int i = 0; i < 3; i++)
                {
                    var copy = UnityEngine.Object.Instantiate(levels[i], root.transform);   // 프리팹 연결 없는 복사본
                    copy.name = $"Visual_Level{i + 1}";
                    copy.transform.localPosition = Vector3.zero;
                    copy.transform.localRotation = Quaternion.identity;
                    copy.transform.localScale = Vector3.one;
                    adjustLevel?.Invoke(copy.transform);
                    copy.SetActive(i == 0);
                    SetLayerRecursive(copy.transform, root.layer);
                    visuals[i] = copy;
                }

                var upgradeVisuals = root.GetComponent<ModuleUpgradeVisuals>() ?? root.AddComponent<ModuleUpgradeVisuals>();
                Configure(upgradeVisuals, so => SetArray(so, "levels", visuals[0], visuals[1], visuals[2]));
                bindBase?.Invoke(module, visuals[0].transform);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Weapon Pack Upgrades] {prefabName}: Visual_Level1~3 ← {artName}_T0~T2");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BakeIcons(params string[] ids)
        {
            foreach (var id in ids)
            {
                var def = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{ModuleDefDir}/{id}.asset");
                if (def == null || def.Prefab == null) continue;
                var icon = NavalIconBaker.Bake(def.Prefab, def.name);
                if (icon != null) Configure(def, so => Set(so, "icon", icon));

                var profile = ModuleUpgradesEditor.ProfileFor(def);
                if (profile == null) continue;
                var pso = new SerializedObject(profile);
                var stages = pso.FindProperty("stages");
                for (int level = 1; level <= profile.MaxLevel && level <= stages.arraySize; level++)
                {
                    var stageIcon = NavalIconBaker.Bake(def.Prefab, $"{def.name}_U{level}", level + 1);
                    if (stageIcon != null) stages.GetArrayElementAtIndex(level - 1).FindPropertyRelative("icon").objectReferenceValue = stageIcon;
                }
                pso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
        }

        private static Transform Need(Transform root, string name)
        {
            var t = FindDeep(root, name);
            if (t == null) throw new InvalidOperationException($"[Weapon Pack Upgrades] {root.name}에 {name}이 없습니다.");
            return t;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursive(child, layer);
        }

        /// <summary>에디터에서 강화 데이터 찾기(런타임 캐시와 무관하게 에셋에서 직접).</summary>
        private static class ModuleUpgradesEditor
        {
            public static ModuleUpgradeProfile ProfileFor(ModuleDefinition def)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:ModuleUpgradeProfile", new[] { Root + "/Resources/Upgrades" }))
                {
                    var p = AssetDatabase.LoadAssetAtPath<ModuleUpgradeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                    if (p != null && p.Module == def) return p;
                }
                return null;
            }
        }
    }
}
