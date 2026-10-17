using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Modules;

namespace Game.UI
{
    /// <summary>
    /// 도전 과제 확인과 출격 기록(2026-10-11 점검 K06). HUD가 만든다.
    ///   - 스테이지 도달(구간 시작 이벤트) · 스테이지 2 보스 격침 때 대잠 블록 유무 · 1초마다 한 판 기록(격침·요격·편대·강화·고유 스킬·충돌 격침)
    ///   - 판이 끝나면(승패) 함정별 기록을 남긴다. 훈련 중에는 기록하지 않는다(훈련 완료 과제만).
    ///   - 달성하면 배너 + 종소리. 결과 화면이 이번 판에 달성한 것을 보여 준다.
    /// </summary>
    public sealed class AchievementTracker : MonoBehaviour
    {
        private float _poll;
        private bool _recorded;

        private static AchievementTracker s_instance;

        public static AchievementTracker Create(Transform parent) => new GameObject("Achievements").AddComponent<AchievementTracker>();

        /// <summary>
        /// 판 끝 기록을 지금 남긴다(결과 화면이 이번 판 도전 과제를 쓰기 전에 부른다 — 같은 상태 이벤트의 구독 순서에 기대지 않게).
        /// 한 판에 한 번만 기록된다.
        /// </summary>
        public static void RecordRunEnd(GameState state) { if (s_instance != null) s_instance.OnState(state); }

        private void OnEnable()
        {
            s_instance = this;
            GameEvents.PhaseStarted += OnPhase;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.StateChanged += OnState;
            PlayerProfile.AchievementUnlocked += OnUnlocked;
        }

        private void OnDisable()
        {
            if (s_instance == this) s_instance = null;
            GameEvents.PhaseStarted -= OnPhase;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.StateChanged -= OnState;
            PlayerProfile.AchievementUnlocked -= OnUnlocked;
        }

        private static StageDirector Director => FindFirstObjectByType<StageDirector>();
        private static bool Training => Director != null && (Director.Training || Director.Ww2Test);   // WWII 시험 전투도 기록·도전 과제를 세지 않는다

        private void OnPhase(int stage, int phase, int count, string title)
        {
            if (Training) return;
            if (stage >= 2) PlayerProfile.Unlock("stage2");
            if (stage >= 3) PlayerProfile.Unlock("stage3");
        }

        private void OnBossDefeated()
        {
            var director = Director;
            if (director == null || director.Training || director.StageNumber != 2) return;
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (ship == null) return;
            foreach (var m in ship.Grid.Modules)
                if (m?.Definition != null && m.Definition.Type is ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.HelicopterDeck) return;
            PlayerProfile.Unlock("no_asw");
        }

        private void OnState(GameState state)
        {
            if (state is not (GameState.Victory or GameState.GameOver) || _recorded || Training) return;
            _recorded = true;
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var init = FindFirstObjectByType<Game.Ship.ShipInitializer>();
            var director = Director;
            PlayerProfile.RecordRun(init != null && init.SelectedConcept != null ? init.SelectedConcept.Id : "default",
                state == GameState.Victory, director != null ? director.StageNumber : 1, gm.TotalKills, gm.ElapsedTime,
                ship != null ? ship.LastHullDamageCause : null, director != null && director.DangerStartRun);
        }

        private void Update()
        {
            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0f) return;
            _poll = 1f;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || Training) return;
            if (gm.TotalKills >= 150) PlayerProfile.Unlock("kills150");
            if (CombatStats.Threats.TryGetValue(ThreatOutcome.Intercepted, out var t) && t.Count >= 30) PlayerProfile.Unlock("intercept30");
            var ship = gm.Player;
            if (ship == null) return;
            foreach (var m in ship.Grid.Modules)
                if (m != null && m.MaxUpgradeLevel > 0 && m.UpgradeLevel >= m.MaxUpgradeLevel) { PlayerProfile.Unlock("max_upgrade"); break; }
            var fleet = ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>();
            if (fleet != null && fleet.EscortCount >= Game.TaskForce.TaskForceEscortFormation.MaxEscorts)
            {
                bool all = true;
                for (int i = 0; i < fleet.EscortCount; i++) { var e = fleet.GetInfo(i); if (e.Role == Game.TaskForce.EscortRole.None || e.Tier < 3) all = false; }
                if (all) PlayerProfile.Unlock("fleet_t3");
            }
            if (Game.Ship.SignatureSkill.Active != null && Game.Ship.SignatureSkill.Active.Uses >= 10) PlayerProfile.Unlock("signature10");
            foreach (var e in RunReport.Ranked()) if (e.Key == "충돌·충각" && e.Kills >= 5) { PlayerProfile.Unlock("ram5"); break; }
        }

        private void OnUnlocked(PlayerProfile.Achievement a)
        {
            UiSound.Play(UiSound.Cue.Doctrine);
            GameEvents.RaiseBanner($"도전 과제 달성: {a.Name} — 칭호 「{a.Title}」");
        }
    }
}
