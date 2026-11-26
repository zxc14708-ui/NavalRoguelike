using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Enemies;
using Game.World;

namespace Game.Dev
{
    /// <summary>
    /// 섬 끼임 검사(2026-11-26, -islandStuckOnly). 플레이어를 멈춰 두고 장면마다 적을 놓아,
    /// 스스로 열린 물길을 찾아 다시 공격 자리(플레이어가 보이고 교전 반경 × 1.5 안, 1초 유지)를 잡는지 본다.
    ///   A 섬 뒤: 플레이어와 적 사이에 섬 하나.
    ///   B 두 섬 사이: 좁은 틈을 둔 두 섬 너머.
    ///   C 오목한 해안: 섬 세 개로 만든 만 안에 적(앞이 막힘, 열린 쪽은 뒤).
    ///   D 섬에 붙은 플레이어: 선회 원이 섬을 지나간다.
    /// 적: 연안 경비정(1스테이지 보스) · 현대화 초계함(2스테이지 보스) · 초계함(일반).
    /// 합격: 공격 자리 10초 안(막다른 만 C는 돌아 나오는 열린 물길이 약 115m라 26초) · 2초 넘게 멈춘 적 없음(처음 1초 뺌) · 배 중심이 섬 안에 들어간 프레임 0.
    /// 같은 장면을 우회 없이(예전 동작) 한 번 더 돌려 비교로 적는다(실패로 세지 않음).
    /// </summary>
    public partial class CombatVerificationRunner
    {
        public bool IslandStuckOnly;

        private IEnumerator IslandStuckCheck()
        {
            _report.AppendLine("\n## 섬 끼임 · 우회");
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var field = IslandField.Instance;
            if (field == null) { Fail("IslandField 없음"); yield break; }
            IslandField.Disabled = true;
            field.ClearAll();
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(1.5f);

            string[] enemies = { "ene_boss", "ene_boss_corvette", "ene_pcc_corvette" };
            string[] scenes = { "A 섬 뒤", "B 두 섬 사이", "C 오목한 해안", "D 섬에 붙은 플레이어" };
            for (int s = 0; s < scenes.Length; s++)
            {
                foreach (var id in enemies)
                {
                    var def = CombatDevTools.FindEnemy(id);
                    if (def == null) { _report.AppendLine($"- {id}: 정의 없음(건너뜀)"); continue; }
                    for (int pass = 0; pass < 2; pass++)
                    {
                        bool legacy = pass == 1;
                        if (legacy && s == 3) continue;   // D는 비교 생략
                        EnemyController.DevDetourOff = legacy;
                        var result = new float[4];
                        yield return RunStuckScene(s, def, ship, field, result, legacy);
                        EnemyController.DevDetourOff = false;
                        float seen = result[0], stall = result[1], inside = result[2], detours = result[3];
                        string seenTxt = seen < 0f ? "못 잡음(30초)" : $"{seen:0.0}초";
                        if (legacy)
                        {
                            _report.AppendLine($"  - (비교: 예전 동작) 공격 자리 {seenTxt} · 가장 길게 멈춤 {stall:0.0}초 · 섬 안 프레임 {inside:0}");
                            continue;
                        }
                        _report.AppendLine($"- {scenes[s]} · {def.DisplayName}({id}): 공격 자리 {seenTxt} · 가장 길게 멈춤 {stall:0.0}초 · 섬 안 프레임 {inside:0} · 우회 {detours:0}번");
                        // 막다른 만: 배 폭보다 넓은 가장 짧은 열린 물길이 만 뒤로 나가 옆섬을 돌아오는 약 115m(초속 6~8m면 15~19초 + 만 안 U턴)
                        float limit = s == 2 ? 26f : 10f;
                        if (seen < 0f || seen > limit) Fail($"{scenes[s]}: {def.DisplayName}가 {limit:0}초 안에 공격 자리를 다시 잡지 못함");
                        if (stall > 2f) Fail($"{scenes[s]}: {def.DisplayName}가 {stall:0.0}초 멈춤(2초 넘음)");
                        if (inside > 0f) Fail($"{scenes[s]}: {def.DisplayName}가 섬 안으로 들어감");
                    }
                }
            }

            // 정리
            EnemyController.DevDetourOff = false;
            ship.DevRudderOverride = null;
            foreach (var w in off) if (w != null) w.enabled = true;
            CombatDevTools.ClearBattlefield();
            field.ClearAll();
            IslandField.Disabled = false;
            field.Regenerate(0);
        }

