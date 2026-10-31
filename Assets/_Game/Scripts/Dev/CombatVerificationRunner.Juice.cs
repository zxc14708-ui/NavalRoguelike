using System.Collections;
using UnityEngine;
using Game.Core;
using Game.UI;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 화면 연출(2026-10-31, UiJuice · PrimeTween): 정비 카드 등장(작게 → 제 크기), 선체 피해 때 선체 수치 흔들림·화면 흔들림,
        /// 흔들림이 끝난 뒤 카메라·글자가 제자리인지. 자동 검증은 연출을 꺼 두므로 이 검사에서만 켠다.
        /// </summary>
        private IEnumerator JuiceCheck()
        {
            _report.AppendLine("\n## 화면 연출(PrimeTween)");
            bool was = UiJuice.Enabled;
            UiJuice.Enabled = true;
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            yield return new WaitForSeconds(2f);

            // 1) 정비 카드 등장
            var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
            var ui = Object.FindFirstObjectByType<RefitUI>(FindObjectsInactive.Include);
            float firstScale = -1f, settled = -1f;
            if (refit != null && ui != null)
            {
                refit.BeginLevelUp(2, () => GameManager.Instance.SetState(GameState.Playing));
                // 여는 순간(같은 프레임) 크기 — 정비 화면을 여는 프레임이 길어 다음 프레임에는 이미 거의 끝나 있다
                var cards = Get<RectTransform[]>(ui, "cardRects");
                var card = cards != null && cards.Length > 1 ? cards[1] : null;
                if (card != null) firstScale = card.localScale.x;
                yield return new WaitForSecondsRealtime(0.6f);
                if (card != null) settled = card.localScale.x;
                yield return ScreenShot("juice_cards");
                Call(refit, "Resume");
                yield return null;
            }
            _report.AppendLine($"- 정비 카드 등장: 열자마자 둘째 카드 크기 {firstScale:0.00} → 0.6초 뒤 {settled:0.00}");
            if (firstScale < 0f) Fail("정비 카드를 열지 못함");
            else if (firstScale > 0.97f || Mathf.Abs(settled - 1f) > 0.01f) Fail("정비 카드 등장 연출이 없거나 제 크기로 돌아오지 않음");

            // 2) 선체 피해 → 선체 수치 흔들림 · 화면 흔들림
            var panel = Object.FindFirstObjectByType<ShipStatusPanelUI>();
            var hullText = panel != null ? Get<TMPro.TMP_Text>(panel, "_hullValue") : null;
            var view = Camera.main != null ? Camera.main.GetComponent<Game.View.QuarterViewCamera>() : null;
            yield return new WaitForSecondsRealtime(0.5f);
            Vector3 textHome = hullText != null ? hullText.transform.localPosition : Vector3.zero;
            // 배가 미끄러지며 카메라가 따라가므로 배 기준 상대 위치로 잰다
            Vector3 CamRel() => view != null ? view.transform.position - ship.transform.position : Vector3.zero;
            Vector3 camHome = CamRel();
            ship.ApplyHullDamage(ship.HullMaxHp * 0.1f, "검증");
            float textMoved = 0f, camMoved = 0f, shakePeak = 0f;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                if (hullText != null) textMoved = Mathf.Max(textMoved, (hullText.transform.localPosition - textHome).magnitude);
                if (view != null) { camMoved = Mathf.Max(camMoved, (CamRel() - camHome).magnitude); shakePeak = Mathf.Max(shakePeak, view.ShakeAmount); }
            }
            yield return new WaitForSecondsRealtime(0.8f);
            float textBack = hullText != null ? (hullText.transform.localPosition - textHome).magnitude : -1f;
            // 배가 감속하며 추적 간격이 바뀌므로 위치가 아니라 흔들림 오프셋 자체가 0으로 돌아왔는지 본다
            float camBack = view != null ? Get<Vector3>(view, "_shakeApplied").magnitude + view.ShakeAmount : -1f;
            _report.AppendLine($"- 선체 10% 피해: 선체 수치 흔들림 최대 {textMoved:0.0}px → 끝난 뒤 {textBack:0.00}px · 화면 흔들림 세기 {shakePeak:0.00} · 카메라 최대 {camMoved:0.00}m(배 기준) → 끝난 뒤 남은 흔들림 {camBack:0.000}");
            if (textMoved < 1f || textBack > 0.05f) Fail("선체 수치 흔들림이 없거나 제자리로 돌아오지 않음");
            if (shakePeak < 0.2f || camMoved < 0.05f) Fail("큰 선체 피해에 화면 흔들림이 없음");
            if (camBack > 0.001f) Fail("화면 흔들림이 끝난 뒤 카메라가 제자리로 돌아오지 않음");
            ship.RepairHull(ship.HullMaxHp);

            UiJuice.Enabled = was;
        }
    }
}
