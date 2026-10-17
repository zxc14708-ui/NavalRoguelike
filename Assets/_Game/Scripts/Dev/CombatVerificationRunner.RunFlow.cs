#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Modules;
using Game.Ship;
using Game.UI;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 일시정지·결과 화면·재출격 흐름(2026-10-11 점검 A 묶음: U01·U02·B03).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 실제 게임 흐름 그대로:
        ///   1) 출항 → ESC 일시정지(시간·소리 정지, 설정 겹침, 판 버리기 버튼은 한 번 눌러선 실행 안 됨) → 계속. 정비 중엔 안 열림
        ///   2) 교리·성장 카드를 쌓고 미사일로 격침 → 결과 화면(격침 원인·요약·버튼 3개). 이어서 보스 격침·승리·정비 요청이 와도 패배 유지(B03)
        ///   3) 결과 화면 재출격 → 일시정지 재출격 → 일시정지 항구로 → 다시 출항: 매번 씬이 새로 열리고 이전 판 상태가 남지 않는지
        /// 검증기는 씬 전환을 넘어 살아남아야 하므로 DontDestroyOnLoad로 둔다.
        /// </summary>
        private IEnumerator RunFlowCheck()
        {
            _report.AppendLine("\n## 일시정지 · 결과 화면 · 재출격");
            DontDestroyOnLoad(gameObject);
            int exceptions = 0;
            void OnLog(string m, string st, LogType t) { if (t == LogType.Exception) { exceptions++; _report.AppendLine($"  - 예외: {m}"); } }
            Application.logMessageReceived += OnLog;

            LaunchFromStartScreen();
            yield return new WaitForSecondsRealtime(1f);
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) { Fail($"출항 뒤 전투 상태가 아님: {gm?.State}"); yield break; }
            var concept = Object.FindFirstObjectByType<ShipInitializer>()?.SelectedConcept;
            _report.AppendLine($"- 출항: {concept?.Title ?? "(기본 배치)"} · 전투 카메라 그림 층 {(Camera.main != null ? Camera.main.cullingMask : 0) != 0}");
            if (Camera.main == null || Camera.main.cullingMask == 0) Fail("출항 뒤에도 전투 카메라가 아무것도 그리지 않음(시작 화면이 가린 채)");
            var env0 = EnvironmentSignature();
            _report.AppendLine($"- 씬 부트스트랩(처음): {env0}");

            // ---- 1) 일시정지
            var hud = Object.FindFirstObjectByType<HUDView>();
            bool opened = hud != null && hud.OpenPauseMenu();
            yield return null;
            _report.AppendLine($"- 일시정지 열기: {Yes(opened)} · 상태 {gm.State} · 배속 {Time.timeScale} · 소리 정지 {Yes(AudioListener.pause)}");
            if (!opened || gm.State != GameState.Paused || Time.timeScale != 0f || !AudioListener.pause) Fail("일시정지가 시간·소리를 멈추지 않음");
            yield return ScreenShot("runflow_pause");

            Call(hud, "ShowSettings");
            yield return null;
            var settings = Get<GameObject>(hud, "_settingsOverlay");
            bool settingsShown = settings != null && settings.activeInHierarchy;
            _report.AppendLine($"- 일시정지 → 설정 화면 보임: {Yes(settingsShown)}");
            if (!settingsShown) Fail("일시정지 중 설정 화면이 보이지 않음");
            yield return ScreenShot("runflow_pause_settings");
            Call(hud, "CloseSettings");

            var retryButton = FindButton("재출격  /");
            retryButton?.onClick.Invoke();
            yield return null;
            string confirmText = retryButton != null ? retryButton.GetComponentInChildren<TMPro.TMP_Text>().text : "(없음)";
            _report.AppendLine($"- 재출격 한 번 누름: 실행 {Yes(RunRestart.InProgress)} · 문구 \"{confirmText}\"");
            if (retryButton == null || RunRestart.InProgress || !confirmText.Contains("한 번 더")) Fail("판을 버리는 버튼이 확인 없이 실행되거나 확인 문구가 없음");

            hud.ResumeFromPause();
            yield return null;
            _report.AppendLine($"- 계속: 상태 {gm.State} · 배속 {Time.timeScale} · 소리 {Yes(!AudioListener.pause)}");
            if (gm.State != GameState.Playing || Time.timeScale != 1f || AudioListener.pause) Fail("계속 후 전투로 돌아가지 않음");

            gm.SetState(GameState.Refit);
            bool openedInRefit = hud.OpenPauseMenu();
            gm.SetState(GameState.Playing);
            _report.AppendLine($"- 정비 중 일시정지 열림: {Yes(openedInRefit)}(아니오여야 함)");
            if (openedInRefit) Fail("정비 화면 중에 일시정지가 열림");

            // ---- 2) 격침 → 결과 화면, 종료 상태 고정(B03)
            CombatPolicies.ChooseDoctrine(NavalDoctrine.AirDefense);
            RunUpgrades.Add(RunStat.Damage, CardTier.Epic);
            var ship = gm.Player;
            for (int i = 0; i < 400 && ship.IsAlive; i++)
                ship.TakeDamage(new DamageInfo(400f, ship.transform.position + Vector3.up, Vector3.forward, DamageSource.Missile));
            yield return null;
            _report.AppendLine($"- 격침: 상태 {gm.State} · 원인 \"{ship.LastHullDamageCause}\"");
            if (gm.State != GameState.GameOver) Fail("선체 0인데 패배 상태가 아님");
            if (ship.LastHullDamageCause != "미사일·자폭 공격") Fail("격침 원인이 미사일로 기록되지 않음");

            var director = Object.FindFirstObjectByType<StageDirector>();
            GameEvents.RaiseBossDefeated();
            gm.TriggerVictory();
            gm.SetState(GameState.Refit);
            gm.SetState(GameState.Playing);
            yield return null;
            bool transitioning = director != null && Get<bool>(director, "_stageTransitioning");
            _report.AppendLine($"- 패배 뒤 보스 격침·승리·정비·전투 요청: 상태 {gm.State} · 보스 보상 시작 {Yes(transitioning)}");
            if (gm.State != GameState.GameOver || transitioning) Fail("패배 뒤에 다른 결과·보상으로 바뀜(B03)");

            var result = Object.FindFirstObjectByType<ResultScreenUI>();
            var detail = result != null ? Get<TMPro.TMP_Text>(result, "detailText") : null;
            int buttons = result != null ? result.GetComponentsInChildren<Button>(true).Length : 0;
            _report.AppendLine($"- 결과 화면: 보임 {Yes(result != null && result.IsShown)} · 버튼 {buttons} · 내용 \"{detail?.text.Replace("\n", " / ")}\"");
            if (result == null || !result.IsShown || buttons < 3 || detail == null || !detail.text.Contains("격침 원인")) Fail("결과 화면에 원인·버튼이 없음");
            yield return ScreenShot("runflow_result");

            // ---- 3) 재출격 3회(결과 화면 → 일시정지 → 항구)
            for (int round = 1; round <= 3; round++)
            {
                var oldGm = GameManager.Instance;
                var oldShip = oldGm.Player;
                string how;
                if (round == 1)
                {
                    how = "결과 화면 재출격";
                    FindButton("재출격  [")?.onClick.Invoke();
                }
                else
                {
                    hud = Object.FindFirstObjectByType<HUDView>();
                    hud.OpenPauseMenu();
                    yield return null;
                    var b = FindButton(round == 2 ? "재출격  /" : "항구로  /");
                    how = round == 2 ? "일시정지 재출격" : "일시정지 항구로";
                    b?.onClick.Invoke();
                    b?.onClick.Invoke();
                }
                if (!RunRestart.InProgress) { Fail($"{how}: 씬 다시 열기가 시작되지 않음"); break; }

                float t0 = Time.realtimeSinceStartup;
                while ((RunRestart.InProgress || GameManager.Instance == null || GameManager.Instance == oldGm) && Time.realtimeSinceStartup - t0 < 30f)
                    yield return null;
                yield return new WaitForSecondsRealtime(1.5f);   // StageDirector.Start · 바로 출항
                float loadSeconds = Time.realtimeSinceStartup - t0;
                gm = GameManager.Instance;
                if (gm == null || gm == oldGm) { Fail($"{how}: 새 판이 열리지 않음"); break; }

                bool harbor = round == 3;
                if (harbor)
                {
                    bool menuShown = GameObject.Find("Fleet command / main menu") != null;
                    _report.AppendLine($"- {how}: 시작 화면 보임 {Yes(menuShown)} · 상태 {gm.State}");
                    if (!menuShown || gm.State == GameState.Playing) Fail("항구로 돌아가지 않음(시작 화면 없음)");
                    yield return ScreenShot("runflow_harbor");
                    LaunchFromStartScreen();
                    yield return new WaitForSecondsRealtime(1f);
                }

                var init = Object.FindFirstObjectByType<ShipInitializer>();
                string shipName = init?.SelectedConcept?.Title ?? "(기본 배치)";
                int stale = StaleTargets();
                int managers = Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length;
                int ships = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None).Length;
                int huds = Object.FindObjectsByType<HUDView>(FindObjectsSortMode.None).Length;
                var env = EnvironmentSignature();
                _report.AppendLine($"- {round}회 {how}: {loadSeconds:0.0}초 · 상태 {gm.State} · 배속 {Time.timeScale} · 함정 {shipName} · " +
                                   $"교리 {CombatPolicies.Doctrine} · 성장 카드 {RunUpgrades.Cards.Count} · 격침 {gm.TotalKills} · 작전 시간 {gm.ElapsedTime:0.0}초 · " +
                                   $"GameManager {managers} · 기함 {ships} · HUD {huds} · 파괴된 표적 참조 {stale} · 부트스트랩 {env}");
                if (gm.State != GameState.Playing || Time.timeScale != 1f) Fail($"{how}: 새 판이 전투 상태로 시작되지 않음");
                if (!harbor && concept != null && init?.SelectedConcept != concept) Fail($"{how}: 같은 함정으로 출항하지 않음");
                if (CombatPolicies.Doctrine != NavalDoctrine.None || RunUpgrades.Cards.Count != 0 || gm.ElapsedTime > 10f) Fail($"{how}: 이전 판 교리·성장·작전 시간이 남음");
                if (managers != 1 || ships != 1 || huds != 1) Fail($"{how}: 이전 씬 객체가 남음");
                if (stale > 0) Fail($"{how}: 파괴된 표적이 목록에 남음");
                if (env != env0) Fail($"{how}: 바다·섬 등 씬 부트스트랩이 처음과 다름");
                if (oldShip != null) Fail($"{how}: 이전 기함이 파괴되지 않음");
                // 시작 화면이 가렸던 전투 카메라가 출항 뒤 다시 그리는가(항구 그리기 줄이기, 2026-10-13)
                if (Camera.main == null || Camera.main.cullingMask == 0) Fail($"{how}: 출항 뒤에도 전투 카메라가 아무것도 그리지 않음");
                if (round == 1) yield return ScreenShot("runflow_retry");

                // 다음 회차를 위해 이번 판에도 교리·성장을 쌓는다(초기화 확인)
                CombatPolicies.ChooseDoctrine(NavalDoctrine.Gunnery);
                RunUpgrades.Add(RunStat.FireRate, CardTier.Common);
                yield return new WaitForSecondsRealtime(1f);
            }

            Application.logMessageReceived -= OnLog;
            _report.AppendLine($"- 흐름 중 예외: {exceptions}");
            if (exceptions > 0) Fail("재출격 흐름 중 예외 발생");
        }

        private static Button FindButton(string prefix)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name.StartsWith(prefix) && b.isActiveAndEnabled) return b;
            return null;
        }

        /// <summary>목록에 남은 파괴된 표적 수.</summary>
        private static int StaleTargets()
        {
            int n = 0;
            foreach (TargetKind kind in System.Enum.GetValues(typeof(TargetKind)))
                foreach (var t in TargetRegistry.Get(kind))
                    if (t is Object o && o == null) n++;
            return n;
        }

        /// <summary>첫 씬에서만 불리던 부트스트랩이 만든 것: 바다 표면·구름 그림자·섬·바다 생물·선체 위험 표시·개발 도구.</summary>
        private static string EnvironmentSignature()
        {
            string Has<T>() where T : Object => Object.FindFirstObjectByType<T>() != null ? "O" : "x";
            return $"바다{Has<Game.View.OceanSurface>()} 구름{Has<Game.View.CloudShadows>()} 섬{Has<Game.World.IslandField>()} " +
                   $"생물{Has<Game.View.SeaLife>()} 위험{Has<HullDangerOverlay>()} 개발{Has<CombatDevTools>()} 편대{Has<Game.TaskForce.TaskForceEscortFormation>()}";
        }
    }
}
#endif
