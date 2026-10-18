namespace Game.Modules
{
    /// <summary>
    /// 포탑 마운트 등급(WWII 패치 C2, 2026-10-18 — 패치 A 5장 배치 규칙 1 "주포는 함종이 허용하는 마운트 등급과 탑재 예산을 모두 만족").
    /// 함종(HullProfile.MaxMountClass)이 허용하는 등급까지만 설치된다. 값은 저장 순번 — 바꾸지 말고 끝에만 덧붙인다.
    /// </summary>
    public enum MountClass
    {
        None = 0,     // 포탑이 아님(함교·어뢰·지원 장비, 현대 블록) — 제한을 받지 않는다
        Light = 1,    // 경포탑: 대공포·기관포
        Medium = 2,   // 중포탑: 양용포
        Heavy = 3,    // 대구경 주포 — 전함
    }

    public static class MountClasses
    {
        public static string Name(MountClass c) => c switch
        {
            MountClass.Light => "경포탑",
            MountClass.Medium => "중포탑",
            MountClass.Heavy => "대구경 주포탑",
            _ => "포탑 아님",
        };
    }
}
