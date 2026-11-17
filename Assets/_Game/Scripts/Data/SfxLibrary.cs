using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>게임 코드가 부르는 효과음 이름. 실제 클립과 음량은 SfxLibrary가 정한다.</summary>
    public enum SfxId
    {
        AutocannonShot,
        CiwsBurst,
        MissileLaunch,
        Explosion,
        HullImpact,
        SonarPing,
        NavalGunShot,
        EscortGunShot,        // 호위함 함포(기함 기관포와 보이스를 나눠 써 묻히지 않게)
        EscortMissileLaunch,  // 호위함 함대공·대함 미사일·유도로켓
        EscortTorpedoLaunch,  // 호위함 경어뢰 발사
        EscortJam,            // 호위함 근접 교란
        TwinGunShot,          // 40mm 쌍열포(노봉) 한 발(2026-11-15 전용 소리)
        HullHitGun,           // 기함 피격(2026-11-15): 기관포·기총탄(작은 피해)
        HullHitShell,         // 기함 피격: 포탄(큰 피해) · 유폭
        HullHitMissile,       // 기함 피격: 대함미사일 · 자폭 드론 · 자폭 보트
        HullHitTorpedo,       // 기함 피격: 어뢰 · 기뢰
        HullHitCollision,     // 기함 피격: 충돌 · 좌초
        EnemyGunShot,         // 적 포탑 사격(2026-11-16 전용 소리 — 예전엔 76mm 클립을 음높이만 바꿔 썼다)
        MissileHit,           // 미사일이 함체·표적에 명중(적·아군 공통)
        MissileBurst,         // 미사일 공중 폭파(요격·격추)
        ShellImpact,          // 포탄이 적 선체에 명중
        TorpedoLaunch,   // 어뢰 발사(압축 공기 + 입수)
        UnderwaterBlast,   // 수중 폭발(어뢰 명중 · 폭뢰 · 기뢰)
        SinkCapsize,   // 적 격침: 침수 전복(물 · 공기 방울 · 선체 신음)
        SinkGroan,   // 적 격침: 선체 찢어짐
        WreckBlast,   // 적 격침: 유폭 · 연쇄 폭발
        EnemyAutocannonShot,   // 적 기관포 · 기총 한 발
        JetPass,   // 제트기 공격 진입
        PropPass,   // 프로펠러기 등장(WWII)
        DiveSiren,   // 급강하 폭격기 급강하
        DroneBuzz,   // 자폭 드론 돌입
        ShellSplash,   // 빗나간 포탄의 수면 착탄 · 폭뢰 입수
        DepthDrop,   // 폭뢰 · 기뢰 투하
        ChaffLaunch,   // 기만체 · 연막 발사
        JamPulse,   // 전자전 교란
    }

    /// <summary>효과음 하나의 재생 규칙.</summary>
    [Serializable]
    public class SfxEntry
    {
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;

        [Tooltip("재생마다 음높이를 이만큼 무작위로 흔든다. 같은 소리 반복이 기계적으로 들리지 않게.")]
        [Range(0f, 0.5f)] public float PitchJitter = 0.05f;

        [Tooltip("0 = 화면 소리(2D), 1 = 위치 소리(3D)")]
        [Range(0f, 1f)] public float SpatialBlend = 1f;

        [Tooltip("같은 소리를 이 간격(초)보다 자주 틀지 않는다")]
        [Min(0f)] public float MinInterval;

        [Tooltip("동시에 울릴 수 있는 최대 개수. 넘으면 가장 오래된 것을 끊는다.")]
        [Min(1)] public int MaxVoices = 4;
    }

    /// <summary>
    /// 효과음 클립과 믹스 수치. 코드는 SfxId만 알고, 소리 교체와 음량 조정은 이 에셋에서만 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Sfx Library")]
    public class SfxLibrary : ScriptableObject
    {
        [Header("One-shots")]
        [SerializeField] private SfxEntry autocannonShot = new();
        [SerializeField] private SfxEntry ciwsBurst = new();
        [SerializeField] private SfxEntry missileLaunch = new();
        [SerializeField] private SfxEntry explosion = new();
        [SerializeField] private SfxEntry hullImpact = new();
        [SerializeField] private SfxEntry sonarPing = new();
        [Tooltip("76mm 포성(2026-11-15 전용 클립 — 예전엔 기관포 클립을 낮게 틀었다). 곡사포·적 함포도 음높이를 바꿔 쓴다")]
        [SerializeField] private SfxEntry navalGunShot = new();
        [Tooltip("40mm 쌍열포(노봉) 한 발(2026-11-15 전용 클립)")]
        [SerializeField] private SfxEntry twinGunShot = new();

        [Header("Combat (2026-11-16 합성 — ArtSource/Audio/CombatSfx_v1/synth.py)")]
        [Tooltip("적 포탑 사격. 적마다 코드가 음높이를 바꿔 쓴다")]
        [SerializeField] private SfxEntry enemyGunShot = new();
        [Tooltip("미사일이 함체·표적에 명중(적·아군 공통)")]
        [SerializeField] private SfxEntry missileHit = new();
        [Tooltip("미사일 공중 폭파(요격·격추)")]
        [SerializeField] private SfxEntry missileBurst = new();
        [Tooltip("포탄이 적 선체에 명중. 작은 탄은 코드가 작고 높게 튼다")]
        [SerializeField] private SfxEntry shellImpact = new();

        [Header("Combat 2 (2026-11-16 합성 — ArtSource/Audio/CombatSfx_v2/synth.py)")]
        [Tooltip("어뢰 발사(압축 공기 + 입수)")]
        [SerializeField] private SfxEntry torpedoLaunch = new();
        [Tooltip("수중 폭발(어뢰 명중 · 폭뢰 · 기뢰)")]
        [SerializeField] private SfxEntry underwaterBlast = new();
        [Tooltip("적 격침: 침수 전복(물 · 공기 방울 · 선체 신음)")]
        [SerializeField] private SfxEntry sinkCapsize = new();
        [Tooltip("적 격침: 선체 찢어짐")]
        [SerializeField] private SfxEntry sinkGroan = new();
        [Tooltip("적 격침: 유폭 · 연쇄 폭발")]
        [SerializeField] private SfxEntry wreckBlast = new();
        [Tooltip("적 기관포 · 기총 한 발")]
        [SerializeField] private SfxEntry enemyAutocannonShot = new();
        [Tooltip("제트기 공격 진입")]
        [SerializeField] private SfxEntry jetPass = new();
        [Tooltip("프로펠러기 등장(WWII)")]
        [SerializeField] private SfxEntry propPass = new();
        [Tooltip("급강하 폭격기 급강하")]
        [SerializeField] private SfxEntry diveSiren = new();
        [Tooltip("자폭 드론 돌입")]
        [SerializeField] private SfxEntry droneBuzz = new();
        [Tooltip("빗나간 포탄의 수면 착탄 · 폭뢰 입수")]
        [SerializeField] private SfxEntry shellSplash = new();
        [Tooltip("폭뢰 · 기뢰 투하")]
        [SerializeField] private SfxEntry depthDrop = new();
        [Tooltip("기만체 · 연막 발사")]
        [SerializeField] private SfxEntry chaffLaunch = new();
        [Tooltip("전자전 교란")]
        [SerializeField] private SfxEntry jamPulse = new();

        [Header("Flagship hits (기함 피격, 2026-11-15 무기 종류별)")]
        [SerializeField] private SfxEntry hullHitGun = new();
        [SerializeField] private SfxEntry hullHitShell = new();
        [SerializeField] private SfxEntry hullHitMissile = new();
        [SerializeField] private SfxEntry hullHitTorpedo = new();
        [SerializeField] private SfxEntry hullHitCollision = new();

        [Header("Escorts (편대)")]
        [Tooltip("호위함 함포. 기함 기관포와 같은 클립이라도 항목을 따로 둬 기함 연사에 묻히거나 끊기지 않게")]
        [SerializeField] private SfxEntry escortGunShot = new();
        [SerializeField] private SfxEntry escortMissileLaunch = new();
        [SerializeField] private SfxEntry escortTorpedoLaunch = new();
        [SerializeField] private SfxEntry escortJam = new();

        [Header("Weapon fire gain (무기 발사음 배율, 2026-11-17)")]
        [Tooltip("무기 발사음(기함 · 편대 · 적) 음량 배율. AudioSource 음량은 1이 한계라 넘는 몫은 같은 소리를 보이스 하나 더로 겹쳐 낸다(최대 2배)")]
        [SerializeField, Range(1f, 2f)] private float weaponFireGain = 2f;

        [Header("Missile Warning (2D loop)")]
        [SerializeField] private AudioClip missileWarning;
        [SerializeField, Range(0f, 1f)] private float missileWarningVolume = 0.3f;

        [Header("Engine (2D loop)")]
        [SerializeField] private AudioClip engineLoop;
        [SerializeField, Range(0f, 1f)] private float engineVolumeIdle = 0.08f;
        [SerializeField, Range(0f, 1f)] private float engineVolumeFull = 0.3f;
        [SerializeField] private float enginePitchIdle = 0.8f;
        [SerializeField] private float enginePitchFull = 1.2f;
        [Tooltip("이 속력에서 최대 음량/음높이")]
        [SerializeField, Min(0.1f)] private float engineFullSpeed = 10f;
        [Tooltip("속력 변화를 따라가는 빠르기")]
        [SerializeField, Min(0.1f)] private float engineResponse = 3f;

        [Header("Ambience · Music (2D loops, 2026-11-16)")]
        [Tooltip("바다 환경음(파도 · 바람). 출항 중 늘 깔린다")]
        [SerializeField] private AudioClip seaAmbience;
        [SerializeField, Range(0f, 1f)] private float seaAmbienceVolume = 0.35f;
        [SerializeField] private AudioClip battleMusic;
        [Tooltip("보스가 살아 있는 동안 전투 음악 대신 튼다")]
        [SerializeField] private AudioClip bossMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;

        [Header("3D Distance")]
        [Tooltip("카메라가 함선에서 약 35m 떨어져 있으므로 가까운 전투음이 줄지 않게 넉넉히 잡는다")]
        [SerializeField, Min(0f)] private float minDistance = 20f;
        [SerializeField, Min(1f)] private float maxDistance = 90f;

        public AudioClip MissileWarning => missileWarning;
        public float MissileWarningVolume => missileWarningVolume;

        public AudioClip EngineLoop => engineLoop;
        public float EngineVolumeIdle => engineVolumeIdle;
        public float EngineVolumeFull => engineVolumeFull;
        public float EnginePitchIdle => enginePitchIdle;
        public float EnginePitchFull => enginePitchFull;
        public float EngineFullSpeed => engineFullSpeed;
        public float EngineResponse => engineResponse;

        public AudioClip SeaAmbience => seaAmbience;
        public float SeaAmbienceVolume => seaAmbienceVolume;
        public AudioClip BattleMusic => battleMusic;
        public AudioClip BossMusic => bossMusic;
        public float MusicVolume => musicVolume;

        public float WeaponFireGain => weaponFireGain;

        /// <summary>무기 발사음인가(발사 · 연사 · 미사일/어뢰 발사 · 기만체 발사). 명중 · 폭발 · 피격음은 아니다.</summary>
        public static bool IsWeaponFire(SfxId id) => id is SfxId.AutocannonShot or SfxId.CiwsBurst or SfxId.MissileLaunch
            or SfxId.NavalGunShot or SfxId.TwinGunShot or SfxId.EscortGunShot or SfxId.EscortMissileLaunch
            or SfxId.EscortTorpedoLaunch or SfxId.EnemyGunShot or SfxId.TorpedoLaunch or SfxId.EnemyAutocannonShot
            or SfxId.ChaffLaunch;

        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        public SfxEntry Get(SfxId id) => id switch
        {
            SfxId.AutocannonShot => autocannonShot,
            SfxId.CiwsBurst => ciwsBurst,
            SfxId.MissileLaunch => missileLaunch,
            SfxId.Explosion => explosion,
            SfxId.HullImpact => hullImpact,
            SfxId.SonarPing => sonarPing,
            SfxId.NavalGunShot => navalGunShot,
            SfxId.TwinGunShot => twinGunShot,
            SfxId.EnemyGunShot => enemyGunShot,
            SfxId.MissileHit => missileHit,
            SfxId.MissileBurst => missileBurst,
            SfxId.ShellImpact => shellImpact,
            SfxId.TorpedoLaunch => torpedoLaunch,
            SfxId.UnderwaterBlast => underwaterBlast,
            SfxId.SinkCapsize => sinkCapsize,
            SfxId.SinkGroan => sinkGroan,
            SfxId.WreckBlast => wreckBlast,
            SfxId.EnemyAutocannonShot => enemyAutocannonShot,
            SfxId.JetPass => jetPass,
            SfxId.PropPass => propPass,
            SfxId.DiveSiren => diveSiren,
            SfxId.DroneBuzz => droneBuzz,
            SfxId.ShellSplash => shellSplash,
            SfxId.DepthDrop => depthDrop,
            SfxId.ChaffLaunch => chaffLaunch,
            SfxId.JamPulse => jamPulse,
            SfxId.HullHitGun => hullHitGun,
            SfxId.HullHitShell => hullHitShell,
            SfxId.HullHitMissile => hullHitMissile,
            SfxId.HullHitTorpedo => hullHitTorpedo,
            SfxId.HullHitCollision => hullHitCollision,
            SfxId.EscortGunShot => escortGunShot,
            SfxId.EscortMissileLaunch => escortMissileLaunch,
            SfxId.EscortTorpedoLaunch => escortTorpedoLaunch,
            SfxId.EscortJam => escortJam,
            _ => null,
        };
    }
}
