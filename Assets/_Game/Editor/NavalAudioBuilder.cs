using UnityEditor;
using UnityEngine;
using Game.Data;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 효과음 임포트 설정과 SfxLibrary 에셋을 만든다.
    /// 여기 적힌 음량은 출발점일 뿐이고, 이후 조정은 생성된 SfxLibrary 에셋에서 한다.
    /// </summary>
    public static class NavalAudioBuilder
    {
        public const string SfxDir = Root + "/Audio/SFX";
        public const string LibraryPath = Root + "/Data/Config/SfxLibrary.asset";

        public static SfxLibrary BuildLibrary()
        {
            // 짧은 효과음은 미리 풀어 둬야 첫 발사 때 끊기지 않는다. 긴 루프는 압축 상태로 둔다.
            ConfigureImport("SFX_Autocannon_Shot", AudioClipLoadType.DecompressOnLoad);
            ConfigureImport("SFX_CIWS_Burst", AudioClipLoadType.DecompressOnLoad);
            ConfigureImport("SFX_Missile_Launch", AudioClipLoadType.DecompressOnLoad);
            ConfigureImport("SFX_Explosion_Large", AudioClipLoadType.DecompressOnLoad);
            ConfigureImport("SFX_Hull_Impact", AudioClipLoadType.DecompressOnLoad);
            ConfigureImport("SFX_Missile_Warning", AudioClipLoadType.DecompressOnLoad);
            ConfigureImport("SFX_Sonar_Ping", AudioClipLoadType.CompressedInMemory);
            ConfigureImport("SFX_Engine_Loop", AudioClipLoadType.CompressedInMemory);

            var lib = CreateSO<SfxLibrary>(LibraryPath);
            Configure(lib, so =>
            {
                //                       이름                클립                    음량  음높이  3D   간격   동시
                Entry(so, "autocannonShot", "SFX_Autocannon_Shot", 0.4f,  0.1f,  1f, 0.05f, 6);   // 속사라 한 발은 작게
                Entry(so, "navalGunShot",   "SFX_Autocannon_Shot", 0.95f, 0.04f, 1f, 0.08f, 3);   // 76mm: 코드가 낮은 음으로 튼다
                Entry(so, "ciwsBurst",      "SFX_CIWS_Burst",      0.7f,  0.03f, 1f, 0f,    3);
                Entry(so, "missileLaunch",  "SFX_Missile_Launch",  0.6f,  0.06f, 1f, 0.1f,  4);
                Entry(so, "explosion",      "SFX_Explosion_Large", 0.8f,  0.10f, 1f, 0.08f, 6);
                Entry(so, "hullImpact",     "SFX_Hull_Impact",     0.7f,  0.08f, 0.5f, 0.12f, 3);
                // 소나는 내 배의 장비음이다. 위치와 상관없이 들려야 한다.
                Entry(so, "sonarPing",      "SFX_Sonar_Ping",      0.6f,  0f,    0f, 2.5f,  1);

                Set(so, "missileWarning", Clip("SFX_Missile_Warning"));
                Set(so, "missileWarningVolume", 0.25f);

                Set(so, "engineLoop", Clip("SFX_Engine_Loop"));
                Set(so, "engineVolumeIdle", 0.08f);
                Set(so, "engineVolumeFull", 0.28f);
                Set(so, "enginePitchIdle", 0.8f);
                Set(so, "enginePitchFull", 1.2f);
                Set(so, "engineFullSpeed", 10f);   // ShipConfig.baseMaxSpeed
                Set(so, "engineResponse", 3f);

                Set(so, "minDistance", 20f);
                Set(so, "maxDistance", 90f);
            });

            return lib;
        }

        public static SfxLibrary Load() => AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);

        private static void Entry(SerializedObject so, string field, string clip, float volume,
                                  float pitchJitter, float spatial, float minInterval, int maxVoices)
        {
            Set(so, $"{field}.Clip", Clip(clip));
            Set(so, $"{field}.Volume", volume);
            Set(so, $"{field}.PitchJitter", pitchJitter);
            Set(so, $"{field}.SpatialBlend", spatial);
            Set(so, $"{field}.MinInterval", minInterval);
            Set(so, $"{field}.MaxVoices", maxVoices);
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/{name}.wav");
            if (clip == null) Debug.LogError($"[Setup] 효과음을 찾지 못했습니다: {SfxDir}/{name}.wav");
            return clip;
        }

        /// <summary>설정이 다를 때만 재임포트한다. 매번 재임포트하면 셋업이 느려진다.</summary>
        private static void ConfigureImport(string name, AudioClipLoadType loadType)
        {
            string path = $"{SfxDir}/{name}.wav";
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            {
                Debug.LogError($"[Setup] 오디오 임포터를 찾지 못했습니다: {path}");
                return;
            }

            var s = importer.defaultSampleSettings;
            if (s.loadType == loadType && s.preloadAudioData) return;

            s.loadType = loadType;
            s.preloadAudioData = true;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            importer.defaultSampleSettings = s;
            importer.SaveAndReimport();
        }
    }
}
