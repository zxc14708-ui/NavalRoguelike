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
        [Tooltip("76mm 포성. 기관포와 같은 클립을 낮게 틀지만, 연사에 묻혀 끊기지 않도록 항목을 따로 둔다")]
        [SerializeField] private SfxEntry navalGunShot = new();

        [Header("Escorts (편대)")]
        [Tooltip("호위함 함포. 기함 기관포와 같은 클립이라도 항목을 따로 둬 기함 연사에 묻히거나 끊기지 않게")]
        [SerializeField] private SfxEntry escortGunShot = new();
        [SerializeField] private SfxEntry escortMissileLaunch = new();
        [SerializeField] private SfxEntry escortTorpedoLaunch = new();
        [SerializeField] private SfxEntry escortJam = new();

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
            SfxId.EscortGunShot => escortGunShot,
            SfxId.EscortMissileLaunch => escortMissileLaunch,
            SfxId.EscortTorpedoLaunch => escortTorpedoLaunch,
            SfxId.EscortJam => escortJam,
            _ => null,
        };
    }
}
