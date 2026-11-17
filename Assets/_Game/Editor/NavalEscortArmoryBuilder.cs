using UnityEditor;
using UnityEngine;
using Game.TaskForce;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 호위함 실제 무장(2026-10-11): Resources/TaskForce/EscortArmory를 만들어 플레이어 블록과 같은 탄·미사일·어뢰·헬기 프리팹을 연결한다.
    /// 다른 프리팹·데이터는 건드리지 않는다. 배치 실행: -executeMethod Game.EditorTools.NavalEscortArmoryBuilder.Run
    /// (같은 배치에서 노봉 강화 외형도 다시 넣는다 — 노봉 생성기를 다시 돌려 외형이 지워졌을 때.)
    /// </summary>
    public static class NavalEscortArmoryBuilder
    {
        private const string Path = Root + "/Resources/TaskForce/EscortArmory.asset";
        private const string Projectiles = Root + "/Prefabs/Projectiles";

        [MenuItem("Naval/Task Force/Build Escort Armory", priority = 60)]
        public static void Build()
        {
            var armory = AssetDatabase.LoadAssetAtPath<EscortArmory>(Path);
            if (armory == null)
            {
                armory = ScriptableObject.CreateInstance<EscortArmory>();
                AssetDatabase.CreateAsset(armory, Path);
            }
            GameObject P(string name)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Projectiles}/{name}.prefab");
                if (go == null) Debug.LogWarning($"[EscortArmory] {name} 없음");
                return go;
            }
            armory.heavyRound = P("PRJ_Gun76");
            armory.lightRound = P("PRJ_Autocannon");
            armory.ciwsRound = P("PRJ_Ciws");
            armory.samMissile = P("MIS_PlayerSam");
            armory.strikeMissile = P("MIS_PlayerVls");
            armory.aswTorpedo = P("TOR_PlayerAsw");
            armory.aswRocket = P("MIS_PlayerRocket");
            armory.helicopter = P("HEL_Asw");
            EditorUtility.SetDirty(armory);
            AssetDatabase.SaveAssets();
            Debug.Log("[EscortArmory] 호위함 실제 무장 프리팹 연결 완료");
        }

        public static void Run()
        {
            Build();
            NavalWeaponUpgradeVisuals.ReapplyAfterRebuild("mod_nobong");
        }
    }
}
