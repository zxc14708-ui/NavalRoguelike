#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.UI;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 군항 건물 1단계(2026-10-28, -harborBuildingsOnly): 시작 화면 그대로(검사 환경 없이).
        ///   1) 기능 건물 이름표 셋이 화면 안에 보인다 · 다른 건물은 가까이 가면 이름만
        ///   2) 이름표를 누르면 카메라가 그 건물로 다가가(화면 가운데 근처) 패널이 열리고 출항 콘솔은 숨는다 · 돌아가면 개관
        ///   3) 기록실 패널 → 출격 기록 화면
        ///   4) 작전 포인트: 판 보상 공식 · 도전 과제 +3 · 판 기록이 포인트를 쌓는다
        /// </summary>
        private IEnumerator HarborBuildingsCheck()
        {
            _report.AppendLine("\n## 군항 건물(1단계)");
            var ui = Object.FindFirstObjectByType<HarborBuildingsUI>();
            var harbor = NavalBaseMenu.Active;
            if (ui == null || harbor == null) { Fail("군항 건물 UI 또는 군항 배경이 없음"); yield break; }
            for (float t = 0f; t < 3f && !harbor.CameraSettled; t += Time.unscaledDeltaTime) yield return null;
            yield return null;

            // 1) 이름표
            int shown = 0; string names = "";
            foreach (var b in HarborBuildingsUI.All)
            {
                if (b.Role == HarborRole.None) continue;
                var tag = GameObject.Find("Harbor tag " + b.Id);
                bool on = tag != null && tag.activeInHierarchy;
                if (on) shown++;
                names += $"{b.Name} {(on ? "보임" : "숨음")} · ";
            }
            // 이름만 있는 건물: 사령부 행정동 A 위치 근처에서 이름이 나오는가
            var admin = System.Array.Find(HarborBuildingsUI.All, b => b.Id == "admin_a");
            string hover = harbor.TryProject(admin.Anchor, out var vp) ? ui.NearestName(vp, 0.03f).name : null;
            _report.AppendLine($"- 이름표: {names}건물 {HarborBuildingsUI.All.Length}곳 · 행정동 A 근처 이름 \"{hover}\"");
            int quick = 0;
            foreach (var b in HarborBuildingsUI.All)
                if (b.Role != HarborRole.None && GameObject.Find("Harbor facility " + b.Id) is { } q && q.activeInHierarchy) quick++;
            _report.AppendLine($"- 시설 바로 가기 {quick}개 · 콘솔에 가려지지 않은 이름표 {shown}개");
            if (quick != 3) Fail("시설 바로 가기(연구소·기록실·정비창)가 보이지 않음");
            if (shown < 1) Fail("기능 건물 이름표가 하나도 보이지 않음");
            if (hover != "행정동 A") Fail("이름만 있는 건물에 마우스를 올려도 이름이 나오지 않음");
            yield return HarborShot("harbor_buildings_overview");

            // 2) 건물 확대 · 패널 · 돌아가기
            var launch = GameObject.Find("Harbor departure console");
            foreach (var b in HarborBuildingsUI.All)
            {
                if (b.Role == HarborRole.None) continue;
                ui.Focus(b);
                for (float t = 0f; t < 6f && !harbor.CameraSettled; t += Time.unscaledDeltaTime) yield return null;
                yield return null;
                bool centred = harbor.TryProject(b.Anchor, out var c) && Mathf.Abs(c.x - 0.5f) < 0.15f && Mathf.Abs(c.y - 0.5f) < 0.2f;
                bool consoleHidden = launch == null || !launch.activeInHierarchy;
                _report.AppendLine($"- {b.Name}: 패널 {Yes(ui.PanelOpen)} \"{ui.PanelTitle}\" · 카메라 화면 가운데 {Yes(centred)}({c.x:0.00}, {c.y:0.00}) · 출항 콘솔 숨김 {Yes(consoleHidden)}");
                if (!ui.PanelOpen || ui.PanelTitle != b.Name) Fail($"{b.Name} 패널이 열리지 않음");
                if (!centred) Fail($"{b.Name}로 카메라가 다가가지 않음");
                if (!consoleHidden) Fail("건물 패널이 열렸는데 출항 콘솔이 남아 있음");
                yield return HarborShot($"harbor_building_{b.Id}");
                if (b.Role == HarborRole.Records)
                {
                    ui.PanelAction.onClick.Invoke();
                    yield return null;
                    var records = Object.FindFirstObjectByType<RecordsUI>();
                    bool open = records != null && records.gameObject.activeInHierarchy;
                    _report.AppendLine($"  - [출격 기록 · 도전 과제 열기] → 기록 화면 {Yes(open)}");
                    if (!open) Fail("기록실 패널에서 기록 화면이 열리지 않음");
                    if (records != null) records.Close();
                }
                ui.Back();
                yield return null;
                bool back = !ui.PanelOpen && !harbor.FocusingBuilding && (launch == null || launch.activeInHierarchy);
                if (!back) Fail($"{b.Name}에서 돌아가도 개관·출항 콘솔로 돌아오지 않음");
            }

            // 바로 가기 단추로 들어가기
            var dockQuick = GameObject.Find("Harbor facility floating_dock");
            if (dockQuick != null) dockQuick.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            bool viaQuick = ui.PanelOpen && ui.PanelTitle == "함정 정비창";
            _report.AppendLine($"- 바로 가기 [함정 정비창] → 패널 {Yes(viaQuick)}");
            if (!viaQuick) Fail("시설 바로 가기로 건물 패널이 열리지 않음");
            ui.Back();
            yield return null;

            // 4) 작전 포인트
            int p1 = PlayerProfile.RunPoints(1, false, false), p2 = PlayerProfile.RunPoints(2, false, false), p3 = PlayerProfile.RunPoints(3, true, false), p3d = PlayerProfile.RunPoints(3, true, true);
            _report.AppendLine($"- 판 보상: 스테이지 1 패배 {p1} · 스테이지 2 패배 {p2} · 승리 {p3} · 고난도 승리 {p3d}");
            if (p1 != 1 || p2 != 4 || p3 != 9 || p3d != 14) Fail("작전 포인트 판 보상 공식이 1 / 4 / 9 / 14가 아님");
            PlayerProfile.ResetForVerify();
            int before = PlayerProfile.OpPoints;
            PlayerProfile.RecordRun("ship_patrol", false, 2, 10, 100f, null, false);   // 판 4 + 첫 출항 과제 3
            int afterRun = PlayerProfile.OpPoints;
            bool spent = PlayerProfile.SpendPoints(5);
            int afterSpend = PlayerProfile.OpPoints;
            _report.AppendLine($"- 포인트 쌓기: {before} → 판 기록 뒤 {afterRun}(판 4 + 도전 과제 3) → 5 쓰기 {Yes(spent)} → {afterSpend} · 누적 {PlayerProfile.OpPointsEarned}");
            if (afterRun - before != 7 || !spent || afterSpend != afterRun - 5 || PlayerProfile.OpPointsEarned != 7) Fail("작전 포인트가 판 기록·도전 과제로 쌓이거나 쓰이지 않음");
            PlayerProfile.ResetForVerify();

            yield return ResearchCheck(ui);
        }

        /// <summary>
        /// 연구소 2단계(2026-10-28): 해군 기술연구소 → 연구 목록. 실제 단추로 연구하면 포인트가 줄고 능력이 오르며(성장 카드와 같은 경로),
        /// 모자라면 못 하고, 되돌리면 모두 돌려받는다. 자동 검증에서는 연구가 꺼져 있으므로 이 검사 동안만 켠다.
        /// </summary>
        private IEnumerator ResearchCheck(HarborBuildingsUI ui)
        {
            var research = System.Array.Find(HarborBuildingsUI.All, b => b.Role == HarborRole.Research);
            Game.Modules.MetaResearch.DevForce = true;
            PlayerProfile.ResetForVerify();
            Game.Modules.MetaResearch.Invalidate();
            PlayerProfile.RefundPoints(30);   // 시험용 포인트
            ui.Focus(research);
            yield return null;
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            float hull0 = ship != null ? ship.HullMaxHp : 0f;
            int p0 = PlayerProfile.OpPoints;
            bool rowsOk = ui.ResearchOpen && ui.ResearchRowText("gun") is { } gunRow && gunRow.Contains("함포 연구") && gunRow.Contains("연구 · 2");
            ui.ResearchButton("gun").onClick.Invoke();
            ui.ResearchButton("gun").onClick.Invoke();
            ui.ResearchButton("planning").onClick.Invoke();
            ui.ResearchButton("hull").onClick.Invoke();
            yield return null;
            int p1 = PlayerProfile.OpPoints;
            float gun = Game.Modules.RunUpgrades.Get(Game.Modules.RunStat.GunDamage);
            float acMul = Game.Modules.RunUpgrades.DamageMultiplier(Game.Modules.ModuleType.Autocannon);
            float vlsMul = Game.Modules.RunUpgrades.DamageMultiplier(Game.Modules.ModuleType.Vls);
            int rerolls = Game.Refit.RefitDraft.RerollsPerRun;
            float hull1 = ship != null ? ship.HullMaxHp : 0f;
            _report.AppendLine($"- 연구소 목록 {Yes(rowsOk)} · \"{ui.ResearchRowText("gun")}\"");
            _report.AppendLine($"- 연구: 함포 2단계 · 작전 기획 1 · 선체 1 → 포인트 {p0} → {p1}(2+3+5+2 = 12) · 함포계 +{gun * 100f:0.#}% · 기관포 피해 ×{acMul:0.00} · VLS ×{vlsMul:0.00} · " +
                               $"다시 뽑기 {rerolls}회 · 선체 최대 {hull0:0} → {hull1:0}");
            if (!rowsOk) Fail("연구소에 연구 목록이 열리지 않음");
            if (p0 - p1 != 12) Fail("연구 비용(2 · 3 · 5 · 2)이 포인트에서 빠지지 않음");
            if (Mathf.Abs(gun - 0.04f) > 0.0001f || Mathf.Abs(acMul - 1.04f) > 0.0001f || Mathf.Abs(vlsMul - 1f) > 0.0001f) Fail("함포 연구 2단계가 함포계에만 +4%로 들어가지 않음");
            if (rerolls != Game.Refit.RefitDraft.BaseRerolls + 1) Fail("작전 기획이 다시 뽑기를 늘리지 않음");
            if (ship != null && Mathf.Abs(hull1 - hull0 * 1.03f) > 0.6f) Fail("선체 구조 연구가 함선 선체 최대치를 3% 올리지 않음");

            // 모자라면 못 한다
            PlayerProfile.SpendPoints(PlayerProfile.OpPoints);
            ui.Focus(research);
            yield return null;
            bool blocked = !ui.ResearchButton("missile").interactable;
            ui.ResearchButton("missile").onClick.Invoke();
            bool unchanged = Game.Modules.MetaResearch.Level("missile") == 0;
            yield return HarborShot("harbor_research");

            // 되돌리기: 두 번 눌러야 · 쓴 포인트 모두 반환
            int spent = Game.Modules.MetaResearch.Spent();
            ui.ResearchResetButton.onClick.Invoke();
            bool armedOnly = Game.Modules.MetaResearch.Spent() == spent;
            ui.ResearchResetButton.onClick.Invoke();
            yield return null;
            float gunAfter = Game.Modules.RunUpgrades.Get(Game.Modules.RunStat.GunDamage);
            float hull2 = ship != null ? ship.HullMaxHp : 0f;
            _report.AppendLine($"- 포인트 0: 미사일 연구 단추 막힘 {Yes(blocked)} · 눌러도 그대로 {Yes(unchanged)} · 되돌리기 한 번은 확인만 {Yes(armedOnly)} → 두 번째에 반환 +{spent} · 포인트 {PlayerProfile.OpPoints} · 함포계 +{gunAfter * 100f:0.#}% · 선체 최대 {hull2:0}");
            if (!blocked || !unchanged) Fail("포인트가 모자란데 연구가 됨");
            if (!armedOnly || PlayerProfile.OpPoints != spent || gunAfter != 0f || Game.Modules.MetaResearch.Spent() != 0) Fail("연구 되돌리기가 포인트를 돌려주고 능력을 원래대로 하지 않음");
            if (ship != null && Mathf.Abs(hull2 - hull0) > 0.6f) Fail("연구를 되돌려도 선체 최대치가 돌아오지 않음");
            ui.Back();
            Game.Modules.MetaResearch.DevForce = false;
            PlayerProfile.ResetForVerify();
            Game.Modules.MetaResearch.Invalidate();

            yield return WorkshopCheck(ui);
        }

        /// <summary>
        /// 정비창 3단계(2026-10-28): 함정 정비창 → 함급 탭 · 함교 강화 · 시작 배치 개조. 실제 단추로 강화하면 실제 함교 탐지가 오르고,
        /// 개조안을 열면 출항 배치(ShipInitializer.TrySelect)의 그 칸 블록이 바뀌며 원본 시작 배치는 그대로, 훈련(workshop:false)은 기본형.
        /// 개조안 8종이 모두 실제로 설치되는지, 포인트가 모자라면 막히는지, 되돌리면 모두 돌려받는지.
        /// </summary>
        private IEnumerator WorkshopCheck(HarborBuildingsUI ui)
        {
            var workshop = System.Array.Find(HarborBuildingsUI.All, b => b.Role == HarborRole.Workshop);
            var init = Object.FindFirstObjectByType<Game.Ship.ShipInitializer>();
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (init == null || ship == null) { Fail("정비창 검사: 함선 · 시작 배치 설치기 없음"); yield break; }
            var originalConcept = init.SelectedConcept;
            var patrol = Game.Data.StartingShipCatalog.Get(Game.Data.StartShipKind.Patrol);
            Game.Modules.MetaResearch.DevForce = true;
            PlayerProfile.ResetForVerify();
            PlayerProfile.RefundPoints(40);   // 시험용 포인트
            if (!init.TrySelect(patrol, out string why)) Fail("정비창 검사: 초계함 선택 실패 " + why);
            yield return null;
            float d0 = ship.Systems.DetectionRange;

            ui.Focus(workshop);
            yield return null;
            ui.WorkshopTab(Game.Data.StartShipKind.Patrol).onClick.Invoke();
            bool open = ui.WorkshopOpen && ui.PanelTitle == "함정 정비창" && ui.WorkshopKind == Game.Data.StartShipKind.Patrol;
            string bridgeRow = ui.WorkshopBridgeText;
            bool bridgeRowOk = bridgeRow != null && bridgeRow.Contains("초계함 함교 레이더") && bridgeRow.Contains("강화 · 3");
            int p0 = PlayerProfile.OpPoints;
            ui.WorkshopBridgeButton.onClick.Invoke();
            ui.WorkshopBridgeButton.onClick.Invoke();
            yield return null;
            int p1 = PlayerProfile.OpPoints;
            float d1 = ship.Systems.DetectionRange;
            _report.AppendLine("\n## 함정 정비창(3단계)");
            _report.AppendLine($"- 정비창 열림 {Yes(open)} · 함교 줄 {Yes(bridgeRowOk)} \"{bridgeRow}\"");
            _report.AppendLine($"- 함교 강화 2단계: 포인트 {p0} → {p1}(3+5) · 실제 함선 탐지 {d0:0.#} → {d1:0.#}");
            if (!open) Fail("정비창에 정비 화면이 열리지 않음");
            if (!bridgeRowOk) Fail("정비창 함교 강화 줄 문구가 다름");
            if (p0 - p1 != 8) Fail("함교 강화 비용(3 · 5)이 포인트에서 빠지지 않음");
            if (Mathf.Abs(d1 - (d0 + 8f)) > 0.01f) Fail("초계함 함교 강화 2단계가 실제 탐지를 +8 올리지 않음");

            // 시작 배치 개조: 화력형(선수 기관포 → 76mm)
            string row1 = ui.WorkshopLayoutText(1);
            bool rowOk = row1 != null && row1.Contains("화력형") && row1.Contains("76mm") && row1.Contains("개조 · 6") && (ui.WorkshopLayoutText(0) ?? "").Contains("사용 중");
            ui.WorkshopLayoutButton(1).onClick.Invoke();
            yield return null;
            int p2 = PlayerProfile.OpPoints;
            bool chosen = Game.Modules.MetaWorkshop.SelectedVariant(Game.Data.StartShipKind.Patrol)?.Id == "patrol_gun" && (ui.WorkshopLayoutText(1) ?? "").Contains("사용 중");
            init.TrySelect(patrol, out why);
            yield return null;
            var bow = ModuleAt(ship, 2, 0);
            int count = ship.Grid.Modules.Count;
            bool assetIntact = false;
            foreach (var e in patrol.StartLoadout.Entries) if (e.Origin.X == 2 && e.Origin.Z == 0) assetIntact = e.Module != null && e.Module.Type == Game.Modules.ModuleType.Autocannon;
            init.TrySelect(patrol, out why, workshop: false);   // 훈련 경로
            yield return null;
            var trainingBow = ModuleAt(ship, 2, 0);
            ui.WorkshopLayoutButton(0).onClick.Invoke();   // 기본형으로 되돌려 고르기(무료)
            int p3 = PlayerProfile.OpPoints;
            init.TrySelect(patrol, out why);
            yield return null;
            var baseBow = ModuleAt(ship, 2, 0);
            bool reselect = (ui.WorkshopLayoutText(1) ?? "").Contains("선택");
            _report.AppendLine($"- 개조안 줄 {Yes(rowOk)} \"{row1}\"");
            _report.AppendLine($"- 화력형 개조: 포인트 {p1} → {p2} · 고름 {Yes(chosen)} · 출항 배치 선수 {Name(bow)} · 블록 {count}개 · 원본 시작 배치 그대로 {Yes(assetIntact)} · 훈련 선수 {Name(trainingBow)} · " +
                               $"기본형 다시 고름(무료 {Yes(p3 == p2)}) → 선수 {Name(baseBow)} · 화력형 다시 [선택] {Yes(reselect)}");
            if (!rowOk) Fail("정비창 개조안 줄 문구가 다름");
            if (p1 - p2 != 6 || !chosen) Fail("개조안을 열어도 포인트가 빠지거나 그 배치가 골라지지 않음");
            if (bow == null || bow.Definition.Type != Game.Modules.ModuleType.NavalGun || count != 4) Fail("화력형 개조안이 출항 배치 선수에 76mm를 놓지 않음");
            if (!assetIntact) Fail("개조안이 원본 시작 배치 데이터를 바꿈");
            if (trainingBow == null || trainingBow.Definition.Type != Game.Modules.ModuleType.Autocannon) Fail("훈련(workshop:false)이 기본형으로 시작하지 않음");
            if (p3 != p2 || baseBow == null || baseBow.Definition.Type != Game.Modules.ModuleType.Autocannon || !reselect) Fail("기본형으로 다시 고르기가 안 되거나 포인트가 빠짐");

            // 개조안 8종 모두 실제로 설치된다
            var installed = new System.Text.StringBuilder();
            bool allOk = true;
            foreach (var v in Game.Modules.MetaWorkshop.Variants)
            {
                PlayerProfile.RefundPoints(v.Cost);
                bool unlocked = Game.Modules.MetaWorkshop.Unlocked(v) ? Game.Modules.MetaWorkshop.SelectLayout(v.Kind, v.Id) : Game.Modules.MetaWorkshop.TryUnlock(v.Id);
                var concept = Game.Data.StartingShipCatalog.Get(v.Kind);
                bool selected = init.TrySelect(concept, out why);
                yield return null;
                var m = ModuleAt(ship, v.X, v.Z);
                bool ok = unlocked && selected && m != null && m.Definition.Id == v.ModuleId && m.RotationSteps == v.Rotation;
                allOk &= ok;
                installed.Append($"{v.Id} {(ok ? "○" : "×" + why)} · ");
            }
            _report.AppendLine($"- 개조안 8종 실제 설치: {installed}");
            if (!allOk) Fail("설치되지 않는 시작 배치 개조안이 있음");

            // 모자라면 못 한다(지휘함 함교)
            PlayerProfile.SpendPoints(PlayerProfile.OpPoints);
            ui.WorkshopTab(Game.Data.StartShipKind.Command).onClick.Invoke();
            bool blocked = !ui.WorkshopBridgeButton.interactable;
            ui.WorkshopBridgeButton.onClick.Invoke();
            bool unchanged = Game.Modules.MetaWorkshop.BridgeLevel(Game.Data.StartShipKind.Command) == 0;
            ui.WorkshopTab(Game.Data.StartShipKind.Patrol).onClick.Invoke();
            yield return null;
            yield return HarborShot("harbor_workshop");

            // 되돌리기: 두 번 · 모두 반환 · 기본형으로
            init.TrySelect(patrol, out why);
            yield return null;
            int spent = Game.Modules.MetaWorkshop.Spent();
            ui.WorkshopResetButton.onClick.Invoke();
            bool armedOnly = Game.Modules.MetaWorkshop.Spent() == spent;
            ui.WorkshopResetButton.onClick.Invoke();
            yield return null;
            float d2 = ship.Systems.DetectionRange;
            bool cleared = Game.Modules.MetaWorkshop.Spent() == 0 && Game.Modules.MetaWorkshop.SelectedVariant(Game.Data.StartShipKind.Patrol) == null;
            _report.AppendLine($"- 포인트 0: 지휘함 함교 강화 막힘 {Yes(blocked)} · 눌러도 그대로 {Yes(unchanged)} · 되돌리기 한 번은 확인만 {Yes(armedOnly)} → 반환 +{spent} · 포인트 {PlayerProfile.OpPoints} · " +
                               $"모두 기본형 {Yes(cleared)} · 탐지 {d2:0.#}");
            if (!blocked || !unchanged) Fail("포인트가 모자란데 함교 강화가 됨");
            if (!armedOnly || PlayerProfile.OpPoints != spent || !cleared) Fail("정비 되돌리기가 포인트를 돌려주고 기본형으로 하지 않음");
            if (Mathf.Abs(d2 - d0) > 0.01f) Fail("정비를 되돌려도 함교 탐지가 돌아오지 않음");

            ui.Back();
            Game.Modules.MetaResearch.DevForce = false;
            PlayerProfile.ResetForVerify();
            if (originalConcept != null) init.TrySelect(originalConcept, out _);
        }

        private static Game.Modules.ModuleInstance ModuleAt(Game.Ship.ShipController ship, int x, int z)
        {
            foreach (var m in ship.Grid.Modules) if (m.Origin.X == x && m.Origin.Z == z) return m;
            return null;
        }

        private static string Name(Game.Modules.ModuleInstance m) => m != null ? m.Definition.DisplayName : "없음";

        /// <summary>시작 화면 전체(군항 배경 + UI)를 화면 그대로 찍는다. 창 모드(-Windowed)로 돌려야 그려진다.</summary>
        private IEnumerator HarborShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutputDirectory, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
#endif
