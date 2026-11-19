using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 반잠수 어뢰정: 거리를 두고 어뢰를 쏘고 빠진다(어뢰정과 같은 공격). (2026-11-19 낮과 밤 — 밤 구간 적)
    /// 물에 거의 잠긴 저피탐 선체(LowProfileHull): 레이더 탐지 거리 40%, 소나 · 조명탄 · 탐조등 · 근접에 드러난다. 폭뢰에도 맞는다.
    /// </summary>
    public class SemiSubTorpedoBoat : TorpedoBoat, ILowObservable, IIlluminable
    {
        private readonly LowProfileHull _hull = new();

        public bool Stealthy => _hull.Stealthy;
        public void MarkDetected(float seconds) => _hull.MarkDetected(seconds);
        public void Illuminate(float seconds) => _hull.MarkDetected(seconds);
        /// <summary>드러나지 않으면 표식도 달리지 않는다.</summary>
        public override bool IsRevealed => !_hull.Stealthy;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _hull.Reset();
            _hull.Shape(this);
            _hull.Repaint(this);
        }

        protected override void UpdateBehaviour(float dt)
        {
            _hull.Tick(this, Player, dt);
            base.UpdateBehaviour(dt);
        }
    }
}
