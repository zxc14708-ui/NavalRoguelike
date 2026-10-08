namespace Game.Modules
{
    /// <summary>모듈의 역할 분류. UI 아이콘/정렬과 드래프트 가중치에 쓰인다.</summary>
    public enum ModuleType
    {
        Bridge,          // 함교 (시작 모듈, 기관 통합)
        Autocannon,      // 기관포 (시작 모듈)
        Radar,           // 탐지거리 / 동시추적 수 증가
        Vls,             // 장거리 유도 미사일 (2x2 Both)
        Ciws,            // 미사일 요격 전용
        DecoyLauncher,   // 기만체
        Magazine,        // 인접 함포 재장전 지원 + 유폭 위험
        Sonar,           // 잠수함 탐지 (함내)
        HelicopterDeck,  // 대잠 헬기 (갑판 후방)
        RepairBay,       // 손상 통제반 — 선체와 모듈을 서서히 수리
        GuidedRocket,    // 유도로켓 — VLS보다 약하지만 빠르게 연사
        AswLauncher,     // 대잠 폭뢰 — 잠항 중인 잠수함도 공격
        NavalGun,        // 76mm 함포 — 중거리 다목적, 고폭 파편
        SamLauncher,     // 함대공 미사일 — 적 미사일을 멀리서 요격
        EwSuite,         // 전자전 장비 — 재밍 스킬(E)

        // --- 예전 데이터용(드래프트 풀에 없음). 값은 직렬화에 쓰이므로 순서를 바꾸지 말고 끝에만 추가한다.
        //     mod_armor는 8(헬기데크), mod_generator는 3(VLS)으로 잘못 저장되어 있었다 → Naval/Migrate Legacy Module Types
        Armor = 15,      // 장갑(폐지 — 손상통제반 방호 거점으로 대체)
        Generator = 16,  // 발전기(폐지)
        FleetRelay = 17,       // 호위함 자율 능력 재사용 속도
        TurboIntake = 18,      // 최고속력·가속·항해 중 실탄 피해
        FireControlArray = 19,// 동시 추적·유도무장 사거리
        MissileLogistics = 20,// VLS·유도로켓 발사 간격·셀 보급
        TorpedoTube = 21,      // 경어뢰 발사관 — 트인 현측으로 부채꼴 3발(대잠 전용)
    }

    /// <summary>
    /// 갑판 위로 솟은 정도. 무기의 사격각을 가리는지 판단하는 데 쓴다.
    /// 낮은 장비는 포 사선 아래에 있어 아무것도 가리지 않는다.
    /// </summary>
    public enum ModuleHeight
    {
        Low,    // 탄약고, 손상통제반, 소나, 기만체, 대잠 폭뢰, 경어뢰 발사관 — 가리지 않음
        Mid,    // 기관포, CIWS, VLS, 유도로켓 — 같은 높이 이하 무기를 가림
        High,   // 함교, 레이더, 헬기데크 — 모든 무기를 가림
    }

    /// <summary>
    /// 탄약 계통. 탄약고는 포탄 계통만 지원한다. 계통을 늘리지 않는 것이 원칙(복잡도 제한).
    /// </summary>
    public enum AmmoFamily
    {
        Gun,       // 기관포, 76mm, CIWS
        Missile,   // VLS, 함대공, 유도로켓
        Asw,       // 폭뢰, 경어뢰, 대잠 헬기
    }

    /// <summary>드래프트 확률 가중에 쓰는 희귀도.</summary>
    public enum Rarity { Common, Uncommon, Rare, Epic }
}
