using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Modules;

namespace Game.EditorTools
{
    /// <summary>
    /// 블록 강화 데이터와 예전 모듈 타입 정리. 씬·프리팹·다른 데이터는 건드리지 않는다.
    ///   - Naval/Add Module Upgrade Profiles: Resources/Upgrades에 기관포·76mm·CIWS·탄약고 강화 데이터를 만든다(이미 있으면 그대로 둔다).
    ///     2026-10-11: 노봉·곡사포·경어뢰 발사관·충각 함수 추가(외형은 NavalWeaponUpgradeVisuals가 프리팹에 넣는다).
    ///   - Naval/Migrate Legacy Module Types: mod_armor(8=헬기데크로 잘못 저장) → Armor, mod_generator(3=VLS) → Generator.
    /// 외형(U1/U2 모델)은 기존 메뉴 Naval/Art/Apply Module Upgrade Models가 프리팹 안에 넣는다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalUpgradeBuilder.RunBatch
    /// </summary>
    public static class NavalUpgradeBuilder
    {
        private const string ModuleDir = "Assets/_Game/Data/Modules";
        private const string UpgradeDir = "Assets/_Game/Resources/Upgrades";
        private const string ResourceModuleDir = "Assets/_Game/Resources/Modules";   // 2026-10-08 이후 추가 무장

        public static void RunBatch()
        {
            MigrateLegacyModuleTypes();
            AddModuleUpgradeProfiles();
        }

        [MenuItem("Naval/Add Module Upgrade Profiles", priority = 4)]
        public static void AddModuleUpgradeProfiles()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources")) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (!AssetDatabase.IsValidFolder(UpgradeDir)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "Upgrades");

            // 모든 배율은 기본값 대비(강화 II는 I에 다시 곱하지 않는다)
            Make("UPG_Autocannon", "mod_autocannon",
                Stage("기관포 강화 I", "피해 +25% · 탄창 +15%", m => { m.damageMultiplier = 1.25f; m.magazineMultiplier = 1.15f; }),
                Stage("기관포 강화 II", "피해 +50% · 탄창 +30% · 포탑 선회 +15%", m => { m.damageMultiplier = 1.5f; m.magazineMultiplier = 1.3f; m.turnSpeedMultiplier = 1.15f; }));
            Make("UPG_Gun76", "mod_gun76",
                Stage("76mm 함포 강화 I", "피해 +25% · 발사 간격 −5%", m => { m.damageMultiplier = 1.25f; m.cooldownMultiplier = 0.95f; }),
                Stage("76mm 함포 강화 II", "피해 +50% · 발사 간격 −10%", m => { m.damageMultiplier = 1.5f; m.cooldownMultiplier = 0.9f; }));
            // CIWS: 공격력·요격 사거리·연사는 바꾸지 않는다. "재장전" = 탄창이 비었을 때의 전량 재장전 시간
            Make("UPG_Ciws", "mod_ciws",
                Stage("CIWS 강화 I", "추적 속도 +20% · 탄창 +20%", m => { m.turnSpeedMultiplier = 1.2f; m.magazineMultiplier = 1.2f; }),
                Stage("CIWS 강화 II", "추적 속도 +35% · 탄창 +40% · 재장전 −15%", m => { m.turnSpeedMultiplier = 1.35f; m.magazineMultiplier = 1.4f; m.ammoReloadMultiplier = 0.85f; }));
            // 탄약고: 무기 성능 전체가 아니라 탄약고가 주는 지원량 자체만 늘린다
            Make("UPG_Magazine", "mod_magazine",
                Stage("탄약고 강화 I", "지원 효과 +15%", m => { m.supplyMultiplier = 1.15f; }),
                Stage("탄약고 강화 II", "지원 효과 +30% · 유폭 피해 −25%", m => { m.supplyMultiplier = 1.3f; m.explosionDamageMultiplier = 0.75f; }));
            AddWeaponPackProfiles();

            AssetDatabase.SaveAssets();
            Debug.Log("[Upgrade Profiles] 완료");
        }

