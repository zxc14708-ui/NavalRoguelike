using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Ship
{
    /// <summary>
    /// 응급 수리 [R]. 손상 통제반 하나가 선체와 모든 모듈을 한 번에 크게 고친다.
    /// 효과가 겹치면 낭비라 한 번에 한 장비만 쓴다.
    /// </summary>
    public class RepairBurstSkill : SlotSkillBase<IRepairBurstSource>
    {
        protected override ActiveSkillId Id => ActiveSkillId.Repair;
        protected override bool FireAllSources => false;

        protected override bool WasKeyPressed()
        {
            if (GameSettings.Pressed(NavalControl.Repair)) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonEast.wasPressedThisFrame;
        }

        protected override bool IsReady(IRepairBurstSource s) => s.BurstReady;
        protected override float Readiness01(IRepairBurstSource s) => s.BurstReadiness01;
        protected override void Fire(IRepairBurstSource s) => s.Burst();
    }
}
