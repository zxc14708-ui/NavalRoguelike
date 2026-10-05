using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Core;
using Game.TaskForce;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>편대 시스템(2026-10-02 개편)의 씬 부착, 출항 편성 제거, 정비 카드(편대 배치·역할 지정·개량)와 편대 패널을 빠르게 확인한다.</summary>
    [InitializeOnLoad]
    public static class TaskForceVerification
    {
        private const string ScenePath = "Assets/_Game/Scenes/Prototype_Main.unity";
        private const string KeyRunning = "NavalRoguelike.TaskForceVerify.Running";
        private static double s_enteredAt;
        private static bool s_activated;
        private static string s_failure;

        static TaskForceVerification()
        {
            if (!SessionState.GetBool(KeyRunning, false)) return;
            Hook();
            RequestPlayMode();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(KeyRunning, true);
            s_activated = false;
            s_failure = null;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Hook();
            RequestPlayMode();
        }

        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void RequestPlayMode()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(KeyRunning, false)) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    RequestPlayMode();
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
            };
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            s_enteredAt = EditorApplication.timeSinceStartup;
            s_activated = false;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(KeyRunning, false) || !EditorApplication.isPlaying) return;
            double elapsed = EditorApplication.timeSinceStartup - s_enteredAt;
            if (!s_activated && elapsed > 0.5)
            {
                s_activated = true;
                try
                {
                    // 편대 개편(2026-10-02): 출항 편성(CP)·지원 스킬은 없고, 편대는 정비 카드(배치·역할 지정·개량)로 꾸린다
                    Require(GameObject.Find("Force package overlay") == null, "출항 편성 화면이 아직 남아 있음");
                    Require(GameObject.Find("Task force activity") == null, "지원 알림 배너가 아직 남아 있음");
                    Require(Object.FindFirstObjectByType<TaskForcePanelUI>() != null, "편대 현황 패널(TaskForcePanelUI)이 생성되지 않음");
                    Require(Object.FindFirstObjectByType<TaskForceWorldFeedback>() != null, "TaskForceWorldFeedback이 생성되지 않음");
                    var formation = Object.FindFirstObjectByType<TaskForceEscortFormation>();
                    Require(formation != null, "TaskForceEscortFormation이 생성되지 않음");
                    Require(formation.EscortCount == 0, $"시작 편대 {formation.EscortCount}척, 기대 0척(카드로 합류)");
                    Require(TaskForcePanelUI.VisiblePanel == null, "편대가 없는데 편대 패널이 보임");

                    var refits = Object.FindObjectsByType<RefitUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Require(refits.Length > 0, "RefitUI를 찾지 못함");
                    var refit = refits[0];
                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    void Call(string method, params object[] args) => typeof(RefitUI).GetMethod(method, flags).Invoke(refit, args);
                    bool Shows(string text)
                    {
                        foreach (var t in refit.GetComponentsInChildren<TMPro.TMP_Text>(true))
                            if (t.text != null && t.text.Contains(text)) return true;
                        return false;
                    }
                    Game.Modules.ModuleDefinition filler = null;
                    foreach (string guid in AssetDatabase.FindAssets("t:ModuleDefinition", new[] { "Assets/_Game/Data" }))
                    {
                        filler = AssetDatabase.LoadAssetAtPath<Game.Modules.ModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                        if (filler != null) break;
                    }
                    Require(filler != null, "정비 카드 검증용 ModuleDefinition이 없음");
                    System.Collections.Generic.List<Game.Refit.RefitCard> Cards(Game.Refit.RefitCard fleetCard)
                        => new() { Game.Refit.RefitCard.Install(filler), Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Common), fleetCard };

                    // 편대 배치 카드 → 역할 없는 고속정
                    refit.Open(3, Cards(Game.Refit.RefitCard.FleetDeploy()), null);
                    Require(Shows("고속정 합류"), "정비 화면에 편대 배치 카드가 표시되지 않음");
                    Call("ChooseCard", 2);
                    Call("ChooseFleetSlot", 0);   // 편대 슬롯 화면(2026-10-03): 1번 슬롯
                    Require(formation.EscortCount == 1, $"편대 배치 후 {formation.EscortCount}척, 기대 1척");
                    Require(formation.GetInfo(0).Role == EscortRole.None, "합류한 고속정에 이미 역할이 있음");
                    var boat = formation.GetEscort(0);
                    var visual = boat.transform.Find("Escort visual");
                    Require(visual != null && Mathf.Approximately(visual.localScale.x, 0.60f), $"고속정 축척 {(visual != null ? visual.localScale.x : -1f)}, 기대 0.60");
                    Require(TaskForcePanelUI.VisiblePanel != null, "편대가 생겼는데 편대 패널이 보이지 않음");

                    // 편대 강화(역할 없음) → 역할 선택 → 방공
                    refit.Open(4, Cards(Game.Refit.RefitCard.FleetUpgrade(0)), null);
                    Require(Shows("역할 지정"), "정비 화면에 역할 지정 카드가 표시되지 않음");
                    Call("ChooseCard", 2);
                    Call("ChooseFleetSlot", 0);
                    Call("ChooseRole", 0);
                    Require(formation.GetInfo(0).Role == EscortRole.AirDefense, "역할 지정 카드로 방공 역할이 정해지지 않음");
                    Require(GameObject.Find("Escort CAP - 방공 호위함 1") != null, "방공 호위함 1이 보이지 않음");

                    // 편대 강화(개량) → 축척 0.76
                    refit.Open(5, Cards(Game.Refit.RefitCard.FleetUpgrade(0)), null);
                    Require(Shows("개량 0 → "), "정비 화면에 개량 카드가 표시되지 않음");
                    Call("ChooseCard", 2);
                    Call("ChooseFleetSlot", 0);
                    Require(formation.GetInfo(0).Tier == 1, "개량 카드가 단계를 올리지 않음");
                    Require(Mathf.Approximately(visual.localScale.x, 0.76f), $"개량 1단계 축척 {visual.localScale.x}, 기대 0.76");
                    Require(boat.GetComponent<EscortDefense>() != null, "호위함 자율 능력(EscortDefense)이 없음");
                    refit.Close();
                    GameManager.Instance.SetState(GameState.Playing);
                }
                catch (System.Exception ex)
                {
                    s_failure = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                }
            }

            if (!s_activated || elapsed < 1.0) return;

            bool success = string.IsNullOrEmpty(s_failure);
            Debug.Log(success ? "[TaskForceVerification] PASS — 편대 카드(배치·역할 지정·개량)·편대 패널·출항 편성 없음" : $"[TaskForceVerification] FAIL — {s_failure}");
            Finish(success ? 0 : 2);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(KeyRunning, false);
            EditorApplication.update -= Poll;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
