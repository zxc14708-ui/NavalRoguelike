using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 돌아가는 부품(장식, 2026-11-16): 프로펠러 · 덕트 팬 날개 · 레이더 · 크레인. 모델 노드의 로컬 축과 상관없이
    /// 기체 기준 축(앞 = 프로펠러, 위 = 레이더 · 크레인)으로 돌린다. 시간이 멈추면 멈춘다.
    /// </summary>
    public sealed class PartSpin : MonoBehaviour
    {
        public enum Axis { Forward, Up }

        [SerializeField] private Transform[] parts;
        [SerializeField] private Axis axis = Axis.Forward;
        [SerializeField] private float rpm = 600f;

        public void Configure(Transform[] spinning, Axis around, float turnsPerMinute)
        {
            parts = spinning;
            axis = around;
            rpm = turnsPerMinute;
        }

        private void Update()
        {
            if (parts == null) return;
            Vector3 a = axis == Axis.Forward ? transform.forward : transform.up;
            float deg = rpm * 6f * Time.deltaTime;
            foreach (var p in parts) if (p != null) p.Rotate(a, deg, Space.World);
        }
    }
}
