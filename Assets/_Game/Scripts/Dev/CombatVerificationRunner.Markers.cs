#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 표식 디자인(2026-11-20): 광역 탐색 약점 표시(갈라진 방패) · 화면 밖 적 종류 아이콘 배지.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator MarkersCheck()
        {
            _report.AppendLine("\n## 표식 디자인(2026-11-20)");
            var ship = GameManager.Instance.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            if (director != null) director.enabled = false;
            EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            var modules = ship.GetComponentsInChildren<Game.Modules.ModuleRuntime>(true);
            var wasOn = new bool[modules.Length];
            for (int i = 0; i < modules.Length; i++) { wasOn[i] = modules[i].enabled; modules[i].enabled = modules[i] is Game.Modules.Runtime.IRadarSource; }
            yield return null;

            // ---- 1) 광역 탐색 → 갈라진 방패
            var skill = ship.GetComponent<SignatureSkill>();
            if (skill == null) { Fail("고유 스킬 컴포넌트 없음"); yield break; }
            skill.ForceKind(StartShipKind.Patrol);
            var boats = new List<EnemyController>();
            Vector3 f = ship.transform.forward; f.y = 0f; f.Normalize();
            foreach (var e in CombatDevTools.SpawnRing("ene_fastboat", 3, 22f, -30f)) { e.DevFrozen = true; boats.Add(e); }
            yield return new WaitForSeconds(0.4f);
            bool used = skill.TryActivate();
            yield return null;
            int early = 0;
            foreach (var b in boats) if (Game.Ship.FxBrokenShield.Active.TryGetValue(b.transform, out var s0) && s0.Split < 0.2f) early++;
            yield return new WaitForSeconds(0.7f);
            int shields = 0, split = 0;
            foreach (var b in boats)
                if (Game.Ship.FxBrokenShield.Active.TryGetValue(b.transform, out var s))
                {
                    shields++;
                    if (s.Split > 0.95f) split++;
                }
            // 다시 써도 방패가 겹쳐 생기지 않는다(시간만 늘림)
            int before = Object.FindObjectsByType<Game.Ship.FxBrokenShield>(FindObjectsSortMode.None).Length;
            Game.Ship.SignatureFx.BrokenShield(boats[0].transform, 5f, Color.red);
            int after = Object.FindObjectsByType<Game.Ship.FxBrokenShield>(FindObjectsSortMode.None).Length;
            _report.AppendLine($"- 광역 탐색 약점 표시: 사용 {Yes(used)} · 표시된 적 {shields}/{boats.Count} · 처음엔 붙은 방패 {early} · 0.7초 뒤 갈라짐 {split} · 다시 써도 겹치지 않음 {Yes(before == after)}");
            if (!used || shields < boats.Count) Fail("광역 탐색으로 표시된 적에 갈라진 방패가 없음");
            if (split < boats.Count) Fail("방패가 둘로 갈라지지 않음");
            if (before != after) Fail("약점 표시가 겹쳐 생김");
            if (boats.Count > 0) yield return CloseShot("sweep_shield", boats[0].transform, 10f);
            yield return ScreenShot("sweep_shield_view");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.3f);

            // ---- 2) 화면 밖 적 종류 배지: 종류마다 다른 실루엣
            var spawned = new List<EnemyController>();
            void Put(string id, float angle, float radius)
            {
                foreach (var e in CombatDevTools.SpawnRing(id, 1, radius, angle)) { e.DevFrozen = true; spawned.Add(e); }
            }
            Put("ene_fastboat", 70f, 74f);
            Put("ene_pcc_corvette", 110f, 76f);
            Put("ene_fighter", 200f, 70f);
            Put("ene_drone", 250f, 66f);
            Put("ene_missileboat", 300f, 74f);
            yield return new WaitForSeconds(1.2f);
            var icons = new HashSet<Texture>();
            int badges = 0;
            foreach (var img in Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None))
                if (img.name == "Icon" && img.enabled && img.gameObject.activeInHierarchy && img.texture != null) { badges++; icons.Add(img.texture); }
            var kinds = new List<string>();
            foreach (var e in spawned) kinds.Add($"{e.Definition.DisplayName}→{Game.UI.ThreatIcons.KindFor(e, false)}");
            _report.AppendLine($"- 화면 밖 배지: {badges}개 · 서로 다른 실루엣 {icons.Count}종 · {string.Join(", ", kinds)}");
            if (badges < 3) Fail("화면 밖 적 배지가 거의 없음");
            if (icons.Count < 3) Fail("화면 밖 배지가 적 종류마다 다르지 않음");
            // 미사일정은 고속정과 다른 아이콘(2026-11-21)
            EnemyController fast = spawned.Find(e => e.Definition.Id == "ene_fastboat"), msl = spawned.Find(e => e.Definition.Id == "ene_missileboat");
            if (fast != null && msl != null && Game.UI.ThreatIcons.KindFor(fast, false) == Game.UI.ThreatIcons.KindFor(msl, false))
                Fail("미사일정 아이콘이 고속정과 같음");
            yield return ScreenShot("offscreen_badges");

            for (int i = 0; i < modules.Length; i++) if (modules[i] != null) modules[i].enabled = wasOn[i];
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = null;
        }
    }
}
#endif
