using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 항공 위협의 공통 뼈대. 함정과 달리 고도를 갖고 3차원으로 난다.
    ///
    /// 표적 분류가 Aircraft라 레이더에 잡히고, 기관포·76mm·CIWS·함대공 미사일이 쏠 수 있다.
    /// VLS·유도로켓·대잠 무기는 노리지 않는다 — 대공 모듈이 존재 이유를 갖게 한다.
    /// </summary>
    public abstract class AirEnemy : EnemyController, IHasVelocity
    {
        [Header("Flight")]
        [Tooltip("평소 비행 고도(m)")]
        [SerializeField] protected float cruiseAltitude = 7f;

        private Vector3 _velocity;

        public override TargetKind Kind => TargetKind.Aircraft;

        /// <summary>요격 무기의 예측 사격용.</summary>
        public Vector3 Velocity => _velocity;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);

            // 스폰 링은 수면 높이다. 이미 순항 고도로 날아오던 것처럼 올린다.
            var p = transform.position;
            p.y = cruiseAltitude;
            transform.position = p;
            _velocity = transform.forward * (def != null ? def.MoveSpeed : 0f);
        }

        /// <summary>
        /// 목표 지점을 향해 기수를 돌리며 전진한다. terminal이면 남은 거리 안에 돌아설 수 있을 만큼 선회를 올려
        /// 표적 주위를 맴돌지 않게 한다(미사일과 같은 규칙).
        /// </summary>
        protected void Fly(Vector3 point, float speed, float turnRate, bool terminal, float dt)
        {
            Vector3 to = point - transform.position;
            float dist = to.magnitude;

            if (dist > 0.001f)
            {
                float rate = turnRate;
                if (terminal)
                {
                    float needed = Vector3.Angle(transform.forward, to) * speed / Mathf.Max(dist, 0.5f) * 2f;
                    rate = Mathf.Min(Mathf.Max(rate, needed), 1080f);
                }
                var want = Quaternion.LookRotation(to / dist, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * dt);
            }

            _velocity = transform.forward * speed;
            transform.position += _velocity * dt;
        }
    }
}
