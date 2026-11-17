#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Enemies;
using Game.Missions;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 전장 이벤트(2026-11-17): 자폭 보트 협공 · 위치 노출 · 구조 활동 · 적 보급선 추격 · 진행.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator FieldEventsCheck()
        {
            _report.AppendLine("\n## 전장 이벤트(2026-11-17)");
            var ship = GameManager.Instance.Player;
            var xp = Object.FindFirstObjectByType<Game.Progression.ExperienceSystem>();
            var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
            var rb = ship.GetComponent<Rigidbody>();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);   // 기함 무장이 협공 보트를 쏘아 막지 않게(무장 명중은 적 보급선에서 따로 본다)
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            void Warp(Vector3 p) { p.y = ship.transform.position.y; if (rb != null) rb.position = p; ship.transform.position = p; }
            IEnumerator CloseRefit()
            {
                yield return null;
                if (GameManager.Instance.State == GameState.Refit) { Call(refit, "Resume"); yield return null; }
            }
            Vector3 home = ship.transform.position;

            FieldEventDirector.BeginStage(ship, xp, 1, () => false);
            var dir = FieldEventDirector.Instance;
            bool scheduled = dir != null && dir.Active && dir.NextIn > 30f && dir.Exposure.enabled;
            _report.AppendLine($"- 진행: 스테이지 시작 → 첫 이벤트까지 {dir?.NextIn:0}초 · 위치 노출 게이지 켜짐 {Yes(dir != null && dir.Exposure.enabled)}");
            if (!scheduled) Fail("전장 이벤트 진행이 스테이지 시작에 열리지 않음");
            if (dir == null) yield break;
            dir.Exposure.enabled = false;   // 게이지는 따로 본다(다른 이벤트 중 포격이 섞이지 않게)
            dir.DevNoAuto = true;           // 검사 도중 이벤트가 저절로 열리지 않게

            // ---- 1) 자폭 보트 협공: 제자리면 맞는다
            {
                ship.RepairHull(ship.HullMaxHp);
                float hp0 = ship.HullHp;
                bool started = dir.StartEvent(FieldEventDirector.Kind.Pincer);
                var p = dir.Current as PincerAttack;
                yield return new WaitForSeconds(1f);
                bool lanes = GameObject.Find("Pincer lane 0") != null && GameObject.Find("Pincer aim ring") != null;
                yield return ScreenShot("field_pincer_warn");
                float t0 = Time.realtimeSinceStartup;
                while (p != null && p.State != PincerAttack.Phase.Done && Time.realtimeSinceStartup - t0 < 12f) yield return null;
                bool hit = p != null && p.Hits >= 1 && ship.HullHp < hp0;
                _report.AppendLine($"- 협공(제자리): 시작 {Yes(started)} · 예고선 · 표적 원 {Yes(lanes)} · 나온 보트 {p?.Launched} · 기함 충돌 {p?.Hits} · 선체 {hp0:0} → {ship.HullHp:0} · 결과 {p?.Outcome}");
                if (!started || !lanes || p == null || p.Launched < 2) Fail("자폭 보트 협공이 예고 · 세 방향 돌진을 하지 않음");
                if (!hit) Fail("제자리에 머문 기함에 협공 보트가 부딪히지 않음");
                yield return CloseRefit();
                CombatDevTools.ClearBattlefield();
            }

            // ---- 2) 협공 회피: 돌진이 시작되면 옆으로 비킨다 → 보트끼리 부딪혀 터지고 둘레 적이 휩쓸린다
            {
                ship.RepairHull(ship.HullMaxHp);
                Warp(home);
                int xp0 = xp.Xp + xp.Level * 100000;
                dir.StartEvent(FieldEventDirector.Kind.Pincer);
                var p = dir.Current as PincerAttack;
                float t0 = Time.realtimeSinceStartup;
                while (p != null && p.State == PincerAttack.Phase.Warning && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                EnemyController bystander = null;
                bool charging = p != null && p.State == PincerAttack.Phase.Charging && p.BoatsList.Count >= 2;
                if (charging)
                {
                    // 두 보트 사이 방향으로 40m 비킨다(돌진선 위가 아니게)
                    Vector3 a = p.BoatsList[0].transform.position - p.AimPoint; a.y = 0f;
                    Vector3 away = Quaternion.Euler(0f, 60f, 0f) * a.normalized;
                    Warp(p.AimPoint + away * 40f);
                    // 한가운데 근처에 적 고속정 하나(폭발에 휩쓸리는지)
                    var fast = FieldEventDirector.FindEnemy("ene_fastboat");
                    bystander = EnemySpawner.Instance.SpawnAt(fast, p.AimPoint + Quaternion.Euler(0f, -60f, 0f) * a.normalized * 7f, Quaternion.identity);
                    if (bystander != null) bystander.DevFrozen = true;
                }
                while (p != null && p.State != PincerAttack.Phase.Done && Time.realtimeSinceStartup - t0 < 12f) yield return null;
                bool swept = bystander == null || !bystander.IsAlive || bystander.CurrentHp < bystander.Definition.MaxHp;
                bool gained = xp.Xp + xp.Level * 100000 > xp0;
                _report.AppendLine($"- 협공(회피): 돌진 시작 {Yes(charging)} · 서로 충돌 {p?.Collided}척 · 기함 충돌 {p?.Hits} · 결과 {p?.Outcome} · 보너스 경험치 +{p?.BonusXp} · 둘레 적 휩쓸림 {Yes(swept)}");
                if (!charging || p == null || p.Outcome != PincerAttack.Result.Collided || p.Collided < 2 || p.Hits > 0)
                    Fail("협공을 피했는데 보트끼리 부딪혀 터지지 않음");
                if (p != null && (p.BonusXp <= 0 || !gained)) Fail("협공 회피 보너스 경험치가 들어오지 않음");
                if (!swept) Fail("협공 충돌 폭발이 둘레 적에게 피해를 주지 않음");
                yield return CloseRefit();
                CombatDevTools.ClearBattlefield();
            }

            // ---- 3) 위치 노출: 제자리면 차오르고 포격, 움직이면 줄어든다
            {
                Warp(home);
                ship.RepairHull(ship.HullMaxHp);
                float hp0 = ship.HullHp;
                var g = dir.Exposure;
                g.Begin(ship, 1);
                g.enabled = true;
                g.DevSkipGrace();
                g.DevFillScale = 8f;   // 14초 → 약 1.8초
                float t0 = Time.realtimeSinceStartup;
                bool panelSeen = false;
                while (g.Barrages == 0 && Time.realtimeSinceStartup - t0 < 6f)
                {
                    if (g.PanelText.Contains("위치 노출")) panelSeen = true;
                    yield return null;
                }
                yield return new WaitForSeconds(1.2f);
                yield return ScreenShot("field_exposure");
                t0 = Time.realtimeSinceStartup;
                while (g.ShellsLanded < ExposureGauge.Shells && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                bool hit = g.ShellHits >= 1 && ship.HullHp < hp0;
                // 움직이면: 30m씩 옮겨 다니는 동안 줄어든다
                g.DevFillScale = 1f;
                float before = g.Value;
                Vector3 p0 = ship.transform.position;
                for (int i = 0; i < 8; i++) { Warp(p0 + Vector3.right * (i + 1) * 30f); yield return new WaitForSeconds(0.25f); }
                float after = g.Value;
                g.DevFillScale = 1f;
                _report.AppendLine($"- 위치 노출: 막대 표시 {Yes(panelSeen)} · 포격 {g.Barrages}회 · 착탄 {g.ShellsLanded}/{ExposureGauge.Shells} · 기함 피격 {g.ShellHits} · 선체 {hp0:0} → {ship.HullHp:0} · 움직이면 {before * 100f:0}% → {after * 100f:0}%");
                if (g.Barrages == 0 || g.ShellsLanded < ExposureGauge.Shells) Fail("위치 노출 게이지가 차도 포격이 떨어지지 않음");
                if (!hit) Fail("제자리 기함에 노출 포격이 맞지 않음(첫 발은 예상 위치)");
                if (!panelSeen) Fail("위치 노출 막대가 보이지 않음");
                if (after > 0.3f) Fail("움직여도 위치 노출 게이지가 줄지 않음");
                g.enabled = false;
                Warp(home);
            }

            // ---- 4) 구조 활동
            {
                ship.ApplyHullDamage(ship.HullMaxHp * 0.5f, "검증");
                float hp0 = ship.HullHp;
                int lv0 = xp.Level, xp0 = xp.Xp;
                bool started = dir.StartEvent(FieldEventDirector.Kind.Rescue);
                var r = dir.Current as RescueMission;
                int rafts = r != null ? r.Rafts.Count : 0;
                float minD = float.MaxValue, maxD = 0f;
                if (r != null)
                    foreach (var raft in r.Rafts)
                    {
                        float d = Vector3.Distance(new Vector3(raft.Position.x, 0f, raft.Position.z), new Vector3(home.x, 0f, home.z));
                        minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
                    }
                bool marks = GameObject.Find("Rescue beacon 0") != null && GameObject.Find("Rescue ring 0") != null && GameObject.Find("Rescue guide") != null;
                yield return null;
                string text = r != null ? Strip(r.PanelText) : "";
                yield return ScreenShot("field_rescue");
                _report.AppendLine($"- 구조 활동: 시작 {Yes(started)} · 구명정 {rafts}척 · 거리 {minD:0}~{maxD:0}m · 기둥 · 원 · 안내선 {Yes(marks)} · \"{text}\"");
                if (!started || rafts < 2 || !marks) Fail("구조 활동이 구명정 · 표시를 띄우지 않음");
                if (r != null && rafts >= 2)
                {
                    Warp(r.Rafts[0].Position + Vector3.forward * 5f);
                    float t0 = Time.realtimeSinceStartup;
                    string holding = "";
                    while (r.RescuedCount == 0 && Time.realtimeSinceStartup - t0 < 6f)
                    {
                        if (holding == "" && r.Rafts[0].Hold > 0.5f) holding = Strip(r.PanelText);
                        yield return null;
                    }
                    yield return CloseRefit();
                    bool rescued = r.RescuedCount == 1 && ship.HullHp > hp0 && (xp.Level > lv0 || xp.Xp > xp0);
                    r.DevSetTimeLeft(1, 0.2f);
                    yield return new WaitForSeconds(0.5f);
                    bool lost = r.LostCount == 1;
                    for (int i = 2; i < rafts; i++)
                    {
                        Warp(r.Rafts[i].Position + Vector3.forward * 5f);
                        t0 = Time.realtimeSinceStartup;
                        while (!r.Rafts[i].Rescued && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                        yield return CloseRefit();
                    }
                    bool finished = r.State == RescueMission.Outcome.Finished && !r.AllRescuedBonus;
                    _report.AppendLine($"- 구조: 원 안에서 멈추면 {RescueMission.HoldTime:0}초 뒤 구조 {Yes(rescued)}(선체 {hp0:0} → 수리 · 경험치) · \"{holding}\" · 시간 끝 → 가라앉음 {Yes(lost)} · 끝 {r.State}(구조 {r.RescuedCount} · 잃음 {r.LostCount})");
                    if (!rescued) Fail("구조 원 안에서 멈춰도 구조 · 보상이 되지 않음");
                    if (!lost) Fail("저체온 시간이 끝난 구명정이 가라앉지 않음");
                    if (!finished) Fail("구명정이 모두 끝났는데 구조 활동이 끝나지 않음");
                }
                // 전원 구조 보너스: 다시 뽑기 +1
                Warp(home);
                Game.Refit.RefitDraft.SetRerolls(0);
                dir.StartEvent(FieldEventDirector.Kind.Rescue);
                var all = dir.Current as RescueMission;
                if (all != null)
                    for (int i = 0; i < all.Rafts.Count; i++)
                    {
                        Warp(all.Rafts[i].Position + Vector3.forward * 4f);
                        float t0 = Time.realtimeSinceStartup;
                        while (!all.Rafts[i].Rescued && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                        yield return CloseRefit();
                    }
                bool bonus = all != null && all.AllRescuedBonus && Game.Refit.RefitDraft.RerollsLeft == 1;
                _report.AppendLine($"- 전원 구조: 다시 뽑기 +1 {Yes(bonus)}");
                if (!bonus) Fail("구명정을 모두 구했는데 다시 뽑기 +1이 들어오지 않음");
                Warp(home);
                CombatDevTools.ClearBattlefield();
            }

            // ---- 5) 적 보급선 추격
            {
                ship.ApplyHullDamage(ship.HullMaxHp * 0.4f, "검증");
                float hp0 = ship.HullHp;
                int lv0 = xp.Level;
                bool mineDef = FieldEventDirector.FindEnemy("ene_sea_mine") != null;
                bool started = dir.StartEvent(FieldEventDirector.Kind.Runner);
                var u = dir.Current as RunnerMission;
                var runner = u != null ? u.Ship : null;
                bool hostile = runner != null && System.Linq.Enumerable.Contains(Game.Combat.TargetRegistry.HostileTo(Game.Combat.CombatFaction.Player, Game.Combat.TargetKind.Surface), runner);
                float d0 = runner != null ? Vector3.Distance(runner.transform.position, ship.transform.position) : 0f;
                yield return new WaitForSeconds(2f);
                float d1 = runner != null ? Vector3.Distance(runner.transform.position, ship.transform.position) : 0f;
                yield return ScreenShot("field_runner");
                string text = u != null ? Strip(u.PanelText) : "";
                // 기뢰: 뒤를 60m 거리로 따라가는 동안 떨군다
                float t0 = Time.realtimeSinceStartup;
                while (u != null && runner != null && u.MinesLaid == 0 && Time.realtimeSinceStartup - t0 < 7f)
                {
                    Warp(runner.transform.position - runner.transform.forward * 60f + runner.transform.right * 25f);
                    yield return new WaitForSeconds(0.2f);
                }
                // 기함 무장이 실제로 쏘아 맞히는지: 무장을 켜고 18m 옆에 붙어 8초
                foreach (var b in off) if (b != null) b.enabled = true;
                off.Clear();
                float hull0 = runner != null ? runner.Hull01 : 1f;
                t0 = Time.realtimeSinceStartup;
                while (runner != null && runner.IsAlive && runner.Hull01 >= hull0 && Time.realtimeSinceStartup - t0 < 8f)
                {
                    Warp(runner.transform.position + runner.transform.right * 18f);
                    yield return new WaitForSeconds(0.2f);
                }
                bool shot = runner != null && runner.Hull01 < hull0;
                _report.AppendLine($"- 적 보급선: 시작 {Yes(started)} · 적 수상 표적 {Yes(hostile)} · 거리 {d0:0}m → 2초 뒤 {d1:0}m(멀어짐 {Yes(d1 > d0)}) · 기뢰 {u?.MinesLaid}개(기뢰 정의 {Yes(mineDef)}) · 기함 무장 명중 {Yes(shot)}(선체 {hull0 * 100f:0}% → {(runner != null ? runner.Hull01 * 100f : 0f):0}%) · \"{text}\"");
                if (!started || !hostile) Fail("적 보급선이 나오지 않거나 적 표적이 아님");
                if (d1 <= d0) Fail("적 보급선이 기함에서 달아나지 않음");
                if (u == null || u.MinesLaid == 0) Fail("적 보급선이 기뢰를 떨구지 않음");
                if (!shot) Fail("기함 무장이 적 보급선을 쏘아 맞히지 않음");
                if (runner != null && runner.IsAlive) runner.TakeDamage(new Game.Combat.DamageInfo(runner.MaxHp * 2f, runner.transform.position, Vector3.down, Game.Combat.DamageSource.Gun, "검증"));
                t0 = Time.realtimeSinceStartup;
                while (u != null && u.State == RunnerMission.Outcome.Running && Time.realtimeSinceStartup - t0 < 3f) yield return null;
                yield return CloseRefit();
                bool sunk = u != null && u.State == RunnerMission.Outcome.Sunk;
                bool repaired = ship.HullHp > hp0;
                bool leveled = xp.Level > lv0;
                if (!sunk || !repaired || !leveled) _report.AppendLine($"  - 격침 {sunk} · 수리 {repaired}({hp0:0} → {ship.HullHp:0}) · 레벨 {leveled}");
                _report.AppendLine($"- 격침 보상: 결과 {u?.State} · 선체 수리 {Yes(repaired)} · 정비 1회(레벨 {lv0}→{xp.Level}, 경험치 +{u?.RewardXp})");
                if (!sunk || !repaired || !leveled) Fail("적 보급선을 격침해도 보상(수리 · 정비 1회)이 맞지 않음");

                Warp(home);
                CombatDevTools.ClearBattlefield();
                var esc = RunnerMission.Begin(ship, xp, 1, null, null, 60f);
                t0 = Time.realtimeSinceStartup;
                while (esc != null && esc.State == RunnerMission.Outcome.Running && Time.realtimeSinceStartup - t0 < 20f) { Warp(home); yield return null; }
                bool escaped = esc != null && esc.State == RunnerMission.Outcome.Escaped;
                _report.AppendLine($"- 도주: 항로 끝에 닿으면 실패 {Yes(escaped)}");
                if (!escaped) Fail("적 보급선이 항로 끝에 닿아도 도주(실패)로 끝나지 않음");
                CombatDevTools.ClearBattlefield();
            }

            // ---- 6) 진행: 보스 구간에는 협공만 · 스테이지 끝에 정리
            {
                Warp(home);
                FieldEventDirector.BeginStage(ship, xp, 2, () => true);
                dir.Exposure.enabled = false;
                dir.DevNoAuto = false;
                dir.DevSetNext(0f);
                yield return null;
                yield return null;
                var kind = dir.CurrentKind;
                dir.DevNoAuto = true;
                FieldEventDirector.BeginStage(ship, xp, 2, () => false);
                dir.Exposure.enabled = false;
                dir.StartEvent(FieldEventDirector.Kind.Rescue);
                FieldEventDirector.EndStage();
                yield return new WaitForSeconds(0.3f);
                bool cleaned = GameObject.Find("Rescue panel") == null && GameObject.Find("Rescue raft 0") == null && dir.Current == null && !dir.Active;
                _report.AppendLine($"- 보스 구간 이벤트 {kind} · 스테이지 끝 정리 {Yes(cleaned)}");
                if (kind != FieldEventDirector.Kind.Pincer) Fail("보스 구간에 협공이 아닌 이벤트를 냄");
                if (!cleaned) Fail("스테이지가 끝나도 전장 이벤트가 남음");
                CombatDevTools.ClearBattlefield();
            }
            foreach (var b in off) if (b != null) b.enabled = true;
            if (field != null) Game.World.IslandField.Disabled = false;
            ship.DevRudderOverride = null;
        }
    }
}
#endif
