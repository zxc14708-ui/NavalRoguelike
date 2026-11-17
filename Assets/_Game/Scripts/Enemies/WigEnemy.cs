using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 수면 비행체(위그선) 공통(2026-11-16, 4스테이지 "내해의 괴물").
    ///   - 날고 있을 때: 항공 표적(TargetKind.Aircraft) + ISkimmer — 기관포 · CIWS · 함대공 · 76mm와 대함 유도탄(VLS · 유도로켓)이 노린다.
    ///     수상 표적 전용 무기(곡사포 · 기뢰 · 충각 · 헬기)는 못 노리고, 레이더에는 평소의 75% 거리에서 잡힌다.
    ///   - 내려앉았을 때(SetLanded): 수상 표적(TargetKind.Surface) — 모든 대함 무기가 통한다.
    /// 종류가 바뀔 때 표적 목록(TargetRegistry)에 다시 등록한다. 모델 원점은 수면선이라 내려앉은 높이는 수면(y −0.9)이다.
    /// </summary>
    public abstract class WigEnemy : AirEnemy, ISkimmer
    {
        /// <summary>내려앉았을 때 루트 높이(모델 원점 = 수면선).</summary>
        public const float WaterY = Game.World.IslandBuilder.SeaY;

        private bool _landed;
        private bool _registered;

        public bool Skimming => !_landed;
        public bool Landed => _landed;
        public override TargetKind Kind => _landed ? TargetKind.Surface : TargetKind.Aircraft;

        public override void OnSpawned()
        {
            _landed = false;
            base.OnSpawned();
            _registered = true;
        }

        public override void OnDespawned()
        {
            base.OnDespawned();   // 지금 종류로 등록을 지운다
            _registered = false;
            _landed = false;
        }

        /// <summary>내려앉음/뜸. 표적 종류가 바뀌므로 목록에 다시 등록한다(이 기체를 노리던 유도탄 배정도 지운다).</summary>
        protected void SetLanded(bool landed)
        {
            if (_landed == landed) return;
            if (_registered) TargetRegistry.Unregister(this);
            _landed = landed;
            if (_registered) TargetRegistry.Register(this);
        }

        /// <summary>앞으로 speed만큼 나아가며 높이를 altitude로 맞춘다(내려앉기 · 뜨기 · 활주). 요격 무기 예측용 속도도 알린다.</summary>
        protected void Glide(Vector3 heading, float speed, float altitude, float climbRate, float turnRate, float dt)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading.normalized, Vector3.up), turnRate * dt);
            Vector3 fwd = transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 p = transform.position + fwd * (speed * dt);
            p.y = Mathf.MoveTowards(p.y, altitude, climbRate * dt);
            transform.position = p;
            SetVelocity(fwd * speed);
        }
    }
}
