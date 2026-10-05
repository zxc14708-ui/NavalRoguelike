using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Ship
{
    /// <summary>
    /// 연막 [F]. 기만체 발사기가 연막을 쳐 잠시 적 함정이 조준 사격을 못 하게 한다.
    /// 이미 날아오는 미사일·어뢰와 자폭 드론은 막지 못한다.
    /// </summary>
    public class SmokeSkill : SlotSkillBase<ISmokeSource>
    {
        protected override ActiveSkillId Id => ActiveSkillId.Smoke;
        protected override bool FireAllSources => false;

        protected override bool WasKeyPressed()
        {
            if (GameSettings.Pressed(NavalControl.Smoke)) return true;
            var gp = Gamepad.current;
            return gp != null && gp.rightShoulder.wasPressedThisFrame;
        }

        protected override bool Provides(ISmokeSource s) => s.CarriesSmoke;
        protected override bool IsReady(ISmokeSource s) => s.SmokeReady;
        protected override float Readiness01(ISmokeSource s) => s.SmokeReadiness01;
        protected override void Fire(ISmokeSource s) => s.DeploySmoke();
    }
}
