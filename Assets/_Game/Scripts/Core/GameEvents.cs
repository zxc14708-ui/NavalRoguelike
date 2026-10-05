using System;
using UnityEngine;
using Game.Modules;

namespace Game.Core
{
    /// <summary>공용 스킬 이벤트로 통신하는 액티브 스킬(기만체·재밍은 전용 이벤트를 쓴다).</summary>
    public enum ActiveSkillId { Repair, Smoke, Flank }

    /// <summary>
    /// 시스템 간 느슨한 결합을 위한 전역 이벤트 허브.
    /// 모듈끼리 직접 참조하지 않고 여기를 경유한다.
    /// 구독자는 반드시 OnDisable에서 해제할 것.
    /// </summary>
    public static class GameEvents
    {
        // --- 게임 흐름
        public static event Action<GameState> StateChanged;

        // --- 스테이지 / 성장
        public static event Action<int, int, int, string> PhaseStarted;  // stage, phase, phaseCount, title
        public static event Action<float, float> StageProgressChanged;   // elapsed, total
        public static event Action<int> EnemyKilled;                     // 누적 격침 수
        public static event Action<Game.Data.EnemyDefinition> EnemyDefeated; // 격침된 적 종류(경험치)
        public static event Action<int, int, int> ExperienceChanged;     // level, xp, xpToNext
        public static event Action<int> LevelUp;                         // 달성한 레벨

        // --- 함선 상태
        public static event Action<float, float> HullHpChanged;   // current, max
        public static event Action<float> DetectionRangeChanged;
        public static event Action<float> SpeedChanged;

        // --- 모듈
        public static event Action<ModuleInstance> ModuleInstalled;
        public static event Action<ModuleInstance> ModuleDestroyed;
        public static event Action<ModuleInstance> ModuleRemoved;
        public static event Action<ModuleInstance> ModuleUpgraded;   // 설치된 블록의 강화 단계가 올랐다

        // --- 전투 피드백
        public static event Action<Transform> IncomingMissileDetected;
        public static event Action<Transform> IncomingMissileCleared;
        public static event Action<Transform> SubmarineContact;   // 소나/헬기가 새 잠수함을 잡음

        // --- 기만체 스킬
        public static event Action DecoyRequested;                     // HUD 버튼 등에서 발사 요청
        public static event Action<int, int, float> DecoyStatusChanged; // 준비 수, 전체 수, 다음 준비 진행도
        public static event Action JamRequested;
        public static event Action<int, int, float> JamStatusChanged;

        // --- 응급 수리·연막·전속
        public static event Action<ActiveSkillId> SkillRequested;
        public static event Action<ActiveSkillId, int, int, float> SkillStatusChanged;  // 스킬, 준비 수, 전체 수, 다음 준비 진행도

        // --- 모듈 거치대
        public static event Action<ModuleInstance> ModuleRaisedChanged;

        // --- 적
        public static event Action<int> ReconChanged;   // 살아 있는 정찰기 수
        public static event Action BossDefeated;        // 스테이지 보스 격침
        public static event Action<string> BossPhaseChanged;   // 보스 단계 전환 안내 문구

        public static void RaiseBossPhaseChanged(string message) => BossPhaseChanged?.Invoke(message);

        public static void RaiseSkillRequested(ActiveSkillId id) => SkillRequested?.Invoke(id);
        public static void RaiseSkillStatusChanged(ActiveSkillId id, int ready, int total, float next01)
            => SkillStatusChanged?.Invoke(id, ready, total, next01);
        public static void RaiseReconChanged(int count) => ReconChanged?.Invoke(count);
        public static void RaiseBossDefeated() => BossDefeated?.Invoke();

        public static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);

        public static void RaisePhaseStarted(int stage, int phase, int count, string title)
            => PhaseStarted?.Invoke(stage, phase, count, title);
        public static void RaiseStageProgressChanged(float elapsed, float total) => StageProgressChanged?.Invoke(elapsed, total);
        public static void RaiseEnemyKilled(int total) => EnemyKilled?.Invoke(total);
        public static void RaiseEnemyDefeated(Game.Data.EnemyDefinition def) => EnemyDefeated?.Invoke(def);
        public static void RaiseExperienceChanged(int level, int xp, int next) => ExperienceChanged?.Invoke(level, xp, next);
        public static void RaiseLevelUp(int level) => LevelUp?.Invoke(level);

        public static void RaiseHullHpChanged(float cur, float max) => HullHpChanged?.Invoke(cur, max);
        public static void RaiseDetectionRangeChanged(float r) => DetectionRangeChanged?.Invoke(r);
        public static void RaiseSpeedChanged(float s) => SpeedChanged?.Invoke(s);

        public static void RaiseModuleInstalled(ModuleInstance m) => ModuleInstalled?.Invoke(m);
        public static void RaiseModuleDestroyed(ModuleInstance m) => ModuleDestroyed?.Invoke(m);
        public static void RaiseModuleRemoved(ModuleInstance m) => ModuleRemoved?.Invoke(m);
        public static void RaiseModuleUpgraded(ModuleInstance m) => ModuleUpgraded?.Invoke(m);

        public static void RaiseIncomingMissile(Transform t) => IncomingMissileDetected?.Invoke(t);
        public static void RaiseIncomingMissileCleared(Transform t) => IncomingMissileCleared?.Invoke(t);
        public static void RaiseSubmarineContact(Transform t) => SubmarineContact?.Invoke(t);

        public static void RaiseDecoyRequested() => DecoyRequested?.Invoke();
        public static void RaiseDecoyStatusChanged(int ready, int total, float next01)
            => DecoyStatusChanged?.Invoke(ready, total, next01);
        public static void RaiseJamRequested() => JamRequested?.Invoke();
        public static void RaiseJamStatusChanged(int ready, int total, float next01)
            => JamStatusChanged?.Invoke(ready, total, next01);
        public static void RaiseModuleRaisedChanged(ModuleInstance m) => ModuleRaisedChanged?.Invoke(m);
    }
}
