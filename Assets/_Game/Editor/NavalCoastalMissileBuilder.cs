using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Data;
using Game.Enemies;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 해안 미사일 포대(2026-11-15, 고정 적) 프리팹과 적 데이터를 만든다. 다른 적·씬은 건드리지 않는다.
    ///   - Prefabs/Enemies/ENE_CoastalMissile.prefab: Codex MIL_MissileBattery_2(Resources/Environment/Military) + 판정 상자 + CoastalMissileBattery
    ///     섬 재질 이름(MAT_Island…)은 게임 섬 재질로 바꿔 끼운다. 미사일 · 포구 섬광은 미사일정 · 해안포대 것을 빌린다
    ///   - Resources/Enemies/ene_coastal_missile.asset: 체력 100 · 사거리 50 · 미사일 20 × 2발 · 12초 · 경험치 7 · 대형 수상 표적
    /// 배치 실행: -executeMethod Game.EditorTools.NavalCoastalMissileBuilder.Run
    /// </summary>
    public static class NavalCoastalMissileBuilder
    {
        private const string PrefabPath = Root + "/Prefabs/Enemies/ENE_CoastalMissile.prefab";
        private const string DefPath = Root + "/Resources/Enemies/ene_coastal_missile.asset";
        public const string ModelPath = Root + "/Resources/Environment/Military/MIL_MissileBattery_2.fbx";

        [MenuItem("Naval/Enemies/Build Coastal Missile Battery", priority = 61)]
        public static void Run()
        {
            EnsureFolder(Root + "/Resources/Enemies");
            var missileBoat = FindEnemy("ene_missileboat");
            var gunBattery = FindEnemy("ene_coastal_battery");
            if (missileBoat == null || missileBoat.MissilePrefab == null) { Debug.LogError("[Coastal Missile] 미사일정 미사일을 찾지 못했습니다."); return; }
            GameObject flash = null;
            var gb = gunBattery != null && gunBattery.Prefab != null ? gunBattery.Prefab.GetComponent<CoastalBattery>() : null;
            if (gb != null) flash = new SerializedObject(gb).FindProperty("muzzleFlash").objectReferenceValue as GameObject;

            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefPath) ?? CreateSO<EnemyDefinition>(DefPath);
            var prefab = BuildPrefab(def, flash);
            if (prefab == null) return;
            Configure(def, so =>
            {
                Set(so, "id", "ene_coastal_missile");
                Set(so, "displayName", "해안 미사일 포대");
                Set(so, "kind", (int)TargetKind.Surface);
                Set(so, "prefab", prefab);
                Set(so, "category", (int)TargetCategory.LargeSurface);
                Set(so, "rank", (int)EnemyRank.Normal);
                Set(so, "maxHp", 100f);
                Set(so, "moveSpeed", 0f);
                Set(so, "turnRateDegPerSec", 0f);
                Set(so, "preferredRange", 50f);
                Set(so, "attackDamage", 20f);
                Set(so, "attackCooldown", 12f);
                Set(so, "missilePrefab", missileBoat.MissilePrefab);
                Set(so, "salvoSize", 2);
                Set(so, "salvoSpacing", 0.45f);
                Set(so, "xpReward", 7);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[Coastal Missile] ENE_CoastalMissile · ene_coastal_missile 생성 완료.");
        }

        private static GameObject BuildPrefab(EnemyDefinition def, GameObject flash)
        {
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (art == null) { Debug.LogError("[Coastal Missile] 모델을 찾지 못했습니다: " + ModelPath); return null; }
            var root = new GameObject("ENE_CoastalMissile");
            try
            {
                root.layer = Factions.EnemyLayer;
                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(4.8f, 3.2f, 5.6f);
                col.center = new Vector3(0f, 1.6f, 0f);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(art, visual);
                model.name = "MIL_MissileBattery_2";
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // 섬 재질 이름은 게임 섬 재질로(FBX 기본 재질은 색만 비슷하다)
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null || !mats[i].name.StartsWith("MAT_Island")) continue;
                        var m = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Resources/Islands/{mats[i].name}.mat");
                        if (m != null) mats[i] = m;
                    }
                    r.sharedMaterials = mats;
                }

                Transform Deep(string n)
                {
                    foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                    return null;
                }
                var turret = Deep("TurretPivot");
                var elevation = Deep("ElevationPivot");
                var muzzle = Deep("Muzzle");
                var lp1 = Deep("LaunchPoint_01");
                var lp2 = Deep("LaunchPoint_02");
                if (turret == null || elevation == null || lp1 == null)
                    Debug.LogError("[Coastal Missile] 모델에서 TurretPivot · ElevationPivot · LaunchPoint를 찾지 못했습니다.");

                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Factions.EnemyLayer;
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                var battery = root.AddComponent<CoastalMissileBattery>();
                Configure(battery, so =>
                {
                    Set(so, "definition", def);
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.muzzle", muzzle != null ? muzzle : lp1);
                    Set(so, "elevationPivot", elevation);
                    SetArray(so, "launchPoints", lp1, lp2);
                    Set(so, "muzzleFlash", flash);
                });
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static EnemyDefinition FindEnemy(string id)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && d.Id == id) return d;
            }
            return null;
        }
    }
}
