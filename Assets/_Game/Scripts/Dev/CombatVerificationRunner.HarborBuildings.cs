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
        }

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
