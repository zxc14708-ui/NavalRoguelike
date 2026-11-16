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
            ConfigureImport("SFX_NavalGun76_Shot", AudioClipLoadType.DecompressOnLoad);   // 2026-11-15 전용 포성
            ConfigureImport("SFX_TwinGun40_Shot", AudioClipLoadType.DecompressOnLoad);
            foreach (var hit in new[] { "SFX_HullHit_Gun", "SFX_HullHit_Shell", "SFX_HullHit_Missile", "SFX_HullHit_Torpedo", "SFX_HullHit_Collision" })
                ConfigureImport(hit, AudioClipLoadType.DecompressOnLoad);   // 2026-11-15 기함 피격음(ArtSource/Audio/HullHitSfx_v1/synth.py)
            ConfigureImport("SFX_Missile_Launch", AudioClipLoadType.DecompressOnLoad);
            foreach (var combat in new[] { "SFX_EnemyGun_Shot", "SFX_Missile_Hit", "SFX_Missile_Burst", "SFX_Shell_Impact" })
                ConfigureImport(combat, AudioClipLoadType.DecompressOnLoad);   // 2026-11-16 전투 효과음(ArtSource/Audio/CombatSfx_v1/synth.py)
            foreach (var combat2 in new[] { "SFX_Torpedo_Launch", "SFX_Underwater_Blast", "SFX_Sink_Capsize", "SFX_Sink_Groan", "SFX_Wreck_Blast", "SFX_EnemyAutocannon_Shot", "SFX_Jet_Pass", "SFX_Prop_Pass", "SFX_Dive_Siren", "SFX_Drone_Buzz", "SFX_Shell_Splash", "SFX_Depth_Drop", "SFX_Chaff_Launch", "SFX_Jam_Pulse" })
                ConfigureImport(combat2, AudioClipLoadType.DecompressOnLoad);   // 2026-11-16 전투 효과음 2차(ArtSource/Audio/CombatSfx_v2/synth.py)
            foreach (var loop in new[] { "SFX_Sea_Ambience", "BGM_Battle", "BGM_Boss" })
                ConfigureImport(loop, AudioClipLoadType.CompressedInMemory);
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
                Entry(so, "navalGunShot",   "SFX_NavalGun76_Shot", 0.8f,  0.04f, 1f, 0.08f, 4);   // 76mm 전용(곡사포·적 함포는 코드가 음높이를 바꿔 쓴다)
                // 기함 피격(무기 종류별) — 3D 절반: 기함 소리라 멀리서도 들리게
                Entry(so, "hullHitGun",       "SFX_HullHit_Gun",       0.55f, 0.12f, 0.5f, 0.06f, 4);
                Entry(so, "hullHitShell",     "SFX_HullHit_Shell",     0.8f,  0.06f, 0.5f, 0.1f,  3);
                Entry(so, "hullHitMissile",   "SFX_HullHit_Missile",   0.75f, 0.05f, 0.5f, 0.12f, 3);
                Entry(so, "hullHitTorpedo",   "SFX_HullHit_Torpedo",   0.85f, 0.04f, 0.5f, 0.2f,  2);
                Entry(so, "hullHitCollision", "SFX_HullHit_Collision", 0.7f,  0.06f, 0.5f, 0.25f, 2);
                Entry(so, "twinGunShot",    "SFX_TwinGun40_Shot",  0.2f,  0.06f, 1f, 0.05f, 5);   // 40mm 쌍열포(노봉) — 저음 보강판이 5.5dB 커서 음량 절반
                // 전투 효과음(2026-11-16 합성): 적 포탑 사격 · 미사일 명중 · 미사일 공중 폭파 · 포탄 명중
                Entry(so, "enemyGunShot",  "SFX_EnemyGun_Shot",   0.8f,  0.05f, 1f, 0.08f, 4);
                Entry(so, "missileHit",    "SFX_Missile_Hit",     0.85f, 0.06f, 1f, 0.1f,  4);
                Entry(so, "missileBurst",  "SFX_Missile_Burst",   0.7f,  0.08f, 1f, 0.08f, 4);
                Entry(so, "shellImpact",   "SFX_Shell_Impact",    0.6f,  0.1f,  1f, 0.07f, 4);
                // 전투 효과음 2차(2026-11-16 합성)
                Entry(so, "torpedoLaunch", "SFX_Torpedo_Launch", 0.7f, 0.05f, 1f, 0.15f, 3);   // 어뢰 발사(압축 공기 + 입수)
                Entry(so, "underwaterBlast", "SFX_Underwater_Blast", 0.85f, 0.08f, 1f, 0.1f, 4);   // 수중 폭발(어뢰 명중 · 폭뢰 · 기뢰)
                Entry(so, "sinkCapsize", "SFX_Sink_Capsize", 0.6f, 0.05f, 1f, 0.8f, 2);   // 적 격침: 침수 전복(물 · 공기 방울 · 선체 신음)
                Entry(so, "sinkGroan", "SFX_Sink_Groan", 0.6f, 0.08f, 1f, 0.6f, 2);   // 적 격침: 선체 찢어짐
                Entry(so, "wreckBlast", "SFX_Wreck_Blast", 0.8f, 0.1f, 1f, 0.1f, 4);   // 적 격침: 유폭 · 연쇄 폭발
                Entry(so, "enemyAutocannonShot", "SFX_EnemyAutocannon_Shot", 0.4f, 0.1f, 1f, 0.05f, 5);   // 적 기관포 · 기총 한 발
                Entry(so, "jetPass", "SFX_Jet_Pass", 0.5f, 0.06f, 1f, 1.2f, 2);   // 제트기 공격 진입
                Entry(so, "propPass", "SFX_Prop_Pass", 0.45f, 0.06f, 1f, 1.5f, 2);   // 프로펠러기 등장(WWII)
                Entry(so, "diveSiren", "SFX_Dive_Siren", 0.5f, 0.05f, 1f, 0.6f, 2);   // 급강하 폭격기 급강하
                Entry(so, "droneBuzz", "SFX_Drone_Buzz", 0.45f, 0.08f, 1f, 0.8f, 2);   // 자폭 드론 돌입
                Entry(so, "shellSplash", "SFX_Shell_Splash", 0.45f, 0.15f, 1f, 0.08f, 3);   // 빗나간 포탄의 수면 착탄 · 폭뢰 입수
                Entry(so, "depthDrop", "SFX_Depth_Drop", 0.5f, 0.1f, 1f, 0.15f, 3);   // 폭뢰 · 기뢰 투하
                Entry(so, "chaffLaunch", "SFX_Chaff_Launch", 0.55f, 0.08f, 1f, 0.15f, 3);   // 기만체 · 연막 발사
                Entry(so, "jamPulse", "SFX_Jam_Pulse", 0.5f, 0.04f, 1f, 0.3f, 2);   // 전자전 교란
                Entry(so, "ciwsBurst",      "SFX_CIWS_Burst",      0.7f,  0.03f, 1f, 0f,    3);
                Entry(so, "missileLaunch",  "SFX_Missile_Launch",  0.6f,  0.06f, 1f, 0.1f,  4);
                Entry(so, "explosion",      "SFX_Explosion_Large", 0.8f,  0.10f, 1f, 0.08f, 6);
                Entry(so, "hullImpact",     "SFX_Hull_Impact",     0.7f,  0.08f, 0.5f, 0.12f, 3);
                // 소나는 내 배의 장비음이다. 위치와 상관없이 들려야 한다.
                Entry(so, "sonarPing",      "SFX_Sonar_Ping",      0.6f,  0f,    0f, 2.5f,  1);

                Set(so, "missileWarning", Clip("SFX_Missile_Warning"));
                Set(so, "missileWarningVolume", 0.25f);

                Set(so, "seaAmbience", Clip("SFX_Sea_Ambience"));
                Set(so, "seaAmbienceVolume", 0.35f);
                Set(so, "battleMusic", Clip("BGM_Battle"));
                Set(so, "bossMusic", Clip("BGM_Boss"));
                Set(so, "musicVolume", 0.3f);

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
