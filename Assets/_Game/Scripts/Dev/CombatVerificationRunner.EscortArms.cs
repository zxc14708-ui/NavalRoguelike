#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.TaskForce;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 호위함 실제 무장 · Codex v17 대잠·미사일 호위함 모델(2026-10-11).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 방공·미사일·대잠 호위함을 T3까지 개량하고(모델 v17), 포탑 수를 센 뒤 실제로 쏘는지 본다:
        ///   1) 고속정·초계함 무리 → 미사일 호위함 함포(포탑마다)·대함 미사일, 다른 호위함 함포도 쏨 · 적 피해
        ///   2) 적 미사일 일제사격 → 방공 호위함 함대공 미사일(VLS·SAM)·CIWS 탄막
        ///   3) 잠수함 → 대잠 호위함 헬기 출격·경어뢰·대잠 로켓 · 잠수함 피해
        /// </summary>
        private IEnumerator EscortArmsCheck()
        {
            _report.AppendLine("\n## 호위함 실제 무장 · v17 모델");
            var ship = GameManager.Instance.Player;
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = TimeScale;
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            var off = DisableWeapons(null);
            var formation = Object.FindFirstObjectByType<TaskForceEscortFormation>();
            if (formation == null) { Fail("편대 없음"); yield break; }
            _report.AppendLine($"- 무장 데이터(EscortArmory): {Yes(EscortArmory.Instance != null)}");
            if (EscortArmory.Instance == null) { Fail("EscortArmory 없음"); yield break; }

            formation.ClearAll();
            yield return null;
            var roles = new[] { EscortRole.AirDefense, EscortRole.SurfaceStrike, EscortRole.AntiSubmarine };
            for (int i = 0; i < roles.Length; i++)
            {
                int idx = formation.Deploy();
                formation.AssignRole(idx, roles[i]);
                for (int k = 0; k < 3; k++) formation.Upgrade(idx);
            }
            formation.SetFormation(FleetFormation.Circular);
            yield return new WaitForSeconds(3f);

            EscortDefense Def(EscortRole r) { var c = formation.GetEscort(r); return c != null ? c.GetComponentInChildren<EscortDefense>() ?? c.GetComponent<EscortDefense>() : null; }
            var cap = Def(EscortRole.AirDefense); var stk = Def(EscortRole.SurfaceStrike); var asw = Def(EscortRole.AntiSubmarine);
            foreach (var (r, d) in new[] { (EscortRole.AirDefense, cap), (EscortRole.SurfaceStrike, stk), (EscortRole.AntiSubmarine, asw) })
            {
                var rig = d != null ? d.GetComponentInChildren<EscortTurrets>() : null;
                var c = formation.GetEscort(r);
                _report.AppendLine($"- {r}: 모델 {EscortModelKey(c)} · 함포 {rig?.Mounts(EscortTurrets.MountKind.Gun)}(기관총 {CountLight(rig)}) · CIWS {rig?.Mounts(EscortTurrets.MountKind.Ciws)} · 함대공 {rig?.Mounts(EscortTurrets.MountKind.Sam)} · 대함 {rig?.Mounts(EscortTurrets.MountKind.Strike)}(발사점 {rig?.Sockets("LaunchPoint_MissileBank").Count}) · 대잠 로켓 {rig?.Mounts(EscortTurrets.MountKind.Asw)} · 헬기 갑판 {Yes(rig != null && rig.TrySocket("HelicopterLaunch", out _))}");
                if (d == null || rig == null) Fail($"{r} 호위함·포탑 없음");
            }
            if (cap == null || stk == null || asw == null) yield break;
            foreach (var d in new[] { cap, stk, asw }) d.ResetCooldowns();

            // ---- 1) 수상전
            {
                var boats = new List<EnemyController>();
                Vector3 c = stk.transform.position; Vector3 f = (c - ship.transform.position); f.y = 0f; f = f.sqrMagnitude > 1f ? f.normalized : ship.transform.forward;
                foreach (float a in new[] { -25f, 0f, 25f })
                {
                    var p = c + Quaternion.Euler(0f, a, 0f) * f * 26f; p.y = 0f;
                    var e = EnemySpawner.Instance.SpawnAt(CombatDevTools.FindEnemy("ene_pcc_corvette") ?? CombatDevTools.FindEnemy("ene_fastboat"), p, Quaternion.LookRotation(-f));
                    if (e != null) { e.DevFrozen = true; e.transform.position = p; boats.Add(e); }
                }
                var hp0 = new List<float>(); foreach (var b in boats) hp0.Add(b.CurrentHp);
                int g0 = stk.GunShots, s0 = stk.StrikeLaunches, cg0 = cap.GunShots, ag0 = asw.GunShots;
                // 고정 발사관 · 직사(2026-10-27): 발사관 피벗이 돌지 않고, 대함 미사일이 솟구치지 않고 낮게 곧장 간다. 터지는 순간을 찍는다
                var banks = new List<(Transform t, Quaternion rest)>();
                foreach (var tr in stk.GetComponentsInChildren<Transform>(true))
                    if (tr.name.StartsWith("MissileBank") && tr.name.EndsWith("Pivot")) banks.Add((tr, tr.localRotation));
                float maxAlt = 0f, bankTurn = 0f; int seen = 0; bool blastShot = false;
                var flying = new Dictionary<Missile, Vector3>();
                bool closeShot = false;
                for (float t = 0f; t < 10f; t += Time.deltaTime)
                {
                    foreach (var b in banks) bankTurn = Mathf.Max(bankTurn, Quaternion.Angle(b.rest, b.t.localRotation));
                    foreach (var m in Object.FindObjectsByType<Missile>(FindObjectsSortMode.None))
                    {
                        if (!m.isActiveAndEnabled || m.StatsKey != Game.TaskForce.EscortDefense.EscortKey) continue;
                        if (!flying.ContainsKey(m)) seen++;
                        flying[m] = m.transform.position;
                        maxAlt = Mathf.Max(maxAlt, m.transform.position.y);
                    }
                    // 방금 사라진 대함 미사일 = 터진 자리 — 그 순간을 찍는다(한 번)
                    if (!blastShot)
                        foreach (var kv in flying)
                            if (kv.Key == null || !kv.Key.isActiveAndEnabled)
                            {
                                blastShot = true;
                                var at = new GameObject("BlastShotTarget").transform; at.position = kv.Value;
                                yield return CloseShot("escortarms_missile_blast", at, 12f);
                                yield return ScreenShot("escortarms_missile_blast_game");
                                Object.Destroy(at.gameObject);
                                break;
                            }
                    var gone = new List<Missile>(); foreach (var kv in flying) if (kv.Key == null || !kv.Key.isActiveAndEnabled) gone.Add(kv.Key);
                    foreach (var g in gone) flying.Remove(g);
                    if (!closeShot && t >= 4f) { closeShot = true; yield return CloseShot("escortarms_strike", stk.transform, 14f); }
                    yield return null;
                }
                _report.AppendLine($"- 대함 미사일(고정 발사관 · 직사): 관측 {seen}발 · 최고 고도 {maxAlt:0.0} m · 발사관 피벗 회전 {bankTurn:0.0}° · 폭발 사진 {Yes(blastShot)}");
                if (banks.Count == 0) Fail("미사일 호위함 발사관 피벗(MissileBank*Pivot)을 찾지 못함");
                if (bankTurn > 0.5f) Fail("미사일 호위함 발사관이 표적 쪽으로 돈다(고정이어야 함)");
                if (seen > 0 && maxAlt > 4f) Fail("미사일 호위함 대함 미사일이 높이 솟는다(직사여야 함)");
                float lost = 0f; for (int i = 0; i < boats.Count; i++) lost += hp0[i] - (boats[i] != null && boats[i].IsAlive ? boats[i].CurrentHp : 0f);
                _report.AppendLine($"- 수상전 10초: 미사일 호위함 함포 {stk.GunShots - g0}발 · 대함 미사일 {stk.StrikeLaunches - s0}발 · 방공 함포 {cap.GunShots - cg0} · 대잠 함포 {asw.GunShots - ag0} · 적 피해 {lost:0}");
                if (stk.GunShots - g0 < 3 || stk.StrikeLaunches - s0 < 2 || lost <= 0f) Fail("미사일 호위함이 함포·대함 미사일을 실제로 쏘지 않음");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2f);
            }

            // ---- 2) 방공
            {
                foreach (var d in new[] { cap, stk, asw }) d.ResetCooldowns();
                int sam0 = cap.SamLaunches, ciws0 = cap.CiwsRounds;
                int fired = CombatDevTools.MissileVolley(6, 50f, 20f, 4f);
                float t0 = Time.time;
                bool shot = false;
                while (Time.time - t0 < 10f)
                {
                    ship.RepairHull(ship.HullMaxHp);
                    if (!shot && cap.SamLaunches > sam0) { shot = true; yield return CloseShot("escortarms_sam", cap.transform, 14f); }
                    yield return null;
                }
                _report.AppendLine($"- 방공 10초: 적 미사일 {fired}발 → 함대공 미사일 {cap.SamLaunches - sam0}발 · CIWS {cap.CiwsRounds - ciws0}발");
                if (cap.SamLaunches - sam0 < 1) Fail("방공 호위함이 함대공 미사일을 쏘지 않음");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2f);
            }

            // ---- 3) 대잠
            {
                foreach (var d in new[] { cap, stk, asw }) d.ResetCooldowns();
                Vector3 c = asw.transform.position; Vector3 f = (c - ship.transform.position); f.y = 0f; f = f.sqrMagnitude > 1f ? f.normalized : -ship.transform.forward;
                var subDef = CombatDevTools.FindEnemy("ene_submarine");
                Vector3 sp = c + f * 26f; sp.y = 0f;
                var sub = subDef != null ? EnemySpawner.Instance.SpawnAt(subDef, sp, Quaternion.LookRotation(f)) as SubmarineBase : null;
                if (sub != null) { sub.DevFrozen = true; sub.transform.position = new Vector3(sp.x, sub.transform.position.y, sp.z); }
                float hp0 = sub != null ? sub.CurrentHp : 0f;
                int tor0 = asw.TorpedoLaunches, rk0 = asw.RocketLaunches, so0 = asw.Helideck != null ? asw.Helideck.Sorties : 0;
                float t0 = Time.time;
                bool shot = false;
                while (Time.time - t0 < 22f)
                {
                    if (!shot && asw.Helideck != null && asw.Helideck.Airborne && Time.time - t0 > 3f) { shot = true; yield return CloseShot("escortarms_helo", asw.transform, 16f); }
                    yield return null;
                }
                int sorties = (asw.Helideck != null ? asw.Helideck.Sorties : 0) - so0;
                float lost = sub != null ? hp0 - (sub.IsAlive ? sub.CurrentHp : 0f) : 0f;
                _report.AppendLine($"- 대잠 22초: 헬기 출격 {sorties} · 경어뢰 {asw.TorpedoLaunches - tor0} · 대잠 로켓 {asw.RocketLaunches - rk0} · 잠수함 피해 {lost:0}");
                if (sub == null) Fail("잠수함 생성 실패");
                else if (sorties < 1 || asw.TorpedoLaunches - tor0 < 1 || lost <= 0f) Fail("대잠 호위함이 헬기·경어뢰를 실제로 쓰지 않음");
                CombatDevTools.ClearBattlefield();
            }

            foreach (var b in off) if (b != null) b.enabled = true;
            if (field != null) Game.World.IslandField.Disabled = false;
            ship.DevRudderOverride = null;
        }

        private static string EscortModelKey(Component c)
        {
            var key = c != null ? Get<string>(c, "_modelKey") : null;
            return string.IsNullOrEmpty(key) ? "(회색박스)" : key;
        }

        private static int CountLight(EscortTurrets rig)
        {
            if (rig == null) return 0;
            int n = 0;
            for (int i = 0; i < rig.Mounts(EscortTurrets.MountKind.Gun); i++) if (rig.IsLight(EscortTurrets.MountKind.Gun, i)) n++;
            return n;
        }
    }
}
#endif
