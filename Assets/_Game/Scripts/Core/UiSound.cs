using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 메뉴·알림음(2026-10-11 점검 O06). 음원 파일 없이 코드로 짧은 소리를 합성해 쓴다(출처·라이선스 걱정 없음).
    ///   - 메뉴: 선택(짧은 틱) · 확정(오르는 두 음) · 취소(내리는 두 음) · 실패(낮은 버저) · 다시 뽑기(섞는 틱 셋)
    ///   - 진행: 레벨업(밝은 세 음) · 교리 결정(종소리) · 저장(부드러운 두 음) · 승리/패배
    ///   - 보스 단서: 등장(낮은 경적 두 번) · 단계 전환(내려가는 세 음) · 공격 직전(높은 삑 한 번)
    /// 2D 소리, "메뉴·알림음" 채널(SoundChannel.Ui)과 전체 음량을 따른다. 일시정지(AudioListener.pause) 중에도 울린다.
    /// 같은 소리는 0.06초 안에 겹쳐 틀지 않는다. 소리마다 화면 문구가 함께 나오는 곳에서만 쓴다(무음에서도 알 수 있게).
    /// </summary>
    public static class UiSound
    {
        public enum Cue { Select, Confirm, Cancel, Fail, Reroll, LevelUp, Doctrine, Save, Victory, Defeat, BossAlert, BossPhase, AttackSoon }

        private const int Rate = 44100;
        private static readonly Dictionary<Cue, AudioClip> s_clips = new();
        private static readonly Dictionary<Cue, float> s_last = new();
        private static AudioSource s_source;

        /// <summary>검증용: 지금까지 울린 수(소리별).</summary>
        public static readonly Dictionary<Cue, int> Played = new();

        public static void Play(Cue cue, float volume = 1f)
        {
            float now = Time.unscaledTime;
            if (s_last.TryGetValue(cue, out float last) && now - last < 0.06f) return;
            s_last[cue] = now;
            Played.TryGetValue(cue, out int n);
            Played[cue] = n + 1;
            if (Application.isBatchMode) return;   // 자동 검증 플레이어: 소리 장치 없이 횟수만
            var src = Source();
            if (src == null) return;
            src.PlayOneShot(Clip(cue), volume * GameSettings.Volume(SoundChannel.Ui));
        }

        private static AudioSource Source()
        {
            if (s_source != null) return s_source;
            var go = new GameObject("UI sound");
            Object.DontDestroyOnLoad(go);
            s_source = go.AddComponent<AudioSource>();
            s_source.spatialBlend = 0f;
            s_source.playOnAwake = false;
            s_source.ignoreListenerPause = true;   // 일시정지 메뉴에서도
            return s_source;
        }

        private static AudioClip Clip(Cue cue)
        {
            if (s_clips.TryGetValue(cue, out var clip) && clip != null) return clip;
            // (주파수 Hz, 시작 초, 길이 초, 세기) 음 여럿을 이어 붙인다
            var notes = cue switch
            {
                Cue.Select => new[] { (1400f, 0f, 0.035f, 0.35f) },
                Cue.Confirm => new[] { (660f, 0f, 0.07f, 0.45f), (990f, 0.07f, 0.11f, 0.45f) },
                Cue.Cancel => new[] { (700f, 0f, 0.07f, 0.4f), (470f, 0.07f, 0.1f, 0.4f) },
                Cue.Fail => new[] { (180f, 0f, 0.16f, 0.5f), (150f, 0.02f, 0.16f, 0.35f) },
                Cue.Reroll => new[] { (900f, 0f, 0.03f, 0.3f), (1100f, 0.05f, 0.03f, 0.3f), (1300f, 0.1f, 0.05f, 0.35f) },
                Cue.LevelUp => new[] { (523f, 0f, 0.1f, 0.4f), (659f, 0.09f, 0.1f, 0.4f), (784f, 0.18f, 0.22f, 0.45f) },
                Cue.Doctrine => new[] { (392f, 0f, 0.5f, 0.35f), (784f, 0f, 0.4f, 0.2f), (1176f, 0f, 0.3f, 0.12f) },
                Cue.Save => new[] { (587f, 0f, 0.12f, 0.3f), (880f, 0.1f, 0.2f, 0.3f) },
                Cue.Victory => new[] { (523f, 0f, 0.14f, 0.4f), (659f, 0.14f, 0.14f, 0.4f), (784f, 0.28f, 0.14f, 0.4f), (1046f, 0.42f, 0.4f, 0.45f) },
                Cue.Defeat => new[] { (392f, 0f, 0.25f, 0.4f), (330f, 0.25f, 0.25f, 0.4f), (262f, 0.5f, 0.6f, 0.45f) },
                Cue.BossAlert => new[] { (220f, 0f, 0.35f, 0.5f), (220f, 0.45f, 0.35f, 0.5f), (330f, 0f, 0.35f, 0.2f), (330f, 0.45f, 0.35f, 0.2f) },
                Cue.BossPhase => new[] { (660f, 0f, 0.12f, 0.4f), (520f, 0.12f, 0.12f, 0.4f), (390f, 0.24f, 0.25f, 0.45f) },
                _ => new[] { (1760f, 0f, 0.07f, 0.4f) },   // AttackSoon
            };
            float length = 0f;
            foreach (var (_, start, dur, _) in notes) length = Mathf.Max(length, start + dur);
            int samples = Mathf.CeilToInt((length + 0.02f) * Rate);
            var data = new float[samples];
            foreach (var (freq, start, dur, amp) in notes)
            {
                int from = Mathf.FloorToInt(start * Rate), count = Mathf.FloorToInt(dur * Rate);
                for (int i = 0; i < count && from + i < samples; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-3.5f * t / dur);   // 빠른 시작, 자연스러운 감쇠
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * t);
                    data[from + i] += wave * env * amp * 0.6f;
                }
            }
            for (int i = 0; i < samples; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            clip = AudioClip.Create($"UI {cue}", samples, 1, Rate, false);
            clip.SetData(data, 0);
            s_clips[cue] = clip;
            return clip;
        }
    }
}
