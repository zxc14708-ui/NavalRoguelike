namespace Game.Enemies
{
    /// <summary>
    /// 보스 학습(2026-10-11 점검 K05): 보스가 지금 단계와 다음 공격을 알려 준다. 화면 위 보스 막대(BossBarUI)가 읽는다.
    /// 공격 자체는 바꾸지 않는다 — 원래 있던 타이머를 그대로 보여 준다.
    /// </summary>
    public interface IBossTelegraph
    {
        /// <summary>지금 단계 이름(예: "원거리 — 미사일").</summary>
        string PhaseName { get; }
        /// <summary>단계가 바뀌는 체력 비율(막대 눈금). 높은 것부터.</summary>
        float[] PhaseThresholds { get; }
        /// <summary>다음 공격과 남은 초. 지금 거리에서 쓸 공격이 없으면 이름 null.</summary>
        (string name, float seconds) NextAttack { get; }
    }
}
