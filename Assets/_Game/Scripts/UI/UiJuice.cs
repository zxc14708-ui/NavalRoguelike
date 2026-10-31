using PrimeTween;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 화면 연출 모음(2026-10-31, PrimeTween 1.5.0 — npm 레지스트리로 설치. 공개 저장소라 PrimeTween 소스는 넣지 않는다).
    ///   - 정비 카드가 새로 나올 때(열기·다시 뽑기) 차례로 튀어나온다.
    ///   - 선체 피해 때 선체 수치가 옆으로 흔들린다.
    ///   - 큰 피해 때 화면이 흔들린다(QuarterViewCamera.Shake, 번쩍임 줄이기 설정을 따른다).
    /// 정비 화면은 시간이 멈추므로 모두 실제 시간(unscaled)으로 돈다. 자동 검증은 UI 위치·크기를 재므로 끄고, 연출 검사만 켠다.
    /// </summary>
    public static class UiJuice
    {
        private static readonly bool s_verify = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-combatVerify") >= 0;

        /// <summary>연출을 쓸지(자동 검증에서는 기본 끔).</summary>
        public static bool Enabled = !s_verify;

        /// <summary>카드 등장: 작게 시작해 살짝 넘치며 제 크기로. order번째일수록 조금 늦게.</summary>
        public static void PopIn(Transform card, int order)
        {
            if (!Enabled || card == null) return;
            Tween.StopAll(card);
            card.localScale = Vector3.one * 0.86f;
            Tween.Scale(card, 0.86f, 1f, 0.24f, Ease.OutBack, startDelay: order * 0.06f, useUnscaledTime: true);
        }

        /// <summary>글자·막대를 옆으로 짧게 흔든다(이미 흔들리는 중이면 겹치지 않는다).</summary>
        public static void Nudge(Transform target, ref Tween handle, float pixels = 6f)
        {
            if (!Enabled || target == null || handle.isAlive) return;
            handle = Tween.ShakeLocalPosition(target, new Vector3(pixels, 0f, 0f), 0.3f, frequency: 22f, useUnscaledTime: true);
        }
    }
}
