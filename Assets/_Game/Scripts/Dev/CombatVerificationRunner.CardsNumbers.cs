#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.UI;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 간단한 무장 카드 · 부상 잠수함 = 대함 · 전투 숫자(2026-11-17).</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator CardsNumbersCheck()
        {
            _report.AppendLine("\n## 간단한 무장 카드 · 부상 잠수함 · 전투 숫자(2026-11-17)");
            var ship = GameManager.Instance.Player;

            // ---- 1) 카드 본문: 등급 줄 · 공격력 · 탄약 · 사거리 단계
            {
                bool ok = true;
                void Card(string id, string mustHave, string mustNot, string band)
                {
                    var def = CombatDevTools.FindModule(id);
                    if (def == null) { _report.AppendLine($"- 카드 {id}: 정의 없음"); ok = false; return; }
                    string text = Strip(ModuleCardText.BuildWeaponCard(def)).Replace("\n", " / ");
                    var r = ModuleCardText.Ratings(def);
                    var sb = new System.Text.StringBuilder();
                    foreach (var (n, v) in r) sb.Append($"{n} {ModuleCardText.PipCount(v)}칸 ");
                    string rangeName = ModuleCardText.RangeName(def.Stats.Range);
                    _report.AppendLine($"- 카드 {def.DisplayName}: {sb}· 사거리 {def.Stats.Range:0}m → {rangeName} · \"{text}\"");
                    foreach (var w in mustHave.Split(',')) if (w.Length > 0 && !text.Contains(w)) { ok = false; Fail($"{def.DisplayName} 카드에 '{w}'가 없음"); }
                    foreach (var w in mustNot.Split(',')) if (w.Length > 0 && text.Contains(w)) { ok = false; Fail($"{def.DisplayName} 카드에 '{w}'가 있음"); }
                    if (rangeName != band) { ok = false; Fail($"{def.DisplayName} 사거리 단계 {rangeName} (기대 {band})"); }
                    if (text.Contains("발사 간격") || text.Contains("강함") || text.Contains("교전 거리")) { ok = false; Fail($"{def.DisplayName} 카드에 자세한 수치가 남음"); }
                }
                Card("mod_autocannon", "대공,대함,공격력,탄약,사거리", "대잠", "근");
                Card("mod_gun76", "대공,대함,공격력", "대잠", "중");
                Card("mod_asw", "대잠,16×2", "대공,대함", "근");
                Card("mod_sam", "대공", "대함,대잠", "장");
                Card("mod_vls", "대공,대함,대잠", "", "장");
                Card("mod_rocket", "대함", "대공,대잠", "중");
                _report.AppendLine($"- 카드 본문 검사: {Yes(ok)}");
            }

            // ---- 2) 정비 화면: 무장 설치 카드가 간단한 카드로(다시 뽑으며 찾는다)
            {
                var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
                var ui = Object.FindFirstObjectByType<RefitUI>(FindObjectsInactive.Include);
                refit.BeginLevelUp(2, () => GameManager.Instance.SetState(GameState.Playing));
                yield return null;
                Game.Refit.RefitDraft.SetRerolls(12);
                int found = -1;
                for (int tries = 0; tries < 12 && found < 0; tries++)
                {
                    for (int i = 0; i < ui.CardCount; i++) if (ui.CardStatsText(i).Contains("공격력")) { found = i; break; }
                    if (found < 0) { ui.TryReroll(); yield return null; }
                }
                string card = found >= 0 ? Strip(ui.CardStatsText(found)).Replace("\n", " / ") : "";
                _report.AppendLine($"- 정비 화면 무장 카드: {(found >= 0 ? $"{found + 1}번 \"{card}\"" : "못 찾음(다시 뽑기 12회)")}");
                yield return ScreenShot("cards_simple_weapon");
                if (found >= 0 && (card.Contains("함선 변화") || card.Contains("발사 간격"))) Fail("정비 화면 무장 카드가 간단한 카드가 아님");
                Game.Refit.RefitDraft.SetRerolls(Game.Refit.RefitDraft.RerollsPerRun);
                Call(refit, "Resume");
                yield return null;
            }

            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            ship.SetEngineOrder(0f);
            Vector3 front = ship.transform.position + ship.transform.forward * 20f;

            // ---- 3) 부상한 잠수함 = 대함: 함포 효율이 대함(중형함) 값, 폭뢰는 대잠 값 그대로
            {
                var subDef = Game.Missions.FieldEventDirector.FindEnemy("ene_submarine");
                var sub = subDef != null ? EnemySpawner.Instance.SpawnAt(subDef, front, Quaternion.identity) as SubmarineBase : null;
                var gun = CombatDevTools.FindModule("mod_gun76");
                var asw = CombatDevTools.FindModule("mod_asw");
                if (sub == null || gun == null || asw == null) Fail("부상 잠수함 검사 준비 실패");
                else
                {
                    sub.DevFrozen = true;
                    float gunDown = gun.TargetEfficiency.Against(sub), aswDown = asw.TargetEfficiency.Against(sub);
                    sub.RevealFor(10f);
                    yield return null;
                    float gunUp = gun.TargetEfficiency.Against(sub), aswUp = asw.TargetEfficiency.Against(sub);
                    _report.AppendLine($"- 잠수함 효율: 76mm 잠항 {gunDown:0.##} → 부상 {gunUp:0.##}(중형함 {gun.TargetEfficiency.MediumSurface:0.##}) · 폭뢰 잠항 {aswDown:0.##} → 부상 {aswUp:0.##}");
                    if (!Mathf.Approximately(gunUp, Mathf.Max(gun.TargetEfficiency.Submarine, gun.TargetEfficiency.MediumSurface))) Fail("부상 잠수함에 함포가 대함 효율로 맞지 않음");
                    if (!Mathf.Approximately(aswUp, asw.TargetEfficiency.Submarine) || aswUp <= 0f) Fail("부상 잠수함에 폭뢰 효율이 바뀜(대잠 값이어야)");
                    if (!Mathf.Approximately(gunDown, gun.TargetEfficiency.Submarine)) Fail("잠항 잠수함 효율이 바뀜");
                }
                CombatDevTools.ClearBattlefield();
            }

            // ---- 4) 전투 숫자: 0.3초 동안 받은 피해를 하나로 · 맞힌 적 체력 · 격침
            {
                var numbers = CombatNumbersUI.Instance;
                var fastDef = Game.Missions.FieldEventDirector.FindEnemy("ene_fastboat");
                var boat = fastDef != null ? EnemySpawner.Instance.SpawnAt(fastDef, front, Quaternion.identity) : null;
                if (numbers == null || boat == null) { Fail("전투 숫자 검사 준비 실패"); yield break; }
                boat.DevFrozen = true;
                int shown0 = CombatNumbersUI.Shown;
                for (int i = 0; i < 3; i++)
                {
                    boat.TakeDamage(new DamageInfo(2f, boat.transform.position, Vector3.down, DamageSource.Gun, "검사"));
                    yield return new WaitForSeconds(0.05f);
                }
                yield return new WaitForSeconds(0.5f);
                int merged = CombatNumbersUI.Shown - shown0;
                string last = CombatNumbersUI.LastText;
                string hp = Strip(numbers.HpText);
                _report.AppendLine($"- 피해 숫자: 2 × 3발 → 띄운 숫자 {merged}개 \"{last}\" · 체력 표시 \"{hp}\"");
                if (merged != 1 || last != "6") Fail("0.3초 안의 피해가 하나로 합쳐 뜨지 않음");
                if (!hp.Contains("6") || !hp.Contains("/ 12")) Fail("맞힌 적의 체력 숫자가 보이지 않음");
                boat.TakeDamage(new DamageInfo(20f, boat.transform.position, Vector3.down, DamageSource.Gun, "검사"));
                yield return null;
                yield return null;
                _report.AppendLine($"- 격침 숫자: \"{Strip(CombatNumbersUI.LastText)}\"");
                if (!CombatNumbersUI.LastText.Contains("격침")) Fail("격침한 한 방에 격침 표시가 없음");

                DisplaySettings.SetCombatNumbers(false);
                int shown1 = CombatNumbersUI.Shown;
                var b2 = EnemySpawner.Instance.SpawnAt(fastDef, front, Quaternion.identity);
                if (b2 != null) { b2.DevFrozen = true; b2.TakeDamage(new DamageInfo(2f, b2.transform.position, Vector3.down, DamageSource.Gun, "검사")); }
                yield return new WaitForSeconds(0.5f);
                bool silent = CombatNumbersUI.Shown == shown1;
                DisplaySettings.SetCombatNumbers(true);
                _report.AppendLine($"- 설정 끄기: 숫자 안 뜸 {Yes(silent)}");
                if (!silent) Fail("전투 숫자를 꺼도 피해 숫자가 뜸");
                CombatDevTools.ClearBattlefield();

                // 실제 무장으로 쏘는 장면(사진)
                foreach (var w in off) if (w != null) w.enabled = true;
                for (int i = 0; i < 4; i++)
                {
                    var e = EnemySpawner.Instance.SpawnAt(fastDef, ship.transform.position + Quaternion.Euler(0f, i * 90f + 30f, 0f) * Vector3.forward * 18f, Quaternion.identity);
                    if (e != null) e.DevFrozen = true;
                }
                yield return new WaitForSeconds(0.9f);
                yield return ScreenShot("combat_numbers");
                _report.AppendLine($"- 실제 사격: 띄운 숫자 누계 {CombatNumbersUI.Shown}");
                CombatDevTools.ClearBattlefield();
            }
        }
    }
}
#endif