        /// <summary>장면 하나: 섬을 놓고 적을 놓은 뒤 25초까지 잰다. result = [공격 자리 초(-1 = 못 잡음), 가장 긴 멈춤 초, 섬 안 프레임, 우회 횟수].</summary>
        private IEnumerator RunStuckScene(int scene, Game.Data.EnemyDefinition def, Game.Ship.ShipController ship, IslandField field, float[] result, bool legacy)
        {
            CombatDevTools.ClearBattlefield();
            field.ClearAll();
            ship.RestoreHull(1f);
            yield return null;
            Vector3 o = ship.transform.position; o.y = 0f;
            Vector3 f = ship.transform.forward; f.y = 0f; f.Normalize();
            Vector3 rt = Vector3.Cross(Vector3.up, f);
            // 섬 반경을 먼저 잰다(멀리 하나 놓고 지움)
            var probe = field.PlaceArt(o + f * 400f, "ENV_PineIsland", 4);
            float R = probe != null ? probe.Radius : 15f;
            field.ClearAll();
            yield return null;

            Vector3 spawn; Vector3 face;
            switch (scene)
            {
                case 0:   // 섬 뒤
                    field.PlaceArt(o + f * (R + 14f), "ENV_PineIsland", 4);
                    spawn = o + f * (2f * R + 34f); face = -f;
                    break;
                case 1:   // 두 섬 사이(틈 약 6m)
                    field.PlaceArt(o + f * (R + 16f) + rt * (R + 3f), "ENV_PineIsland", 4);
                    field.PlaceArt(o + f * (R + 16f) - rt * (R + 3f), "ENV_PineIsland", 5);
                    spawn = o + f * (2f * R + 40f); face = -f;
                    break;
                case 2:   // 오목한 해안: 앞(플레이어 쪽) · 양옆이 막힌 만 안
                {
                    float d = R + 16f;
                    field.PlaceArt(o + f * d, "ENV_PineIsland", 4);
                    field.PlaceArt(o + f * (d + R * 1.3f) + rt * (R * 1.7f), "ENV_PineIsland", 5);
                    field.PlaceArt(o + f * (d + R * 1.3f) - rt * (R * 1.7f), "ENV_PineIsland", 6);
                    spawn = o + f * (d + R * 1.9f); face = -f;   // 만 안쪽(앞섬 기슭에 선수가 닿지 않게)
                    break;
                }
                default:  // 섬에 붙은 플레이어
                    field.PlaceArt(o + rt * (R + 7f), "ENV_PineIsland", 4);
                    spawn = o - rt * 55f; face = rt;
                    break;
            }
            yield return null;
            Physics.SyncTransforms();
            Random.InitState(9100 + scene * 31 + def.Id.Length);   // 같은 장면은 같은 선회 방향(예전 동작과 공정하게 비교)
            var e = EnemySpawner.Instance.SpawnAt(def, spawn, Quaternion.LookRotation(face, Vector3.up));
            if (e == null) { result[0] = -1f; yield break; }
            float radius = Mathf.Max(def.PreferredRange, 8f);
            float t0 = Time.time, seenAt = -1f, seenSince = -1f, stillSince = -1f, longestStill = 0f;
            int inside = 0;
            float sampleAt = Time.time;
            Vector3 samplePos = spawn; samplePos.y = 0f;
            while (Time.time - t0 < 30f && e != null && e.IsAlive)
            {
                yield return null;
                ship.RestoreHull(1f);
                Vector3 p = e.transform.position; p.y = 0f;
                float dist = Vector3.Distance(p, o);
                bool visible = !Islands.BlocksShipLine(p, o) && dist <= radius * 1.5f + 6f;
                if (visible) { if (seenSince < 0f) seenSince = Time.time; if (seenAt < 0f && Time.time - seenSince >= 1f) seenAt = seenSince - t0; }
                else seenSince = -1f;
                // 멈춤 = 실제로 움직인 거리(1초에 최고 속력의 15% 미만) — 속력만 보면 틈에 끼어 밀려나며 제자리인 배를 못 잡았다
                if (Time.time - sampleAt >= 1f)
                {
                    bool still = Vector3.Distance(p, samplePos) < def.MoveSpeed * 0.15f * (Time.time - sampleAt) && Time.time - t0 > 1f;
                    if (still) { if (stillSince < 0f) stillSince = sampleAt; longestStill = Mathf.Max(longestStill, Time.time - stillSince); }
                    else stillSince = -1f;
                    samplePos = p; sampleAt = Time.time;
                }
                if (!Islands.IsClear(p, 0.4f)) inside++;
                if (seenAt >= 0f && Time.time - t0 > seenAt + 3f) break;   // 잡은 뒤 3초 더 보고 끝(멈춤 · 박힘 확인)
            }
            if (scene == 2 && !legacy && e != null) yield return CloseShot($"island_detour_{def.Id}", e.transform, 60f);
            result[0] = seenAt; result[1] = longestStill; result[2] = inside; result[3] = e != null ? e.DevDetours : 0;
            if (e != null) e.RemoveWithoutReward();
        }
    }
}
