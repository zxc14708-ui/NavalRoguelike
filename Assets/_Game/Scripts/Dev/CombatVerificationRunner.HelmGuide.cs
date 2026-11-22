#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Core;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 타륜 표시(2026-11-22): 디자인 5가지 · 예상 항적이 실제 항로와 맞는가 · 놓으면 옅게 · 설정 › 조함 미리보기.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator HelmGuideCheck()
        {
            _report.AppendLine("\n## 타륜 표시(2026-11-22)");
            var ship = GameManager.Instance.Player;
            var guide = ship.GetComponent<Game.View.HelmGuide>();
            if (guide == null) { Fail("기함에 타륜 표시(HelmGuide)가 없음"); yield break; }
            CombatDevTools.ClearBattlefield();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            int style0 = DisplaySettings.HelmGuide, idle0 = DisplaySettings.HelmGuideIdle;

            // 1) 예상 항적 vs 실제: 전속 · 우현 20° 정도로 달리며 5초 앞 예측 끝과 5초 뒤 실제 뱃머리 앞 위치를 잰다
            DisplaySettings.SetHelmGuide(1); DisplaySettings.SetHelmGuideIdle(0);
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(4f);
            ship.DevRudderOverride = 0.57f;   // 약 20°
            yield return new WaitForSeconds(3f);   // 선회율이 자리 잡게
            var path = GameObject.Find("Helm guide/Path")?.GetComponent<LineRenderer>();
            Vector3 predicted = path != null && path.positionCount > 0 ? path.GetPosition(path.positionCount - 1) : Vector3.zero;
            float len = guide.PathLength;
            yield return new WaitForSeconds(Game.View.HelmGuide.Horizon);
            Vector3 actual = ship.transform.position;
            float err = path != null ? Vector3.Distance(Flat(predicted), Flat(actual)) : 999f;
            // 예측 끝은 뱃머리 앞이라 배 가운데보다 반 길이 앞 — 반 길이(최대 15m) 안쪽이면 맞는 것으로 본다
            _report.AppendLine($"- 예상 항적(전속 · 우현 약 20°): 5초 길이 {len:0.0}m · 예측 끝과 5초 뒤 실제 배 가운데 거리 {err:0.0}m(뱃머리 앞 기준이라 반 길이쯤 차이)");
            if (path == null || len < 10f) Fail("달리는데 예상 항적이 그려지지 않음");
            if (err > 16f) Fail("예상 항적 끝이 5초 뒤 실제 위치와 크게 다름");

            // 2) 디자인 5가지 화면
            for (int st = 1; st <= 5; st++)
            {
                DisplaySettings.SetHelmGuide(st);
                ship.DevRudderOverride = st % 2 == 0 ? -0.6f : 0.6f;
                yield return new WaitForSeconds(0.6f);
                _report.AppendLine($"- 디자인 {st} · {DisplaySettings.HelmGuideNames[st]}: 그림 {Yes(guide.Style == st && guide.Alpha > 0.9f)} · 유령선 {guide.GhostsShown}척");
                if (guide.Style != st || guide.Alpha < 0.9f) Fail($"타륜 표시 디자인 {st}가 그려지지 않음");
                if ((st == 4 || st == 5) && guide.GhostsShown != 3) Fail($"디자인 {st}에 1 · 3 · 5초 유령선이 없음");
                yield return CloseShot($"helm_guide_{st}", ship.transform, 34f);
            }

            // 3) 타륜을 놓으면 2초 뒤 옅게 · 숨김
            DisplaySettings.SetHelmGuide(4);
            ship.DevRudderOverride = 0.3f;
            yield return null;
            yield return new WaitForSeconds(Game.View.HelmGuide.ActiveHold + 0.5f);
            float dim = guide.Alpha;
            DisplaySettings.SetHelmGuideIdle(1);
            yield return null;
            float hidden = guide.Alpha;
            DisplaySettings.SetHelmGuide(0);
            yield return null;
            bool off = guide.Alpha <= 0f || !(GameObject.Find("Helm guide/Arrow")?.GetComponent<MeshRenderer>()?.enabled ?? false);
            _report.AppendLine($"- 놓은 뒤 2초: 옅게 {dim:0.00} · 숨김 {hidden:0.00} · 끄기 화살표 숨김 {Yes(off)}");
            if (Mathf.Abs(dim - Game.View.HelmGuide.IdleAlpha) > 0.01f || hidden > 0.001f || !off) Fail("타륜을 놓은 뒤 옅게 · 숨김 · 끄기가 맞지 않음");

            // 4) 설정 › 조함 탭 미리보기
            var hud = Object.FindFirstObjectByType<Game.UI.HUDView>(FindObjectsInactive.Include);
            DisplaySettings.SetHelmGuide(5); DisplaySettings.SetHelmGuideIdle(0);
            hud.OpenPauseMenu();
            yield return null;
            Call(hud, "ShowSettings");
            Call(hud, "ShowSettingsTab", 5);
            yield return new WaitForSecondsRealtime(1.2f);
            bool tab = GameObject.Find("Helm page") != null && hud.HelmPreview != null && hud.HelmPreview.Style == 5;
            _report.AppendLine($"- 설정 › 조함: 탭 · 미리보기 {Yes(tab)}");
            if (!tab) Fail("설정에 조함 탭 · 타륜 표시 미리보기가 없음");
            yield return ScreenShot("helm_settings");
            Call(hud, "CloseSettings");
            hud.ResumeFromPause();

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            DisplaySettings.SetHelmGuide(style0); DisplaySettings.SetHelmGuideIdle(idle0);
            if (field != null) Game.World.IslandField.Disabled = false;
        }

    }
}
#endif