        /// <summary>
        /// 노봉·곡사포·경어뢰 발사관·충각 함수 강화(2026-10-11). 기존 강화와 같은 폭(I ≈ 주 능력 +25%, II ≈ +50%).
        /// 곡사포 포탄 수는 HowitzerModule.shellsByLevel(1·2·3발)이 정하고, 여기에는 문구와 사거리만 둔다.
        /// </summary>
        public static void AddWeaponPackProfiles()
        {
            Make("UPG_Nobong", "mod_nobong",
                Stage("40mm 쌍열포 강화 I", "피해 +25% · 탄창 +25%", m => { m.damageMultiplier = 1.25f; m.magazineMultiplier = 1.25f; }),
                Stage("40mm 쌍열포 강화 II", "피해 +50% · 탄창 +50% · 사거리 +15%", m => { m.damageMultiplier = 1.5f; m.magazineMultiplier = 1.5f; m.rangeMultiplier = 1.15f; }));
            Make("UPG_Howitzer", "mod_howitzer",
                Stage("곡사포 강화 I", "한 번에 2발(적 진행 방향 앞뒤로 벌려 떨어짐)", m => { }),
                Stage("곡사포 강화 II", "한 번에 3발(삼각형으로 벌려 떨어짐) · 사거리 +15%", m => { m.rangeMultiplier = 1.15f; }));
            Make("UPG_TorpedoTube", "mod_torpedotube",
                Stage("경어뢰 발사관 강화 I", "피해 +20% · 발사 간격 −15% · 보급 15% 빠르게",
                      m => { m.damageMultiplier = 1.2f; m.cooldownMultiplier = 0.85f; m.ammoReloadMultiplier = 0.85f; }),
                Stage("경어뢰 발사관 강화 II", "피해 +40% · 발사 간격 −25% · 보급 25% 빠르게 · 사거리 +15%",
                      m => { m.damageMultiplier = 1.4f; m.cooldownMultiplier = 0.75f; m.ammoReloadMultiplier = 0.75f; m.rangeMultiplier = 1.15f; }));
            Make("UPG_RamBow", "mod_rambow",
                Stage("충각 함수 강화 I", "피해 +25% · 내구 +30%", m => { m.damageMultiplier = 1.25f; m.maxHpMultiplier = 1.3f; }),
                Stage("충각 함수 강화 II", "피해 +50% · 내구 +50% · 반동 피해 25% → 10%",
                      m => { m.damageMultiplier = 1.5f; m.maxHpMultiplier = 1.5f; m.selfDamageMultiplier = 0.4f; }));
            AssetDatabase.SaveAssets();
        }

        private static ModuleUpgradeStage Stage(string name, string desc, System.Action<ModuleStatModifier> set)
        {
            var mod = new ModuleStatModifier();
            set(mod);
            return new ModuleUpgradeStage(name, desc, mod);
        }

        private static void Make(string assetName, string moduleId, params ModuleUpgradeStage[] stages)
        {
            string path = $"{UpgradeDir}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<ModuleUpgradeProfile>(path) != null)
            {
                Debug.Log($"[Upgrade Profiles] {assetName}은 이미 있어 그대로 둡니다(수치를 덮어쓰지 않음).");
                return;
            }
            var def = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{ModuleDir}/{moduleId}.asset")
                      ?? AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{ResourceModuleDir}/{moduleId}.asset");
            if (def == null) { Debug.LogWarning($"[Upgrade Profiles] {moduleId}를 찾지 못해 {assetName}을 만들지 않았습니다."); return; }

            var profile = ScriptableObject.CreateInstance<ModuleUpgradeProfile>();
            profile.EditorSetup(def, new List<ModuleUpgradeStage>(stages));
            AssetDatabase.CreateAsset(profile, path);
            Debug.Log($"[Upgrade Profiles] {assetName} 생성");
        }

        /// <summary>
        /// 예전 에셋의 타입 값을 고친다. enum 순서는 바꾸지 않고 끝에 추가한 Armor(15)·Generator(16)로 옮긴다.
        /// id가 정확히 맞는 에셋만 바꾸고, 이미 맞으면 아무것도 하지 않는다.
        /// </summary>
        [MenuItem("Naval/Migrate Legacy Module Types", priority = 5)]
        public static void MigrateLegacyModuleTypes()
        {
            Fix("mod_armor", ModuleType.Armor);
            Fix("mod_generator", ModuleType.Generator);
            AssetDatabase.SaveAssets();
        }

        private static void Fix(string id, ModuleType type)
        {
            var def = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{ModuleDir}/{id}.asset");
            if (def == null || def.Id != id) return;
            if (def.Type == type) return;
            var so = new SerializedObject(def);
            var prop = so.FindProperty("type");
            int before = prop.intValue;
            prop.intValue = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            Debug.Log($"[Migrate] {id}: type {before} → {(int)type} ({type})");
        }
    }
}
