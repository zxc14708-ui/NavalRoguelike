using System;
using UnityEngine;
using Game.Core;
using Game.Data;

namespace Game.Progression
{
    /// <summary>
    /// 격침 경험치와 레벨. 레벨이 오르면 전투를 멈추고 모듈 카드를 고르게 한다.
    ///
    /// 성장 속도를 시간이 아니라 격침 수에 묶는다. 적이 많아질수록 경험치도 많이 들어와서
    /// "업그레이드는 제자리인데 물량만 늘어나는" 구간이 생기지 않는다.
    /// 한 번에 여러 레벨이 오르면 전부 쌓아두고, 배치를 마칠 때마다 하나씩 연다.
    /// </summary>
    public class ExperienceSystem : MonoBehaviour
    {
        [SerializeField] private ProgressionConfig config;

        /// <summary>카드 풀·경험치 설정(사전이 무장 목록을 읽는다).</summary>
        public ProgressionConfig Config => config;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpToNext => config != null ? config.XpRequired(Level) : 10;
        private float _rewardMultiplier = 1f;
        private float _rewardFraction;
        public float RewardMultiplier
        {
            get => _rewardMultiplier;
            set { _rewardMultiplier = Mathf.Max(0f, value); _rewardFraction = 0f; }
        }

        private int _pendingLevelUps;
        private bool _levelUpOpen;

        /// <summary>레벨업 화면을 열어야 할 때. 인자는 달성한 레벨, 닫을 때 부를 콜백.</summary>
        public event Action<int, Action> LevelUpRequested;

        private void Awake()
        {
            if (config == null) Debug.LogError("[ExperienceSystem] ProgressionConfig 미할당.", this);
        }

        private void OnEnable() => GameEvents.EnemyDefeated += OnEnemyDefeated;
        private void OnDisable() => GameEvents.EnemyDefeated -= OnEnemyDefeated;

        private void Start() => RaiseChanged();

        private void OnEnemyDefeated(EnemyDefinition def)
        {
            if (def == null || def.XpReward <= 0) return;
            float reward = def.XpReward * RewardMultiplier + _rewardFraction;
            int whole = Mathf.FloorToInt(reward);
            _rewardFraction = reward - whole;
            AddXp(whole);
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;

            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                _pendingLevelUps++;
                GameEvents.RaiseLevelUp(Level);
            }

            RaiseChanged();
            TryOpenLevelUp();
        }

        private void Update() => TryOpenLevelUp();

        /// <summary>전투 중일 때만 연다. 게임오버 직전이나 이미 열린 화면 위에 겹치지 않게 한다.</summary>
        private void TryOpenLevelUp()
        {
            if (_pendingLevelUps <= 0 || _levelUpOpen) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (LevelUpRequested == null) return;

            _pendingLevelUps--;
            _levelUpOpen = true;

            // 쌓인 레벨업이 여럿이면 가장 먼저 달성한 레벨부터 보여준다
            LevelUpRequested.Invoke(Level - _pendingLevelUps, OnLevelUpClosed);
        }

        private void OnLevelUpClosed()
        {
            _levelUpOpen = false;
            TryOpenLevelUp();
        }

        private void RaiseChanged() => GameEvents.RaiseExperienceChanged(Level, Xp, XpToNext);
    }
}
