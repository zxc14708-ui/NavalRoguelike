using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Combat;

namespace Game.Ship
{
    /// <summary>
    /// 기만체 액티브 스킬. Q(또는 HUD 버튼)를 누르면 준비된 기만체 장비 하나가 발사한다(2026-11-22 — 장비가 여럿이면 사용 횟수로 쌓이고
    /// 하나씩 쓴다. 각 장비는 따로 재장전하므로 하나를 쓴 동안 나머지 횟수는 그대로 남는다).
    ///
    /// 자동 발사는 미사일이 가까워진 뒤에야 반응해 늦었다. 플레이어가 경보를 보고
    /// 직접 타이밍을 잡게 하면 기만이 실력의 영역이 되고, 성능을 올려도 공짜 방어가 되지 않는다.
    /// </summary>
    public class DecoySkill : ActiveSkillBase<IDecoySource>
    {
        [SerializeField] private TargetingSystem targeting;

        private Vector3? _threat;

        protected override void Subscribe() => GameEvents.DecoyRequested += Activate;
        protected override void Unsubscribe() => GameEvents.DecoyRequested -= Activate;

        protected override bool WasKeyPressed()
        {
            if (GameSettings.Pressed(NavalControl.Decoy)) return true;

            var gp = Gamepad.current;
            return gp != null && gp.buttonWest.wasPressedThisFrame;
        }

        /// <summary>가장 가까운 미사일 반대쪽으로 뿌리도록 한 번만 계산한다.</summary>
        protected override void BeforeFire()
        {
            var missile = targeting != null ? targeting.GetNearestMissile(transform.position, float.MaxValue) : null;
            _threat = missile != null ? missile.Transform.position : null;
        }

        /// <summary>기만체를 끈 장비(WWII 함교)는 세지 않는다 — 기만체 칸이 "장비 없음"으로 보인다.</summary>
        protected override bool Provides(IDecoySource source)
            => source is not Game.Modules.Runtime.DecoyLauncherModule d || d.DecoyEnabled;

        protected override bool IsReady(IDecoySource source) => source.IsReady;
        protected override float Readiness01(IDecoySource source) => source.Readiness01;

        protected override void Fire(IDecoySource source) => source.Deploy(_threat);

        protected override void RaiseStatus(int ready, int total, float next01)
            => GameEvents.RaiseDecoyStatusChanged(ready, total, next01);
    }
}
