#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.World;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// -harborBossOnly: 2스테이지 보스전 무대 = 수로 출구 적 군항(2026-11-15).
        ///   1) 보스 구간 시작 → 군항이 열림(만 폭 · 안벽 경계 · 군항 외형 · 기슭 다시 만듦) · 보스는 아직 없음 · 새 적 멈춤
        ///   2) 기함이 만에 들어서면 보스 · 호위 3 · 해안포대 2 출항, 새 적 재개
        ///   3) 만 안 전투 30초: 보스가 경계에 거의 막히지 않고 공격을 이어 간다
        ///   4) 예전 문제 재현: 좁은 수로에서 기함이 기슭 가까이 있을 때도 보스가 멈추지 않는다
        ///   5) 시간 초과 출항(만에 들어오지 않으면 60초 뒤)
        /// </summary>
        public bool HarborBossOnly;

        private IEnumerator HarborBossCheck()
        {
            _report.AppendLine("\n## 2스테이지 보스전 무대 — 수로 출구 적 군항");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            var rb = ship.GetComponent<Rigidbody>();
            void Warp(Vector3 p, Vector3 fwd)
            {
                p.y = ship.transform.position.y;
                var rot = Quaternion.LookRotation(fwd, Vector3.up);
                if (rb != null) { rb.position = p; rb.rotation = rot; }
                ship.transform.SetPositionAndRotation(p, rot);
            }
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);

            var director = Object.FindFirstObjectByType<StageDirector>();
            RoundSet.Round bossRound = default;
            bool found = false;
            if (director != null && director.Stages != null && director.Stages.Count > 1 && director.Stages[1] != null)
                for (int i = 0; i < director.Stages[1].Count; i++)
                {
                    var r = director.Stages[1].Get(i);
                    if (r.Boss != null) { bossRound = r; found = true; }
                }
            if (!found) { Fail("2스테이지 보스 구간을 찾지 못함"); yield break; }

            SeaRegion.Enter(SeaKind.Strait, ship.transform.position, ship.transform.forward);
            yield return new WaitForSecondsRealtime(1.5f);
            int banksBefore = IslandField.Instance != null ? IslandField.Instance.BankSegments : 0;

            // ---- 1) 보스 구간 시작 → 군항
            EnemySpawner.Instance.SetPhase(bossRound, ship.transform);
            yield return new WaitForSeconds(0.6f);
            var assault = EnemySpawner.Instance.GetComponent<HarborAssault>();
            var field = IslandField.Instance;
            int bosses = Object.FindObjectsByType<ModernCorvetteBoss>(FindObjectsSortMode.None).Length;
            float centerAlong = SeaRegion.HarborCenterAlong;
            int firstDir = SeaRegion.HarborDir;
            float quayHalf = SeaRegion.HarborQuayHalfWidth;
            float widthCenter = SeaRegion.HalfWidthAt(centerAlong);
            float widthFar = SeaRegion.HalfWidthAt(centerAlong - SeaRegion.HarborDir * 260f);
            float bankCenter = field != null ? IslandBuilder.BankShore(centerAlong, 1, field.Seed) : 0f;
            bool insideBasin = SeaRegion.InWater(SeaRegion.HarborPoint(0f, 100f), 5f);
            bool pastQuay = SeaRegion.InWater(SeaRegion.HarborPoint(SeaRegion.HarborQuayOffset + 3f, 0f), 0f);
            Vector3 probe = SeaRegion.HarborPoint(SeaRegion.HarborQuayOffset + 10f, 0f);
            SeaRegion.ClampToChannel(ref probe, 2f);
            float probeT = SeaRegion.HarborT(probe);
            float entranceDist = SeaRegion.HarborT(ship.transform.position);
            _report.AppendLine($"- 군항 열림 {Yes(SeaRegion.HarborActive)} · 단계 {assault?.State} · 군항 외형 {Yes(field != null && field.Harbor != null)} · 보스 {bosses}척(기대 0) · 새 적 멈춤 {Yes(EnemySpawner.Instance.HoldSpawns)}");
            _report.AppendLine($"  - 만 절반 폭: 가운데 {widthCenter:0.0}m(기대 120) · 260m 뒤 물길 {widthFar:0.0}m(기대 62) · 안벽 {quayHalf:0.0}m · 기슭 외형 가운데 {bankCenter:0.0}m");
            _report.AppendLine($"  - 경계: 만 가장자리 안쪽(옆 100m) 물 {Yes(insideBasin)} · 안벽 너머 물 {Yes(pastQuay)}(기대 아니오) · 안벽 뒤 점을 되돌림 t {probeT:0.0}(기대 {SeaRegion.HarborQuayOffset - 2f:0})");
            _report.AppendLine($"  - 기함에서 만 입구까지 {SeaRegion.HarborEntranceT - entranceDist:0}m(기대 약 {HarborAssault.ApproachDistance:0}) · 기슭 구간 {banksBefore} → {(field != null ? field.BankSegments : 0)}");
            if (!SeaRegion.HarborActive || assault == null || assault.State != HarborAssault.Step.Approach) Fail("보스 구간에서 군항이 열리지 않음");
            if (field == null || field.Harbor == null) Fail("군항 외형이 서지 않음");
            if (bosses != 0) Fail("기함이 만에 들어오기 전에 보스가 나옴");
            if (!EnemySpawner.Instance.HoldSpawns) Fail("군항 진입 중 새 적 생성이 멈추지 않음");
            if (Mathf.Abs(widthCenter - 120f) > 4f || Mathf.Abs(widthFar - SeaRegion.ChannelHalfWidth) > 0.01f) Fail("만 모양 경계 폭이 맞지 않음");
            if (Mathf.Abs(bankCenter - 120f) > 8f) Fail("기슭 외형이 만 모양으로 벌어지지 않음");
            if (!insideBasin || pastQuay || Mathf.Abs(probeT - (SeaRegion.HarborQuayOffset - 2f)) > 0.05f) Fail("만·안벽 경계 판정이 맞지 않음");

            // 출항 · 포대 자리가 물 위인가(군항 로컬 → 월드 축 확인)
            bool berthWater = SeaRegion.InWater(HarborBuilder.ToWorld(HarborBuilder.BossBerth), 4f) && Islands.IsClear(HarborBuilder.ToWorld(HarborBuilder.BossBerth), 4f);
            bool escortWater = true;
            foreach (var e in HarborBuilder.EscortBerths) escortWater &= SeaRegion.InWater(HarborBuilder.ToWorld(e), 2f) && Islands.IsClear(HarborBuilder.ToWorld(e), 2f);
            bool batteryWater = true;
            foreach (var s in HarborBuilder.BatterySpots) batteryWater &= SeaRegion.HarborT(HarborBuilder.ToWorld(s)) < SeaRegion.HarborQuayOffset;
            _report.AppendLine($"- 출항 자리 물 위: 보스 {Yes(berthWater)} · 호위 {Yes(escortWater)} · 해안포대가 안벽 바다 쪽 {Yes(batteryWater)}");
            if (!berthWater || !escortWater || !batteryWater) Fail("출항 · 해안포대 자리가 물 위(안벽 바다 쪽)가 아님");
            yield return AerialShot("harbor_overview", SeaRegion.HarborPoint(10f, 0f), -SeaRegion.HarborForward, 210f, 150f);
            yield return AerialShot("harbor_quay", HarborBuilder.ToWorld(new Vector3(0f, 0f, 10f)), -SeaRegion.HarborForward, 90f, 55f);
            yield return AerialShot("harbor_cargo", HarborBuilder.ToWorld(new Vector3(HarborBuilder.CargoPierX, 0f, -5f)), -SeaRegion.HarborForward + SeaRegion.ChannelRight * 0.4f, 55f, 30f);
            int mil = 0, codex = 0;
            if (field != null && field.Harbor != null && field.Harbor.Root != null)
                foreach (Transform t in field.Harbor.Root.transform)
                {
                    if (t.name.StartsWith("MIL_")) mil++;
                    if (t.name.StartsWith("Codex ")) codex++;
                }
            _report.AppendLine($"- 군항 외형: Codex 군항 시설 {codex}곳(기대 2 — 화물 부두 · 정비창) · Codex 군사시설 모델 {mil}개");
            if (codex < 2 || mil < 20) Fail("Codex 군항 시설 · 군사시설 모델이 다 놓이지 않음");
            yield return AerialShot("harbor_military", HarborBuilder.ToWorld(new Vector3(0f, 0f, 55f)), SeaRegion.HarborForward, 30f, 38f);
            yield return AerialShot("harbor_hq", HarborBuilder.ToWorld(new Vector3(40f, 0f, 40f)), SeaRegion.HarborForward, 70f, 45f);
            yield return AerialShot("harbor_from_basin", SeaRegion.HarborPoint(SeaRegion.HarborQuayOffset, 0f), SeaRegion.HarborForward, 120f, 40f);
            yield return Shot("harbor_approach_view");

            // ---- 2) 만에 들어서면 출항
            Warp(HarborAssault.ArrivePoint + SeaRegion.HarborForward * 6f, SeaRegion.HarborForward);
            yield return new WaitForSeconds(0.5f);
            var boss = assault.Boss as ModernCorvetteBoss;
            int escorts = 0;
            foreach (var t in Game.Combat.TargetRegistry.HostileTo(Game.Combat.CombatFaction.Player, Game.Combat.TargetKind.Surface))
                if (t is EnemyController e && e.IsAlive && e.Definition == bossRound.Escort) escorts++;
            int batteries = assault.Batteries.FindAll(b => b != null && b.IsAlive).Count;
            // 출항 자리: 안벽 앞(이미 움직이기 시작하므로 안벽에서 얼마나 떨어졌는지로 본다)
            float berthErr = boss != null ? Vector3.Distance(Flat(boss.transform.position), Flat(HarborBuilder.ToWorld(HarborBuilder.BossBerth))) : 99f;
            _report.AppendLine($"- 만 진입 → 단계 {assault.State} · 보스 {(boss != null ? "출항" : "없음")}(출항 자리에서 {berthErr:0.0}m) · 호위 {escorts}(기대 {bossRound.EscortCount}) · 해안포대 {batteries}(기대 2) · 새 적 재개 {Yes(!EnemySpawner.Instance.HoldSpawns)} · 시간 초과 {Yes(assault.TimedOut)}");
            if (assault.State != HarborAssault.Step.Battle || boss == null || berthErr > 12f) Fail("만에 들어서도 보스가 군항에서 출항하지 않음");
            if (escorts < bossRound.EscortCount) Fail("호위정이 함께 출항하지 않음");
            if (batteries != 2) Fail("안벽 해안포대 2기가 서지 않음");
            if (EnemySpawner.Instance.HoldSpawns) Fail("출항 뒤에도 새 적 생성이 멈춰 있음");
            if (boss == null) yield break;
            yield return new WaitForSeconds(2f);
            yield return CloseShot("harbor_boss_sortie", boss.transform, 34f);

            // 해안포대·호위는 치운다(보스 움직임만 본다)
            foreach (var b in assault.Batteries) if (b != null && b.IsAlive) b.RemoveWithoutReward();
            foreach (var t in new List<Game.Combat.ITargetable>(Game.Combat.TargetRegistry.HostileTo(Game.Combat.CombatFaction.Player, Game.Combat.TargetKind.Surface)))
                if (t is EnemyController e && e != boss && e.IsAlive) e.RemoveWithoutReward();
            EnemySpawner.Instance.HoldSpawns = true;

            // ---- 3) 만 안 전투 30초(기함은 만 가운데에서 천천히 돈다)
            Warp(SeaRegion.HarborPoint(-10f, 0f), SeaRegion.ChannelRight);
            yield return MeasureBoss(boss, ship, 30f, "만 안(기함 만 가운데에서 선회)", 0.5f, true);
            yield return AerialShot("harbor_battle", boss.transform.position, -SeaRegion.HarborForward, 120f, 85f);

            // ---- 4) 좁은 수로, 기함이 기슭 가까이(옆 48m) — 예전에 보스가 멈추던 상황
            float backAlong = centerAlong - firstDir * 330f;
            Warp(SeaRegion.ChannelPoint(backAlong, 48f), SeaRegion.ChannelAxis);
            boss.transform.position = SeaRegion.ChannelPoint(backAlong + 40f, 10f);
            yield return new WaitForSeconds(0.5f);
            yield return MeasureBoss(boss, ship, 30f, "좁은 수로(기함이 오른쪽 기슭 옆 48m에 멈춤)", 0f, false);
            yield return AerialShot("strait_boss_bank", ship.transform.position, SeaRegion.ChannelAxis, 110f, 80f);

            // ---- 5) 시간 초과 출항
            CombatDevTools.ClearBattlefield();
            yield return null;
            Warp(SeaRegion.ChannelPoint(backAlong, 0f), -SeaRegion.ChannelAxis * SeaRegion.HarborDir);   // 군항을 등지고 선다
            EnemySpawner.Instance.SetPhase(bossRound, ship.transform);
            yield return null;
            var a2 = EnemySpawner.Instance.GetComponent<HarborAssault>();
            int dir2 = SeaRegion.HarborDir;
            float t2 = Time.time;
            while (a2.State == HarborAssault.Step.Approach && Time.time - t2 < HarborAssault.MaxWait + 3f)
            {
                ship.RepairHull(ship.HullMaxHp);
                yield return null;
            }
            _report.AppendLine($"- 시간 초과: 군항을 등진 기함 → 군항이 반대쪽에 열림 {Yes(dir2 == -firstDir)} · {Time.time - t2:0.0}초 뒤 단계 {a2.State} · 시간 초과 출항 {Yes(a2.TimedOut)} · 보스 {Yes(a2.Boss != null)}");
            if (dir2 != -firstDir) Fail("군항이 기함이 향한 쪽에 열리지 않음");
            if (a2.State != HarborAssault.Step.Battle || !a2.TimedOut || a2.Boss == null) Fail("만에 들어오지 않아도 60초 뒤 보스가 출항하지 않음");
            CombatDevTools.ClearBattlefield();
            SeaRegion.Enter(SeaKind.Open, ship.transform.position, ship.transform.forward);
            yield return null;
            _report.AppendLine($"- 해역 바뀜 → 군항 닫힘 {Yes(!SeaRegion.HarborActive)} · 군항 외형 치움 {Yes(IslandField.Instance == null || IslandField.Instance.Harbor == null)}");
            if (SeaRegion.HarborActive || (IslandField.Instance != null && IslandField.Instance.Harbor != null)) Fail("다음 해역에서 군항이 남음");
            ship.DevRudderOverride = null;
        }

        /// <summary>보스를 seconds 동안 지켜본다: 경계에 막힌 프레임 비율 · 선회 방향 전환 · 공격 수 · 거리.</summary>
        private IEnumerator MeasureBoss(ModernCorvetteBoss boss, Game.Ship.ShipController ship, float seconds, string label, float throttle, bool turn)
        {
            ship.SetEngineOrder(throttle);
            ship.DevRudderOverride = turn ? 0.35f : 0f;
            int a0 = boss.DevAttacks, h0 = boss.DevBoundaryHits, f0 = boss.DevOrbitFlips;
            int g0 = boss.DevGunAttacks, m0 = boss.DevMissileAttacks, tp0 = boss.DevTorpedoAttacks;
            int frames = 0, slow = 0;
            float minRoom = 1e6f, sumDist = 0f;
            float t0 = Time.time;
            Vector3 last = boss.transform.position;
            float travelled = 0f;
            while (Time.time - t0 < seconds && boss.IsAlive)
            {
                frames++;
                minRoom = Mathf.Min(minRoom, SeaRegion.WaterRoom(boss.transform.position));
                sumDist += Vector3.Distance(Flat(boss.transform.position), Flat(ship.transform.position));
                float step = Vector3.Distance(Flat(boss.transform.position), Flat(last));
                travelled += step;
                if (Time.deltaTime > 0f && step / Time.deltaTime < 1f) slow++;
                last = boss.transform.position;
                ship.RepairHull(ship.HullMaxHp);
                yield return null;
            }
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            int hits = boss.DevBoundaryHits - h0;
            float hitShare = frames > 0 ? hits / (float)frames : 1f;
            float slowShare = frames > 0 ? slow / (float)frames : 1f;
            int attacks = boss.DevAttacks - a0;
            _report.AppendLine($"- {label} {seconds:0}초: 경계에 막힌 프레임 {hits}/{frames}({hitShare:P0}) · 거의 멈춘 프레임 {slowShare:P0} · 선회 방향 바꿈 {boss.DevOrbitFlips - f0}번 · 이동 {travelled:0}m · 평균 거리 {(frames > 0 ? sumDist / frames : 0f):0}m · 가장 가까운 경계 {minRoom:0.0}m");
            _report.AppendLine($"  - 공격 {attacks}번(함포 {boss.DevGunAttacks - g0} · 미사일 {boss.DevMissileAttacks - m0} · 어뢰 {boss.DevTorpedoAttacks - tp0})");
            if (hitShare > 0.05f) Fail($"{label}: 보스가 경계에 자주 막힘({hitShare:P0})");
            if (slowShare > 0.15f) Fail($"{label}: 보스가 자주 멈춤({slowShare:P0})");
            if (attacks < 2) Fail($"{label}: 보스 공격이 거의 없음({attacks}번)");
        }

        /// <summary>하늘에서 비스듬히 내려다본 한 장(군항·만 전체 확인용). forward = 화면 위쪽 방향.</summary>
        private IEnumerator AerialShot(string name, Vector3 target, Vector3 forward, float distance, float height)
        {
            yield return new WaitForEndOfFrame();
            var main = Camera.main;
            var go = new GameObject("AerialShotCam");
            var cam = go.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = 45f;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1500f);
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            cam.transform.position = target - forward * distance + Vector3.up * height;
            cam.transform.LookAt(target);
            const int w = 1600, h = 900;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(go);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
#endif
