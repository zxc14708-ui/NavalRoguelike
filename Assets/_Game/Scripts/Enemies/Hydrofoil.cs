using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 수중익 미사일정(4스테이지, 모티브 사란차 · 마트카급). 미사일정과 같은 행동을 훨씬 빠르게.
    /// 달릴수록 선체(HullPivot)가 수중익 위로 떠오른다(최대 liftHeight) — 수중익 지지대는 물속에 남는다.
    /// </summary>
    public class Hydrofoil : MissileBoat
    {
        [Header("Foilborne")]
        [SerializeField] private Transform hullPivot;
        [SerializeField] private float liftHeight = 1.2f;
        [Tooltip("최고 속력의 이 비율부터 뜨기 시작한다")]
        [SerializeField, Range(0f, 1f)] private float liftFrom = 0.35f;

        private Vector3 _hullRest;
        private bool _restCaptured;
        private float _lift;

        /// <summary>검증용: 지금 선체가 떠오른 높이(m).</summary>
        public float Lift => _lift;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            if (hullPivot != null && !_restCaptured) { _hullRest = hullPivot.localPosition; _restCaptured = true; }
            _lift = 0f;
        }

        protected override void UpdateBehaviour(float dt)
        {
            base.UpdateBehaviour(dt);
            if (hullPivot == null) return;
            float frac = Definition.MoveSpeed > 0.01f ? CurrentSpeed / Definition.MoveSpeed : 0f;
            float want = liftHeight * Mathf.InverseLerp(liftFrom, 0.85f, frac);
            _lift = Mathf.MoveTowards(_lift, want, 1.2f * dt);
            // 부모(모델) 축척·회전과 상관없이 월드 위로 들어 올린다
            hullPivot.localPosition = _hullRest + hullPivot.parent.InverseTransformVector(Vector3.up * _lift);
        }
    }
}
