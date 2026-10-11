#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;
using Game.Core;
using Game.UI;
using Game.Enemies;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 점검 D 묶음(2026-10-11): 배포 기본(O07)부터.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator BundleDCheck()
        {
            _report.AppendLine("\n## 점검 D 묶음");
            var hud = Object.FindFirstObjectByType<HUDView>();

            // ---- O07: 런 시드 · 버전 · 진단 내보내기 · 정보 화면
            {
                RunSeed.Begin(424242);
                float a1 = Random.value, a2 = Random.value;
                RunSeed.Begin(424242);
                float b1 = Random.value, b2 = Random.value;
                bool same = Mathf.Approximately(a1, b1) && Mathf.Approximately(a2, b2);
                _report.AppendLine($"- 런 시드: 같은 시드 두 번 → 같은 난수 {Yes(same)} · 지금 {RunSeed.Current} · 버전 \"{Diagnostics.Version}\"");
                if (!same || RunSeed.Current != 424242) Fail("런 시드가 난수를 맞추지 않음(O07)");

                string path = Diagnostics.Export(out string error);
                string text = path != null && File.Exists(path) ? File.ReadAllText(path) : "";
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                bool hasParts = text.Contains("런 시드 424242") && text.Contains("블록:") && text.Contains("설정:") && text.Contains("## 로그 끝부분");
                bool noHome = string.IsNullOrEmpty(home) || !text.Contains(home);
                _report.AppendLine($"- 진단 내보내기: {(path != null ? Path.GetFileName(path) : error)} · {text.Length / 1024}KB · 필수 항목 {Yes(hasParts)} · 계정 경로 지움 {Yes(noHome)}");
                if (path == null || !hasParts) Fail("진단 파일에 버전·설정·판 상태·로그가 없음(O07)");
                if (!noHome) Fail("진단 파일에 사용자 계정 경로가 남음(O07)");

                hud.OpenPauseMenu();
                yield return null;
                string pauseInfo = Get<TMPro.TMP_Text>(hud, "_pauseInfo")?.text ?? "";
                _report.AppendLine($"- 일시정지 정보 줄에 시드·버전: {Yes(pauseInfo.Contains("시드 424242") && pauseInfo.Contains("빌드"))}");
                if (!pauseInfo.Contains("시드 424242")) Fail("일시정지 화면에 런 시드가 없음(O07)");
                Call(hud, "ShowSettings");
                Call(hud, "ShowSettingsTab", 4);
                yield return null;
                bool credits = Resources.Load<TextAsset>("Credits") != null;
                _report.AppendLine($"- 설정 › 정보: 크레딧 파일 {Yes(credits)}");
                if (!credits) Fail("크레딧 파일이 없음(O07)");
                yield return ScreenShot("bundled_about");
                Call(hud, "CloseSettings");
                hud.ResumeFromPause();
                yield return null;
            }

            // ---- O06: 메뉴·알림음 — 정비(레벨업·다시 뽑기·확정)·교리·보스 등장/단계/공격 직전·메뉴 선택
            {
                int Count(UiSound.Cue c) => UiSound.Played.TryGetValue(c, out int n) ? n : 0;
                var before = new System.Collections.Generic.Dictionary<UiSound.Cue, int>();
                foreach (UiSound.Cue c in System.Enum.GetValues(typeof(UiSound.Cue))) before[c] = Count(c);
                int Delta(UiSound.Cue c) => Count(c) - before[c];

                var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
                var ui = Object.FindFirstObjectByType<RefitUI>(FindObjectsInactive.Include);
                refit.BeginLevelUp(3, () => GameManager.Instance.SetState(GameState.Playing));
                yield return null;
                ui.TryReroll();
                yield return new WaitForSecondsRealtime(0.1f);
                Call(ui, "ChooseCard", 1);
                yield return null;
                Call(refit, "Resume");
                yield return null;
                Call(hud, "ShowDoctrineChoice", (System.Action<NavalDoctrine>)(_ => { }));
                Call(hud, "ChooseDoctrine", NavalDoctrine.Gunnery);
                hud.OpenPauseMenu();
                yield return null;
                FindButton("설정  /")?.onClick.Invoke();
                yield return null;
                Call(hud, "CloseSettings");
                hud.ResumeFromPause();

                var ship = GameManager.Instance.Player;
                var def = CombatDevTools.FindEnemy("ene_boss_corvette");
                var boss = EnemySpawner.Instance.SpawnAt(def, ship.transform.position + ship.transform.forward * 30f, Quaternion.identity);
                if (boss != null) boss.DevFrozen = false;
                float t0 = Time.realtimeSinceStartup;
                while (Delta(UiSound.Cue.AttackSoon) == 0 && Time.realtimeSinceStartup - t0 < 20f) { ship.RepairHull(ship.HullMaxHp); yield return null; }
                // 단계 전환: 체력을 절반 아래로
                if (boss != null) boss.TakeDamage(new Game.Combat.DamageInfo(def.MaxHp * 0.55f, boss.transform.position, Vector3.forward, Game.Combat.DamageSource.Missile));
                yield return new WaitForSecondsRealtime(0.6f);
                CombatDevTools.ClearBattlefield();

                var sb = new System.Text.StringBuilder();
                foreach (UiSound.Cue c in System.Enum.GetValues(typeof(UiSound.Cue))) if (Delta(c) > 0) sb.Append($"{c} {Delta(c)} ");
                _report.AppendLine($"- 메뉴·알림음: {sb}");
                foreach (var c in new[] { UiSound.Cue.LevelUp, UiSound.Cue.Reroll, UiSound.Cue.Confirm, UiSound.Cue.Doctrine, UiSound.Cue.Select, UiSound.Cue.BossAlert, UiSound.Cue.AttackSoon, UiSound.Cue.BossPhase })
                    if (Delta(c) == 0) Fail($"알림음 {c}이 울리지 않음(O06)");
                _report.AppendLine($"- 음량 채널: 메뉴·알림음 {GameSettings.Volume(SoundChannel.Ui):0.##} (설정 › 음향)");
            }

            // ---- K06: 출격 기록 · 도전 과제 · 칭호 · 고난도 출항 해금
            {
                PlayerProfile.ResetForVerify();
                bool tracker = Object.FindFirstObjectByType<AchievementTracker>() != null;
                int bellBefore = UiSound.Played.TryGetValue(UiSound.Cue.Doctrine, out int nb) ? nb : 0;
                bool first = PlayerProfile.Unlock("stage2");
                bool again = PlayerProfile.Unlock("stage2");
                yield return new WaitForSecondsRealtime(0.1f);
                int bellAfter = UiSound.Played.TryGetValue(UiSound.Cue.Doctrine, out int na) ? na : 0;
                _report.AppendLine($"- 도전 과제: 추적기 {Yes(tracker)} · 첫 달성 {Yes(first)} · 두 번째는 무시 {Yes(!again)} · 종소리 {Yes(bellAfter > bellBefore)} · 첫 칭호 자동 장착 「{PlayerProfile.Title}」");
                if (!tracker) Fail("도전 과제 추적기가 없음(K06)");
                if (!first || again || !PlayerProfile.NewThisRun.Contains("stage2")) Fail("도전 과제 달성이 한 번만 기록되지 않음(K06)");
                if (PlayerProfile.Title != "해역 돌파자") Fail("첫 칭호가 장착되지 않음(K06)");

                bool lockedBefore = !PlayerProfile.DangerStartUnlocked;
                PlayerProfile.SetDangerStart(true);
                bool stillOff = !PlayerProfile.DangerStart;
                PlayerProfile.Unlock("stage3");
                PlayerProfile.SetDangerStart(true);
                _report.AppendLine($"- 고난도 출항: 해금 전 잠김 {Yes(lockedBefore && stillOff)} · 스테이지 3 도달 뒤 켜짐 {Yes(PlayerProfile.DangerStart)}");
                if (!lockedBefore || !stillOff || !PlayerProfile.DangerStart) Fail("고난도 출항 해금 조건이 맞지 않음(K06)");

                var concept = Game.Data.StartingShipCatalog.All.Count > 0 ? Game.Data.StartingShipCatalog.All[0] : null;
                string shipId = concept != null ? concept.Id : "default";
                PlayerProfile.RecordRun(shipId, false, 2, 57, 412f, "대함 미사일", false);
                PlayerProfile.RecordRun(shipId, true, 3, 133, 905f, null, true);
                var rec = PlayerProfile.RecordOf(shipId);
                bool recOk = rec.runs == 2 && rec.wins == 1 && rec.bestStage == 3 && rec.bestKills == 133 && Mathf.Approximately(rec.fastestWin, 905f) && rec.lastDeath == "대함 미사일";
                bool endAch = PlayerProfile.Has("first_run") && PlayerProfile.Has("victory") && PlayerProfile.Has("danger_victory");
                _report.AppendLine($"- 함정별 기록: 출격 {rec.runs} · 승리 {rec.wins} · 최고 S{rec.bestStage} · 최다 격침 {rec.bestKills} · 최단 승리 {rec.fastestWin:0}초 · 마지막 패배 \"{rec.lastDeath}\" {Yes(recOk)} · 판 끝 과제(첫 출항·작전 성공·위험한 항로) {Yes(endAch)}");
                if (!recOk) Fail("함정별 기록이 맞지 않음(K06)");
                if (!endAch) Fail("판 끝 도전 과제가 달성되지 않음(K06)");

                string json = File.Exists(PlayerProfile.FilePath) ? File.ReadAllText(PlayerProfile.FilePath) : "";
                bool saved = json.Contains("\"danger_victory\"") && json.Contains("\"dangerStart\": true") && json.Contains(shipId);
                _report.AppendLine($"- 기록 파일: {Path.GetFileName(PlayerProfile.FilePath)} {json.Length}B · 과제·고난도·함정 기록 저장 {Yes(saved)} · 백업 {Yes(File.Exists(PlayerProfile.FilePath + ".bak"))}");
                if (!saved) Fail("기록 파일에 도전 과제·고난도 출항·함정 기록이 없음(K06)");

                PlayerProfile.SetTitle("victory");
                PlayerProfile.SetTitle("ram5");   // 달성하지 않은 칭호는 무시
                bool titleOk = PlayerProfile.Title == "작전 성공 함장";
                _report.AppendLine($"- 칭호 선택: 「{PlayerProfile.Title}」 · 미달성 칭호 거부 {Yes(titleOk)}");
                if (!titleOk) Fail("칭호 선택이 맞지 않음(K06)");

                var result = Object.FindFirstObjectByType<ResultScreenUI>(FindObjectsInactive.Include);
                string overview = result != null ? Call(result, "Overview") as string ?? "" : "";
                bool listed = overview.Contains("도전 과제 달성") && overview.Contains("해역 돌파자");
                _report.AppendLine($"- 결과 화면에 이번 판 달성 과제 {Yes(listed)} ({PlayerProfile.NewThisRun.Count}개)");
                if (!listed) Fail("결과 화면에 이번 판 도전 과제가 없음(K06)");

                hud.OpenPauseMenu();
                yield return null;
                Call(hud, "ShowRecords");
                yield return null;
                var records = Object.FindFirstObjectByType<RecordsUI>();
                bool shown = records != null && records.gameObject.activeInHierarchy;
                int unlocked = PlayerProfile.Current.achievements.Count;
                bool rows = shown && records.RowCount == PlayerProfile.Catalog.Length && records.UnlockedRows == unlocked;
                bool table = shown && concept != null && records.ShipsText.Contains(concept.Title) && records.ShipsText.Contains("대함 미사일");
                _report.AppendLine($"- 기록 화면: 열림 {Yes(shown)} · 과제 줄 {(shown ? records.RowCount : 0)} (달성 {(shown ? records.UnlockedRows : 0)}/{unlocked}) {Yes(rows)} · 함정 표 {Yes(table)}");
                if (!rows) Fail("기록 화면 도전 과제 줄이 맞지 않음(K06)");
                if (!table) Fail("기록 화면 함정별 기록이 없음(K06)");
                if (shown)
                {
                    records.ChooseTitle("stage3");
                    _report.AppendLine($"- 기록 화면에서 칭호 바꾸기: 「{PlayerProfile.Title}」 {Yes(PlayerProfile.Title == "봉쇄 돌파자")}");
                    if (PlayerProfile.Title != "봉쇄 돌파자") Fail("기록 화면에서 칭호를 바꾸지 못함(K06)");
                }
                yield return ScreenShot("bundled_records");
                if (records != null) records.Close();
                hud.ResumeFromPause();
                yield return null;
                PlayerProfile.ResetForVerify();
            }

            // ---- K03: 선택 임무 — 보급선 호위
            {
                bool missionPicked = false, routePicked = false;
                GameManager.Instance.SetState(GameState.Paused);
                hud.ShowRouteChoice(2, 30f, _ => routePicked = true, () => missionPicked = true);
                yield return null;
                var missionButton = GameObject.Find("Mission route");
                bool three = missionButton != null && GameObject.Find("Supply route") != null && GameObject.Find("Danger route") != null;
                yield return ScreenShot("bundled_route_mission");
                missionButton?.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                _report.AppendLine($"- 항로 선택: 세 칸(보급·위험·임무) {Yes(three)} · [3] 임무 항로 선택 {Yes(missionPicked && !routePicked)}");
                if (!three || !missionPicked || routePicked) Fail("항로 선택에 임무 항로가 없거나 고르지 못함(K03)");
                GameManager.Instance.SetState(GameState.Playing);

                var ship = GameManager.Instance.Player;
                var xp = Object.FindFirstObjectByType<Game.Progression.ExperienceSystem>();
                var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
                CombatDevTools.ClearBattlefield();
                Game.Refit.RefitDraft.SetRerolls(1);
                ship.ApplyHullDamage(ship.HullMaxHp * 0.4f, "검증");
                int levelBefore = xp.Level;
                var mission = Game.TaskForce.SupplyEscortMission.Begin(ship, xp, 8f, 0.5f);
                float t0 = Time.realtimeSinceStartup;
                while (mission != null && mission.State != Game.TaskForce.SupplyEscortMission.Outcome.Running && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                var supply = mission != null ? mission.Ship : null;
                bool spawned = supply != null && supply.IsAlive;
                bool registered = spawned && System.Linq.Enumerable.Contains(Game.Combat.TargetRegistry.Of(Game.Combat.CombatFaction.Player, Game.Combat.TargetKind.Surface), supply);

                // 적이 보급선을 노리는지: 보급선 옆에 고속정 셋
                int aiming = 0;
                if (spawned)
                {
                    var def = CombatDevTools.FindEnemy("ene_fastboat");
                    for (int i = 0; i < 3; i++)
                    {
                        var e = EnemySpawner.Instance.SpawnAt(def, supply.transform.position + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 12f, Quaternion.identity);
                        if (e != null) e.DevFrozen = false;
                    }
                    // 적은 0.4~1.4초 뒤 처음 표적을 다시 고른다
                    float tAim = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - tAim < 2.5f)
                    {
                        aiming = 0;
                        foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                            if (e.IsAlive && e.EngagedTarget == supply.transform) aiming++;
                        if (aiming > 0) break;
                        yield return null;
                    }
                    yield return ScreenShot("bundled_supply");
                    CombatDevTools.ClearBattlefield();
                }
                _report.AppendLine($"- 보급선: 합류 {Yes(spawned)} · 아군 표적 등록 {Yes(registered)} · 옆의 고속정 3척 중 보급선을 노림 {aiming}");
                if (!spawned || !registered) Fail("보급선이 나오지 않거나 아군 표적이 아님(K03)");
                if (aiming == 0) Fail("적이 보급선을 노리지 않음(K03)");

                float followMax = 0f;
                t0 = Time.realtimeSinceStartup;
                while (mission != null && mission.State == Game.TaskForce.SupplyEscortMission.Outcome.Running && Time.realtimeSinceStartup - t0 < 15f)
                {
                    if (supply != null) followMax = Mathf.Max(followMax, Vector3.Distance(supply.transform.position, ship.transform.position));
                    yield return null;
                }
                float tRefit = Time.realtimeSinceStartup;
                while (GameManager.Instance.State != GameState.Refit && Time.realtimeSinceStartup - tRefit < 2f) yield return null;
                bool refitOpened = GameManager.Instance.State == GameState.Refit;
                if (refitOpened) { Call(refit, "Resume"); yield return null; }
                bool ok = Game.TaskForce.SupplyEscortMission.LastOutcome == Game.TaskForce.SupplyEscortMission.Outcome.Succeeded;
                bool repaired = Mathf.Approximately(ship.HullHp, ship.HullMaxHp);
                bool reroll = Game.Refit.RefitDraft.RerollsLeft == 2;
                bool leveled = xp.Level > levelBefore;
                _report.AppendLine($"- 임무 성공(8초 단축): {Yes(ok)} · 따라온 최대 거리 {followMax:0}m · 완전 수리 {Yes(repaired)} · 다시 뽑기 +1 {Yes(reroll)} · 정비 1회(레벨 {levelBefore}→{xp.Level}{(refitOpened ? ", 정비 열림" : " — 검사 환경은 StageDirector를 꺼 둬서 정비 화면은 실제 판에서 열림")}) · 도전 과제 {Yes(PlayerProfile.Has("supply"))}");
                if (!ok || !repaired || !reroll || !leveled) Fail("보급선 호위 성공 보상이 맞지 않음(K03)");
                if (followMax > 90f) Fail("보급선이 기함을 따라오지 않음(K03)");

                // 실패 경로: 보급선 격침 → 보상 없음
                ship.ApplyHullDamage(ship.HullMaxHp * 0.3f, "검증");
                float hullBefore = ship.HullHp;
                int rerollBefore = Game.Refit.RefitDraft.RerollsLeft;
                var failing = Game.TaskForce.SupplyEscortMission.Begin(ship, xp, 30f, 0.2f);
                t0 = Time.realtimeSinceStartup;
                while (failing != null && failing.State != Game.TaskForce.SupplyEscortMission.Outcome.Running && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                if (failing != null && failing.Ship != null)
                    failing.Ship.TakeDamage(new Game.Combat.DamageInfo(9999f, failing.Ship.transform.position, Vector3.forward, Game.Combat.DamageSource.Missile));
                yield return new WaitForSecondsRealtime(0.3f);
                bool failed = Game.TaskForce.SupplyEscortMission.LastOutcome == Game.TaskForce.SupplyEscortMission.Outcome.Failed;
                bool noReward = Mathf.Approximately(ship.HullHp, hullBefore) && Game.Refit.RefitDraft.RerollsLeft == rerollBefore;
                bool panelGone = GameObject.Find("Supply escort panel") == null;
                _report.AppendLine($"- 임무 실패(보급선 격침): {Yes(failed)} · 보상 없음 {Yes(noReward)} · 임무 막대 정리 {Yes(panelGone)}");
                if (!failed || !noReward) Fail("보급선 격침 때 실패 처리·보상이 맞지 않음(K03)");
                if (!panelGone) Fail("임무가 끝났는데 화면 막대가 남음(K03)");
                ship.RepairHull(ship.HullMaxHp);
                PlayerProfile.ResetForVerify();
            }
        }
    }
}
#endif
