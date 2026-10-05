namespace Game.Ship
{
    // 액티브 스킬을 제공하는 장비의 약속. 한 모듈이 여러 스킬을 가질 수 있도록(함교 = 기만체 + 전속)
    // 스킬마다 준비 상태 이름을 따로 둔다.

    /// <summary>연막 [F]. 기만체 발사기(전용 장비만)가 연막탄을 쏜다.</summary>
    public interface ISmokeSource
    {
        bool CarriesSmoke { get; }
        bool SmokeReady { get; }
        float SmokeReadiness01 { get; }
        void DeploySmoke();
    }

    /// <summary>응급 수리 [R]. 손상 통제반이 선체와 모듈을 한 번에 크게 고친다.</summary>
    public interface IRepairBurstSource
    {
        bool BurstReady { get; }
        float BurstReadiness01 { get; }
        void Burst();
    }

    /// <summary>전속 [Shift]. 함교(기관실)가 잠깐 기관 출력을 한계까지 올린다.</summary>
    public interface IFlankSource
    {
        bool FlankReady { get; }
        float FlankReadiness01 { get; }
        void Flank();
    }
}
