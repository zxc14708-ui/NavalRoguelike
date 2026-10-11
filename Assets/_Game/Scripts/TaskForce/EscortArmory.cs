using UnityEngine;

namespace Game.TaskForce
{
    /// <summary>
    /// 호위함 실제 무장이 쓰는 탄·미사일·어뢰·헬기 프리팹(2026-10-11). Resources/TaskForce/EscortArmory 하나.
    /// 플레이어 함선 블록과 같은 프리팹을 쓴다(아군 진영 탄 — 호위함·기함을 맞히지 않는다).
    /// 메뉴 Naval/Task Force/Build Escort Armory가 만든다. 없으면 호위함은 예전처럼 즉시 판정으로 싸운다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Escort Armory", fileName = "EscortArmory")]
    public sealed class EscortArmory : ScriptableObject
    {
        [Tooltip("주포·후방포·현측포(76mm 탄)")] public GameObject heavyRound;
        [Tooltip("원격 기관총·현측 기관총(기관포 탄)")] public GameObject lightRound;
        [Tooltip("CIWS 탄")] public GameObject ciwsRound;
        [Tooltip("함대공 요격 미사일(VLS·SAM 발사기)")] public GameObject samMissile;
        [Tooltip("대함 미사일(미사일 발사관)")] public GameObject strikeMissile;
        [Tooltip("경어뢰(어뢰관·대잠 로켓 탄두)")] public GameObject aswTorpedo;
        [Tooltip("대잠 로켓(대잠 로켓 발사기, 탄두 = 경어뢰)")] public GameObject aswRocket;
        [Tooltip("대잠 헬기(헬기 갑판)")] public GameObject helicopter;
        [Tooltip("개량 단계별 대잠 헬기(0 = T1: 호위함 T0·T1, 1 = T2, 2 = T3). Codex v19 — 비어 있으면 helicopter")]
        public GameObject[] helicoptersByTier;

        /// <summary>호위함 개량 단계(0~3)에 맞는 헬기. T0·T1 → 헬기 T1, T2 → T2, T3 → T3.</summary>
        public GameObject HelicopterFor(int escortTier)
        {
            if (helicoptersByTier != null && helicoptersByTier.Length > 0)
                for (int i = Mathf.Clamp(escortTier - 1, 0, helicoptersByTier.Length - 1); i >= 0; i--)
                    if (helicoptersByTier[i] != null) return helicoptersByTier[i];
            return helicopter;
        }

        private static EscortArmory s_instance;
        private static bool s_loaded;

        public static EscortArmory Instance
        {
            get
            {
                if (!s_loaded) { s_loaded = true; s_instance = Resources.Load<EscortArmory>("TaskForce/EscortArmory"); }
                return s_instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { s_loaded = false; s_instance = null; }
    }
}
