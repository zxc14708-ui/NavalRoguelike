using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Dev;

namespace Game.EditorTools
{
    /// <summary>
    /// 자동 전투 검증 실행기(개발용). 전투 씬을 열고 플레이 모드에서 CombatVerificationRunner를 돌린다.
    /// 씬·에셋은 저장하거나 바꾸지 않는다.
    ///
    /// 메뉴:   Naval/Dev/Run Combat Verification  → 프로젝트 폴더의 Logs/CombatVerification 에 보고서·스크린샷
    /// 배치:   Unity -batchmode -projectPath ... -executeMethod Game.EditorTools.CombatVerification.RunBatch -verifyOut &lt;폴더&gt;
    ///         끝나면 에디터를 스스로 종료한다(통과 0, 실패 2, 시간 초과 3).
    /// </summary>
    [InitializeOnLoad]
    public static class CombatVerification
    {
        private const string ScenePath = "Assets/_Game/Scenes/Prototype_Main.unity";
        private const string KeyRunning = "NavalRoguelike.Verify.Running";
        private const string KeyOutput = "NavalRoguelike.Verify.Output";
        private const string KeyBatch = "NavalRoguelike.Verify.Batch";
        private const string KeyStarted = "NavalRoguelike.Verify.StartedAt";
        private const double TimeoutSeconds = 45 * 60;   // 첫 실행은 검색 색인·임포트로 오래 걸린다

        // 플레이 모드 진입 시 도메인이 다시 로드되므로, 진행 상태는 SessionState에 두고 여기서 다시 붙는다.
        // SessionState는 이 에디터 프로세스에만 남는다(EditorPrefs는 모든 Unity 에디터가 공유해 다른 창에 영향을 줄 수 있다).
        static CombatVerification()
        {
            if (!SessionState.GetBool(KeyRunning, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Poll;

            // 요청 직후 스크립트 재컴파일로 도메인이 다시 로드되면 요청이 사라진다 — 아직 플레이 전이면 다시 요청
            if (!EditorApplication.isPlayingOrWillChangePlaymode) RequestPlayMode();
        }

        private static void RequestPlayMode()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode) RequestPlayMode();   // 컴파일·임포트가 끝나면 다시
                    return;
                }
                Debug.Log("[CombatVerification] 플레이 모드 진입");
                EditorApplication.isPlaying = true;
            };
        }

        [MenuItem("Naval/Dev/Run Combat Verification")]
        public static void RunFromMenu()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CombatVerification"));
            Begin(output, batch: false);
        }

        /// <summary>
        /// 검증용 개발 플레이어 빌드(배치 모드 진입점). 에디터 배치 모드는 플레이 루프를 안정적으로 돌리지 못해서,
        /// 개발 빌드를 만들어 "-batchmode -combatVerify &lt;폴더&gt;"로 실행한다. 빌드 경로: -buildOut &lt;exe 경로&gt;.
        /// </summary>
        public static void BuildVerificationPlayer()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verify", "NavalRoguelike.exe"));
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOut") output = args[i + 1];

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            Debug.Log($"[CombatVerification] 빌드 결과: {report.summary.result}, {report.summary.totalErrors} errors → {output}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>배치 모드 진입점.</summary>
        public static void RunBatch()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CombatVerification"));
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-verifyOut") output = args[i + 1];
            Begin(output, batch: true);
        }

        private static void Begin(string output, bool batch)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CombatVerification] 플레이 중에는 시작할 수 없습니다.");
                return;
            }
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(output);
            SessionState.SetBool(KeyRunning, true);
            SessionState.SetString(KeyOutput, output);
            SessionState.SetBool(KeyBatch, batch);
            SessionState.SetString(KeyStarted, DateTime.UtcNow.ToString("o"));

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;

            // 배치 모드에서는 에디터 초기화(검색 색인 등 지연 호출)가 끝난 뒤에 요청해야 플레이 모드로 들어간다
            Debug.Log("[CombatVerification] 플레이 모드 진입 요청");
            RequestPlayMode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(KeyRunning, false)) return;

            Debug.Log("[CombatVerification] 플레이 모드 진입 완료 — 시나리오 시작");
            var runner = new GameObject("CombatVerificationRunner").AddComponent<CombatVerificationRunner>();
            runner.OutputDirectory = SessionState.GetString(KeyOutput, "Logs/CombatVerification");
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(KeyRunning, false)) { EditorApplication.update -= Poll; return; }

            bool timedOut = DateTime.TryParse(SessionState.GetString(KeyStarted, ""), null,
                                              System.Globalization.DateTimeStyles.RoundtripKind, out var started)
                            && (DateTime.UtcNow - started).TotalSeconds > TimeoutSeconds;

            if (!CombatVerificationRunner.Finished && !timedOut) return;

            bool batch = SessionState.GetBool(KeyBatch, false) && Application.isBatchMode;
            int code = timedOut ? 3 : CombatVerificationRunner.Succeeded ? 0 : 2;
            SessionState.SetBool(KeyRunning, false);
            EditorApplication.update -= Poll;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            Debug.Log($"[CombatVerification] 종료 코드 {code} (0 통과, 2 규칙 실패, 3 시간 초과)");
            if (batch) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
