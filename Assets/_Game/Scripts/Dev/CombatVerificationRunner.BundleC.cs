#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;
using Game.UI;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 점검 C 묶음(2026-10-11): 다시 뽑기·빌드 안내·항구/효과 품질·보스 막대·결과 분석·중단/재개 저장.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator BundleCCheck()
        {
            _report.AppendLine("\n## 점검 C 묶음");
            DontDestroyOnLoad(gameObject);   // 이어하기 검사가 씬을 다시 연다
            RunSave.Delete();

            // ---- G05: 항구 화질·배경 정지(출항 전 시작 화면)
            {
                var harbor = NavalBaseMenu.Active;
                if (harbor == null) Fail("시작 화면 항구가 없음");
                else
                {
                    var high = harbor.TextureSize; int highMsaa = harbor.TextureMsaa;
                    DisplaySettings.SetHarborQuality(2);
                    yield return null; yield return null;
                    var low = harbor.TextureSize; int lowMsaa = harbor.TextureMsaa;
                    DisplaySettings.SetHarborStill(true);
                    yield return new WaitForSecondsRealtime(3f);
                    bool still = !harbor.CameraRendering;
                    var cam = Get<Camera>(harbor, "_camera");
                    _report.AppendLine($"  - 정지 진단: 카메라 켜짐 {cam?.enabled} · 목표까지 {(cam != null ? Vector3.Distance(cam.transform.position, Get<Vector3>(harbor, "_destination")) : -1f):0.###} · 화각 차 {(cam != null ? Mathf.Abs(cam.fieldOfView - Get<float>(harbor, "_fov")) : -1f):0.###} · dirty {Get<bool>(harbor, "_dirty")} · 화질 {Get<int>(harbor, "_appliedQuality")}");
                    DisplaySettings.SetHarborStill(false);
                    DisplaySettings.SetHarborQuality(0);
                    yield return null; yield return null;
                    _report.AppendLine($"- 항구 화질: 높음 {high.x}×{high.y} MSAA {highMsaa} → 낮음 {low.x}×{low.y} MSAA {lowMsaa} · 배경 정지 중 카메라 멈춤 {Yes(still)} · 되돌림 {harbor.TextureSize.y}");
                    if (low.y > 1080 || lowMsaa > 2 || low.y >= high.y) Fail("항구 화질 낮음이 렌더 크기·MSAA를 줄이지 않음(G05)");
                    if (!still) Fail("항구 배경 정지인데 카메라가 계속 그림(G05)");
                }
            }

            LaunchFromStartScreen();
            yield return new WaitForSecondsRealtime(1f);
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            director.enabled = false;   // 웨이브는 끄고(검사 중 적이 섞이지 않게), 저장·재개는 직접 부른다
            EnemySpawner.Instance.enabled = false;
            BridgeMachineGun.DevHoldFire = true;
            CombatDevTools.ClearBattlefield();
            if (Get<Transform>(EnemySpawner.Instance, "_player") == null) Put(EnemySpawner.Instance, "_player", ship.transform);

            // ---- K01 · K02: 정비 카드 — 함선 역할 구성·카드별 변화·다시 뽑기
            {
                var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
                var ui = Object.FindFirstObjectByType<RefitUI>(FindObjectsInactive.Include);
                refit.BeginLevelUp(2, () => GameManager.Instance.SetState(GameState.Playing));
                yield return null;
                int before = Game.Refit.RefitDraft.RerollsLeft;
                string summary = Strip(ui.CardSummary);
                string firstStats = Strip(ui.CardStatsText(0)).Split('\n')[0];
                yield return ScreenShot("bundlec_cards");
                bool rerolled = ui.TryReroll();
                yield return null;
                int after = Game.Refit.RefitDraft.RerollsLeft;
                string firstAfter = Strip(ui.CardStatsText(0)).Split('\n')[0];
                _report.AppendLine($"- 카드 화면: \"{summary}\" · 첫 카드 \"{firstStats}\"");
                _report.AppendLine($"- 다시 뽑기: {Yes(rerolled)} · 남은 {before} → {after} · 카드 {ui.CardCount}장 · 새 첫 카드 \"{firstAfter}\"");
                if (!summary.Contains("대공") || !summary.Contains("대잠")) Fail("정비 카드 화면에 함선 역할 구성이 없음(K01)");
                bool anyImpact = false;
                for (int i = 0; i < ui.CardCount; i++) if (ui.CardStatsText(i).Contains("함선 변화")) anyImpact = true;
                if (!anyImpact) Fail("카드에 함선 변화 줄이 없음(K01)");
                if (!rerolled || after != before - 1 || ui.CardCount < 3) Fail("다시 뽑기가 횟수를 쓰고 카드를 새로 뽑지 않음(K02)");
                Game.Refit.RefitDraft.SetRerolls(0);
                bool blocked = !ui.TryReroll();
                Game.Refit.RefitDraft.SetRerolls(Game.Refit.RefitDraft.RerollsPerRun);
                _report.AppendLine($"- 횟수 0이면 막힘: {Yes(blocked)}");
                if (!blocked) Fail("다시 뽑기 횟수가 0인데 뽑힘(K02)");
                Call(refit, "Resume");
                yield return null;
            }

            // ---- G04: 장식 효과 품질
            {
                var life = Object.FindFirstObjectByType<Game.View.SeaLife>();
                int Emit()
                {
                    var ps = Game.View.DecorFx.Spray;
                    ps.Clear();
                    for (int i = 0; i < 400; i++) Game.View.DecorFx.Emit(ps, ship.transform.position + Vector3.up * 3f, Vector3.up, 3f, 0.3f, Color.white);
                    return Game.View.DecorFx.Count(ps);
                }
                int high = Emit();
                DisplaySettings.SetEffectsQuality(2);
                int low = Emit();
                yield return null;
                bool lifeHidden = life == null || life.transform.childCount == 0 || !life.transform.GetChild(0).gameObject.activeSelf;
                DisplaySettings.SetEffectsQuality(0);
                yield return null;
                bool lifeBack = life == null || life.transform.childCount == 0 || life.transform.GetChild(0).gameObject.activeSelf;
                _report.AppendLine($"- 장식 효과: 높음 입자 {high}/400 → 낮음 {low}/400 · 낮음에서 바다 생물 숨김 {Yes(lifeHidden)} · 되돌리면 다시 {Yes(lifeBack)}");
                if (low > high * 0.6f) Fail("장식 효과 낮음이 입자를 줄이지 않음(G04)");
                if (!lifeHidden || !lifeBack) Fail("장식 효과 낮음에서 바다 생물이 숨지 않거나 돌아오지 않음(G04)");
            }

            // ---- K05: 보스 막대
            {
                var def = CombatDevTools.FindEnemy("ene_boss_corvette") ?? CombatDevTools.FindEnemy("ene_boss");
                var boss = EnemySpawner.Instance.SpawnAt(def, ship.transform.position + ship.transform.forward * 30f, Quaternion.identity);
                if (boss != null) boss.DevFrozen = true;
                yield return new WaitForSeconds(1f);
                var bar = Object.FindFirstObjectByType<BossBarUI>();
                _report.AppendLine($"- 보스 막대({def?.DisplayName}): 보임 {Yes(bar != null && bar.Shown)} · 단계 \"{bar?.PhaseText}\" · \"{Strip(bar?.NextText)}\"");
                if (bar == null || !bar.Shown || string.IsNullOrEmpty(bar.PhaseText) || !Strip(bar.NextText).Contains("다음") && !Strip(bar.NextText).Contains("없음"))
                    Fail("보스 막대에 단계·다음 공격이 없음(K05)");
                yield return ScreenShot("bundlec_boss_bar");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(0.5f);
            }

            // ---- O01: 스테이지 경계 저장 → 항구 → 이어하기
            {
                // 저장할 상태를 만든다: 블록 강화·추가, 성장 카드, 편대(방공 T1), 교리, 레벨, 선체 60%
                var extra = CombatDevTools.InstallModuleNear("mod_ciws", new GridCoord(0, 0), 8);
                ModuleInstance upgraded = null;
                foreach (var m in ship.Grid.Modules) if (m.CanUpgrade) { upgraded = m; break; }
                upgraded?.ApplyUpgrade();
                RunUpgrades.Add(RunStat.Damage, CardTier.Rare);
                RunUpgrades.Add(RunStat.Range, CardTier.Common);
                var fleet = ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>();
                fleet.ClearAll();
                int e0 = fleet.Deploy();
                fleet.AssignRole(e0, Game.TaskForce.EscortRole.AirDefense);
                fleet.Upgrade(e0);
                fleet.Deploy();
                CombatPolicies.ChooseDoctrine(NavalDoctrine.AntiSub);
                Object.FindFirstObjectByType<Game.Progression.ExperienceSystem>().Restore(4, 3);
                ship.RestoreHull(0.6f);
                Put(director, "_stageIndex", 1);
                Put(director, "_dangerRoute", true);
                yield return null;

                var want = Snapshot(ship);
                bool saved1 = RunSave.Save(director);
                bool saved2 = RunSave.Save(director);   // 두 번 — 앞 것이 백업으로 남는다
                bool backup = File.Exists(RunSave.FilePath + ".bak");
                _report.AppendLine($"- 저장: {Yes(saved1 && saved2)} · 백업 {Yes(backup)} · {want}");
                if (!saved1 || !saved2 || !backup) Fail("저장 파일·백업이 만들어지지 않음(O01)");

                // 손상 복구: 본 파일을 깨뜨리면 백업으로 읽는다
                string good = File.ReadAllText(RunSave.FilePath);
                File.WriteAllText(RunSave.FilePath, "{ 깨진 파일");
                bool fromBackup = RunSave.TryLoad(out var fallback) && fallback != null;
                string problem = RunSave.LastProblem;
                RunSave.ClearProblem();
                File.WriteAllText(RunSave.FilePath, good);
                _report.AppendLine($"- 본 파일 손상 → 백업으로 읽음 {Yes(fromBackup)} · 안내 \"{problem}\"");
                if (!fromBackup || string.IsNullOrEmpty(problem)) Fail("손상된 저장을 백업으로 복구하지 못함(O01)");

                // 항구로 돌아가 [이어하기]
                var oldGm = gm;
                RunRestart.ReturnToHarbor();
                float t0 = Time.realtimeSinceStartup;
                while ((RunRestart.InProgress || GameManager.Instance == null || GameManager.Instance == oldGm) && Time.realtimeSinceStartup - t0 < 30f) yield return null;
                yield return new WaitForSecondsRealtime(1.5f);
                var resume = FindButton("Resume run");
                _report.AppendLine($"- 항구 이어하기 버튼: {Yes(resume != null)}");
                yield return ScreenShot("bundlec_resume_harbor");
                if (resume == null) { Fail("시작 화면에 이어하기가 없음(O01)"); yield break; }
                resume.onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);
                gm = GameManager.Instance;
                ship = gm.Player;
                director = Object.FindFirstObjectByType<StageDirector>();
                var got = Snapshot(ship);
                _report.AppendLine($"- 이어하기 후: {got} · 상태 {gm.State} · 스테이지 {director.StageNumber} · 위험 항로 {Yes(director.DangerRoute)} · 건너뛴 블록 {RunSave.LastSkippedModules}");
                if (got != want) Fail("이어하기한 함선·성장·편대·교리·레벨이 저장과 다름(O01)");
                if (gm.State != GameState.Playing || director.StageNumber != 2 || !director.DangerRoute) Fail("이어하기가 저장된 스테이지·항로로 시작하지 않음(O01)");
                yield return ScreenShot("bundlec_resumed");

                // 둘 다 깨지면 지우고 알린다
                File.WriteAllText(RunSave.FilePath, "x");
                File.WriteAllText(RunSave.FilePath + ".bak", "y");
                bool none = !RunSave.TryLoad(out _);
                bool deleted = !File.Exists(RunSave.FilePath) && !File.Exists(RunSave.FilePath + ".bak");
                _report.AppendLine($"- 둘 다 손상: 불러오기 없음 {Yes(none)} · 지움 {Yes(deleted)} · 안내 \"{RunSave.LastProblem}\"");
                if (!none || !deleted || string.IsNullOrEmpty(RunSave.LastProblem)) Fail("둘 다 손상된 저장을 지우고 알리지 않음(O01)");
                RunSave.ClearProblem();
            }

            // ---- O02: 결과 분석 — 실제 피해·격침을 무장별로
            {
                director.enabled = false;
                EnemySpawner.Instance.enabled = false;
                CombatDevTools.ClearBattlefield();
                ship.DevRudderOverride = 0f;
                ship.SetEngineOrder(0f);
                var boats = CombatDevTools.SpawnRing("ene_fastboat", 4, 18f, 0f);
                foreach (var b in boats) if (b != null) b.DevFrozen = true;
                float t0 = Time.time;
                while (Time.time - t0 < 15f)
                {
                    bool alive = false;
                    foreach (var b in boats) if (b != null && b.IsAlive && b.isActiveAndEnabled) alive = true;
                    if (!alive) break;
                    ship.RepairHull(ship.HullMaxHp);
                    yield return null;
                }
                var ranked = RunReport.Ranked();
                string top = ranked.Count > 0 ? $"{ranked[0].Key} {ranked[0].Damage:0}·격침 {ranked[0].Kills}" : "(없음)";
                _report.AppendLine($"- 결과 분석 기록: 무장 {ranked.Count}종 · 총 피해 {RunReport.TotalDamage:0} · 격침 {RunReport.TotalKills} · 1위 {top}");
                if (ranked.Count == 0 || RunReport.TotalDamage <= 0f || RunReport.TotalKills < 1) Fail("무장별 실제 피해·격침이 기록되지 않음(O02)");
                RunSave.Save(director);   // 패배하면 지워지는지 보려고 다시 저장
                bool savedBeforeLoss = File.Exists(RunSave.FilePath);
                for (int i = 0; i < 400 && ship.IsAlive; i++)
                    ship.TakeDamage(new DamageInfo(400f, ship.transform.position + Vector3.up, Vector3.forward, DamageSource.Gun));
                yield return null;
                var result = Object.FindFirstObjectByType<ResultScreenUI>();
                string contribution = Strip(result.ContributionText).Replace("\n", " / ");
                string overview = Strip(result.OverviewText).Replace("\n", " / ");
                _report.AppendLine($"- 결과 화면 무장 기여: \"{contribution}\"");
                _report.AppendLine($"- 결과 화면 미사일·빌드: \"{overview}\"");
                if (!contribution.Contains(ranked.Count > 0 ? ranked[0].Key : "?") || !overview.Contains("최종 빌드")) Fail("결과 화면에 무장 기여·최종 빌드가 없음(O02)");
                bool saveGone = !File.Exists(RunSave.FilePath);
                _report.AppendLine($"- 패배 뒤 이어하기 기록 지움: 패배 전 있음 {Yes(savedBeforeLoss)} → 지움 {Yes(saveGone)}");
                if (!savedBeforeLoss || !saveGone) Fail("패배 뒤 이어하기 기록이 지워지지 않음(O01)");
                yield return ScreenShot("bundlec_result");
            }
            BridgeMachineGun.DevHoldFire = false;
        }

        /// <summary>저장·이어하기 비교용 요약: 블록(종류·자리·단계)·성장·편대·교리·레벨·선체.</summary>
        private static string Snapshot(ShipController ship)
        {
            var blocks = new List<string>();
            foreach (var m in ship.Grid.Modules) blocks.Add($"{m.Definition.Id}@{m.Origin.X},{m.Origin.Z}r{m.RotationSteps}u{m.UpgradeLevel}");
            blocks.Sort(string.CompareOrdinal);
            var fleet = ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>();
            var escorts = new List<string>();
            for (int i = 0; fleet != null && i < fleet.EscortCount; i++) { var info = fleet.GetInfo(i); escorts.Add($"{info.RosterSlot}:{info.Role}T{info.Tier}"); }
            escorts.Sort(string.CompareOrdinal);
            var xp = Object.FindFirstObjectByType<Game.Progression.ExperienceSystem>();
            return $"블록 {blocks.Count}({string.Join(" ", blocks).GetHashCode():X8}) · 성장 {RunUpgrades.Cards.Count} · 편대 [{string.Join(",", escorts)}] · 교리 {CombatPolicies.Doctrine} · Lv {xp?.Level}/{xp?.Xp} · 선체 {ship.HullHp / ship.HullMaxHp * 100f:0}%";
        }
    }
}
#endif
