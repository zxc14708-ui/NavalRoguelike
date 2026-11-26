using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Enemies;
using Game.World;

namespace Game.Dev
{
    /// <summary>
    /// 항공전함 엘리베이터 드론 발진 검사(2026-11-26, -carrierLaunchOnly).
    /// 예전에는 드론이 함수(함교) 쪽으로 세워져 6m 고도로 곧장 날아 함교를 뚫고 지나갔다.
    ///   - 기함이 보스 앞 · 옆 · 뒤에 있을 때, 엘리베이터에서 뜬 드론이
    ///     ① 활주로(함미) 쪽으로 출발하는지 ② 함교 · 주포 메시 상자를 한 프레임도 뚫지 않는지 ③ 결국 기함에 닿는지.
    ///   - 같은 장면을 예전 동작(AirEnemy.DevLaunchOff + 예전 기수 방향)으로 비교.
    /// </summary>
    public partial class CombatVerificationRunner
    {
        public bool CarrierLaunchOnly;

        private IEnumerator CarrierLaunchCheck()
        {
            _report.AppendLine("\n## 항공전함 엘리베이터 드론 발진 방향");
            var gm = GameManager.Instance;
            if (gm.State != GameState.Playing) gm.SetState(GameState.Playing);
            var ship = gm.Player;
            var rb = ship.GetComponent<Rigidbody>();
            var field = IslandField.Instance;
            if (field != null) { IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            var escorts = new List<Behaviour>();
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) if (e.enabled) { e.enabled = false; escorts.Add(e); }
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(1f);

            var def = CombatDevTools.FindEnemy("ene_boss2");
            if (def == null) { Fail("ene_boss2 정의 없음"); yield break; }
            Vector3 o = ship.transform.position;

            // 기함 기준: 보스를 기함에서 떨어뜨려 두고, 보스 기준 방위로 기함이 앞(0°) · 좌현(-90°) · 뒤(180°)
            var cases = new (string name, float bearing)[] { ("앞", 0f), ("좌현", -90f), ("뒤", 180f) };
            bool shotTaken = false;
            foreach (bool legacy in new[] { true, false })
                foreach (var (name, bearing) in cases)
                {
                    CombatDevTools.ClearBattlefield();
                    yield return null;
                    // 보스 기준 방위 bearing, 42m에 기함이 오게 보스를 둔다(보스는 북쪽을 본다)
                    Vector3 toShip = Quaternion.AngleAxis(bearing, Vector3.up) * Vector3.forward;
                    Vector3 bossAt = o - toShip * 42f;
                    var boss = EnemySpawner.Instance.SpawnAt(def, bossAt, Quaternion.LookRotation(Vector3.forward)) as HybridBattleshipBoss;
                    if (boss == null) { Fail("항공전함 생성 실패"); break; }
                    boss.DevStationary = true;
                    boss.transform.SetPositionAndRotation(bossAt, Quaternion.LookRotation(Vector3.forward));
                    yield return new WaitForSeconds(0.3f);

                    // 함교 · 주포 메시 상자(보스 기준)
                    var solids = new List<(string n, Bounds b)>();
                    foreach (var mf in boss.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        string n = mf.name;
                        if (!(n.StartsWith("BridgeAssembly") || n.StartsWith("MainGun_0") || n.StartsWith("MainGun_Elevation") || n.StartsWith("AuxGunYaw"))) continue;
                        solids.Add((n, LocalBox(boss.transform, mf)));
                    }
                    var clr = boss.Clearance;
                    var mount = FindChild(boss.transform, "DroneElevatorMount");
                    Bounds bridge = default; foreach (var s in solids) if (s.n.StartsWith("BridgeAssembly")) bridge = s.b;
                    if (!legacy && name == "앞")
                    {
                        float mz = mount != null ? boss.transform.InverseTransformPoint(mount.position).z : 0f;
                        _report.AppendLine($"- 모델(보스 기준): 엘리베이터 z {mz:0.0} · 함교 z {bridge.min.z:0.0}~{bridge.max.z:0.0} 꼭대기 {bridge.max.y:0.0}m · 함미 z {(clr != null ? clr.MinZ : 0f):0.0} · 함수 z {(clr != null ? clr.MaxZ : 0f):0.0} · 높이표 {(clr != null ? "있음" : "없음")}");
                        if (clr == null) Fail("항공전함 비행 높이표를 만들지 못함");
                        if (mount != null && !(mz < bridge.min.z)) Fail("엘리베이터가 함교 뒤에 있지 않음 — 활주로 방향 가정이 틀림");
                    }

                    // 엘리베이터 위에 사본을 싣고 바로 띄운다
                    AirEnemy.DevLaunchOff = legacy;
                    Call(boss, "SetElevator", 0f);
                    Call(boss, "EnsureDroneDummies");
                    Call(boss, "ShowDroneDummies", true);
                    var before = new HashSet<KamikazeDrone>(Object.FindObjectsByType<KamikazeDrone>(FindObjectsSortMode.None));
                    Call(boss, "LaunchDrones");
                    var drones = new List<KamikazeDrone>();
                    foreach (var d in Object.FindObjectsByType<KamikazeDrone>(FindObjectsSortMode.None)) if (!before.Contains(d) && d.IsAlive) drones.Add(d);
                    if (legacy)   // 예전 기수: 함수 쪽 + 위로 0.35
                        foreach (var d in drones) d.transform.rotation = Quaternion.LookRotation((boss.transform.forward + Vector3.up * 0.35f).normalized, Vector3.up);

                    int pierce = 0; string pierceWhat = "";
                    float minAbove = float.MaxValue;
                    int aftStart = 0, pastStern = 0, reached = 0;
                    var gone = new HashSet<KamikazeDrone>();
                    var firstDir = new Dictionary<KamikazeDrone, bool>();
                    float t0 = Time.time;
                    while (Time.time - t0 < 14f && gone.Count < drones.Count)
                    {
                        yield return null;
                        ship.RestoreHull(1f);
                        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                        foreach (var d in drones)
                        {
                            if (gone.Contains(d)) continue;
                            if (d == null || !d.isActiveAndEnabled || !d.IsAlive)
                            {
                                gone.Add(d);
                                float dist = d != null ? (d.transform.position - ship.transform.position).magnitude : 999f;
                                if (dist < 12f) reached++;
                                continue;
                            }
                            Vector3 l = boss.transform.InverseTransformPoint(d.transform.position);
                            if (!firstDir.ContainsKey(d))
                            {
                                Vector3 f = boss.transform.InverseTransformDirection(d.transform.forward);
                                firstDir[d] = f.z < -0.8f;
                                if (firstDir[d]) aftStart++;
                            }
                            foreach (var s in solids)
                                if (s.b.Contains(l)) { pierce++; if (!pierceWhat.Contains(s.n)) pierceWhat += s.n + " "; }
                            if (clr != null && l.x > clr.MinX && l.x < clr.MaxX && l.z > clr.MinZ && l.z < clr.MaxZ && Time.time - t0 > 0.4f)
                            {
                                // 엘리베이터 둘레(출발 자리)를 벗어나서부터 모함 위 높이 여유
                                Vector3 m = mount != null ? boss.transform.InverseTransformPoint(mount.position) : Vector3.zero;
                                if (Mathf.Abs(l.z - m.z) > 3f) minAbove = Mathf.Min(minAbove, l.y - clr.LocalTop(l.z));
                            }
                        }
                        foreach (var d in drones)
                            if (d != null && d.IsAlive && boss.transform.InverseTransformPoint(d.transform.position).z < (clr != null ? clr.MinZ : -16f) && !_pastSternSeen.Contains(d)) { _pastSternSeen.Add(d); pastStern++; }
                        if (!legacy && !shotTaken && name == "앞" && Time.time - t0 > 0.55f)
                        {
                            shotTaken = true;
                            yield return CloseShot("carrier_launch_runway", boss.transform, 26f);
                        }
                    }
                    _pastSternSeen.Clear();
                    AirEnemy.DevLaunchOff = false;

                    string tag = legacy ? "예전" : "지금";
                    _report.AppendLine($"- [{tag}] 기함 {name}: 드론 {drones.Count}기 · 함미 쪽 출발 {aftStart} · 함미 끝 넘음 {pastStern} · " +
                                       $"함교·주포 뚫은 프레임 {pierce}{(pierce > 0 ? $"({pierceWhat.Trim()})" : "")} · " +
                                       $"모함 위 최소 여유 {(minAbove == float.MaxValue ? "-" : $"{minAbove:0.0}m")} · 기함 도달 {reached}/{drones.Count} · {Time.time - t0:0.0}초");
                    if (!legacy)
                    {
                        if (drones.Count < 2) Fail($"기함 {name}: 드론이 뜨지 않음");
                        if (aftStart < drones.Count) Fail($"기함 {name}: 드론이 활주로(함미) 쪽으로 출발하지 않음");
                        if (pierce > 0) Fail($"기함 {name}: 드론이 함교 · 주포를 뚫고 지나감");
                        if (reached < drones.Count) Fail($"기함 {name}: 드론이 기함에 닿지 않음({reached}/{drones.Count})");
                    }
                    foreach (var d in drones) if (d != null && d.IsAlive) d.RemoveWithoutReward();
                    boss.RemoveWithoutReward();
                    yield return null;
                }

            ship.DevRudderOverride = null;
            foreach (var w in off) if (w != null) w.enabled = true;
            foreach (var e in escorts) if (e != null) e.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { IslandField.Disabled = false; field.Regenerate(0); }
        }

        private readonly HashSet<KamikazeDrone> _pastSternSeen = new();

        /// <summary>메시 경계를 host 기준 상자로.</summary>
        private static Bounds LocalBox(Transform host, MeshFilter mf)
        {
            var b = mf.sharedMesh.bounds;
            var m = host.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var box = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                box.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return box;
        }
    }
}
