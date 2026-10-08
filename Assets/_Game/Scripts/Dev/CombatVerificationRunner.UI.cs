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

        // ------------------------------------------------------------ 우클릭 항로(자동 조함)

        /// <summary>
        /// 우클릭 항로(2026-10-07): 경유지 3개를 찍고(6번째는 거절) 실제로 차례로 지나가는지, 마지막 뒤 그 구간 방향으로 직진하는지,
        /// 예상 항로가 실제 항적과 얼마나 맞는지, 조함 키 입력(ManualHelm)으로 수동 조함으로 돌아가는지 본다. 화면도 남긴다.
        /// </summary>
        private IEnumerator RouteCheck()
        {
            _report.AppendLine("\n## 우클릭 항로(자동 조함)");
            var ship = GameManager.Instance.Player;
            var pilot = ship.GetComponent<ShipAutopilot>();
            if (pilot == null) { Fail("ShipAutopilot이 없음"); yield break; }
            // 시작 화면 구성이 바뀌어 출항 절차 뒤에도 전투 상태가 아닐 수 있다 — 항로는 전투 중에만 동작하므로 직접 맞춘다
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = TimeScale;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(3f);

            var t = ship.transform;
            // 섬이 없는 넓은 바다로 옮겨 시작한다(선회권까지 섬과 닿지 않게)
            Vector3 open = t.position;
            for (int ring = 0; ring <= 15 && !Game.World.Islands.IsClear(open, 130f); ring++)
                for (int k = 0; k < 16; k++)
                {
                    float a = k / 16f * Mathf.PI * 2f;
                    var cand = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (ring * 60f);
                    if (Game.World.Islands.IsClear(cand, 130f)) { open = cand; break; }
                }
            if (Vector3.Distance(open, t.position) > 1f)
            {
                open.y = t.position.y;
                t.position = open;
                if (ship.TryGetComponent<Rigidbody>(out var body)) body.position = open;
                _report.AppendLine($"- 넓은 바다로 옮겨 시작 ({open.x:0}, {open.z:0})");
                yield return new WaitForSeconds(1f);
            }
            Vector3 o = new(t.position.x, 0f, t.position.z);
            Vector3 f = new Vector3(t.forward.x, 0f, t.forward.z).normalized, r = Vector3.Cross(Vector3.up, f);
            var points = new[] { o + f * 45f + r * 25f, o + f * 70f - r * 30f, o + f * 30f - r * 75f };
            // 섬이 없는 쪽으로 돌려 놓는다(지점과 구간 위 18m 안에 섬이 없어야 함)
            for (int turn = 0; turn < 12; turn++)
            {
                var q = Quaternion.Euler(0f, turn * 30f, 0f);
                var cand = new[] { o + q * (f * 45f + r * 25f), o + q * (f * 70f - r * 30f), o + q * (f * 30f - r * 75f) };
                bool clear = true;
                Vector3 prev = o;
                foreach (var c in cand)
                {
                    for (int k = 1; k <= 8 && clear; k++) clear = Game.World.Islands.IsClear(Vector3.Lerp(prev, c, k / 8f), 18f);
                    prev = c;
                }
                if (clear) { points = cand; _report.AppendLine($"- 섬을 피해 경로 방향 {turn * 30}° 돌림"); break; }
            }

            pilot.SetTarget(points[0], false);
            pilot.SetTarget(points[1], true);
            pilot.SetTarget(points[2], true);
            pilot.SetTarget(o + f * 120f, true);
            pilot.SetTarget(o + f * 140f, true);
            bool sixth = pilot.SetTarget(o + f * 160f, true);
            _report.AppendLine($"- 경유지 5개까지 받음: {pilot.Route.Count}개, 6번째 거절 {Yes(!sixth)}");
            if (pilot.Route.Count != 5 || sixth) Fail("경유지 최대 5개 제한이 이상함");
            // 검사는 3개로: 다시 찍는다(Shift 없이 = 처음부터)
            pilot.SetTarget(points[0], false);
            pilot.SetTarget(points[1], true);
            pilot.SetTarget(points[2], true);
            _report.AppendLine($"- 정지 상태에서 항로 지정 → 전령기 {ship.ThrottleInput:0.##}(앞으로 가게 올림)");
            if (ship.ThrottleInput <= 0f) Fail("정지 상태에서 항로를 찍어도 전령기가 그대로");

            yield return new WaitForSeconds(0.4f);
            var predicted = new List<Vector3>(pilot.Predicted);
            int tailStart = pilot.PredictedTailStart;
            yield return Shot("route_set");

            float start = Time.time, maxPassDist = 0f;
            var reachedAt = new List<float>();
            var track = new List<Vector3>();
            int lastCount = pilot.Route.Count;
            float[] closest = { float.MaxValue, float.MaxValue, float.MaxValue };
            bool aground = false, midShot = false;
            while (Time.time - start < 120f && pilot.HasRoute)
            {
                Vector3 p = new(t.position.x, 0f, t.position.z);
                track.Add(p);
                aground |= ship.IsAground;
                int idx = points.Length - pilot.Route.Count;
                if (idx >= 0 && idx < 3) closest[idx] = Mathf.Min(closest[idx], Vector3.Distance(p, points[idx]));
                if (pilot.Route.Count < lastCount) { reachedAt.Add(Time.time - start); lastCount = pilot.Route.Count; }
                if (!midShot && reachedAt.Count == 1) { midShot = true; yield return Shot("route_mid"); }
                yield return new WaitForSeconds(0.1f);
            }
            if (pilot.Route.Count < lastCount) reachedAt.Add(Time.time - start);
            for (int i = 0; i < 3; i++) maxPassDist = Mathf.Max(maxPassDist, closest[i]);
            _report.AppendLine($"- 경유지 통과 {reachedAt.Count}/3 · 시각 {string.Join(", ", reachedAt.ConvertAll(x => x.ToString("0.0") + "초"))} · " +
                               $"가장 가까이 간 거리 {closest[0]:0.0} / {closest[1]:0.0} / {closest[2]:0.0} m · 좌초 {Yes(aground)}");
            if (reachedAt.Count != 3) Fail("경유지를 모두 지나지 못함");
            if (maxPassDist > 14f) Fail($"경유지를 너무 멀리 지나감({maxPassDist:0.0} m)");

            // 예상 항로와 실제 항적: 실제 점마다 예상 선(직진 꼬리 앞까지)에서 가장 가까운 거리
            if (predicted.Count < 2) { Fail("예상 항로가 계산되지 않음"); pilot.Cancel(); yield break; }
            float sum = 0f, worst = 0f; int n = 0;
            int predEnd = Mathf.Clamp(tailStart + 1, 2, predicted.Count);
            foreach (var p in track)
            {
                float best = float.MaxValue;
                for (int i = 0; i + 1 < predEnd; i++)
                {
                    Vector3 a = predicted[i], b = predicted[i + 1], ab = b - a;
                    float u = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    best = Mathf.Min(best, Vector3.Distance(p, a + ab * u));
                }
                if (best < float.MaxValue) { sum += best; worst = Mathf.Max(worst, best); n++; }
            }
            _report.AppendLine($"- 예상 항로 대비 실제 항적: 평균 {(n > 0 ? sum / n : 0f):0.0} m, 최대 {worst:0.0} m (예상 점 {predicted.Count}개)");
            if (n == 0 || worst > 8f) Fail("예상 항로가 실제 항적과 많이 다름");

            // 마지막 목표 뒤: 지날 때의 침로로 직진
            float legHeading = pilot.HoldHeading;
            yield return new WaitForSeconds(6f);
            float h0 = t.eulerAngles.y;
            yield return new WaitForSeconds(3f);
            float h1 = t.eulerAngles.y;
            float off = Mathf.Abs(Mathf.DeltaAngle(h1, legHeading)), drift = Mathf.Abs(Mathf.DeltaAngle(h0, h1));
            _report.AppendLine($"- 도착 뒤 침로 유지 {Yes(pilot.IsHolding)} · 지날 때 침로와 차이 {off:0.0}° · 3초간 침로 변화 {drift:0.0}° · 속력 {ship.CurrentSpeed:0.0} m/s");
            yield return Shot("route_hold");
            if (!pilot.IsHolding || off > 6f || drift > 2f) Fail("마지막 목표 뒤 직진이 안 됨");

            // 조함 키 → 수동 조함(ShipController.ManualHelm 이벤트를 키 입력 대신 직접 부른다)
            pilot.SetTarget(o + f * 200f, false);
            var field = typeof(ShipController).GetField("ManualHelm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            (field?.GetValue(ship) as System.Action)?.Invoke();
            yield return null;
            _report.AppendLine($"- 조함 키 입력 뒤: 항로 {pilot.Route.Count}개, 침로 유지 {Yes(pilot.IsHolding)}, 자동 타 {(ship.AutoRudder.HasValue ? "남음" : "해제")}");
            if (pilot.HasRoute || pilot.IsHolding || ship.AutoRudder.HasValue) Fail("조함 키를 눌러도 자동 조함이 풀리지 않음");

            // 앵커 위 우클릭 = 그 목표 취소(RemoveAt)
            pilot.SetTarget(o + f * 60f, false);
            pilot.SetTarget(o + f * 90f, true);
            pilot.RemoveAt(0);
            _report.AppendLine($"- 앵커 취소: 2개 중 1번 취소 → {pilot.Route.Count}개 남음");
            pilot.RemoveAt(0);
            _report.AppendLine($"- 마지막 앵커 취소 → 항로 {pilot.Route.Count}개, 지금 방향으로 직진(침로 유지) {Yes(pilot.IsHolding)}");
            if (pilot.HasRoute || !pilot.IsHolding) Fail("앵커 취소가 이상함");
            pilot.Cancel();
            ship.SetEngineOrder(0f);
        }

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
