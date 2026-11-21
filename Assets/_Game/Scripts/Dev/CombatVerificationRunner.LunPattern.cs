#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.View;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 룬급 통과 공격 다양화 · 적 추적(2026-11-20).</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator LunPatternCheck()
        {
            _report.AppendLine("\n## 룬급 통과 공격 · 적 추적(2026-11-20)");
            var ship = GameManager.Instance.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            if (director != null) director.enabled = false;
            EnemySpawner.Instance.enabled = false;
            CoastalBatteryField.Disabled = true;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            // 기함 무장은 멈춘다(레이더만) — 보스 체력이 깎여 단계가 넘어가지 않게
            var modules = ship.GetComponentsInChildren<Game.Modules.ModuleRuntime>(true);
            var wasOn = new bool[modules.Length];
            for (int i = 0; i < modules.Length; i++) { wasOn[i] = modules[i].enabled; modules[i].enabled = modules[i] is Game.Modules.Runtime.IRadarSource; }
            yield return null;

            var list = CombatDevTools.SpawnRing("ene_boss4", 1, 60f, 0f);
            var lun = list.Count > 0 ? list[0] as LunBoss : null;
            if (lun == null) { Fail("룬급 보스를 만들지 못함"); yield break; }
            Vector3 f = ship.transform.forward; f.y = 0f; f.Normalize();

            // 착탄 자리 모으기(폭탄 · 로켓 — ArtilleryShell.Landed)
            var impacts = new List<Vector3>();
            int hits = 0;
            void OnLanded(ArtilleryShell s, bool hit) { impacts.Add(s.transform.position); if (hit) hits++; }
            ArtilleryShell.Landed += OnLanded;

            // ---- 1) 폭격 통과
            lun.DevForcePass(LunBoss.PassKind.Bomb);
            float t0 = Time.time; bool shot = false;
            while (Time.time - t0 < 35f && lun.IsAlive && !(lun.CurrentPass == LunBoss.PassKind.Bomb && lun.DevBombs > 0 && lun.StateName == "Extend"))
            {
                lun.DevSkipWait();
                ship.RepairHull(ship.HullMaxHp);
                if (!shot && lun.DevBombs >= 4) { shot = true; yield return AerialShot("lun_bomb_run", ship.transform.position, f, 70f, 55f); }
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);
            float nearest = float.MaxValue;
            foreach (var p in impacts) nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(ship.transform.position.x, ship.transform.position.z)));
            _report.AppendLine($"- 폭격 통과: 폭탄 {lun.DevBombs}발(기대 8) · 착탄 {impacts.Count} · 기함과 가장 가까운 착탄 {nearest:0.0} m · 기함 명중 {hits} · {Time.time - t0:0.0}초");
            if (lun.DevBombs < 8) Fail("룬급이 폭격 통과에서 폭탄을 다 떨구지 않음");
            if (nearest > 9f) Fail("폭탄 줄이 기함 근처를 지나지 않음");

            // ---- 2) 로켓 통과
            impacts.Clear(); hits = 0;
            lun.DevForcePass(LunBoss.PassKind.Rocket);
            t0 = Time.time; shot = false;
            int rockets0 = lun.DevRockets;
            while (Time.time - t0 < 35f && lun.IsAlive && lun.DevRockets - rockets0 < 10)
            {
                lun.DevSkipWait();
                ship.RepairHull(ship.HullMaxHp);
                if (!shot && lun.DevRockets - rockets0 >= 5) { shot = true; yield return AerialShot("lun_rockets", ship.transform.position, f, 60f, 50f); }
                yield return null;
            }
            yield return new WaitForSeconds(1.6f);
            float spread = 0f;
            foreach (var p in impacts) spread = Mathf.Max(spread, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(ship.transform.position.x, ship.transform.position.z)));
            _report.AppendLine($"- 로켓 통과: 로켓 {lun.DevRockets - rockets0}발(기대 10) · 착탄 {impacts.Count} · 기함에서 가장 먼 착탄 {spread:0.0} m · 기함 명중 {hits}");
            if (lun.DevRockets - rockets0 < 10) Fail("룬급이 로켓을 다 쏘지 않음");
            // 2026-11-21 Codex 룬급 폭탄 · 로켓 모델(PRJ_LunBomb · PRJ_LunRocket)이 붙었나
            int dressedBombs = 0, dressedRockets = 0;
            foreach (var art in Object.FindObjectsByType<Game.View.LunOrdnanceArt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (art.Dressed) { if (art.name.StartsWith("PRJ_LunRocket")) dressedRockets++; else dressedBombs++; }
            _report.AppendLine($"- 폭탄 · 로켓 모델: 모델 붙은 폭탄 {dressedBombs} · 로켓 {dressedRockets}(풀 인스턴스 수)");
            if (dressedBombs == 0 || dressedRockets == 0) Fail("룬급 폭탄 · 로켓에 Codex 모델이 붙지 않음");
            if (impacts.Count > 0 && spread > 25f) Fail("로켓이 기함 둘레가 아닌 먼 곳에 떨어짐");
            ArtilleryShell.Landed -= OnLanded;

            // ---- 3) 원거리 대함 미사일(멀리 돌 때)
            int standoff0 = lun.DevStandoff, moskit0 = lun.DevMissiles, ammo0 = lun.Ammo;
            t0 = Time.time;
            while (Time.time - t0 < 30f && lun.IsAlive && lun.DevStandoff - standoff0 < 2)
            {
                if (lun.StateName == "Extend") lun.DevStandoffNow();
                ship.RepairHull(ship.HullMaxHp);
                yield return null;
            }
            float standoffDist = Vector3.Distance(lun.transform.position, ship.transform.position);
            _report.AppendLine($"- 원거리 대함 미사일: {lun.DevStandoff - standoff0}발(기대 2) · 쏠 때 거리 {standoffDist:0} m · 모스킷 탄수 {ammo0} → {lun.Ammo}(그대로여야)");
            if (lun.DevStandoff - standoff0 < 2) Fail("룬급이 원거리 대함 미사일을 쏘지 않음");
            if (lun.Ammo != ammo0 && lun.DevMissiles == moskit0) Fail("원거리 미사일이 모스킷 탄수를 씀");

            // ---- 4) 순서: 미사일 → 폭격 → 로켓
            lun.DevSetAmmo(6);   // 모스킷이 떨어지면 미사일 통과가 폭격으로 바뀐다 — 순서만 보려고 채운다
            var seen = new List<LunBoss.PassKind>();
            int passes0 = lun.DevPasses;
            t0 = Time.time;
            LunBoss.PassKind last = (LunBoss.PassKind)(-1);
            while (Time.time - t0 < 60f && lun.IsAlive && seen.Count < 3)
            {
                lun.DevSkipWait();
                ship.RepairHull(ship.HullMaxHp);
                if (lun.StateName == "Pass" && (seen.Count == 0 || lun.DevPasses != passes0) && lun.CurrentPass != last)
                {
                    seen.Add(lun.CurrentPass); last = lun.CurrentPass; passes0 = lun.DevPasses;
                }
                yield return null;
            }
            _report.AppendLine($"- 통과 순서: {string.Join(" → ", seen)}");
            if (seen.Count < 3 || new HashSet<LunBoss.PassKind>(seen).Count < 3) Fail("통과 공격이 세 가지로 바뀌지 않음");

            // ---- 5) 적 추적: [Z]처럼 Toggle — 마우스 아래 적이 없으면 보스
            var cam = Camera.main;
            var tracker = cam != null ? cam.GetComponent<EnemyTracker>() : null;
            var qv = cam != null ? cam.GetComponent<QuarterViewCamera>() : null;
            if (tracker == null || qv == null) { Fail("카메라에 적 추적이 없음"); }
            else
            {
                // 추적 없이 / 추적하며 6초씩 화면 안에 보이는 비율
                EnemyTracker.Stop();
                float inView0 = 0f, inView1 = 0f, frames0 = 0f, near1 = 0f, maxShift = 0f, maxExtra = 0f;
                t0 = Time.time;
                while (Time.time - t0 < 6f)
                {
                    ship.RepairHull(ship.HullMaxHp);
                    if (Vector3.Distance(Flat(lun.transform.position), Flat(ship.transform.position)) <= 150f) { frames0++; if (InView(cam, lun.transform.position)) inView0++; }
                    yield return null;
                }
                tracker.Toggle();
                bool picked = EnemyTracker.Target == lun;
                t0 = Time.time; shot = false;
                while (Time.time - t0 < 6f)
                {
                    ship.RepairHull(ship.HullMaxHp);
                    float sepNow = Vector3.Distance(Flat(lun.transform.position), Flat(ship.transform.position));
                    if (sepNow <= 150f) { near1++; if (InView(cam, lun.transform.position)) inView1++; }
                    maxShift = Mathf.Max(maxShift, qv.TrackShift); maxExtra = Mathf.Max(maxExtra, qv.TrackExtraDistance);
                    if (!shot && Time.time - t0 > 2.5f) { shot = true; yield return ScreenShot("lun_tracking"); }
                    yield return null;
                }
                float r0 = inView0 / Mathf.Max(1f, frames0), r1 = inView1 / Mathf.Max(1f, near1);
                bool shipInView = InView(cam, ship.transform.position);
                tracker.Toggle();
                bool released = EnemyTracker.Target == null;
                _report.AppendLine($"- 적 추적: 고른 표적 룬급 {Yes(picked)} · 150m 안일 때 화면 안에 보인 비율 추적 전 {r0:P0} → 추적 중 {r1:P0} · 초점 이동 최대 {maxShift:0} m · 거리 늘림 최대 {maxExtra:0} m · 추적 중 기함도 화면 안 {Yes(shipInView)} · 다시 누르면 해제 {Yes(released)}");
                if (!picked) Fail("추적 키가 보스를 고르지 않음");
                if (near1 > 0f && r1 < 0.85f) Fail("추적해도 보스가 화면 안에 잘 들어오지 않음");
                if (!shipInView) Fail("추적 중 기함이 화면 밖으로 나감");
                if (!released) Fail("다시 눌러도 추적이 풀리지 않음");
            }

            for (int i = 0; i < modules.Length; i++) if (modules[i] != null) modules[i].enabled = wasOn[i];
            lun.RemoveWithoutReward();
            CombatDevTools.ClearBattlefield();
            CoastalBatteryField.Disabled = false;
            ship.DevRudderOverride = null;
        }

        private static bool InView(Camera cam, Vector3 world)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;
        }
    }
}
#endif
