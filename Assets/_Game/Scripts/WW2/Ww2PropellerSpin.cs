using UnityEngine;

namespace Game.WW2
{
    /// <summary>
    /// 프로펠러 회전(장식, WWII 항공기 — Codex v30 모델의 Propeller 노드). 기체 앞 방향(월드) 둘레로 돌려
    /// 모델 노드의 로컬 축과 상관없이 바르게 돈다. 시간이 멈추면 멈춘다.
    /// </summary>
    public sealed class Ww2PropellerSpin : MonoBehaviour
    {
        [SerializeField] private Transform propeller;
        [SerializeField] private float rpm = 900f;

        private void Update()
        {
            if (propeller == null) return;
            propeller.Rotate(transform.forward, rpm * 6f * Time.deltaTime, Space.World);
        }
    }
}
