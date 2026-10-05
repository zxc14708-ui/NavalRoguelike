using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 특정 오브젝트에 속하지 않는 전역 밸런스 값.
    /// 코드 곳곳에 매직 넘버를 흩뿌리지 않기 위한 단일 장소다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("Damage")]
        [Tooltip("셀에 모듈이 있을 때 모듈이 받는 피해 비율(나머지는 선체로)")]
        [Range(0f, 1f)][SerializeField] private float moduleDamageShare = 0.6f;

        [Header("Refit")]
        [Tooltip("정비 페이즈마다 회복되는 선체 HP")]

        public float ModuleDamageShare => moduleDamageShare;
    }
}
