using System.IO;
using UnityEditor;
using UnityEngine;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// Codex 해안포대·보급선 모델 적용(2026-10-13): 해안포대 `ArtSource/CoastalBatteryAngular_v27`(각형 강판 방패·긴 포신),
    /// 보급선 `ArtSource/SupplyShipAngular_v28`(회색 함대 보급함 — 컨테이너 대신 화물창·크레인·보급 기둥).
    ///   - 원본 FBX 두 개를 `Art/Models/Coastal`로 복사·임포트(축 변환 굽기, 재질은 이름이 같은 팔레트가 있으면 그것으로)
    ///   - 해안포대: NavalCoastalBatteryBuilder를 다시 돌린다 — 모델이 있으면 모델의 TurretPivot·ElevationPivot·Muzzle을 그대로 쓰고,
    ///     물속까지 이어지는 바위 받침 위에 진지를 올린다. 적 데이터 값은 빌더가 쓰는 값 그대로.
    ///   - 보급선: `Resources/TaskForce/SupplyShip.prefab`(모델 + 앞 = +Z, 원점 = 수면선)을 만든다. 게임은 보급선이 생길 때
    ///     이것을 불러 0.75배로 붙인다(없으면 예전 상자 모델).
    /// 다른 적·프리팹·씬은 건드리지 않는다. 배치 실행: -executeMethod Game.EditorTools.NavalCoastalSupplyArt.Run
    /// </summary>
    public static class NavalCoastalSupplyArt
    {
        private static readonly (string folder, string name)[] Sources =
        {
            ("ArtSource/CoastalBatteryAngular_v27/Models", "ENE_CoastalBattery_Art"),
            ("ArtSource/SupplyShipAngular_v28/Models", "ALLY_SupplyShip_Art"),
        };
        private const string ModelDir = Root + "/Art/Models/Coastal";
        private const string SupplyModel = ModelDir + "/ALLY_SupplyShip_Art.fbx";
        private const string SupplyPrefab = Root + "/Resources/TaskForce/SupplyShip.prefab";

        [MenuItem("Naval/Art/Apply Coastal Battery v27 · Supply Ship v28")]
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            EnsureFolder(ModelDir);
            foreach (var (folder, name) in Sources)
            {
                string from = Path.Combine(project, folder, name + ".fbx");
                if (!File.Exists(from)) { Debug.LogError($"[Coastal·Supply] 원본 없음: {from}"); return; }
                string to = $"{ModelDir}/{name}.fbx";
                File.Copy(from, Path.Combine(project, to), true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                NavalPlayerAviationArt.ImportAndRemap(to);
            }

            NavalCoastalBatteryBuilder.Run();
            var supply = BuildSupplyPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Coastal·Supply] 해안포대 v27 프리팹 다시 만듦 · 보급선 v28 {(supply != null ? "O" : "X")}");
        }

        private static GameObject BuildSupplyPrefab()
        {
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(SupplyModel);
            if (art == null) { Debug.LogError($"[Coastal·Supply] 보급선 모델 없음: {SupplyModel}"); return null; }
            var root = new GameObject("SupplyShip");
            try
            {
                // 위: 함 가운데 위쪽의 보급 기둥 점이 수면선 점보다 위(레이더는 선미 쪽이라 위 판정에 쓰면 배가 세로로 선다)
                // 앞: 뱃머리(Bow)가 선미(Stern)보다 +Z · 원점은 수면선 그대로(바닥에 맞추지 않음)
                NavalPlayerAviationArt.Mount(art, root.transform, "ModelPivot", 1f, "ReplenishmentPoint_P", "Waterline", "Bow", "Stern", groundAtZero: false);
                EnsureFolder(Root + "/Resources/TaskForce");
                return PrefabUtility.SaveAsPrefabAsset(root, SupplyPrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
