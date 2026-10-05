using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Ship
{
    /// <summary>전속 [Shift]. 함교가 잠깐 기관 출력을 한계까지 올려 최고속력과 가속을 끌어올린다.</summary>
    public class FlankSkill : SlotSkillBase<IFlankSource>
    {
        protected override ActiveSkillId Id => ActiveSkillId.Flank;
        protected override bool FireAllSources => false;

        protected override bool WasKeyPressed()
        {
            if (GameSettings.Pressed(NavalControl.Flank)) return true;
            var gp = Gamepad.current;
            return gp != null && gp.leftShoulder.wasPressedThisFrame;
        }

        protected override bool IsReady(IFlankSource s) => s.FlankReady;
        protected override float Readiness01(IFlankSource s) => s.FlankReadiness01;
        protected override void Fire(IFlankSource s) => s.Flank();
    }
}
