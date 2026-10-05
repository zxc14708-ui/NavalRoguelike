using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 플레이어 함선의 연막. 켜져 있는 동안 적 함정은 조준 사격(포탄·미사일·어뢰 발사)을 멈춘다.
    /// 연기는 월드 공간에 뿜어 배 뒤로 길게 남는다.
    /// </summary>
    public class SmokeScreen : MonoBehaviour
    {
        private static float s_until;

        /// <summary>지금 연막에 가려져 있는가.</summary>
        public static bool IsActive => Time.time < s_until;

        public static SmokeScreen Instance { get; private set; }

        [SerializeField] private ParticleSystem smoke;

        private void Awake()
        {
            Instance = this;
            s_until = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Deploy(float duration)
        {
            s_until = Mathf.Max(s_until, Time.time + duration);
            if (smoke != null && !smoke.isEmitting) smoke.Play(true);
        }

        private void Update()
        {
            if (smoke != null && smoke.isEmitting && !IsActive)
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
