namespace Game.Combat
{
    /// <summary>
    /// 전투 진영. 표적 조회·피해·격침 보상이 "누가 누구의 적인가"를 여기서 판단한다.
    ///   Player  — 기함, 아군 호위함, 아군 기만체·유도탄
    ///   Hostile — 적 함정·항공기·잠수함, 적 미사일·어뢰
    ///   Neutral — 누구의 표적도 아님(예약)
    /// 물리 충돌은 레이어로도 걸러진다(아군 몸체 = PlayerShip 6, 적 몸체 = Enemy 7). 새 아군 유닛은 PlayerShip 레이어에 둔다.
    /// </summary>
    public enum CombatFaction { Player, Hostile, Neutral }

    /// <summary>진영을 가진 것(표적, 피해를 받는 것, 탄).</summary>
    public interface IFactionMember
    {
        CombatFaction Faction { get; }
    }

    /// <summary>
    /// 적이 표적을 고를 때의 우선도(기본 1 = 기함). 적은 "거리 ÷ 우선도"가 가장 작은 대상을 노린다 —
    /// 1보다 작으면 그만큼 더 가까워야 기함 대신 노린다.
    /// </summary>
    public interface ITargetPriority
    {
        float TargetPriority { get; }
        /// <summary>동시에 이것을 노릴 수 있는 적 수(0 이하 = 제한 없음). 한 척에 적이 몰려 순식간에 잃지 않게.</summary>
        int MaxAttackers { get; }
    }

    public static class Factions
    {
        public const int PlayerShipLayer = 6, EnemyLayer = 7, EnemyMissileLayer = 8, PlayerMissileLayer = 9;

        /// <summary>서로 적인가(중립은 누구와도 적이 아니다).</summary>
        public static bool AreHostile(CombatFaction a, CombatFaction b)
            => a != b && a != CombatFaction.Neutral && b != CombatFaction.Neutral;

        /// <summary>이 진영의 적 진영(두 진영뿐). 중립이면 중립(= 적 없음).</summary>
        public static CombatFaction Opposing(CombatFaction f) => f switch
        {
            CombatFaction.Player => CombatFaction.Hostile,
            CombatFaction.Hostile => CombatFaction.Player,
            _ => CombatFaction.Neutral,
        };

        /// <summary>피해를 줄 수 있는가: 진영이 없는 대상은 기존처럼 맞고, 같은 진영(아군)은 맞지 않는다.</summary>
        public static bool CanDamage(CombatFaction attacker, object target)
            => target is not IFactionMember member || member.Faction != attacker;

        /// <summary>격침·격추가 경험치·격침 수·보상을 주는가: 플레이어의 적(적대 진영)일 때만.</summary>
        public static bool GrantsReward(IFactionMember victim)
            => victim != null && victim.Faction == CombatFaction.Hostile;

        /// <summary>
        /// 탄의 소유 진영을 맞히는 레이어로 추정한다(프리팹 데이터를 바꾸지 않고).
        /// 적 몸체·적 미사일을 맞히는 탄 = 아군 탄, 기함·아군 미사일을 맞히는 탄 = 적 탄.
        /// </summary>
        public static CombatFaction OwnerFromHitMask(int mask)
        {
            bool hitsHostile = (mask & ((1 << EnemyLayer) | (1 << EnemyMissileLayer))) != 0;
            bool hitsPlayer = (mask & ((1 << PlayerShipLayer) | (1 << PlayerMissileLayer))) != 0;
            if (hitsHostile && !hitsPlayer) return CombatFaction.Player;
            if (hitsPlayer && !hitsHostile) return CombatFaction.Hostile;
            return CombatFaction.Neutral;   // 알 수 없음 → 진영 검사 없이 기존 동작
        }
    }
}
