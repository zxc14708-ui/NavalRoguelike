using UnityEditor;
using UnityEngine;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 프로토타입에 필요한 에셋과 씬을 한 번에 생성한다.
    /// 손으로 드래그해서 만드는 대신 이 스크립트를 고쳐 다시 돌리는 방식으로 작업한다.
    /// </summary>
    public static class NavalPrototypeSetup
    {
        [MenuItem("Naval/Upgrade Stage 1 Coastal Boss", priority = 4)]
        public static void UpgradeStage1Boss() => ApplyStage1BossUpgrade();

        private static bool ApplyStage1BossUpgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating) return false;
            var boss = NavalPrefabBuilder.BuildStage1BossOnly();
            if (boss == null)
            {
                Debug.LogError("[Stage1 Boss] PatrolBoat_CoastalBoss 모델 임포트가 완료되지 않았습니다.");
                return false;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Stage1 Boss] 연안 경비정 모델·발사관·함포 프리팹 적용 완료.");
            return true;
        }

        /// <summary>
        /// 현재 씬을 건드리지 않고 모델 임포트와 프리팹/데이터 참조만 갱신한다.
        /// 아트 교체 때 전체 Prototype_Main 씬을 다시 만드는 일을 피하기 위한 메뉴다.
        /// </summary>
        /// <summary>
        /// 스테이지 2 추가 적(순항미사일 잠수함·엘리트 초계함)의 모델 임포트·프리팹·데이터·웨이브 가중치만 만든다.
        /// 다른 프리팹·데이터·씬은 건드리지 않는다. 배치 실행: -executeMethod Game.EditorTools.NavalPrototypeSetup.AddStage2Enemies
        /// </summary>
        [MenuItem("Naval/Add Stage 2 Enemies (Cruise Sub, PCC)", priority = 2)]
        public static void AddStage2Enemies()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                (!Application.isBatchMode && (EditorApplication.isCompiling || EditorApplication.isUpdating)))
            {
                Debug.LogWarning("[Stage2 Enemies] 재생·컴파일·임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            var prefabs = NavalPrefabBuilder.BuildStage2EnemiesOnly();
            NavalDataBuilder.BuildStage2EnemiesOnly(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Stage2 Enemies] 완료: 잠수함 {(prefabs.CruiseSubmarine != null ? "O" : "X")}, 초계함 {(prefabs.PccCorvette != null ? "O" : "X")}");
        }

        /// <summary>
        /// 자폭 보트의 프리팹·데이터·웨이브 가중치만 만든다. 다른 프리팹·데이터·씬은 건드리지 않는다.
        /// 배치 실행: -executeMethod Game.EditorTools.NavalPrototypeSetup.AddSuicideBoat
        /// </summary>
        [MenuItem("Naval/Add Suicide Boat", priority = 3)]
        public static void AddSuicideBoat()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                (!Application.isBatchMode && (EditorApplication.isCompiling || EditorApplication.isUpdating)))
            {
                Debug.LogWarning("[Suicide Boat] 재생·컴파일·임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            var prefabs = NavalPrefabBuilder.BuildSuicideBoatOnly();
            NavalDataBuilder.BuildSuicideBoatOnly(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Suicide Boat] 완료: 프리팹 {(prefabs.SuicideBoat != null ? "O" : "X")}");
        }

        /// <summary>
        /// 일반 적 5종(장갑 돌격정·경어뢰정·포격 지원정·수리 지원정·기뢰부설정)과 기뢰의 프리팹·데이터·웨이브 가중치만 만든다.
        /// 다른 프리팹·데이터·씬은 건드리지 않는다. 배치 실행: -executeMethod Game.EditorTools.NavalPrototypeSetup.AddNormalEnemies
        /// </summary>
        [MenuItem("Naval/Add Normal Enemies (5)", priority = 3)]
        public static void AddNormalEnemies()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                (!Application.isBatchMode && (EditorApplication.isCompiling || EditorApplication.isUpdating)))
            {
                Debug.LogWarning("[Normal Enemies] 재생·컴파일·임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            var prefabs = NavalPrefabBuilder.BuildNormalEnemiesOnly();
            NavalDataBuilder.BuildNormalEnemiesOnly(prefabs);
            AssetDatabase.SaveAssets();
            string O(GameObject g) => g != null ? "O" : "X";
            Debug.Log($"[Normal Enemies] 완료: 돌격정 {O(prefabs.ArmoredBoat)} 어뢰정 {O(prefabs.TorpedoBoat)} 포격정 {O(prefabs.ArtilleryBoat)} " +
                      $"수리정 {O(prefabs.RepairBoat)} 기뢰부설정 {O(prefabs.MineLayer)} 기뢰 {O(prefabs.SeaMine)}");
        }

        /// <summary>
        /// 미니어처 적 v9 모델 8종(고속정·자폭 보트·연안 경비정·잠수함·순항미사일 잠수함·항공전함·전투기·정찰기)의 프리팹만 다시 만든다.
        /// 다른 적·모듈·데이터·씬은 건드리지 않는다. 배치 실행: -executeMethod Game.EditorTools.NavalPrototypeSetup.ApplyEnemyFleetV9
        /// </summary>
        [MenuItem("Naval/Art/Apply Enemy Fleet v9 Models", priority = 40)]
        public static void ApplyEnemyFleetV9()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                (!Application.isBatchMode && (EditorApplication.isCompiling || EditorApplication.isUpdating)))
            {
                Debug.LogWarning("[Enemy Fleet v9] 재생·컴파일·임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            var r = NavalPrefabBuilder.ApplyEnemyFleetV9Only();
            AssetDatabase.SaveAssets();
            string O(GameObject g) => g != null ? "O" : "X";
            Debug.Log($"[Enemy Fleet v9] 완료: 고속정 {O(r.FastBoat)} 자폭 보트 {O(r.SuicideBoat)} 연안 경비정 {O(r.Boss)} 잠수함 {O(r.Submarine)} " +
                      $"순항 잠수함 {O(r.CruiseSubmarine)} 항공전함 {O(r.Boss2)} 전투기 {O(r.Fighter)} 정찰기 {O(r.Recon)}");
        }

        /// <summary>
        /// 스테이지 2 보스를 현대화 초계함으로 바꾼다: 프리팹(ENE_BossCorvette)·데이터(ene_boss_corvette)를 만들고
        /// RoundSet_Stage2 마지막 구간의 보스만 바꾼다. 다른 프리팹·데이터·씬은 건드리지 않는다(항공전함은 남겨 둠).
        /// 배치 실행: -executeMethod Game.EditorTools.NavalPrototypeSetup.AddCorvetteBoss
        /// </summary>
        [MenuItem("Naval/Add Stage 2 Corvette Boss", priority = 4)]
        public static void AddCorvetteBoss()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                (!Application.isBatchMode && (EditorApplication.isCompiling || EditorApplication.isUpdating)))
            {
                Debug.LogWarning("[Corvette Boss] 재생·컴파일·임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            var prefabs = NavalPrefabBuilder.BuildModernCorvetteOnly();
            var def = NavalDataBuilder.BuildCorvetteBossOnly(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Corvette Boss] 완료: 프리팹 {(prefabs.ModernCorvette != null ? "O" : "X")} · 데이터 {(def != null ? "O" : "X")}");
        }

        /// <summary>
        /// 두 스테이지의 보스 구간 일반 스폰을 고속정·자폭 드론·자폭 보트만 조금씩으로 바꾼다(보스전 집중). 다른 구간·데이터는 그대로.
        /// 배치 실행: -executeMethod Game.EditorTools.NavalPrototypeSetup.ApplyBossRoundFocus
        /// </summary>
        [MenuItem("Naval/Apply Boss Round Focus", priority = 5)]
        public static void ApplyBossRoundFocus()
        {
            NavalDataBuilder.ApplyBossRoundFocus();
            Debug.Log("[Boss Round Focus] 완료: 보스 구간 = 고속정 1 · 자폭 드론 0.5 · 자폭 보트 0.4, 초당 " +
                      $"{NavalDataBuilder.BossRoundSpawnRate} · 동시 {NavalDataBuilder.BossRoundMaxAlive}");
        }

        /// <summary>
        /// 스테이지 3을 만든다: 적 4종(무인 공격정·전자전 코르벳·방공 프리깃·공격 잠수함)과 항공전함(현대화 이세급 v10 모델) 프리팹,
        /// 적 데이터, RoundSet_Stage3, 스테이지 2 → 3 연결. 다른 프리팹·스테이지·씬은 건드리지 않는다.
        /// 배치 실행: -executeMethod Game.EditorTools.NavalPrototypeSetup.AddStage3
        /// </summary>
        [MenuItem("Naval/Add Stage 3", priority = 6)]
        public static void AddStage3()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                (!Application.isBatchMode && (EditorApplication.isCompiling || EditorApplication.isUpdating)))
            {
                Debug.LogWarning("[Stage 3] 재생·컴파일·임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            var prefabs = NavalPrefabBuilder.BuildStage3Only();
            var stage3 = NavalDataBuilder.BuildStage3(prefabs);
            AssetDatabase.SaveAssets();
            string O(UnityEngine.Object o) => o != null ? "O" : "X";
            Debug.Log($"[Stage 3] 완료: 항공전함 {O(prefabs.Boss2)} · 무인 공격정 {O(prefabs.UnmannedCraft)} · 전자전 코르벳 {O(prefabs.EwCorvette)} · " +
                      $"방공 프리깃 {O(prefabs.AaFrigate)} · 공격 잠수함 {O(prefabs.AttackSubmarine)} · RoundSet_Stage3 {O(stage3)} · 구간 {(stage3 != null ? stage3.Count : 0)}");
        }

        [MenuItem("Naval/Refresh Art Prefabs", priority = 1)]
        public static void RefreshArtPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[Art Refresh] 재생·컴파일·임포트가 끝난 뒤 다시 실행하세요.");
                return;
            }

            EnsureAllFolders();
            var prefabs = NavalPrefabBuilder.BuildAll();
            NavalDataBuilder.BuildAll(prefabs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Art Refresh] 새 해군 모델과 프리팹 참조 갱신 완료.");
        }

        [MenuItem("Naval/Setup Prototype Scene", priority = 0)]
        public static void Run()
        {
            // 재생 중에는 새 씬을 만들 수 없다. 조용히 실패하지 않도록 먼저 막는다.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("프로토타입 셋업",
                    "재생 중에는 실행할 수 없습니다.\n먼저 ■(정지) 버튼을 눌러 재생을 멈춰주세요.", "확인");
                return;
            }

            // 컴파일이 끝나기 전에 실행하면 옛 어셈블리로 씬이 만들어져
            // 새로 추가한 필드가 통째로 빠진 채 저장된다.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("프로토타입 셋업",
                    "스크립트 컴파일이 진행 중입니다.\n" +
                    "우측 하단 진행 표시가 사라진 뒤에 다시 실행해주세요.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "프로토타입 셋업",
                    "그레이박스 프리팹, 데이터 에셋, Prototype_Main 씬을 생성합니다.\n" +
                    "현재 열려 있는 씬은 닫히며, 저장하지 않은 변경은 사라집니다.\n\n계속할까요?",
                    "생성", "취소"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Naval Setup", "레이어와 폴더 준비", 0.1f);
                EnsureLayers();
                EnsureAllFolders();

                EditorUtility.DisplayProgressBar("Naval Setup", "그레이박스 프리팹 생성", 0.35f);
                var prefabs = NavalPrefabBuilder.BuildAll();

                EditorUtility.DisplayProgressBar("Naval Setup", "데이터 에셋 생성", 0.65f);
                var data = NavalDataBuilder.BuildAll(prefabs);

                EditorUtility.DisplayProgressBar("Naval Setup", "효과음 설정", 0.75f);
                NavalAudioBuilder.BuildLibrary();

                // 여기서 Refresh()를 부르면 방금 만든 에셋이 재임포트되면서
                // 메모리상의 참조가 무효화되고, 씬에 넣은 값이 전부 null이 된다.
                // 저장만 하고 Refresh는 씬 조립이 끝난 뒤(finally)에 한다.
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("Naval Setup", "씬 조립", 0.85f);
                NavalSceneBuilder.Build(prefabs, data);

                Debug.Log("[Setup] 완료. Prototype_Main 씬을 열고 재생하세요.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureAllFolders()
        {
            EnsureFolder($"{Root}/Art");
            EnsureFolder($"{Root}/Audio");
            EnsureFolder($"{Root}/Scenes");
            EnsureFolder($"{Root}/Data/Config");
            EnsureFolder($"{Root}/Data/Modules");
            EnsureFolder($"{Root}/Data/Enemies");
            EnsureFolder($"{Root}/Data/Waves");
            EnsureFolder($"{Root}/Prefabs/Ship");
            EnsureFolder($"{Root}/Prefabs/Modules");
            EnsureFolder($"{Root}/Prefabs/Enemies");
            EnsureFolder($"{Root}/Prefabs/Projectiles");
            EnsureFolder($"{Root}/Prefabs/VFX");
            EnsureFolder($"{Root}/Prefabs/UI");
        }
    }
}
