#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Progression;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — UI 스크린샷·HUD 배치·조함·탭 배치 검사.</summary>
    public partial class CombatVerificationRunner
    {
        // ------------------------------------------------------------ UI 전체 화면

        /// <summary>
        /// UI 점검용 전체 화면 캡처: 전투 HUD(적·미사일 경고 포함), Tab 현황판, 정비 카드(모듈·강화·호위함 개량), 설치/강화 선택.
        /// 규칙 검사는 겹침(카드 위로 비치는 정비 문구)과 전투 중 불필요한 전투단 수치 표시만 본다.
        /// </summary>
        private IEnumerator UiShotsCheck()
        {
            _report.AppendLine("\n## UI 전체 화면");
            var ship = GameManager.Instance.Player;
            var off = DisableWeapons(null);
            CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            CombatDevTools.SpawnRing("ene_fastboat", 3, 30f, 20f);
            CombatDevTools.SpawnRing("ene_missileboat", 1, 40f, 200f);
            yield return new WaitForSeconds(2f);
            yield return ScreenShot("ui_combat");

            var tab = Object.FindFirstObjectByType<Game.UI.ModuleStatusUI>(FindObjectsInactive.Include);
            if (tab != null)
            {
                tab.SetOpen(true);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return ScreenShot("ui_tab");
                tab.SetOpen(false);
            }
            CombatDevTools.ClearBattlefield();

            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            if (refit != null)
            {
                GameManager.Instance.SetState(GameState.Refit);
                refit.Open(7, new List<ModuleDefinition> { CombatDevTools.FindModule("mod_autocannon"), CombatDevTools.FindModule("mod_gun76"), CombatDevTools.FindModule("mod_sam") }, () => { });
                yield return null;
                yield return ScreenShot("ui_refit_cards");
                Call(refit, "ChooseCard", 0);
                yield return null;
                yield return ScreenShot("ui_refit_choice");
                Call(refit, "BackToCards");
                Call(refit, "ChooseCard", 1);
                yield return null;
                yield return ScreenShot("ui_refit_place");
                refit.Close();
                GameManager.Instance.SetState(GameState.Playing);
            }
            foreach (var d in off) if (d != null) d.enabled = true;
        }

        // ------------------------------------------------------------ 하단 HUD 배치

        /// <summary>
        /// 하단 HUD 덩어리: 레벨 막대 · 스킬 줄 · [레이더|함 현황|무장]이 겹치지 않고 화면 안에 있는지,
        /// 스킬 그림이 준비·재장전·장비 없음을 구분하는지 보고 캡처한다.
        /// </summary>
        private IEnumerator HudLayoutCheck()
        {
            _report.AppendLine("\n## 하단 HUD 배치");
            CombatDevTools.ClearBattlefield();
            // 스킬 장비를 갖춘다: 전자전(재밍), 수리반(응급 수리), 기만체 발사기(연막)
            foreach (var id in new[] { "mod_ew", "mod_repairbay", "mod_decoy" }) CombatDevTools.InstallModule(id);
            yield return new WaitForSeconds(1f);

            var names = new[] { "Level bar", "Skills", "Radar scope", "Ship status", "Weapon status" };
            var rects = new List<(string, Rect)>();
            var hud = Object.FindFirstObjectByType<Game.UI.HUDView>();
            foreach (var n in names)
            {
                var t = hud != null ? FindChild(hud.transform, n) as RectTransform : null;
                if (t == null) { Fail($"HUD 패널 없음: {n}"); continue; }
                var c = new Vector3[4];
                t.GetWorldCorners(c);
                var canvas = t.GetComponentInParent<Canvas>().rootCanvas;
                float s = canvas.transform.lossyScale.x;
                rects.Add((n, new Rect(c[0].x / s, c[0].y / s, (c[2].x - c[0].x) / s, (c[2].y - c[0].y) / s)));
            }
            var sb = new StringBuilder();
            foreach (var (n, r) in rects) sb.Append($"{n} ({r.xMin:0},{r.yMin:0})~({r.xMax:0},{r.yMax:0}) · ");
            _report.AppendLine($"- 패널(캔버스 좌표): {sb}");
            if (rects.Count == 5)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                foreach (var (_, r) in rects) { minX = Mathf.Min(minX, r.xMin); maxX = Mathf.Max(maxX, r.xMax); }
                var root = hud.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
                float canvasW = root.rect.width;
                _report.AppendLine($"- 덩어리 가로 {minX:0}~{maxX:0} (캔버스 폭 {canvasW:0}, 왼쪽 여백 {minX:0} · 오른쪽 여백 {canvasW - maxX:0})");
                if (Mathf.Abs(minX - (canvasW - maxX)) > 2f) Fail("하단 HUD가 가운데가 아님");
            }
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                {
                    var a = rects[i].Item2; var b = rects[j].Item2;
                    var inter = Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
                    if (inter.width > 0.5f && inter.height > 0.5f) Fail($"HUD 패널 겹침: {rects[i].Item1} / {rects[j].Item1}");
                }

            // 스킬: 기만체·전속은 쓰고(재장전), 나머지는 준비 상태로
            GameEvents.RaiseDecoyRequested();
            GameEvents.RaiseSkillRequested(ActiveSkillId.Flank);
            var ship = GameManager.Instance.Player;
            ship.DevRudderOverride = -0.6f;
            ship.SetEngineOrder(0.75f);
            yield return new WaitForSeconds(2.5f);
            yield return RadarShot("hud_layout");
            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            _report.AppendLine("- 캡처: hud_layout.png (기만체·전속 재장전 중, 좌현 타)");
        }

        // ------------------------------------------------------------ 조함

        /// <summary>
        /// 실제 배식 조함: 전령기 단계 가속, 전령기를 내린 뒤 미끄러짐, 타각 전환 시간, 선회 반경, 타를 푼 뒤 계속 도는 각도.
        /// </summary>
        private IEnumerator HelmCheck()
        {
            _report.AppendLine("\n## 조함(전령기·방향타)");
            var ship = GameManager.Instance.Player;
            var t = ship.transform;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(4f);

            // 1) 전령기 한 칸씩: STOP → 1/4 → 1/2
            ship.SetEngineOrder(0f);
            ship.ShiftEngineOrder(+1);
            float o1 = ship.ThrottleInput;
            ship.ShiftEngineOrder(+1);
            float o2 = ship.ThrottleInput;
            ship.ShiftEngineOrder(-1); ship.ShiftEngineOrder(-1); ship.ShiftEngineOrder(-1);
            float oRev = ship.ThrottleInput;
            ship.ShiftEngineOrder(-1);
            float oRev2 = ship.ThrottleInput;
            _report.AppendLine($"- 전령기: STOP+1 → {o1}, +1 → {o2}, -3 → {oRev}, 더 내려도 {oRev2}");
            if (!Mathf.Approximately(o1, 0.25f) || !Mathf.Approximately(o2, 0.5f) || oRev >= 0f || !Mathf.Approximately(oRev, oRev2))
                Fail("전령기 단계 이동이 이상함");

            // 2) 가속: STOP → FULL, 입력 없이도 유지
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(3f);
            ship.SetEngineOrder(1f);
            float start = Time.time, reach90 = -1f;
            while (Time.time - start < 8f)
            {
                if (reach90 < 0f && ship.CurrentSpeed >= ship.BaseMaxSpeed * 0.9f) reach90 = Time.time - start;
                yield return null;
            }
            float cruise = ship.CurrentSpeed;
            _report.AppendLine($"- FULL: 최고속력 90%까지 {reach90:0.0}초, 8초 뒤 {cruise:0.0} m/s (키를 누르지 않아도 유지)");
            if (reach90 < 0f) Fail("FULL에서 최고속력에 닿지 않음");

            // 3) 선회: 타 우현 최대. 타각 전환 시간, 선회율, 선회 반경(한 바퀴 궤적)
            ship.DevRudderOverride = 1f;
            start = Time.time;
            float fullRudderAt = -1f;
            float headingPrev = t.eulerAngles.y, turned = 0f;
            var minP = new Vector2(float.MaxValue, float.MaxValue);
            var maxP = new Vector2(float.MinValue, float.MinValue);
            float minSpeed = cruise;
            while (Time.time - start < 25f && turned < 400f)
            {
                if (fullRudderAt < 0f && ship.RudderAngle >= ship.MaxRudderAngle - 0.01f) fullRudderAt = Time.time - start;
                float h = t.eulerAngles.y;
                turned += Mathf.DeltaAngle(headingPrev, h);
                headingPrev = h;
                if (turned > 45f)
                {
                    var p = new Vector2(t.position.x, t.position.z);
                    minP = Vector2.Min(minP, p);
                    maxP = Vector2.Max(maxP, p);
                    minSpeed = Mathf.Min(minSpeed, ship.CurrentSpeed);
                }
                yield return null;
            }
            float circle = turned >= 400f ? Mathf.Max(maxP.x - minP.x, maxP.y - minP.y) : -1f;
            float yawRate = ship.YawRate;
            _report.AppendLine($"- 타 우현 최대: 타각 {ship.MaxRudderAngle:0}°까지 {fullRudderAt:0.0}초 · 선회율 {yawRate:0.0}°/초 · 선회 지름 약 {circle:0.0}m · 선회 중 속력 {minSpeed:0.0} m/s");
            if (fullRudderAt < 0f) Fail("타각이 최대에 닿지 않음");
            if (circle < 0f) Fail("한 바퀴 선회를 마치지 못함");
            if (yawRate <= 0f) Fail("우현 타에 우현으로 돌지 않음");
            yield return RadarShot("helm_turn");

            // 4) 타 중앙: 타를 풀어도 한동안 더 돈다
            ship.DevRudderOverride = 0f;
            headingPrev = t.eulerAngles.y;
            float carry = 0f;
            start = Time.time;
            while (Time.time - start < 6f)
            {
                float h = t.eulerAngles.y;
                carry += Mathf.DeltaAngle(headingPrev, h);
                headingPrev = h;
                yield return null;
            }
            _report.AppendLine($"- 타 중앙으로 돌린 뒤 더 돈 각도: {carry:0}° · 6초 뒤 선회율 {ship.YawRate:0.00}°/초");
            if (carry < 5f) Fail("타를 풀자마자 선회가 멈춤(관성 없음)");
            if (Mathf.Abs(ship.YawRate) > 1f) Fail("타 중앙인데 계속 돔");

            // 5) STOP: 전령기를 내려도 미끄러진다
            ship.SetEngineOrder(0f);
            Vector3 p0 = t.position;
            start = Time.time;
            float stopAt = -1f;
            while (Time.time - start < 10f)
            {
                if (stopAt < 0f && ship.CurrentSpeed < 0.3f) stopAt = Time.time - start;
                yield return null;
            }
            float glide = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(t.position.x, 0, t.position.z));
            _report.AppendLine($"- STOP: 멈추기까지 {stopAt:0.0}초, 미끄러진 거리 {glide:0.0}m");
            if (stopAt < 1f) Fail("STOP에 곧바로 멈춤");

            // 6) 정지 상태 타: 거의 돌지 않는다
            ship.DevRudderOverride = 1f;
            yield return new WaitForSeconds(2f);
            float idleYaw = ship.YawRate;
            _report.AppendLine($"- 정지 상태 최대 타 선회율: {idleYaw:0.0}°/초 (항주 중 {yawRate:0.0})");
            if (Mathf.Abs(idleYaw) > Mathf.Abs(yawRate) * 0.3f) Fail("정지 상태에서 너무 잘 돔");

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
        }

        // ------------------------------------------------------------ Tab 현황판

        /// <summary>모듈을 가득 설치해도 Tab 현황판이 창 안에 들어가는지(두 단·글자 줄이기·생략) 보고 캡처한다.</summary>
        private IEnumerator TabLayoutCheck()
        {
            _report.AppendLine("\n## Tab 현황판 레이아웃");
            var ui = Object.FindFirstObjectByType<Game.UI.ModuleStatusUI>(FindObjectsInactive.Include);
            if (ui == null) { Fail("ModuleStatusUI 없음"); yield break; }

            string[] fill = { "mod_autocannon", "mod_ciws", "mod_magazine", "mod_radar", "mod_sonar", "mod_asw", "mod_gun76", "mod_sam", "mod_rocket", "mod_decoy", "mod_repairbay", "mod_ew" };
            int[] targets = { 12, 30, 60 };
            foreach (int target in targets)
            {
                var grid = GameManager.Instance.Player.Grid;
                int guard = 0;
                while (grid.Modules.Count < target && guard++ < 400)
                    if (!CombatDevTools.InstallModule(fill[guard % fill.Length]) && guard > 200) break;

                // 몇 개는 부서지거나 탄이 떨어진 상태로
                int n = 0;
                foreach (var m in grid.Modules)
                    if (m.Definition.Type != ModuleType.Bridge && ++n % 7 == 0) m.TakeDamage(m.MaxHp * 0.8f);

                ui.SetOpen(true);
                yield return null;
                yield return null;
                var l = ui.LastLayout;
                _report.AppendLine($"- 모듈 {grid.Modules.Count}개: 표시 {l.shown} · 생략 {l.hidden} · 글자 {l.fontSize:0} · {l.columns}단");
                if (l.shown + l.hidden != grid.Modules.Count) Fail($"Tab 현황판 줄 수 불일치({l.shown}+{l.hidden} ≠ {grid.Modules.Count})");
                if (grid.Modules.Count <= 40 && l.hidden > 0) Fail($"모듈 {grid.Modules.Count}개인데 생략이 생김");
                yield return RadarShot($"tab_{grid.Modules.Count}");
                ui.SetOpen(false);
            }
            ResetLoadout();
            CombatDevTools.InstallTestLoadout();
        }
    }
}
#endif
