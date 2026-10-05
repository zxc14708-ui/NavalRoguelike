using UnityEngine.InputSystem;
using Game.Core;
using Game.Combat;

namespace Game.Ship
{
    /// <summary>
    /// 전자전 재밍 액티브 스킬. E(또는 HUD 버튼)를 누르면 준비된 전자전 장비가 주변 적 미사일의 유도를 교란한다.
    /// 기만체(Q)는 미사일을 다른 곳으로 끌어가고, 재밍(E)은 표적 자체를 잃게 한다 — 두 스킬을 번갈아 쓰는 조작이 된다.
    /// </summary>
    public class JammerSkill : ActiveSkillBase<IJammerSource>
    {
        protected override void Subscribe() => GameEvents.JamRequested += Activate;
        protected override void Unsubscribe() => GameEvents.JamRequested -= Activate;

        protected override bool WasKeyPressed()
        {
            if (GameSettings.Pressed(NavalControl.Jam)) return true;

            var gp = Gamepad.current;
            return gp != null && gp.buttonNorth.wasPressedThisFrame;
        }

        protected override bool IsReady(IJammerSource source) => source.IsReady;
        protected override float Readiness01(IJammerSource source) => source.Readiness01;

        protected override void Fire(IJammerSource source) => source.Jam();

        protected override void RaiseStatus(int ready, int total, float next01)
            => GameEvents.RaiseJamStatusChanged(ready, total, next01);
    }
}
