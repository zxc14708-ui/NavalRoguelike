using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.World;

namespace Game.Dev
{
    /// <summary>
    /// 1스테이지 연안 경비정 현측 일제사격 검사(2026-11-26, -broadsideOnly).
    ///   1 돌려 세우기 → 예고(1.2초 이상, 붉은 부채꼴) → 예고한 현측에서 일제사격.
    ///   2 재장전 창: 그 현측에서 쏘면 겨냥점이 발사관, 반대쪽이면 배 중심 · 발사관 부위 피해 1.5배(선체는 그대로) · 3초 뒤 닫힘.
    ///   3 한쪽 발사관 파괴 → 기함이 부서진 쪽 정횡에 붙어 따라가면 사격을 거른다 → 따라가지 않으면 살아 있는 현측으로 쏜다.
    ///   4 둘 다 파괴 → 함포만 남아 가까이 붙는다.
    ///   5 예고 중에 불 들어온 발사관을 부수면 사격 취소.
    /// </summary>
    public partial class CombatVerificationRunner
    {
        public bool BroadsideOnly;

        private IEnumerator BroadsideCheck()
        {
            _report.AppendLine("\n## 연안 경비정 현측 일제사격");
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var rb = ship.GetComponent<Rigidbody>();
            var field = IslandField.Instance;
            if (field != null) { IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(1f);
            void Warp(Vector3 p) { p.y = ship.transform.position.y; if (rb != null) rb.position = p; ship.transform.position = p; }

            var def = CombatDevTools.FindEnemy("ene_boss");
            if (def == null) { Fail("ene_boss 정의 없음"); yield break; }
            Vector3 o = ship.transform.position;
            var boss = EnemySpawner.Instance.SpawnAt(def, o + ship.transform.forward * 40f, Quaternion.LookRotation(ship.transform.right)) as BossShip;
            if (boss == null) { Fail("연안 경비정을 만들지 못함"); yield break; }
            var fx = boss.GetComponentInChildren<BossBroadsideFx>(true);

            // 1) 돌려 세우기 → 예고 → 일제사격
            float t0 = Time.time, telegraphAt = -1f;
            int fxSide = 0; bool abeamAtTelegraph = false;
            while (Time.time - t0 < 14f && boss.DevSalvos == 0)
            {
                yield return null;
                ship.RestoreHull(1f);
                if (telegraphAt < 0f && boss.DevBroadside == BossShip.Broadside.Telegraph)
                {
                    telegraphAt = Time.time;
                    fxSide = fx != null && fx.Current == BossBroadsideFx.Mode.Warn ? (int)fx.Side : 0;
                    Vector3 local = boss.transform.InverseTransformPoint(ship.transform.position);
                    abeamAtTelegraph = Mathf.Abs(Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) - 90f) <= BossShip.AlignHalfAngle + 1f;
                    yield return new WaitForSeconds(0.6f);
                    yield return CloseShot("boss1_broadside_warn", boss.transform, 34f);
                }
            }
            float lead = telegraphAt >= 0f ? boss.DevLastTelegraph : 0f;
            _report.AppendLine($"- 예고: {Yes(telegraphAt >= 0f)} · 예고 {lead:0.0}초 뒤 사격 · 붉은 부채꼴 현측 {(fxSide > 0 ? "우현" : fxSide < 0 ? "좌현" : "-")} · 사격 현측 {(boss.DevSalvoSide > 0 ? "우현" : boss.DevSalvoSide < 0 ? "좌현" : "-")} · 예고 때 기함이 그 현측 정횡 {Yes(abeamAtTelegraph)}");
            if (telegraphAt < 0f || boss.DevSalvos == 0) Fail("연안 경비정이 현측을 돌려 세워 예고한 뒤 쏘지 않음");
            else
            {
                if (lead < 1.2f) Fail($"예고가 너무 짧음({lead:0.0}초 < 1.2초)");
                if (fxSide == 0 || fxSide != boss.DevSalvoSide) Fail("예고한 현측과 쏜 현측이 다름");
                if (!abeamAtTelegraph) Fail("기함이 정횡에 오기 전에 예고함");
            }

            // 2) 재장전 창
            float w0 = Time.time;
            while (Time.time - w0 < 3f && boss.DevBroadside != BossShip.Broadside.Window) { yield return null; ship.RestoreHull(1f); }
            bool window = boss.DevBroadside == BossShip.Broadside.Window;
            var bank = boss.ExposedBank;
            bool gold = fx != null && fx.Current == BossBroadsideFx.Mode.Open;
            if (window) yield return CloseShot("boss1_broadside_window", boss.transform, 34f);
            bool aimSame = false, aimOther = false;
            float partDrop = 0f, hullDrop = 0f, dmg = 20f;
            if (window && bank != null)
            {
                int sign = boss.DevSalvoSide;
                Vector3 sameShooter = boss.transform.position + boss.transform.right * (sign * 30f);
                Vector3 otherShooter = boss.transform.position - boss.transform.right * (sign * 30f);
                aimSame = (boss.AimPointFor(sameShooter, false) - bank.Anchor.position).sqrMagnitude < 0.01f;
                aimOther = (boss.AimPointFor(otherShooter, false) - boss.transform.position).sqrMagnitude < 0.01f;
                float p0 = bank.Hp, h0 = boss.CurrentHp;
                boss.TakeDamage(new DamageInfo(dmg, bank.Anchor.position + Vector3.up * 0.2f, Vector3.down, DamageSource.Gun, "검증"));
                partDrop = p0 - bank.Hp; hullDrop = h0 - boss.CurrentHp;
            }
            float c0 = Time.time;
            while (Time.time - c0 < BossShip.ReloadWindow + 1f && boss.DevBroadside == BossShip.Broadside.Window) { yield return null; ship.RestoreHull(1f); }
            bool closed = boss.DevBroadside != BossShip.Broadside.Window && boss.ExposedBank == null;
            _report.AppendLine($"- 재장전 창: 열림 {Yes(window)} · 금색 부채꼴 {Yes(gold)} · 그 현측에서 겨냥 = 발사관 {Yes(aimSame)} · 반대쪽 = 배 중심 {Yes(aimOther)} · 피해 {dmg:0} → 부위 {partDrop:0.0}(기대 {dmg * BossShip.ExposedPartBonus:0}) · 선체 {hullDrop:0.0}(기대 {dmg:0}) · {Time.time - c0:0.0}초 뒤 닫힘 {Yes(closed)}");
            if (!window || !gold) Fail("사격 뒤 재장전 창(금색 부채꼴)이 열리지 않음");
            if (!aimSame || !aimOther) Fail("재장전 창 겨냥점이 현측에 따라 바뀌지 않음");
            if (Mathf.Abs(partDrop - dmg * BossShip.ExposedPartBonus) > 0.5f || Mathf.Abs(hullDrop - dmg) > 0.5f) Fail("열린 발사관 부위 피해가 1.5배가 아니거나 선체 피해가 바뀜");
            if (!closed) Fail("재장전 창이 3초 뒤 닫히지 않음");

            // 3) 한쪽 파괴 → 부서진 쪽에 붙어 따라가면 사격을 거른다
            int deadSign = boss.DevSalvoSide != 0 ? boss.DevSalvoSide : 1;
            var deadBank = boss.BankOnSide(deadSign);
            int guard = 0;
            while (deadBank != null && !deadBank.Destroyed && guard++ < 80)
                boss.TakeDamage(new DamageInfo(deadBank.MaxHp * 0.3f, deadBank.Anchor.position + Vector3.up * 0.2f, Vector3.down, DamageSource.Gun, "검증"));
            int salvos0 = boss.DevSalvos, blocked0 = boss.DevBlockedSalvos;
            float s0 = Time.time;
            while (Time.time - s0 < 16f && boss.DevBlockedSalvos == blocked0)
            {
                yield return null;
                ship.RestoreHull(1f);
                // 부서진 쪽 정횡 30m에 붙어 따라간다(보스가 돌면 함께 돈다)
                Warp(boss.transform.position + boss.transform.right * (deadSign * 30f));
            }
            bool blocked = boss.DevBlockedSalvos > blocked0 && boss.DevSalvos == salvos0;
            _report.AppendLine($"- {(deadSign > 0 ? "우현" : "좌현")} 발사관 파괴 → 부서진 쪽에 붙어 따라감: 사격 거름 {Yes(blocked)}({Time.time - s0:0.0}초) · 그동안 일제사격 {boss.DevSalvos - salvos0}회");
            if (!deadBank.Destroyed) Fail("검사용 발사관이 부서지지 않음(검사 무효)");
            else if (!blocked) Fail("부서진 쪽에 붙어 따라가도 보스가 사격함(또는 거르지 않음)");

            // 따라가지 않으면 살아 있는 현측으로 돌려 쏜다
            int salvos1 = boss.DevSalvos;
            float s1 = Time.time;
            while (Time.time - s1 < 16f && boss.DevSalvos == salvos1) { yield return null; ship.RestoreHull(1f); }
            bool liveShot = boss.DevSalvos > salvos1 && boss.DevSalvoSide == -deadSign;
            _report.AppendLine($"- 따라가지 않으면: 살아 있는 {(deadSign > 0 ? "좌현" : "우현")}으로 일제사격 {Yes(liveShot)}({Time.time - s1:0.0}초)");
            if (!liveShot) Fail("부서진 쪽을 놓아 주어도 보스가 살아 있는 현측으로 쏘지 않음");

            // 4) 둘 다 파괴 → 함포 돌격
            var liveBank = boss.BankOnSide(-deadSign);
            guard = 0;
            while (liveBank != null && !liveBank.Destroyed && guard++ < 80)
                boss.TakeDamage(new DamageInfo(liveBank.MaxHp * 0.3f, liveBank.Anchor.position + Vector3.up * 0.2f, Vector3.down, DamageSource.Gun, "검증"));
            float g0 = Time.time, minDist = float.MaxValue;
            while (Time.time - g0 < 15f) { yield return null; ship.RestoreHull(1f); minDist = Mathf.Min(minDist, Vector3.Distance(boss.transform.position, ship.transform.position)); if (minDist < 28f) break; }
            _report.AppendLine($"- 발사관 전멸: 함포 돌격 {Yes(boss.GunsOnly)} · 가장 가까이 {minDist:0}m(기대 30m 안)");
            if (!boss.GunsOnly || minDist > 30f) Fail("발사관이 다 부서져도 보스가 함포로 붙어 오지 않음");
            // 보스전 요약(결과 화면 한 줄)
            string line = BossDuelLog.Line();
            _report.AppendLine($"- 보스전 요약: 일제사격 {BossDuelLog.Salvos} · 거름 {BossDuelLog.Blocked} · 열린 발사관 명중 {BossDuelLog.ExposedHits} · 부서진 쪽에 붙음 {BossDuelLog.DeadSideSeconds:0}초 · 부위 파괴 {BossDuelLog.PartKills.Count} · \"{line.Replace("\n", " / ")}\"");
            if (!BossDuelLog.Fought || BossDuelLog.Salvos < 2 || BossDuelLog.Blocked < 1 || BossDuelLog.ExposedHits < 1 || BossDuelLog.PartKills.Count < 2 || BossDuelLog.DeadSideSeconds < 5f || line.Length == 0)
                Fail("보스전 요약이 일제사격 · 막아 냄 · 열린 발사관 명중 · 부서진 쪽 시간 · 부위 파괴를 모으지 않음");
            boss.RemoveWithoutReward();
            yield return null;

            // 5) 예고 중 불 들어온 발사관을 부수면 사격 취소
            Warp(o);
            var boss2 = EnemySpawner.Instance.SpawnAt(def, o + ship.transform.forward * 40f, Quaternion.LookRotation(ship.transform.right)) as BossShip;
            if (boss2 != null)
            {
                float t2 = Time.time;
                while (Time.time - t2 < 14f && boss2.DevBroadside != BossShip.Broadside.Telegraph) { yield return null; ship.RestoreHull(1f); }
                var lit = boss2.ExposedBank;
                bool gotTelegraph = lit != null;
                guard = 0;
                while (lit != null && !lit.Destroyed && guard++ < 80)
                    boss2.TakeDamage(new DamageInfo(lit.MaxHp * 0.3f, lit.Anchor.position + Vector3.up * 0.2f, Vector3.down, DamageSource.Gun, "검증"));
                yield return new WaitForSeconds(BossShip.TelegraphTime + 0.8f);
                bool cancelled = gotTelegraph && boss2.DevSalvos == 0;
                _report.AppendLine($"- 예고 중 불 들어온 발사관 파괴: 사격 취소 {Yes(cancelled)}");
                if (!cancelled) Fail("예고 중에 그 발사관을 부숴도 일제사격이 나감");
                boss2.RemoveWithoutReward();
            }

            // 정리
            ship.DevRudderOverride = null;
            foreach (var w in off) if (w != null) w.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { IslandField.Disabled = false; field.Regenerate(0); }
        }
    }
}
