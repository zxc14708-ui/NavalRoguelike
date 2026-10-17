using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Data;

namespace Game.Core
{
    /// <summary>
    /// 새 판 시작(2026-10-11 점검 U01·U02): 전투 씬을 처음부터 다시 연다. 앱을 다시 켜지 않고 재출격·항구 복귀를 한다.
    ///
    /// 순서: 빈 임시 씬으로 옮겨 지금 씬을 완전히 내림 → 정적 상태 초기화 → 전투 씬을 다시 연다.
    ///   - 정적 상태: 게임 코드의 [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] 초기화 함수를 모두 다시 부른다.
    ///     (도메인 리로드 없는 플레이 진입에 대비해 이미 각 클래스가 두고 있는 것 — 표적 목록·통계·교리·성장·전투 기록 등.
    ///     새 정적 상태를 만들면 같은 방식으로 초기화 함수를 두면 여기서도 비워진다.)
    ///   - 씬을 다시 연 뒤에는 [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 부트스트랩(바다 표면·구름 그림자·섬·바다 생물·
    ///     선체 위험 표시·개발 도구)을 다시 부른다. 유니티는 이것들을 첫 씬에서만 부르기 때문이다.
    ///   - 시간 배속은 1로 되돌리고, 새 GameManager가 상태를 처음(Boot)부터 다시 정한다.
    /// 재출격이면 고른 시작 함정을 기억해 두었다가 시작 화면(항구) 없이 바로 출항한다(<see cref="ConsumeQuickLaunch"/>).
    /// </summary>
    public static class RunRestart
    {
        private static bool s_quickLaunch;
        private static StartingShipConcept s_concept;

        /// <summary>씬을 다시 여는 중.</summary>
        public static bool InProgress { get; private set; }
        /// <summary>앱 실행 후 새로 연 판 수(검증용).</summary>
        public static int Restarts { get; private set; }

        /// <summary>같은 시작 함정으로 바로 재출격. concept가 없으면 기본 배치로 출항한다.</summary>
        public static void Retry(StartingShipConcept concept) => Begin(true, concept);

        /// <summary>항구(시작 화면)로 돌아가 함정을 다시 고른다.</summary>
        public static void ReturnToHarbor() => Begin(false, null);

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>시작 화면이 부른다: 재출격으로 열린 씬이면 true와 함께 고를 함정을 돌려준다(한 번만).</summary>
        public static bool ConsumeQuickLaunch(out StartingShipConcept concept)
        {
            concept = s_concept;
            bool quick = s_quickLaunch;
            s_quickLaunch = false;
            s_concept = null;
            return quick;
        }

        private static void Begin(bool quickLaunch, StartingShipConcept concept)
        {
            if (InProgress) return;
            InProgress = true;
            s_quickLaunch = quickLaunch;
            s_concept = concept;
            var runner = new GameObject("Run restart");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<Runner>().StartCoroutine(Reload(runner));
        }

        private static IEnumerator Reload(GameObject runner)
        {
            var old = SceneManager.GetActiveScene();
            string path = old.path;
            AudioListener.pause = false;
            Time.timeScale = 1f;

            // 1) 빈 씬으로 옮기고 지금 씬을 완전히 내린다 — 내리는 동안 객체들이 OnDisable로 목록에서 빠진다
            var temp = SceneManager.CreateScene("Run restart " + Restarts);
            SceneManager.SetActiveScene(temp);
            var unload = SceneManager.UnloadSceneAsync(old);
            while (unload != null && !unload.isDone) yield return null;

            // 2) 이전 판의 정적 상태를 비운다(파괴된 객체 참조·성장·교리·통계)
            Invoke(RuntimeInitializeLoadType.SubsystemRegistration);

            // 3) 전투 씬을 다시 연다. Awake·OnEnable 직후(Start 전) 첫 씬 부트스트랩을 다시 부른다
            SceneManager.sceneLoaded += AfterLoad;
            var load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            while (load != null && !load.isDone) yield return null;

            Restarts++;
            InProgress = false;
            UnityEngine.Object.Destroy(runner);
        }

        private static void AfterLoad(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= AfterLoad;
            Invoke(RuntimeInitializeLoadType.AfterSceneLoad);
        }

        /// <summary>게임 코드의 해당 시점 [RuntimeInitializeOnLoadMethod] 정적 함수를 부른다.</summary>
        private static void Invoke(RuntimeInitializeLoadType when)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in typeof(RunRestart).Assembly.GetTypes())
            {
                if (type.Namespace == null || !type.Namespace.StartsWith("Game")) continue;
                foreach (var method in type.GetMethods(flags))
                {
                    var attr = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    if (attr == null || attr.loadType != when || method.GetParameters().Length != 0) continue;
                    try { method.Invoke(null, null); }
                    catch (Exception e) { Debug.LogException(e.InnerException ?? e); }
                }
            }
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
