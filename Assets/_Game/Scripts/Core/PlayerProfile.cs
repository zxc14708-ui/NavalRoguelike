using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 회차 밖 목표(2026-10-11 점검 K06): 출격 기록 · 도전 과제 · 해금. 판이 끝나도 남는다.
    ///   - 기록(함정별): 출격 · 승리 · 최고 스테이지 · 한 판 최다 격침 · 가장 빠른 승리 · 마지막 패배 원인. 훈련은 세지 않는다.
    ///   - 도전 과제(Catalog): 달성하면 칭호를 받는다(힘이 세지는 보상은 없다). 칭호 하나를 골라 항구·결과 화면에 단다.
    ///   - 해금: "고난도 출항"(첫 스테이지부터 위험 항로 — 적 +25%, 경험치 +25%)은 스테이지 3에 도달하면 열린다. 기본 대응 수단은 잠그지 않는다.
    ///   - 파일: persistentDataPath/naval_profile.json(자동 검증은 naval_profile_verify.json). 저장 방식은 RunSave와 같다(임시 → 교체, .bak).
    /// </summary>
    public static class PlayerProfile
    {
        public const int Version = 1;

        public readonly struct Achievement
        {
            public readonly string Id, Name, Condition, Title;
            public Achievement(string id, string name, string condition, string title) { Id = id; Name = name; Condition = condition; Title = title; }
        }

        /// <summary>도전 과제 목록(순서 = 화면 순서). id는 바꾸지 않는다(저장 파일이 id로 기억한다).</summary>
        public static readonly Achievement[] Catalog =
        {
            new("training", "첫 출격 훈련", "첫 출격 훈련을 끝까지 마친다", "신참 함장"),
            new("first_run", "첫 출항", "한 판을 끝낸다(승패 무관)", "출항한 함장"),
            new("stage2", "제2 해역", "스테이지 2에 도달한다", "해역 돌파자"),
            new("stage3", "A2/AD 해역", "스테이지 3에 도달한다 — 고난도 출항 해금", "봉쇄 돌파자"),
            new("victory", "작전 성공", "스테이지 3 보스를 격침한다", "작전 성공 함장"),
            new("all_ships", "네 척의 함장", "시작 함정 네 척으로 모두 한 번씩 승리한다", "함대 사령관"),
            new("no_asw", "소나 없는 바다", "대잠 블록(폭뢰·경어뢰·헬기갑판) 없이 스테이지 2 보스를 격침한다", "무모한 항해사"),
            new("intercept30", "방공의 벽", "한 판에 적 미사일 30발을 요격한다", "방공의 벽"),
            new("kills150", "해역 소탕", "한 판에 적 150척을 격침한다", "소탕 전문가"),
            new("fleet_t3", "정예 편대", "호위함 네 척을 모두 T3로 개량한다", "편대장"),
            new("max_upgrade", "명장의 손", "블록 하나를 최대 단계까지 강화한다", "정비 장인"),
            new("signature10", "함정의 이름으로", "한 판에 고유 스킬을 10번 쓴다", "고유 전술가"),
            new("ram5", "들이받아라", "한 판에 충돌·충각으로 적 5척을 격침한다", "충각 돌격대"),
            new("danger_victory", "위험한 항로", "고난도 출항으로 승리한다", "위험 항로의 전설"),
            new("supply", "보급선 호위", "임무 항로에서 보급선을 끝까지 지킨다", "호송 지휘관"),
        };

        [Serializable] public class ShipRecord { public string ship; public int runs, wins, bestStage, bestKills; public float fastestWin; public string lastDeath; }
        [Serializable] public class Unlocked { public string id; public string date; }

        [Serializable]
        public class Data
        {
            public int version = Version;
            public int runs, wins, totalKills;
            public List<ShipRecord> ships = new();
            public List<Unlocked> achievements = new();
            public string title;
            public bool dangerStart;
        }

        private static Data s_data;
        private static readonly bool s_verify = Array.IndexOf(Environment.GetCommandLineArgs(), "-combatVerify") >= 0;

        public static string FilePath => Path.Combine(Application.persistentDataPath, s_verify ? "naval_profile_verify.json" : "naval_profile.json");
        public static Data Current => s_data ??= Load();

        /// <summary>이번 실행에서 새로 달성한 도전 과제(결과 화면이 보여 주고 비운다).</summary>
        public static readonly List<string> NewThisRun = new();

        /// <summary>도전 과제를 달성했을 때(배너·소리).</summary>
        public static event Action<Achievement> AchievementUnlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRun()
        {
            // 판을 다시 시작하면(RunRestart) 이번 판 달성 목록을 비운다. 기록 자체(s_data)는 그대로 둔다.
            NewThisRun.Clear();
            AchievementUnlocked = null;
        }

        public static bool Has(string id) => Current.achievements.Exists(a => a.id == id);
        public static Achievement? Find(string id) { foreach (var a in Catalog) if (a.Id == id) return a; return null; }

        /// <summary>고른 칭호(달성한 과제의 칭호만). 없으면 null.</summary>
        public static string Title => !string.IsNullOrEmpty(Current.title) && Find(Current.title) is { } a && Has(a.Id) ? a.Title : null;
        public static void SetTitle(string achievementId) { if (Has(achievementId)) { Current.title = achievementId; Save(); } }

        /// <summary>고난도 출항(스테이지 3 도달로 해금)을 쓸지.</summary>
        public static bool DangerStartUnlocked => Has("stage3");
        public static bool DangerStart => DangerStartUnlocked && Current.dangerStart;
        public static void SetDangerStart(bool on) { Current.dangerStart = on && DangerStartUnlocked; Save(); }

        public static bool Unlock(string id)
        {
            if (Has(id) || Find(id) is not { } a) return false;
            Current.achievements.Add(new Unlocked { id = id, date = DateTime.Now.ToString("yyyy-MM-dd") });
            if (string.IsNullOrEmpty(Current.title)) Current.title = id;   // 첫 칭호는 바로 단다
            NewThisRun.Add(id);
            Save();
            AchievementUnlocked?.Invoke(a);
            return true;
        }

        public static ShipRecord RecordOf(string ship)
        {
            ship = string.IsNullOrEmpty(ship) ? "default" : ship;
            var r = Current.ships.Find(x => x.ship == ship);
            if (r == null) { r = new ShipRecord { ship = ship }; Current.ships.Add(r); }
            return r;
        }

        /// <summary>한 판이 끝났다(승패). 기록을 고치고 판 끝 도전 과제를 확인한다.</summary>
        public static void RecordRun(string ship, bool victory, int stageNumber, int kills, float seconds, string deathCause, bool dangerStart)
        {
            var d = Current;
            var r = RecordOf(ship);
            d.runs++; r.runs++;
            d.totalKills += kills;
            r.bestStage = Mathf.Max(r.bestStage, stageNumber);
            r.bestKills = Mathf.Max(r.bestKills, kills);
            if (victory)
            {
                d.wins++; r.wins++;
                r.fastestWin = r.fastestWin <= 0f ? seconds : Mathf.Min(r.fastestWin, seconds);
            }
            else r.lastDeath = deathCause;
            Save();
            Unlock("first_run");
            if (victory)
            {
                Unlock("victory");
                if (dangerStart) Unlock("danger_victory");
                int shipsWon = 0;
                foreach (var concept in Game.Data.StartingShipCatalog.All)
                    if (concept != null && Current.ships.Exists(x => x.ship == concept.Id && x.wins > 0)) shipsWon++;
                if (shipsWon >= 4) Unlock("all_ships");
            }
        }

        // ------------------------------------------------------------ 파일

        public static void Save()
        {
            if (s_data == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(s_data, true));
                if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch (Exception e) { Debug.LogWarning($"[Profile] 저장 실패: {e.Message}"); }
        }

        private static Data Load()
        {
            foreach (var path in new[] { FilePath, FilePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var d = JsonUtility.FromJson<Data>(File.ReadAllText(path));
                    if (d != null && d.version > 0 && d.version <= Version) { d.ships ??= new(); d.achievements ??= new(); return d; }
                }
                catch (Exception e) { Debug.LogWarning($"[Profile] {Path.GetFileName(path)} 읽기 실패: {e.Message}"); }
            }
            return new Data();
        }

        /// <summary>검증용: 기록을 비운다(자동 검증 파일만).</summary>
        public static void ResetForVerify()
        {
            if (!s_verify) return;
            s_data = new Data();
            NewThisRun.Clear();
            Save();
        }
    }
}
