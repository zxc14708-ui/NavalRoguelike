using System.Collections.Generic;

namespace Game.Combat
{
    /// <summary>
    /// 어뢰 대응 수단(예: 예인식 어뢰 기만기)의 자리. 아직 구현된 모듈은 없다.
    /// 어뢰는 비유도라 미사일 기만체·CIWS로는 막을 수 없고, 소나로 보고 조함으로 피하는 것이 기본 대응이다.
    /// 나중에 기만기 모듈이 Register하면 어뢰가 주기적으로 TryDefeat를 물어 무력화될 수 있다.
    /// </summary>
    public interface ITorpedoCountermeasure
    {
        /// <summary>이 어뢰를 무력화했으면 true(어뢰는 곧바로 사라진다).</summary>
        bool TryDefeat(Torpedo torpedo);
    }

    public static class TorpedoCountermeasures
    {
        private static readonly List<ITorpedoCountermeasure> s_active = new();

        public static int Count => s_active.Count;

        public static void Register(ITorpedoCountermeasure c)
        {
            if (c != null && !s_active.Contains(c)) s_active.Add(c);
        }

        public static void Unregister(ITorpedoCountermeasure c) => s_active.Remove(c);

        public static bool TryDefeat(Torpedo torpedo)
        {
            for (int i = s_active.Count - 1; i >= 0; i--)
                if (s_active[i] != null && s_active[i].TryDefeat(torpedo)) return true;
            return false;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();
    }
}
