using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Modules;

namespace Game.Core
{
    /// <summary>
    /// 재생 중인 효과음을 가리키는 손잡이. 보이스는 재사용되므로
    /// 같은 클립을 여전히 재생 중일 때만 유효하다.
    /// </summary>
    public readonly struct SfxHandle
    {
        private readonly AudioSource _source;
        private readonly AudioClip _clip;

        public SfxHandle(AudioSource source, AudioClip clip)
        {
            _source = source;
            _clip = clip;
        }

        public bool IsPlaying => _source != null && _clip != null && _source.isPlaying && _source.clip == _clip;
        public float Time => IsPlaying ? _source.time : 0f;

        /// <summary>재생 위치를 옮긴다. 연사음을 끊을 때 꼬리 부분으로 건너뛰는 데 쓴다.</summary>
        public void SkipTo(float seconds)
        {
            if (!IsPlaying) return;
            _source.time = Mathf.Clamp(seconds, 0f, _clip.length - 0.01f);
        }
    }

    /// <summary>
    /// 효과음 재생 창구. 게임 코드는 AudioManager.Play(SfxId, 위치)만 부른다.
    ///
    /// 보이스(AudioSource)를 미리 만들어 돌려쓰고, 소리별 최소 간격과 동시 재생 수를 제한해
    /// 수십 척이 동시에 쏴도 소리가 뭉개지거나 AudioSource가 무한히 늘지 않게 한다.
    /// 미사일 경보와 기관음처럼 상태를 따라가는 루프는 이벤트를 구독해 스스로 관리한다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private SfxLibrary library;
        [SerializeField, Min(4)] private int voiceCount = 24;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        private AudioSource[] _voices;
        private SfxId[] _voiceIds;
        private float[] _voiceStartTimes;
        private readonly Dictionary<SfxId, float> _lastPlayed = new();

        private AudioSource _warning;
        private AudioSource _engine;
        private readonly HashSet<Transform> _incoming = new();

        private float _engineSpeed01;
        private float _engineSmoothed;
        private bool _inCombat;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            AudioListener.volume = GameSettings.Volume(SoundChannel.Master);

            if (library == null)
            {
                Debug.LogError("[AudioManager] SfxLibrary 미할당. 효과음이 재생되지 않습니다.", this);
                return;
            }

