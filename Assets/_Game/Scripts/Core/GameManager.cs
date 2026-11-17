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

        /// <summary>승리 또는 패배가 났다. 이후 상태는 바뀌지 않고, 새 판은 씬을 다시 연다(RunRestart).</summary>
        public bool IsRunOver => State == GameState.Victory || State == GameState.GameOver;

        /// <summary>
        /// 상태 전환. 정지가 필요한 상태에서 timeScale을 0으로 만든다.
        /// 승패가 난 뒤의 전환은 무시한다 — 기함과 마지막 보스가 같은 프레임에 격침돼도 먼저 난 결과가 남고,
        /// 패배 뒤 레벨업·보스 보상이 정비 화면을 열지 못한다(2026-10-11 점검 B03).
        /// </summary>
        public void SetState(GameState next)
        {
            if (State == next || IsRunOver) return;
            State = next;
            if (IsRunOver) RunSave.OnRunEnded();   // 판이 끝났다 — 이어하기 기록을 지운다(훈련 제외)
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

        /// <summary>저장 불러오기: 작전 시간·격침 수를 이어 간다.</summary>
        public void RestoreRun(float elapsed, int kills)
        {
            ElapsedTime = Mathf.Max(0f, elapsed);
            TotalKills = Mathf.Max(0, kills);
            GameEvents.RaiseEnemyKilled(TotalKills);
        }

        /// <summary>Hull HP가 0이 되면 ShipController가 호출한다.</summary>
        public void TriggerGameOver() => SetState(GameState.GameOver);

        public void TriggerVictory() => SetState(GameState.Victory);
    }
}
