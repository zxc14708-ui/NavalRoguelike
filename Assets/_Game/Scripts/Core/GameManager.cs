using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 게임 상태 머신과 시간 정지의 단일 소유자.
    /// 스테이지 진행은 StageDirector가, 레벨업 배치는 RefitController가 맡고
    /// 여기서는 "지금 어떤 상태인가"만 관리한다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private Game.Ship.ShipController player;

        public GameState State { get; private set; } = GameState.Boot;
        public float ElapsedTime { get; private set; }
        public int TotalKills { get; private set; }
        public Game.Ship.ShipController Player => player;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (player == null) Debug.LogError("[GameManager] ShipController 미할당.", this);
        }

        private void Update()
        {
            if (State == GameState.Playing) ElapsedTime += Time.deltaTime;
        }

        /// <summary>상태 전환. 정지가 필요한 상태에서 timeScale을 0으로 만든다.</summary>
        public void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            Time.timeScale = IsTimeStopped(next) ? 0f : 1f;
            GameEvents.RaiseStateChanged(next);
        }

        private static bool IsTimeStopped(GameState s) =>
            s == GameState.Refit || s == GameState.Paused ||
            s == GameState.Victory || s == GameState.GameOver;

        /// <summary>적이 격침될 때마다 누적하고, 종류를 알려 경험치를 주게 한다.</summary>
        public void RegisterKill(Game.Data.EnemyDefinition def)
        {
            TotalKills++;
            GameEvents.RaiseEnemyKilled(TotalKills);
            GameEvents.RaiseEnemyDefeated(def);
        }

        /// <summary>Hull HP가 0이 되면 ShipController가 호출한다.</summary>
        public void TriggerGameOver() => SetState(GameState.GameOver);

        public void TriggerVictory() => SetState(GameState.Victory);
    }
}
