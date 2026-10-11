#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>자동 전투 검증 — 점검 B 묶음(2026-10-11): 사전·무장 패널·대잠 카드·화면 설정·적아 표식·위협 묶음·첫 출격 훈련.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator BundleBCheck()
        {
            _report.AppendLine("\n## 점검 B 묶음");
            var ship = GameManager.Instance.Player;
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = 1f;
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            BridgeMachineGun.DevHoldFire = true;
            var hud = Object.FindFirstObjectByType<HUDView>();

            // ---- B07 · G03: 무장 패널 — 일부 파괴 표기, 안 쏘는 이유, 줄 눌러 펼치기
            {
                var a = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
                var b = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
                yield return new WaitForSeconds(0.5f);
                var panel = Object.FindFirstObjectByType<WeaponStatusPanelUI>();
                string Row(string prefix)
                {
                    foreach (var t in panel.GetComponentsInChildren<TMPro.TMP_Text>())
                        if (t.name == "Name" && t.text.StartsWith(prefix)) return t.text + " | " + t.transform.parent.Find("Status")?.GetComponent<TMPro.TMP_Text>()?.text;
                    return "(없음)";
                }
                string idle = Row("기관포");
                var diag = WeaponDiagnosis.Explain(a, ship.GetComponentInChildren<TargetingSystem>());
                CombatDevTools.SpawnRing("ene_fastboat", 1, 30f, 0f);
                foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.DevFrozen = true;
                yield return new WaitForSeconds(0.4f);
                var far = WeaponDiagnosis.Explain(a, ship.GetComponentInChildren<TargetingSystem>());
                string farRow = Row("기관포");
                int total = 0;
                foreach (var m in ship.Grid.Modules) if (m?.Definition != null && m.Definition.Type == ModuleType.Autocannon) total++;
                if (b != null) b.TakeDamage(b.MaxHp * 10f);
                yield return new WaitForSeconds(0.4f);
                string broken = Row("기관포");
                panel.ToggleDetail(ModuleType.Autocannon);
                yield return new WaitForSeconds(0.3f);
                string detail = panel.DetailText.Replace("\n", " / ");
                _report.AppendLine($"- 무장 패널: 적 없음 \"{idle}\"({diag.Short}) · 30 m 고속정 \"{farRow}\"({far.Detail}) · 기관포 {total}문 중 1문 파괴 \"{broken}\"");
                _report.AppendLine($"- 줄 눌러 펼침: {Yes(panel.DetailOpen)} · \"{detail}\"");
                if (far.Short != "범위 밖") Fail("사거리 밖 적에 '범위 밖' 사유가 나오지 않음(G03)");
                if (!broken.Contains($"{total - 1}/{total}") || !broken.Contains("파괴")) Fail("일부 파괴된 무장의 가동/전체 표기가 없음(B07)");
                if (!panel.DetailOpen || !detail.Contains("#1") || !detail.Contains("파괴")) Fail("무장 줄을 눌러도 문마다 이유가 펼쳐지지 않음(G03)");
                yield return ScreenShot("bundleb_weapon_panel");

                // U08: 펼친 줄에서 [사전에서 보기] → 일시정지 + 사전이 그 무장 항목으로
                panel.OpenCodexForDetail();
                yield return null;
                var codexNow = Object.FindFirstObjectByType<CodexUI>();
                _report.AppendLine($"- 무장 줄 → 사전 바로 이동: 일시정지 {Yes(hud.IsPauseMenuOpen)} · 사전 {codexNow?.Section}/{codexNow?.Selected?.Id}");
                if (!hud.IsPauseMenuOpen || codexNow == null || codexNow.Selected?.Id != "mod_autocannon") Fail("무장 줄에서 사전 그 항목으로 바로 가지 않음(U08)");
                codexNow?.Close();
                hud.ResumeFromPause();
                yield return null;

                // U04: 오른쪽 콘솔 접기
                bool before = hud.RightColumnCollapsed;
                hud.RightColumnCollapsed = true;
                yield return null;
                var vls = GameObject.Find("VLS fire-control console") ?? GameObject.Find("Helicopter fire-control console");
                int visibleConsoles = 0;
                foreach (var name in new[] { "Helicopter fire-control console", "Doctrine console" })
                    if (GameObject.Find(name) != null) visibleConsoles++;
                _report.AppendLine($"- 콘솔 접기: 접은 뒤 보이는 오른쪽 콘솔 {visibleConsoles}");
                if (visibleConsoles != 0) Fail("콘솔을 접어도 오른쪽 콘솔이 보임(U04)");
                hud.RightColumnCollapsed = before;
                CombatDevTools.ClearBattlefield();
            }

            // ---- B04: 대잠 보장 카드 — 놓을 자리가 있는 대잠 블록
            {
                var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
                var draft = refit != null ? Get<Game.Refit.RefitDraft>(refit, "draft") : null;
                if (draft == null) Fail("RefitDraft를 찾지 못함");
                else
                {
                    Game.Refit.RefitDraft.ForceAntiSubOnce = true;
                    var cards = draft.DrawCards(3, 3);
                    string first = cards.Count > 0 && cards[0].Module != null ? cards[0].Module.DisplayName : cards.Count > 0 ? cards[0].Kind.ToString() : "(없음)";
                    bool asw = false, placeable = false;
                    foreach (var c in cards)
                        if (c.Kind == Game.Refit.RefitCardKind.Install && c.Module != null &&
                            c.Module.Type is ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.HelicopterDeck)
                        { asw = true; placeable = ship.Grid.HasAnyValidPlacement(c.Module); }
                    _report.AppendLine($"- 대잠 카드 강제: 첫 카드 {first} · 대잠 카드 {Yes(asw)} · 놓을 자리 {Yes(placeable)}");
                    if (!asw || !placeable) Fail("대잠 보장 카드가 놓을 수 있는 대잠 블록이 아님(B04)");
                }
            }

            // ---- G01: 적아 표식
            {
                var formation = Object.FindFirstObjectByType<Game.TaskForce.TaskForceEscortFormation>();
                formation?.ClearAll();
                if (formation != null) { formation.Deploy(); formation.Deploy(); formation.SetFormation(Game.TaskForce.FleetFormation.Circular); }
                yield return new WaitForSeconds(2.5f);   // 호위함이 화면 안 자리로
                var silenced = DisableWeapons(null);
                foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) { d.enabled = false; silenced.Add(d); }
                CombatDevTools.SpawnRing("ene_fastboat", 3, 22f, 30f);
                CombatDevTools.SpawnRing("ene_drone", 2, 26f, 200f);
                foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.DevFrozen = true;
                yield return new WaitForSeconds(0.3f);
                var markers = Object.FindFirstObjectByType<IdentityMarkersUI>();
                yield return null;
                _report.AppendLine($"- 적아 표식: 적 {markers?.HostileShown} · 아군 {markers?.FriendlyShown}");
                if (markers == null || markers.HostileShown < 3 || markers.FriendlyShown < 1) Fail("화면 안 적·아군 호위함에 식별 표식이 없음(G01)");
                yield return ScreenShot("bundleb_identity");
                DisplaySettings.SetIdentityMarkers(false);
                yield return null;
                yield return null;
                _report.AppendLine($"- 표식 끔: 적 {markers?.HostileShown}");
                if (markers != null && markers.HostileShown != 0) Fail("적아 표식을 꺼도 보임");
                DisplaySettings.SetIdentityMarkers(true);
                foreach (var b in silenced) if (b != null) b.enabled = true;
                CombatDevTools.ClearBattlefield();
                formation?.ClearAll();
            }

            // ---- G02: 한 방향 포화 → 표식 묶음, 가장 급한 미사일 먼저
            {
                var def = CombatDevTools.FindEnemy("ene_missileboat");
                var player = ship.transform;
                int fired = 0;
                for (int i = 0; i < 8; i++)
                {
                    float a = (40f + i * 2.5f) * Mathf.Deg2Rad;
                    float r = 80f + i * 3f;
                    Vector3 pos = player.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    pos.y = 3f;
                    var go = PoolManager.Instance.Spawn(def.MissilePrefab, pos, Quaternion.LookRotation((player.position - pos).normalized, Vector3.up));
                    if (go != null && go.TryGetComponent<Missile>(out var m)) { m.Launch(player, 0.01f); fired++; }
                }
                yield return new WaitForSeconds(0.6f);
                var warn = Object.FindFirstObjectByType<MissileWarningUI>();
                int shown = 0;
                string urgent = "";
                foreach (var t in warn.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                    shown++;
                    if (t.text.StartsWith("▶")) urgent = t.text;
                }
                _report.AppendLine($"- 위협 묶음: 미사일 {fired}발 한 방향 → 표식 {shown}개 · 가장 큰 묶음 {warn.LargestGroup} · 가장 급한 \"{urgent}\"");
                if (warn.LargestGroup < 3 || shown >= fired) Fail("한 방향 포화 미사일이 묶이지 않음(G02)");
                if (string.IsNullOrEmpty(urgent)) Fail("가장 급한 미사일 표시(▶)가 없음(G02)");
                yield return ScreenShot("bundleb_threat_group");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(0.5f);
            }

            // ---- C01~C04 · B05 · B06: 사전
            {
                hud.OpenPauseMenu();
                yield return null;
                Call(hud, "ShowCodex");
                yield return null;
                var codex = Object.FindFirstObjectByType<CodexUI>();
                codex.ShowSection(CodexSection.Weapons);
                codex.SetFilter(CodexRole.Sub);
                yield return null;
                var subs = new List<string>();
                bool allSub = true;
                foreach (var e in codex.Entries(CodexSection.Weapons))
                    if ((e.Roles & CodexRole.Sub) != 0) subs.Add(e.Title);
                foreach (var item in (System.Collections.IList)Get<object>(codex, "_listItems"))
                {
                    int index = (int)item.GetType().GetField("Item1").GetValue(item);
                    if ((codex.Entries(CodexSection.Weapons)[index].Roles & CodexRole.Sub) == 0) allSub = false;
                }
                _report.AppendLine($"- 사전 대잠 필터: 목록 {codex.VisibleCount} = 역할 {subs.Count} · 모두 대잠 {Yes(allSub)} ({string.Join(", ", subs)})");
                if (codex.VisibleCount != subs.Count || !allSub || codex.VisibleCount == 0) Fail("사전 역할 필터가 맞지 않음(C01)");
                yield return ScreenShot("bundleb_codex_filter");
                codex.SetFilter(CodexRole.None);
                codex.SetSearch("CIWS");
                yield return null;
                _report.AppendLine($"- 사전 검색 \"CIWS\": {codex.VisibleCount}개 · 선택 {codex.Selected?.Title}");
                if (codex.VisibleCount < 1 || codex.Selected == null || !codex.Selected.Title.Contains("CIWS")) Fail("사전 이름 검색이 맞지 않음(C01)");
                codex.SetSearch("");

                // 단계 비교(C02) — 기본형으로 열리고, 강화 단계를 고르면 본문 위에 그 단계 요약
                int ac = codex.Entries(CodexSection.Weapons).FindIndex(e => e.Id == "mod_autocannon");
                codex.Select(ac);
                string basic = codex.BodyText.Split('\n')[0];
                int model0 = codex.SelectedModel;
                codex.SelectModel(1);
                string stage = codex.BodyText.Split('\n')[0];
                _report.AppendLine($"- 단계 비교: 처음 모델 {model0} \"{Strip(basic)}\" → 강화 I \"{Strip(stage)}\"");
                if (model0 != 0 || !basic.Contains("기본형") || !stage.Contains("강화")) Fail("사전 단계 선택이 본문에 반영되지 않음(C02·B05)");
                yield return ScreenShot("bundleb_codex_stage");

                // 링크(C03)
                int sonar = codex.Entries(CodexSection.Weapons).FindIndex(e => e.Id == "mod_sonar");
                bool jumped = codex.JumpTo("ene_submarine");
                string jumpedTo = codex.Selected?.Id;
                bool hasLinks = codex.BodyText.Contains("<link=");
                _report.AppendLine($"- 링크 이동: 잠수함으로 {Yes(jumped)} → {codex.Section}/{jumpedTo} · 관련 무장 링크 {Yes(hasLinks)}");
                if (!jumped || jumpedTo != "ene_submarine" || !hasLinks) Fail("사전 링크 이동·관련 무장 링크가 없음(C03)");

                // 선택 기억(B06)
                codex.Close();
                yield return null;
                codex.Show();
                yield return null;
                _report.AppendLine($"- 닫았다 다시 열기: {codex.Section}/{codex.Selected?.Id}");
                if (codex.Section != CodexSection.Enemies || codex.Selected?.Id != "ene_submarine") Fail("사전을 다시 열면 보던 항목이 초기화됨(B06)");

                // 함정·용어(C04)
                codex.ShowSection(CodexSection.Guide);
                var guide = codex.Entries(CodexSection.Guide);
                int ships = 0, terms = 0;
                foreach (var e in guide) { if (e.Id.StartsWith("ship_")) ships++; else if (e.Id.StartsWith("term_")) terms++; }
                codex.Select(0);
                float shipCover = codex.MeasurePreview();
                _report.AppendLine($"- 함정·용어 탭: 함정 {ships} · 용어 {terms} · 첫 항목 {codex.Selected?.Title} · 미리보기 메시 {codex.PreviewRendererCount} · 화면 {shipCover * 100f:0.#}%");
                if (codex.PreviewRendererCount == 0 || shipCover < 0.02f) Fail("시작 함정 미리보기가 보이지 않음(C04)");
                if (ships < 4 || terms < 6) Fail("함정·용어 탭 항목이 부족함(C04)");
                yield return ScreenShot("bundleb_codex_guide");
                codex.Select(guide.FindIndex(e => e.Id == "term_detect"));
                yield return ScreenShot("bundleb_codex_term");
                codex.Close();
            }

            // ---- U04·U05·U06: 화면 설정
            {
                Call(hud, "ShowSettings");
                Call(hud, "ShowSettingsTab", 2);
                yield return null;
                yield return ScreenShot("bundleb_settings_display");
                Call(hud, "CloseSettings");
                hud.ResumeFromPause();
                yield return null;
                var scaler = hud.GetComponentInParent<CanvasScaler>();
                DisplaySettings.SetUiScale(2);
                yield return null;
                Vector2 big = scaler.referenceResolution;
                CombatDevTools.InstallModuleNear("mod_ciws", new GridCoord(0, 0), 8);
                yield return new WaitForSeconds(0.3f);
                yield return ScreenShot("bundleb_ui_130");
                DisplaySettings.SetUiScale(0);
                yield return null;
                _report.AppendLine($"- UI 크기 130%: 기준 해상도 {big.x:0}×{big.y:0} · 100%로 {scaler.referenceResolution.x:0}×{scaler.referenceResolution.y:0} · 맞춤 {scaler.screenMatchMode}");
                if (Mathf.Abs(big.x - 1920f / 1.3f) > 1f || Mathf.Abs(scaler.referenceResolution.x - 1920f) > 1f) Fail("UI 크기가 캔버스에 적용되지 않음(U04)");
                DisplaySettings.SetReduceFlashes(true);
                float reduced = DisplaySettings.FlashScale;
                DisplaySettings.SetReduceFlashes(false);
                _report.AppendLine($"- 번쩍임 줄이기: 세기 {reduced:0.##}");
                if (reduced >= 1f) Fail("번쩍임 줄이기가 세기를 낮추지 않음(U06)");
            }

            // ---- U03: 첫 출격 훈련(단계 진행)
            {
                CombatDevTools.ClearBattlefield();
                var director = Object.FindFirstObjectByType<StageDirector>();
                if (director != null) { director.enabled = true; Put(director, "_training", true); }
                var exp = Object.FindFirstObjectByType<Game.Progression.ExperienceSystem>();
                if (exp != null) exp.enabled = true;
                var training = TrainingDirector.Begin(hud.GetComponentInParent<Canvas>().rootCanvas.transform, hud.Font);
                yield return new WaitForSeconds(0.5f);
                yield return ScreenShot("bundleb_training_helm");
                var steps = new List<string> { $"{training.StepIndex}" };
                // 1 조타: 실제로 전령기를 올리고 타를 꺾는다
                ship.SetEngineOrder(1f);
                ship.DevRudderOverride = 1f;
                float helm = Time.realtimeSinceStartup;
                while (training.StepIndex == 0 && Time.realtimeSinceStartup - helm < 30f) yield return null;
                ship.SetEngineOrder(0f);
                ship.DevRudderOverride = 0f;
                steps.Add($"{training.StepIndex}({Time.realtimeSinceStartup - helm:0}초)");

                // 2~5: 단계마다 낸 적을 격침하고, 정비 화면이 열리면 닫는다
                bool refitShot = false, missileShot = false;
                float deadline = Time.realtimeSinceStartup + 60f;
                while (!training.Finished && Time.realtimeSinceStartup < deadline)
                {
                    int before = training.StepIndex;
                    if (GameManager.Instance.State == GameState.Refit)
                    {
                        if (!refitShot) { refitShot = true; yield return ScreenShot("bundleb_training_refit"); }
                        Call(Object.FindFirstObjectByType<Game.Refit.RefitController>(), "Resume");
                        yield return null;
                        _report.AppendLine($"  - 정비 닫음: 상태 {GameManager.Instance.State} · 훈련 단계 {training.StepIndex}");
                        continue;
                    }
                    if (before == 2 && !missileShot) { missileShot = true; yield return new WaitForSeconds(0.5f); yield return ScreenShot("bundleb_training_missile"); }
                    if (before != 3)
                        foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                            if (e.IsAlive) e.TakeDamage(new DamageInfo(99999f, e.transform.position, Vector3.forward, e is SubmarineBase ? DamageSource.Torpedo : DamageSource.Missile));
                    float wait = Time.realtimeSinceStartup;
                    while (training.StepIndex == before && GameManager.Instance.State != GameState.Refit && Time.realtimeSinceStartup - wait < 5f) yield return null;
                    if (training.StepIndex != before) steps.Add($"{training.StepIndex}");
                }
                int aliveEnemies = 0;
                foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) if (e.IsAlive) aliveEnemies++;
                _report.AppendLine($"- 훈련 중 정비 화면: {Yes(refitShot)} · 끝 상태 {GameManager.Instance.State} · 단계 {training.StepIndex} · 남은 적 {aliveEnemies} · 안내 \"{Strip(Get<TMPro.TMP_Text>(training, "_progress")?.text)}\"");
                if (!refitShot) Fail("훈련 정비 단계에서 레벨업 정비가 열리지 않음(U03)");
                _report.AppendLine($"- 첫 출격 훈련 단계: {string.Join(" → ", steps)} · 완료 {Yes(training.Finished)} · 기록 {Yes(TrainingDirector.Done)}");
                if (!training.Finished || !TrainingDirector.Done) Fail("첫 출격 훈련이 끝까지 진행되지 않음(U03)");
                yield return ScreenShot("bundleb_training_done");
            }

            BridgeMachineGun.DevHoldFire = false;
            if (field != null) Game.World.IslandField.Disabled = false;
            ship.DevRudderOverride = null;
        }

        private static string Strip(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<[^>]+>", "");
    }
}
#endif
