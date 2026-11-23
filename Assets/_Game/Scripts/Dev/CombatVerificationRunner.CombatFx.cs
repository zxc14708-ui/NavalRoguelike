#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.World;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 전투 불빛 · 미사일 연기 · 물보라(2026-11-21 사용자: 야간 전투가 보이게, 연기 · 물보라를 사실적으로).</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator CombatFxCheck()
        {
            _report.AppendLine("\n## 전투 불빛 · 미사일 연기 · 물보라(2026-11-21)");
            var ship = GameManager.Instance.Player;
            CombatDevTools.ClearBattlefield();
            CombatDevTools.InstallTestLoadout();
            DayNight.Forced = DayPhase.Night; DayNight.Tick(0, 1, -1, 0f, 1f);
            yield return new WaitForSeconds(1f);
            _report.AppendLine($"- 밤: 어둠 {DayNight.Darkness01:0.00} · 불빛 세기 배율 {Game.View.CombatLights.NightBoost:0.00}(낮 0.30)");

            // 1) 포구 불빛: 적 고속정에 무장이 쏘게
            int mz = 0, tl = 0, tracers = 0;
            CombatDevTools.SpawnRing("ene_fastboat", 3, 26f, 40f);
            float until = Time.time + 6f;
            while (Time.time < until)
            {
                mz = Mathf.Max(mz, Game.View.CombatLights.LitMuzzle);
                tl = Mathf.Max(tl, Game.View.CombatLights.LitTracer);
                tracers = Mathf.Max(tracers, Game.View.CombatLights.Tracers);
                yield return null;
            }
            _report.AppendLine($"- 포구 불빛: 한꺼번에 켜진 최대 {mz}개(20개를 돌려 씀) · 예광탄 불빛 최대 {tl}개(빛나는 탄 최대 {tracers}발, {Game.View.CombatLights.TracerLights}개를 돌려 씀)");
            if (mz < 1) Fail("밤에 쏘는데 포구 불빛이 켜지지 않음");
            if (tl < 1) Fail("밤에 예광탄이 날아가는데 예광탄 불빛이 없음");
            yield return ScreenShot("fx_night_guns");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);

            // 2) 미사일: 흰 띠 대신 연기 뭉치 · 노즐 불꽃 · 추진 불빛
            int smoke0 = Game.View.DecorFx.Count(Game.View.DecorFx.MissileSmoke);
            int blastMax = 0;
            CombatDevTools.MissileVolley(6, 70f, 10f, 0.01f);
            int ex = 0, smoke = 0, emitters = 0; bool trailOff = true;
            until = Time.time + 2.2f;
            bool shot = false;
            while (Time.time < until)
            {
                ex = Mathf.Max(ex, Game.View.CombatLights.LitExhaust);
                emitters = Mathf.Max(emitters, Game.View.CombatLights.Emitters);
                smoke = Mathf.Max(smoke, Game.View.DecorFx.Count(Game.View.DecorFx.MissileSmoke));
                blastMax = Mathf.Max(blastMax, Game.View.DecorFx.Count(Game.View.DecorFx.BlastFire));
                foreach (var m in Object.FindObjectsByType<Missile>(FindObjectsSortMode.None))
                    foreach (var tr in m.GetComponentsInChildren<TrailRenderer>()) if (tr.enabled && tr.emitting) trailOff = false;
                if (!shot && Time.time > until - 1f)
                {
                    shot = true;
                    var mis = Object.FindObjectsByType<Missile>(FindObjectsSortMode.None).FirstOrDefault(m => m.isActiveAndEnabled);
                    if (mis != null) yield return CloseShot("fx_missile_trail_night", mis.transform, 14f);
                }
                yield return null;
            }
            _report.AppendLine($"- 미사일 6발: 추진 불빛 최대 {ex}개(꼬리 {emitters}) · 연기 뭉치 {smoke - smoke0}개 · 예전 흰 띠 꺼짐 {Yes(trailOff)} · 디졸브 불꽃(발사 · 노즐 — 폭발과 같은 질감) 최대 {blastMax}개");
            if (Game.View.DecorFx.BlastFire != null && blastMax < 6) Fail("미사일 발사 · 노즐 불꽃이 폭발과 같은 디졸브 불꽃을 쓰지 않음");
            if (ex < 1) Fail("밤에 날아가는 미사일에 추진 불빛이 없음");
            if (smoke - smoke0 < 30) Fail("미사일 연기 꼬리가 거의 없음");
            if (!trailOff) Fail("예전 흰 띠(TrailRenderer)가 아직 그려짐");
            yield return WaitMissilesGone(12f);

            // 3) 물보라: 달리면 물방울 줄기가 튄다
            DayNight.Forced = DayPhase.Day; DayNight.Tick(0, 1, -1, 0f, 1f);
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(4f);
            int drops = Game.View.DecorFx.Count(Game.View.DecorFx.Drops);
            _report.AppendLine($"- 물보라(전속): 물방울 {drops}개 · 물안개 · 물거품 {Game.View.DecorFx.Count(Game.View.DecorFx.Spray)}개");
            if (drops < 10) Fail("달리는데 뱃머리 물방울이 거의 없음");
            yield return CloseShot("fx_bow_spray", ship.transform, 12f);
            DayNight.Forced = DayPhase.Night; DayNight.Tick(0, 1, -1, 0f, 1f);
            yield return new WaitForSeconds(1f);
            yield return CloseShot("fx_bow_spray_night", ship.transform, 12f);
            ship.SetEngineOrder(0f);

            // 4) 폭발 다듬기: 연기 기둥 · 파편 · 수면 고리 · 물방울 왕관 · 공중 연기 뭉치
            yield return new WaitForSeconds(2f);
            var anchor = new GameObject("FxAnchor").transform;
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            int col0 = Game.View.Explosions.ColumnCount, ring0 = Game.View.Explosions.RingCount;
            int bs0 = Game.View.DecorFx.Count(Game.View.DecorFx.BlastSmoke), dr0 = Game.View.DecorFx.Count(Game.View.DecorFx.Drops);
            Vector3 boom = ship.transform.position + fwd * 30f + right * 6f; boom.y = 0.2f;
            Game.View.Explosions.Spawn(boom, 1.3f, Game.View.Explosions.Kind.Impact);
            Game.View.Explosions.Spawn(boom + right * 14f + fwd * 4f, 1.1f, Game.View.Explosions.Kind.Water);
            Game.View.Explosions.Spawn(boom - right * 12f + Vector3.up * 11f, 1f, Game.View.Explosions.Kind.Air);
            yield return null;
            int rings = Game.View.Explosions.ActiveRings;
            int dr = Game.View.DecorFx.Count(Game.View.DecorFx.Drops) - dr0;
            anchor.position = boom + right * 1f + Vector3.up * 2f;
            yield return new WaitForSeconds(0.25f);
            yield return CloseShot("fx_blast_night_early", anchor, 34f);
            yield return new WaitForSeconds(1.4f);
            int bs = Game.View.DecorFx.Count(Game.View.DecorFx.BlastSmoke) - bs0;
            yield return CloseShot("fx_blast_night_column", anchor, 34f);
            _report.AppendLine($"- 폭발(명중 · 수면 · 공중): 연기 기둥 {Game.View.Explosions.ColumnCount - col0} · 폭발 연기 {bs}개 · 수면 고리 {Game.View.Explosions.RingCount - ring0}(퍼지는 중 {rings}) · 물방울 {dr}개");
            if (Game.View.Explosions.ColumnCount - col0 < 1) Fail("함체 명중 폭발에 연기 기둥이 없음");
            if (Game.View.Explosions.RingCount - ring0 < 2 || rings < 1) Fail("수면 폭발에 충격파 고리가 없음");
            if (bs < 15) Fail("폭발 연기가 거의 없음");
            if (dr < 15) Fail("수면 폭발에 물방울 왕관이 거의 없음");

            DayNight.Forced = DayPhase.Day; DayNight.Tick(0, 1, -1, 0f, 1f);
            yield return new WaitForSeconds(3f);
            Game.View.Explosions.Spawn(boom, 1.3f, Game.View.Explosions.Kind.Impact);
            Game.View.Explosions.Spawn(boom + right * 14f + fwd * 4f, 1.1f, Game.View.Explosions.Kind.Water);
            Game.View.Explosions.Spawn(boom - right * 12f + Vector3.up * 11f, 1f, Game.View.Explosions.Kind.Air);
            yield return new WaitForSeconds(0.3f);
            yield return CloseShot("fx_blast_day_early", anchor, 34f);
            yield return new WaitForSeconds(1.6f);
            yield return CloseShot("fx_blast_day_column", anchor, 34f);
            Destroy(anchor.gameObject);

            // 5) 플레이어블 군함 항해등: 시작 함정마다 골라 보고 등 4개가 새 선체에 맞게 달렸는지(선체가 바뀌면 다시 단다)
            yield return PlayableNavLightCheck(ship);
            DayNight.Forced = null; DayNight.Reset();
        }

        private IEnumerator PlayableNavLightCheck(Game.Ship.ShipController ship)
        {
            var init = Object.FindFirstObjectByType<Game.Ship.ShipInitializer>();
            if (init == null || ship == null) { Fail("항해등 검사: 시작 함정 설치기 없음"); yield break; }
            bool wasLocked = init.SelectionLocked;
            init.DevSetSelectionLocked(false);   // 출항 뒤여도 검사에서는 시작 함정을 차례로 바꿔 본다(끝나면 원래대로)
            DayNight.Forced = DayPhase.Night; DayNight.Tick(0, 1, -1, 0f, 1f);
            var original = init.SelectedConcept;
            var sb = new System.Text.StringBuilder();
            int checkedShips = 0;
            // 현대 시작 함정 4척 + WWII 함정(구축함 · 순양함 · 전함 — WWII 규칙 집합으로 바꿔 고른다, 2026-11-21)
            var concepts = new System.Collections.Generic.List<Game.Data.StartingShipConcept>(Game.Data.StartingShipCatalog.All);
            var ww2 = Game.Core.Ruleset.Ww2Definition;
            if (ww2 != null) foreach (var h in ww2.Hulls) if (h != null) concepts.Add(h);
            string ruleset0 = Game.Core.Ruleset.Current;
            foreach (var concept in concepts)
            {
                if (concept == null) continue;
                Game.Core.Ruleset.EnterFor(concept);
                if (!init.SelectionLocked && !init.TrySelect(concept, out string why)) { sb.Append($"{concept.Title}(이 판에서 못 고름: {why}) · "); continue; }
                yield return null; yield return null; yield return null;   // 선체 다시 만들기 → 다음 프레임 항해등
                var set = ship.GetComponent<Game.View.NightLampSet>();
                var heads = new System.Collections.Generic.List<Transform>();
                // 2026-11-22: 등은 흔들리는 모델(ShipVisualSway) 아래에 붙는다 — 머리만(막대는 머리의 자식)
                foreach (var t in ship.GetComponentsInChildren<Transform>()) if (t.name == "Night lamp" && (t.parent == null || t.parent.name != "Night lamp")) heads.Add(t);
                var swayT = ship.GetComponentInChildren<Game.View.ShipVisualSway>();
                bool riding = heads.Count > 0 && swayT != null && heads.TrueForAll(h => h.IsChildOf(swayT.transform));
                float headSize = heads.Count > 0 ? heads[0].lossyScale.x : 0f;
                // 선체 폭(격자 칸 — 배 좌표) — 현측등이 뱃전 양 끝에 있어야 한다(헬기 날개 · 포신 밖 바다에 뜨면 안 됨)
                float beam = 0f;
                if (ship.Grid != null && ship.Grid.OccupiedCells.Count > 0)
                {
                    int zMin = int.MaxValue, zMax = int.MinValue;
                    foreach (var c in ship.Grid.OccupiedCells) { zMin = Mathf.Min(zMin, c.Z); zMax = Mathf.Max(zMax, c.Z); }
                    beam = (zMax - zMin + 1) * ship.Grid.CellSize;
                }
                float port = heads.Count >= 2 ? ship.transform.InverseTransformPoint(heads[0].position).x : 0f;
                float stbd = heads.Count >= 2 ? ship.transform.InverseTransformPoint(heads[1].position).x : 0f;
                bool placed = heads.Count == Game.View.NavLights.PerShip && beam > 0.5f && stbd - port >= beam - 0.1f && stbd - port <= beam + 2.4f;   // 뱃전은 격자보다 조금 벌어진다(한쪽 최대 1.2m)
                bool lit = set != null && set.Lit == Game.View.NavLights.PerShip;
                sb.Append($"{concept.Title} 등 {heads.Count}개 · 켜짐 {(set != null ? set.Lit : 0)} · 현측 간격 {stbd - port:0.0}m/격자 폭 {beam:0.0}m · 모델에 붙음 {Yes(riding)} · 머리 {headSize * 100f:0}cm · ");
                if (!placed || !lit) Fail($"플레이어블 군함 {concept.Title}: 항해등이 없거나 선체에 맞지 않음");
                if (swayT != null && !riding) Fail($"플레이어블 군함 {concept.Title}: 항해등이 흔들리는 모델에 붙지 않음(파도에 등만 떠 보임)");
                if (headSize > Game.View.NavLights.BulbSize * 0.6f) Fail($"플레이어블 군함 {concept.Title}: 항해등이 큼({headSize * 100f:0}cm)");
                checkedShips++;
                if (checkedShips <= 8) yield return CloseShot($"fx_navlight_{concept.Id}", ship.transform, 16f);
                if (init.SelectionLocked) break;
            }
            Game.Core.Ruleset.Enter(ruleset0);
            if (original != null) { init.TrySelect(original, out _); yield return null; yield return null; }
            init.DevSetSelectionLocked(wasLocked);
            _report.AppendLine($"- 플레이어블 항해등({checkedShips}척): {sb}");
            if (checkedShips == 0) Fail("플레이어블 군함 항해등을 하나도 확인하지 못함");
        }
    }
}
#endif