            CreateVoices();
            _warning = CreateLoopSource("MissileWarning", library.MissileWarning);
            _engine = CreateLoopSource("EngineLoop", library.EngineLoop);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.SpeedChanged += OnSpeedChanged;
            GameEvents.IncomingMissileDetected += OnIncomingMissile;
            GameEvents.IncomingMissileCleared += OnIncomingMissileCleared;
            GameEvents.ModuleDestroyed += OnModuleDestroyed;
            GameEvents.SubmarineContact += OnSubmarineContact;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.SpeedChanged -= OnSpeedChanged;
            GameEvents.IncomingMissileDetected -= OnIncomingMissile;
            GameEvents.IncomingMissileCleared -= OnIncomingMissileCleared;
            GameEvents.ModuleDestroyed -= OnModuleDestroyed;
            GameEvents.SubmarineContact -= OnSubmarineContact;
        }

        // ------------------------------------------------------------------ 생성

        private void CreateVoices()
        {
            _voices = new AudioSource[voiceCount];
            _voiceIds = new SfxId[voiceCount];
            _voiceStartTimes = new float[voiceCount];

            for (int i = 0; i < voiceCount; i++)
            {
                var src = new GameObject($"Voice_{i:00}").AddComponent<AudioSource>();
                src.transform.SetParent(transform, false);
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = library.MinDistance;
                src.maxDistance = library.MaxDistance;
                src.dopplerLevel = 0f;   // 빠른 포탄·미사일에서 음이 뒤틀리지 않게
                _voices[i] = src;
            }
        }

        private AudioSource CreateLoopSource(string name, AudioClip clip)
        {
            var src = new GameObject(name).AddComponent<AudioSource>();
            src.transform.SetParent(transform, false);
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        // ------------------------------------------------------------------ 재생

        /// <summary>위치 효과음을 재생한다. AudioManager가 없으면 조용히 무시한다.</summary>
        public static SfxHandle Play(SfxId id, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (Instance == null) return default;
            return Instance.PlayInternal(id, position, volumeScale, pitchScale);
        }

        private SfxHandle PlayInternal(SfxId id, Vector3 position, float volumeScale, float pitchScale)
        {
            if (library == null || _voices == null) return default;

            var entry = library.Get(id);
            if (entry == null || entry.Clip == null) return default;

            // 정비 중에는 시간이 멈춰 있으므로 실제 시간 기준으로 간격을 잰다
            float now = UnityEngine.Time.unscaledTime;
            if (entry.MinInterval > 0f && _lastPlayed.TryGetValue(id, out float last) &&
                now - last < entry.MinInterval)
                return default;
            _lastPlayed[id] = now;

            int voice = PickVoice(id, entry.MaxVoices);
            var src = _voices[voice];

            src.Stop();
            src.transform.position = position;
            src.clip = entry.Clip;
            src.spatialBlend = entry.SpatialBlend;
            src.volume = entry.Volume * volumeScale * masterVolume * GameSettings.Volume(SoundChannel.Effects);
            src.pitch = pitchScale * (1f + Random.Range(-entry.PitchJitter, entry.PitchJitter));
            src.Play();

            _voiceIds[voice] = id;
            _voiceStartTimes[voice] = now;
            return new SfxHandle(src, entry.Clip);
        }

        /// <summary>
        /// 같은 소리가 이미 한도만큼 울리고 있으면 그중 가장 오래된 것을,
        /// 아니면 빈 보이스를, 그것도 없으면 전체에서 가장 오래된 것을 고른다.
        /// </summary>
        private int PickVoice(SfxId id, int maxVoices)
        {
            int sameCount = 0, oldestSame = -1, free = -1, oldestAny = 0;

            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }

                if (_voiceStartTimes[i] < _voiceStartTimes[oldestAny]) oldestAny = i;

                if (_voiceIds[i] != id) continue;
                sameCount++;
                if (oldestSame < 0 || _voiceStartTimes[i] < _voiceStartTimes[oldestSame]) oldestSame = i;
            }

            if (sameCount >= maxVoices && oldestSame >= 0) return oldestSame;
            return free >= 0 ? free : oldestAny;
        }

        // ------------------------------------------------------------------ 루프

        private void Update()
        {
            if (library == null) return;

            float dt = UnityEngine.Time.unscaledDeltaTime;
            UpdateEngine(dt);
            PruneIncoming();
        }

        /// <summary>기관음은 속력을 따라 커지고 높아진다. 전투가 아니면 잦아든다.</summary>
        private void UpdateEngine(float dt)
        {
            if (_engine == null || _engine.clip == null) return;

            float target = _inCombat ? _engineSpeed01 : 0f;
            _engineSmoothed = Mathf.MoveTowards(_engineSmoothed, target, library.EngineResponse * dt);

            float volume = _inCombat
                ? Mathf.Lerp(library.EngineVolumeIdle, library.EngineVolumeFull, _engineSmoothed)
                : 0f;
            _engine.volume = Mathf.MoveTowards(_engine.volume,
                volume * masterVolume * GameSettings.Volume(SoundChannel.Engine), dt);
            _engine.pitch = Mathf.Lerp(library.EnginePitchIdle, library.EnginePitchFull, _engineSmoothed);

            if (_engine.volume > 0.001f && !_engine.isPlaying) _engine.Play();
            else if (_engine.volume <= 0.001f && _engine.isPlaying && !_inCombat) _engine.Stop();
        }

        /// <summary>경보 해제 이벤트를 놓친 미사일(풀 반납 순서 등)이 경보를 붙잡지 않게 한다.</summary>
        private void PruneIncoming()
        {
            if (_incoming.Count == 0) return;
            _incoming.RemoveWhere(t => t == null || !t.gameObject.activeInHierarchy);
            RefreshWarning();
        }

        private void RefreshWarning()
        {
            if (_warning == null || _warning.clip == null) return;

            bool on = _inCombat && _incoming.Count > 0;
            _warning.volume = library.MissileWarningVolume * masterVolume * GameSettings.Volume(SoundChannel.Warning);
            if (on && !_warning.isPlaying)
            {
                _warning.Play();
            }
            else if (!on && _warning.isPlaying)
            {
                _warning.Stop();
            }
        }

        // ------------------------------------------------------------------ 이벤트

        private void OnStateChanged(GameState state)
        {
            _inCombat = state == GameState.Playing;
            if (!_inCombat) _incoming.Clear();
            RefreshWarning();
        }

        private void OnSpeedChanged(float speed)
        {
            if (library == null) return;
            _engineSpeed01 = Mathf.Clamp01(Mathf.Abs(speed) / library.EngineFullSpeed);
        }

        private void OnIncomingMissile(Transform missile)
        {
            if (missile == null) return;
            _incoming.Add(missile);
            RefreshWarning();
        }

        private void OnIncomingMissileCleared(Transform missile)
        {
            _incoming.Remove(missile);
            RefreshWarning();
        }

        private void OnModuleDestroyed(ModuleInstance module)
        {
            var runtime = module?.Runtime;
            if (runtime == null) return;
            Play(SfxId.Explosion, runtime.transform.position, 0.8f, 0.9f);
        }

        private void OnSubmarineContact(Transform submarine)
        {
            Play(SfxId.SonarPing, submarine != null ? submarine.position : transform.position);
        }
    }
}
