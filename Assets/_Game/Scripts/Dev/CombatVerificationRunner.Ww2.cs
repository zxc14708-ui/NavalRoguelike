#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Refit;
using Game.Ship;
using Game.UI;
using Game.WW2;

namespace Game.Dev
{
    /// <summary>
    /// 자동 전투 검증 — WWII 패치 B(-ww2Only, 2026-10-17). 항구의 [WWII 시험 전투] 버튼으로 실제로 들어가서:
    ///   1) 분리: 규칙 집합 ww2_v1 · 구축함 · 드래프트/사전/스폰에 현대 콘텐츠 없음 · 현대 판에는 WWII 콘텐츠 없음 ·
    ///      현대 이어하기 파일·현대 프로필·현대 키 설정이 그대로 · WWII 프로필은 따로 · HUD에서 기만체·재밍·고유 스킬 칸 숨김
    ///   2) 어뢰: 사격각 밖 거부(이유) · 직진(방향 불변) · 명중 · 재장전 거부 · 막힌 현측 거부 · 일시정지/정비 중 발사 안 함 · 키 변경
    ///   3) 함포: 함수 양용포가 함교 뒤 표적을 쏘지 않음(자기 구조물 관통 금지) · 앞 표적은 쏨 · 대공포는 항공기 먼저
    ///   4) 공습: 방향 확정 → 예고 → 투하 · 확정 뒤 방향 불변 · 투하 전 격추 = 취소(어뢰·폭탄 없음) · 투하 뒤 격추해도 어뢰는 남음 ·
    ///      예고를 보고 조함하면 빗나감
    ///   5) 보스: 주포 일제 사격에 경고 원 · 보스 막대 · 격침 = 승리 — 현대 저장은 그대로
    /// </summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator Ww2Check()
        {
            _report.AppendLine("\n## WWII 패치 B — 시험 전투");
            int exceptions = 0;
            void OnLog(string m, string st, LogType t) { if (t == LogType.Exception) { exceptions++; _report.AppendLine($"  - 예외: {m}"); } }
            Application.logMessageReceived += OnLog;

            // ---- 0) 현대 판 기준값(시작 화면 상태)
            var def = Ruleset.Ww2Definition;
            if (def == null) { Fail("Resources/Rulesets/ww2_v1 없음"); yield break; }
            var problems = def.Validate();
            _report.AppendLine($"- 규칙 집합 점검: {(problems.Count == 0 ? "문제 없음" : string.Join(" / ", problems))}");
            if (problems.Count > 0) Fail("규칙 집합 정의에 문제가 있음");

            bool modernClean = !RefitDraft.CollectInstallDefinitions(null).Any(m => m.Id.StartsWith("ww2_")) &&
                               !CodexCatalog.CollectModules().Any(m => m.Id.StartsWith("ww2_")) &&
                               !CodexCatalog.CollectEnemies().Any(e => e.def.Id.StartsWith("ww2_"));
            _report.AppendLine($"- 현대 판(출항 전) 드래프트·사전에 WWII 콘텐츠 없음: {Yes(modernClean)}");
            if (!modernClean) Fail("현대 판 드래프트·사전에 WWII 콘텐츠가 섞임");

            // 현대 저장·프로필·키를 표시해 둔다(WWII 판이 건드리면 안 된다)
            string runPath = RunSave.FilePath;
            const string sentinel = "{\"sentinel\":\"modern run save — WWII 검사 표시\"}";
            File.WriteAllText(runPath, sentinel);
            PlayerProfile.Discover("ene_fastboat");
            PlayerProfile.Save();
            string modernProfilePath = PlayerProfile.PathFor(Ruleset.Modern);
            string modernProfile = File.Exists(modernProfilePath) ? File.ReadAllText(modernProfilePath) : null;
            var modernKeys = new int[15];
            for (int i = 0; i < modernKeys.Length; i++) modernKeys[i] = PlayerPrefs.GetInt("Naval.Control." + i, -1);

            // ---- 1) 항구 버튼으로 들어간다
            var button = FindButton("WW2 test start");
            _report.AppendLine($"- 항구 [WWII 시험 전투] 버튼: {Yes(button != null)}");
            if (button == null) { Fail("항구에 WWII 시험 전투 버튼이 없음"); yield break; }

            // ---- 0-1) 함종(패치 C1): 출항 전 순양함을 골라 실제 수치·탑재 예산을 본 뒤, 구축함 버튼으로 들어간다
            {
                var pShip = GameManager.Instance.Player;
                var pInit = Object.FindFirstObjectByType<ShipInitializer>();
                var pGrid = pShip.Grid;
                _report.AppendLine($"- 현대 함정(출항 전): 함종 수치 {(pShip.Hull == null ? "없음" : pShip.Hull.name)} · 탑재 예산 {pGrid.TonnageBudget:0} · 격자 ±{pGrid.MaxHalfLength}/±{pGrid.MaxHalfBeam}");
                if (pShip.Hull != null || pGrid.TonnageBudget > 0f) Fail("현대 함정에 WWII 함종 수치가 붙음");
                var cruiserHull = def.Hulls.FirstOrDefault(h => h != null && h.Id == "ww2_hull_cruiser");
                var cruiserButton = FindButton("WW2 hull cruiser");
                _report.AppendLine($"- 함종 버튼: 순양함 {Yes(cruiserButton != null)} · 규칙 집합 함종 {string.Join(", ", def.Hulls.Select(h => h.Id))}");
                if (cruiserHull == null || cruiserHull.HullProfile == null || cruiserButton == null) Fail("WWII 순양함 함종·버튼이 없음");
                else
                {
                    Ruleset.Enter(Ruleset.Ww2);   // 실제 버튼과 같은 순서 — 설치가 사전 발견 기록을 쓰므로 현대 프로필에 WWII ID가 섞이지 않게
                    bool sel = pInit.TrySelect(cruiserHull, out string whySel);
                    var cHull = cruiserHull.HullProfile;
                    _report.AppendLine($"- 순양함 선택: {Yes(sel)}{(sel ? "" : $"({whySel})")} · 최고속력 {pShip.BaseMaxSpeed:0.0}m/s · 선회 {pShip.BaseTurnRate:0}°/초 · 선체 {pShip.HullMaxHp:0} · 장갑 {pShip.HullArmor * 100f:0}% · 격자 ±{pGrid.MaxHalfLength}/±{pGrid.MaxHalfBeam} · 탑재 {pGrid.Tonnage:0.#}/{pGrid.TonnageBudget:0.#}t · 블록 {pGrid.Modules.Count}");
                    if (!sel || pShip.Hull != cHull || !Mathf.Approximately(pShip.BaseTurnRate, cHull.TurnRate) || !Mathf.Approximately(pShip.HullMaxHp, cHull.HullHp)
                        || pGrid.MaxHalfLength != cHull.HalfLength || pGrid.MaxHalfBeam != cHull.HalfBeam || !Mathf.Approximately(pGrid.TonnageBudget, cHull.TonnageBudget))
                        Fail("순양함 함종 수치(선회·선체·격자·탑재 예산)가 함선에 적용되지 않음");
                    // 탑재 예산: 양용포를 놓을 수 있는 만큼 놓은 뒤 하나 더는 "탑재 예산 초과"로 거부
                    var pFactory = Object.FindFirstObjectByType<ModuleFactory>();
                    var gunDefC = def.Modules.First(m => m.Type == ModuleType.Ww2DualPurposeGun);
                    bool Room() => pGrid.Tonnage + gunDefC.Tonnage <= pGrid.TonnageBudget + 0.001f;
                    int added = 0;
                    for (int pass = 0; pass < 4 && Room(); pass++)
                        for (int x = -pGrid.MaxHalfLength; x <= pGrid.MaxHalfLength && Room(); x++)
                            for (int z = -pGrid.MaxHalfBeam; z <= pGrid.MaxHalfBeam && Room(); z++)
                                if (pGrid.CanPlace(gunDefC, new GridCoord(x, z), 0, out _) && pFactory.Install(gunDefC, new GridCoord(x, z), 0) != null) added++;
                    string overReason = null;
                    for (int x = -pGrid.MaxHalfLength; x <= pGrid.MaxHalfLength && overReason == null; x++)
                        for (int z = -pGrid.MaxHalfBeam; z <= pGrid.MaxHalfBeam && overReason == null; z++)
                            if (pGrid.IsFree(new GridCoord(x, z)) && !pGrid.CanPlace(gunDefC, new GridCoord(x, z), 0, out string rr) && rr != null && rr.Contains("탑재 예산")) overReason = rr;
                    _report.AppendLine($"- 탑재 예산: 양용포 {added}문 더 놓음 → {pGrid.Tonnage:0.#}/{pGrid.TonnageBudget:0.#}t · 하나 더: \"{overReason}\"");
                    if (added == 0 || overReason == null || pGrid.Tonnage > pGrid.TonnageBudget + 0.001f) Fail("탑재 예산이 설치를 막지 않음");
                }
            }

            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            var init = Object.FindFirstObjectByType<ShipInitializer>();
            string loadout = string.Join(", ", ship.Grid.Modules.Select(m => $"{m.Definition.Id}@{m.Origin.X},{m.Origin.Z}r{m.RotationSteps}"));
            _report.AppendLine($"- 들어감: 상태 {gm.State} · 규칙 {Ruleset.Current} · 시험 전투 {Yes(director != null && director.Ww2Test)} · 함정 {init?.SelectedConcept?.Id} · 배치 {loadout}");
            if (gm.State != GameState.Playing || !Ruleset.IsWw2 || director == null || !director.Ww2Test) { Fail("WWII 시험 전투로 들어가지 못함"); yield break; }
            if (init?.SelectedConcept?.Id != "ww2_hull_destroyer") Fail("WWII 구축함이 선택되지 않음");
            if (ship.Grid.Modules.Any(m => !m.Definition.Id.StartsWith("ww2_"))) Fail("WWII 구축함에 현대 블록이 있음");
            {
                var dh = init?.SelectedConcept?.HullProfile;
                _report.AppendLine($"- 구축함 함종: 최고속력 {ship.BaseMaxSpeed:0.0}m/s · 선회 {ship.BaseTurnRate:0}°/초 · 선체 {ship.HullMaxHp:0} · 격자 ±{ship.Grid.MaxHalfLength}/±{ship.Grid.MaxHalfBeam} · 탑재 {ship.Grid.Tonnage:0.#}/{ship.Grid.TonnageBudget:0.#}t · 블록 {ship.Grid.Modules.Count}");
                if (dh == null || ship.Hull != dh || ship.Grid.MaxHalfBeam != dh.HalfBeam || !Mathf.Approximately(ship.Grid.TonnageBudget, dh.TonnageBudget) || ship.Grid.Modules.Count != 5)
                    Fail("구축함 함종 수치가 적용되지 않았거나 순양함 블록이 남음");
            }
            yield return new WaitForSeconds(1f);
            yield return ScreenShot("ww2_start");
            yield return CloseShot("ww2_destroyer_close", ship.transform, 15f);   // Codex 모델 v30 조립 모습

            // 선체 모양(Codex v31): WWII 판에서는 규칙 집합의 갑판판·현측 띠·뱃머리 재질
            {
                var hullRoot = ship.transform.Find("Model/HullRoot");
                int plates = 0, skirts = 0, oldPieces = 0;
                string bowMats = "";
                if (hullRoot != null)
                    foreach (Transform t in hullRoot)
                    {
                        if (t.name.StartsWith("HULL_Ww2DeckPlate")) plates++;
                        else if (t.name.StartsWith("HULL_Ww2SideSkirt")) skirts++;
                        else if (t.name.StartsWith("HULL_DeckPlate") || t.name.StartsWith("HULL_SideSkirt")) oldPieces++;
                        else if (t.name == "Bow" && t.TryGetComponent<MeshRenderer>(out var br)) bowMats = string.Join("·", br.sharedMaterials.Select(m => m != null ? m.name : "null"));
                    }
                int cells = ship.Grid.OccupiedCells.Count;
                _report.AppendLine($"- WWII 선체: 칸 {cells} · 갑판판 {plates} · 현측 띠 {skirts} · 현대 조각 {oldPieces} · 뱃머리 재질 {bowMats} (규칙 집합 조각 {Yes(def.HullDeckPlate != null && def.HullSideSkirt != null)})");
                if (def.HullDeckPlate == null || def.HullSideSkirt == null) Fail("규칙 집합에 WWII 선체 조각이 없음");
                else if (plates != cells || skirts == 0 || oldPieces > 0 || !bowMats.Contains("WW2 Deck")) Fail("WWII 판의 선체가 WWII 갑판판·현측 띠·뱃머리 재질로 만들어지지 않음");
            }

            // 이제부터는 규칙 검사: 웨이브를 멈추고 함선을 튼튼하게
            director.enabled = false;
            EnemySpawner.Instance?.ClearAll();
            CombatDevTools.ClearBattlefield();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            foreach (var x in Object.FindObjectsByType<Game.Progression.ExperienceSystem>(FindObjectsSortMode.None)) x.enabled = false;
            typeof(ShipController).GetProperty(nameof(ShipController.HullMaxHp))?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
            typeof(ShipController).GetProperty(nameof(ShipController.HullHp))?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
            StartCoroutine(KeepModulesHealthy());

            // ---- 1-1) 콘텐츠 분리
            var pool = RefitDraft.CollectInstallDefinitions(null).Select(m => m.Id).ToList();
            _report.AppendLine($"- 설치 카드 후보: {string.Join(", ", pool)}");
            if (pool.Count == 0 || pool.Any(id => !id.StartsWith("ww2_"))) Fail("WWII 설치 카드 후보에 현대 블록이 있거나 비어 있음");
            var draft = Object.FindFirstObjectByType<RefitDraft>(FindObjectsInactive.Include);
            int fleetCards = 0, badInstall = 0, badStat = 0, draws = 0;
            if (draft != null)
                for (int i = 0; i < 40; i++)
                    foreach (var c in draft.DrawCards(3, 2 + i % 6, i % 10 == 9))
                    {
                        draws++;
                        if (c.Kind is RefitCardKind.FleetDeploy or RefitCardKind.FleetUpgrade) fleetCards++;
                        if (c.Kind == RefitCardKind.Install && (c.Module == null || !c.Module.Id.StartsWith("ww2_"))) badInstall++;
                        if (c.Kind == RefitCardKind.Growth && !def.AllowsStat(c.Stat)) badStat++;
                    }
            _report.AppendLine($"- 정비 카드 {draws}장: 편대 카드 {fleetCards} · 현대 블록 {badInstall} · 목록 밖 성장 {badStat}");
            if (draft == null || draws == 0) Fail("정비 카드를 뽑지 못함");
            if (fleetCards + badInstall + badStat > 0) Fail("WWII 정비 카드에 편대·현대 블록·목록 밖 성장 카드가 나옴");

            var codexWeapons = CodexCatalog.Build(CodexSection.Weapons).Select(e => e.Id).ToList();
            var codexEnemies = CodexCatalog.Build(CodexSection.Enemies).Select(e => e.Id).ToList();
            int codexEscorts = CodexCatalog.Build(CodexSection.Escorts).Count;
            var codexGuide = CodexCatalog.Build(CodexSection.Guide).Select(e => e.Id).ToList();
            _report.AppendLine($"- 사전: 무장 {string.Join(", ", codexWeapons)} / 적 {string.Join(", ", codexEnemies)} / 호위함 {codexEscorts} / 안내 {string.Join(", ", codexGuide)}");
            if (codexWeapons.Any(id => !id.StartsWith("ww2_")) || codexEnemies.Any(id => !id.StartsWith("ww2_")) || codexEscorts > 0 || !codexGuide.Contains("ww2_rules"))
                Fail("WWII 사전에 현대 항목이 섞이거나 WWII 규칙 항목이 없음");
            if (codexEnemies.Count < 5) Fail("WWII 사전 적 목록이 모자람");

            var modernEnemy = CombatDevTools.FindEnemy("ene_fastboat");
            var leaked = modernEnemy != null ? EnemySpawner.Instance.SpawnAt(modernEnemy, ship.transform.position + ship.transform.forward * 30f, Quaternion.identity) : null;
            _report.AppendLine($"- 현대 적 생성 시도: {(modernEnemy == null ? "데이터 없음" : leaked == null ? "거부" : "생성됨")}");
            if (leaked != null) { Fail("WWII 판에서 현대 적이 생성됨"); leaked.RemoveWithoutReward(); }

            var hud = Object.FindFirstObjectByType<HUDView>();
            var skillBar = Object.FindFirstObjectByType<SkillBarUI>();
            bool tilesHidden = skillBar != null && !skillBar.ButtonAt(0).gameObject.activeSelf && !skillBar.ButtonAt(1).gameObject.activeSelf && !skillBar.ButtonAt(5).gameObject.activeSelf;
            var bridge = ship.Grid.Modules.Select(m => m.Runtime).OfType<BridgeModule>().FirstOrDefault();
            _report.AppendLine($"- HUD: 기만체·재밍·고유 스킬 칸 숨김 {Yes(tilesHidden)} · 함교 기만체 {Yes(bridge != null && bridge.DecoyEnabled)} · 함교 연막 {Yes(bridge != null && bridge.CarriesSmoke)} · 안내판 {Yes(Ww2BattlePanel.Instance != null)}");
            if (!tilesHidden) Fail("WWII HUD에 기만체·재밍·고유 스킬 칸이 남음");
            if (bridge == null || bridge.DecoyEnabled || !bridge.CarriesSmoke) Fail("WWII 함교가 기만체를 쏘거나 연막이 없음");
            yield return new WaitForSecondsRealtime(0.3f);
            string panel = Ww2BattlePanel.Instance != null ? Ww2BattlePanel.Instance.Text : "";
            _report.AppendLine($"  - 안내판: \"{panel.Replace("\n", " / ")}\"");
            if (!panel.Contains("좌현") || !panel.Contains("우현")) Fail("안내판에 좌현·우현 어뢰 표시가 없음");

            // ---- 2) 어뢰
            var control = Ww2TorpedoControl.Instance;
            var mount = control != null ? control.Mounts.FirstOrDefault() : null;
            if (control == null || mount == null) { Fail("어뢰 조작·발사관 없음"); yield break; }
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            yield return new WaitForSeconds(3f);
            var gunboat = def.Enemies.First(e => e.Id == "ww2_enemy_gunboat");
            var fast = def.Enemies.First(e => e.Id == "ww2_enemy_fast_gunboat");
            var torpedoBomber = def.Enemies.First(e => e.Id == "ww2_enemy_torpedo_bomber");
            var diveBomber = def.Enemies.First(e => e.Id == "ww2_enemy_dive_bomber");
            var boss = def.Enemies.First(e => e.Id == "ww2_boss_gunnery_cruiser");
            var off = DisableWeapons(r => r is Ww2BroadsideTorpedoModule);
            BridgeMachineGun.DevHoldFire = true;

            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            _report.AppendLine($"- 발사관: 좌현 트임 {Yes(mount.IsOpen(Ww2Side.Port))} · 우현 트임 {Yes(mount.IsOpen(Ww2Side.Starboard))} · {mount.Loaded}/{mount.Tubes}발 · 사격각 ±{mount.HalfArc:0}°");
            if (!mount.IsOpen(Ww2Side.Port) || !mount.IsOpen(Ww2Side.Starboard)) Fail("가운데 줄 발사관인데 양현이 트여 있지 않음");

            int Running() => Object.FindObjectsByType<Ww2StraightTorpedo>(FindObjectsSortMode.None).Count(t => t.Running);

            // 사격각 밖(뱃머리 방향)
            int before = Running();
            bool okBow = control.TryFire(Ww2Side.Port, fwd, out string whyBow);
            yield return null;
            _report.AppendLine($"- 뱃머리 방향 발사 시도: 발사 {Yes(okBow)} · 이유 \"{whyBow}\" · 새 어뢰 {Running() - before}");
            if (okBow || whyBow == null || !whyBow.Contains("사격각") || Running() != before) Fail("사격각 밖 발사가 거부되지 않거나 이유가 없음");

            // 좌현 정횡 표적
            var target = EnemySpawner.Instance.SpawnAt(gunboat, ship.transform.position - right * 32f, Quaternion.LookRotation(fwd));
            if (target != null) target.DevFrozen = true;
            yield return null;
            control.DevPreview(Ww2Side.Port, target != null ? target.transform.position : ship.transform.position - right * 32f);
            yield return ScreenShot("ww2_torpedo_aim");
            control.DevPreview(Ww2Side.Port, null);
            float hp0 = target != null ? target.CurrentHp : 0f;
            Vector3 aimDir = (target.transform.position - mount.LaunchOrigin(Ww2Side.Port)); aimDir.y = 0f;
            bool okPort = control.TryFire(Ww2Side.Port, aimDir, out string whyPort);
            var torps = Object.FindObjectsByType<Ww2StraightTorpedo>(FindObjectsSortMode.None).Where(t => t.Running).ToList();
            var p0 = torps.Select(t => t.transform.position).ToList();
            var d0 = torps.Select(t => t.Direction).ToList();
            yield return new WaitForSeconds(0.8f);
            float maxDrift = 0f;
            for (int i = 0; i < torps.Count; i++)
            {
                if (!torps[i].Running) continue;
                Vector3 moved = torps[i].transform.position - p0[i]; moved.y = 0f;
                maxDrift = Mathf.Max(maxDrift, Vector3.Angle(moved, d0[i]), Vector3.Angle(torps[i].Direction, d0[i]));
            }
            float t0 = Time.time;
            while (Time.time - t0 < 5f && target != null && target.IsAlive) yield return null;
            float dealt = target != null ? hp0 - Mathf.Max(0f, target.CurrentHp) : 0f;
            var (loaded, tubes, reload, _) = control.Status(Ww2Side.Port);
            _report.AppendLine($"- 좌현 32m 포격함에 일제 발사: 발사 {Yes(okPort)}({whyPort}) · {torps.Count}발 · 진행 방향 어긋남 최대 {maxDrift:0.0}° · 피해 {dealt:0}/{hp0:0} · 격침 {Yes(target == null || !target.IsAlive)} · 남은 {loaded}/{tubes} · 재장전 {reload:0}초");
            if (!okPort || torps.Count != mount.Tubes) Fail("좌현 일제 발사가 장전된 발 수만큼 나가지 않음");
            if (maxDrift > 1f) Fail("어뢰가 발사 방향에서 벗어남(유도하면 안 됨)");
            if (dealt <= 0f) Fail("직진 어뢰가 정횡 표적을 맞히지 못함");
            if (reload < 10f) Fail("일제 발사 뒤 긴 재장전이 시작되지 않음");
            {
                // 2026-10-18 버그: 발사관이 옆으로 눕던 것 — 돈 뒤에도 선체 위가 발사관의 위로 남는가
                var tubeNode = Get<Transform>(mount, "mount");
                float tilt = tubeNode != null ? Ww2Tilt(tubeNode, Get<Quaternion>(mount, "_mountRest"), mount.transform) : -1f;
                Vector3 tubeFront = Vector3.zero;
                if (tubeNode != null) foreach (var lp in tubeNode.GetComponentsInChildren<Transform>().Where(x => x.name.StartsWith("LaunchPoint"))) tubeFront += lp.position - tubeNode.position;
                tubeFront.y = 0f;
                float toBeam = tubeFront.sqrMagnitude > 1e-4f ? Vector3.Angle(tubeFront, -right) : -1f;
                _report.AppendLine($"- 발사관 선회: 기울어짐 {tilt:0.0}° · 발사관 앞 ↔ 좌현 정횡 {toBeam:0}°");
                if (tilt < 0f || tilt > 2f) Fail("현측 어뢰 발사관이 돌면서 옆으로 기울어짐");
            }

            bool okReload = control.TryFire(Ww2Side.Starboard, right, out string whyReload);
            _report.AppendLine($"- 재장전 중 우현 발사: 발사 {Yes(okReload)} · 이유 \"{whyReload}\"");
            if (okReload || whyReload == null || !whyReload.Contains("재장전")) Fail("재장전 중에 발사되거나 이유가 없음");

            // 우현을 막는다 → 막힌 현측 거부
            mount.Ammo.Refill();
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var gunDef = def.Modules.First(m => m.Type == ModuleType.Ww2DualPurposeGun);
            var mc = mount.Instance.Origin;
            ModuleInstance blocker = ship.Grid.CanPlace(gunDef, new GridCoord(mc.X, mc.Z + 1), 0, out _) ? factory.Install(gunDef, new GridCoord(mc.X, mc.Z + 1), 0) : null;
            yield return null;
            bool okBlocked = control.TryFire(Ww2Side.Starboard, right, out string whyBlocked);
            bool bothRefused = !ship.Grid.CanPlace(gunDef, new GridCoord(mc.X, mc.Z - 1), 0, out string whyBoth);
            _report.AppendLine($"- 우현 옆 칸에 블록 설치 {Yes(blocker != null)} → 우현 발사 {Yes(okBlocked)} \"{whyBlocked}\" · 좌현까지 막는 설치 거부 {Yes(bothRefused)} \"{whyBoth}\"");
            if (blocker == null) Fail("시험용 막는 블록을 놓지 못함(시험 무효)");
            else if (okBlocked || whyBlocked == null || !whyBlocked.Contains("막힘")) Fail("막힌 현측으로 어뢰가 나가거나 이유가 없음");
            if (!bothRefused) Fail("발사관의 양현을 모두 막는 설치가 허용됨");
            if (blocker != null) CombatDevTools.RemoveModule(blocker);
            yield return null;

            // 일시정지·정비 중 발사 금지
            mount.Ammo.Refill();
            gm.SetState(GameState.Paused);
            bool okPaused = control.TryFire(Ww2Side.Port, -right, out string whyPaused);
            gm.SetState(GameState.Refit);
            bool okRefit = control.TryFire(Ww2Side.Port, -right, out _);
            gm.SetState(GameState.Playing);
            _report.AppendLine($"- 일시정지 중 발사 {Yes(okPaused)}(\"{whyPaused}\") · 정비 중 발사 {Yes(okRefit)}");
            if (okPaused || okRefit) Fail("일시정지·정비 중에 어뢰가 나감");

            // 키 변경(WWII 이름공간) · 현대 키와 겹치면 거부
            var prevKey = Ww2Controls.Binding(Ww2Control.TorpedoPort);
            bool rebind = Ww2Controls.TryBind(Ww2Control.TorpedoPort, Key.Z, out _);
            bool now = Ww2Controls.Binding(Ww2Control.TorpedoPort) == Key.Z;
            bool sharedRefused = !Ww2Controls.TryBind(Ww2Control.TorpedoPort, GameSettings.Binding(NavalControl.RudderLeft), out string whyShared);
            Ww2Controls.TryBind(Ww2Control.TorpedoPort, prevKey, out _);
            bool keysKept = true;
            for (int i = 0; i < modernKeys.Length; i++) if (PlayerPrefs.GetInt("Naval.Control." + i, -1) != modernKeys[i]) keysKept = false;
            _report.AppendLine($"- WWII 키 변경: Z로 {Yes(rebind && now)} · 조타 키와 같은 키 거부 {Yes(sharedRefused)}(\"{whyShared}\") · 현대 키 설정 그대로 {Yes(keysKept)} · 되돌림 {Ww2Controls.BindingLabel(Ww2Control.TorpedoPort)}");
            if (!rebind || !now || !sharedRefused || !keysKept) Fail("WWII 키 변경이 따로 저장되지 않거나 현대 키와 겹침을 막지 못함");

            // 설정·일시정지 안내가 WWII 키를 보여 준다
            hud.OpenPauseMenu();
            yield return null;
            string guide = hud.PauseGuideText;
            Call(hud, "ShowSettings");
            yield return null;
            var settings = Get<GameObject>(hud, "_settingsOverlay");
            bool settingsWw2 = settings != null && settings.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.Contains("좌현 어뢰"));
            bool settingsNoDecoy = settings != null && !settings.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == "기만체");
            yield return ScreenShot("ww2_settings");
            Call(hud, "CloseSettings");
            hud.ResumeFromPause();
            yield return null;
            _report.AppendLine($"- 조작 안내: 어뢰 {Yes(guide.Contains("좌현 어뢰"))} · 기만체 없음 {Yes(!guide.Contains("기만체"))} / 설정 조작키: 어뢰 줄 {Yes(settingsWw2)} · 기만체 줄 없음 {Yes(settingsNoDecoy)}");
            if (!guide.Contains("좌현 어뢰") || guide.Contains("기만체") || !settingsWw2 || !settingsNoDecoy) Fail("WWII 조작 안내·설정에 어뢰 키가 없거나 기만체가 남음");
            if (gm.State != GameState.Playing) gm.SetState(GameState.Playing);

            // ---- 3) 함포 사격각·대공 우선
            foreach (var b in off) if (b != null && b is not Ww2BroadsideTorpedoModule) b.enabled = true;
            var guns = ship.Grid.Modules.Select(m => m.Runtime).OfType<Ww2DualPurposeGunModule>().ToList();
            var bowGun = guns.OrderByDescending(g => g.Instance.Origin.X).FirstOrDefault();
            var aftGun = guns.OrderBy(g => g.Instance.Origin.X).FirstOrDefault();
            var aa = ship.Grid.Modules.Select(m => m.Runtime).OfType<Ww2QuadAaModule>().FirstOrDefault();
            if (bowGun == null || aftGun == null || aa == null || bowGun == aftGun) { Fail("양용포 2문·대공포를 찾지 못함"); yield break; }
            var behind = EnemySpawner.Instance.SpawnAt(fast, ship.transform.position - fwd * 24f, Quaternion.LookRotation(fwd));
            if (behind != null) behind.DevFrozen = true;
            int bow0 = bowGun.ShotsFired, aft0 = aftGun.ShotsFired;
            yield return new WaitForSeconds(4f);
            bool bowArc = bowGun.Weapon.IsInFireArc(behind != null ? behind.transform.position : Vector3.zero);
            _report.AppendLine($"- 함미 뒤 24m 표적: 함수 양용포 {bowGun.ShotsFired - bow0}발(사격각 {Yes(bowArc)}) · 함미 양용포 {aftGun.ShotsFired - aft0}발");
            if (bowGun.ShotsFired != bow0 || bowArc) Fail("함수 양용포가 함교 너머 뒤쪽을 쏨(자기 구조물 관통)");
            if (aftGun.ShotsFired == aft0) Fail("함미 양용포가 뒤쪽 표적을 쏘지 않음");
            CombatDevTools.ClearBattlefield();
            var ahead = EnemySpawner.Instance.SpawnAt(fast, ship.transform.position + fwd * 26f, Quaternion.LookRotation(-fwd));
            if (ahead != null) ahead.DevFrozen = true;
            bow0 = bowGun.ShotsFired;
            yield return new WaitForSeconds(4f);
            _report.AppendLine($"- 뱃머리 앞 26m 표적: 함수 양용포 {bowGun.ShotsFired - bow0}발");
            if (bowGun.ShotsFired == bow0) Fail("함수 양용포가 앞 표적을 쏘지 않음");
            CombatDevTools.ClearBattlefield();

            var boat = EnemySpawner.Instance.SpawnAt(fast, aa.transform.position + right * 12f, Quaternion.LookRotation(fwd));
            var plane = EnemySpawner.Instance.SpawnAt(torpedoBomber, aa.transform.position - right * 18f, Quaternion.LookRotation(fwd));
            if (boat != null) boat.DevFrozen = true;
            if (plane != null) plane.DevFrozen = true;
            yield return new WaitForSeconds(1.2f);
            bool aaOnPlane = aa.CurrentTarget != null && ReferenceEquals(aa.CurrentTarget, plane);
            _report.AppendLine($"- 대공포: 우현 12m 소형 포함 + 좌현 18m 뇌격기 → 표적 {(aaOnPlane ? "뇌격기" : aa.CurrentTarget?.Transform?.name ?? "없음")} · 쏜 발 {aa.RoundsFired}");
            if (!aaOnPlane) Fail("4연장 대공포가 항공기를 먼저 노리지 않음");
            CombatDevTools.ClearBattlefield();

            // ---- 4) 공습(무장 끔): 확정 → 예고 → 투하, 취소, 투하 뒤 지속, 회피
            off = DisableWeapons(null);
            ship.SetEngineOrder(1f);
            ship.DevRudderOverride = 0f;
            yield return new WaitForSeconds(3f);

            IEnumerator Strike(EnemyDefinition d, bool kill, bool evade, System.Action<string, bool, bool, float> done, string shot = null)
            {
                fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
                right = Vector3.Cross(Vector3.up, fwd);
                var e = EnemySpawner.Instance.SpawnAt(d, ship.transform.position + right * 45f + fwd * 20f, Quaternion.LookRotation(-right));
                var s = e as Ww2StrikeAircraft;
                if (s == null) { done("생성 실패", false, false, 0f); yield break; }
                float start = Time.time;
                while (Time.time - start < 25f && s.IsAlive && !s.IsCommitted && !s.HasReleased) yield return null;
                if (!s.IsCommitted) { done("확정 안 함", false, false, 0f); yield break; }
                var log = Ww2BattleLog.Get(s.StrikeName);
                int hits0 = log.Hits, miss0 = log.Misses, cancel0 = log.Cancelled;
                int torps0 = ActiveTorpedoes(), shells0 = Object.FindObjectsByType<ArtilleryShell>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled);
                Vector3 heading = s.CommittedHeading;
                float speedAtCommit = ship.CurrentSpeed;
                bool telegraph = s is Ww2TorpedoBomber
                    ? Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.enabled && l.name == "WW2 torpedo bomber run")
                    : Object.FindObjectsByType<ImpactMarker>(FindObjectsSortMode.None).Any(m => m.isActiveAndEnabled);
                if (shot != null) { yield return new WaitForSeconds(1.2f); yield return ScreenShot(shot); }
                if (evade) { ship.SetEngineOrder(-0.5f); ship.DevRudderOverride = 1f; }
                if (kill)
                {
                    yield return new WaitForSeconds(0.4f);
                    s.TakeDamage(new DamageInfo(9999f, s.transform.position, Vector3.down, DamageSource.Gun));
                    yield return new WaitForSeconds(4f);
                    int newTorps = ActiveTorpedoes() - torps0;
                    int newShells = Object.FindObjectsByType<ArtilleryShell>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) - shells0;
                    done($"예고 {Yes(telegraph)} · 투하 전 격추 → 취소 +{log.Cancelled - cancel0} · 새 어뢰 {newTorps} · 새 폭탄 {newShells}", telegraph,
                         log.Cancelled - cancel0 == 1 && newTorps <= 0 && newShells <= 0, 0f);
                    yield break;
                }
                while (Time.time - start < 30f && s.IsAlive && !s.HasReleased)
                {
                    Vector3 f = s.transform.forward; f.y = 0f;
                    yield return null;
                }
                Vector3 fly = s.transform.forward; fly.y = 0f;
                float turn = Vector3.Angle(fly, heading);
                bool released = s.HasReleased;
                if (released && s is Ww2TorpedoBomber && s.IsAlive)
                {
                    // 투하 뒤 격추해도 어뢰는 그대로 간다
                    int live = ActiveTorpedoes();
                    s.TakeDamage(new DamageInfo(9999f, s.transform.position, Vector3.down, DamageSource.Gun));
                    yield return new WaitForSeconds(0.5f);
                    if (ActiveTorpedoes() < live && log.Hits + log.Misses == hits0 + miss0) Fail("투하 뒤 뇌격기를 격추하자 어뢰가 사라짐");
                }
                float waitEnd = Time.time + 12f;   // 어뢰는 9초를 다 달려야 빗나감으로 센다
                float closest = float.MaxValue;
                while (Time.time < waitEnd && log.Hits + log.Misses == hits0 + miss0)
                {
                    foreach (var tp in Torpedo.Active)
                        if (tp != null) { Vector3 dd = tp.transform.position - ship.transform.position; dd.y = 0f; closest = Mathf.Min(closest, dd.magnitude); }
                    yield return null;
                }
                if (s is Ww2TorpedoBomber) _report.AppendLine($"  - 진단: 확정 때 함선 속력 {speedAtCommit:0.0} · 어뢰가 함선 중심에 가장 가까이 온 거리 {(closest == float.MaxValue ? -1f : closest):0.0}m");
                ship.SetEngineOrder(1f);
                ship.DevRudderOverride = 0f;
                bool hit = log.Hits > hits0;
                done($"예고 {Yes(telegraph)} · 투하 {Yes(released)} · 확정 뒤 방향 변화 {turn:0.0}° · {(evade ? "회피 조함" : "직진 유지")} → {(hit ? "명중" : log.Misses > miss0 ? "빗나감" : "결과 없음")}",
                     telegraph, hit, turn);
            }

            foreach (var d in new[] { torpedoBomber, diveBomber })
            {
                string name = d.DisplayName;
                string line = null; bool tele = false, okFlag = false; float turn = 0f;
                yield return Strike(d, false, false, (l, t, h, tr) => { line = l; tele = t; okFlag = h; turn = tr; },
                                    d == torpedoBomber ? "ww2_torpedo_bomber_telegraph" : "ww2_dive_bomber_telegraph");
                _report.AppendLine($"- {name} 직진 유지: {line}");
                if (!tele) Fail($"{name}이 방향 확정 뒤 예고를 띄우지 않음");
                if (turn > 3f) Fail($"{name}이 확정한 뒤 방향을 바꿈({turn:0}°)");
                bool controlHit = okFlag;
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2f);

                yield return Strike(d, false, true, (l, t, h, tr) => { line = l; okFlag = h; });
                _report.AppendLine($"- {name} 예고 보고 회피: {line}");
                if (okFlag) Fail($"{name}의 예고를 보고 조함해도 피할 수 없음");
                if (!controlHit) _report.AppendLine($"  - 참고: 직진 유지 때도 {name}이 빗나감 — 조준이 너무 느슨할 수 있음");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2f);

                yield return Strike(d, true, false, (l, t, h, tr) => { line = l; okFlag = h; });
                _report.AppendLine($"- {name} 투하 전 격추: {line}");
                if (!okFlag) Fail($"{name}을 투하 전에 격추했는데 공격이 취소되지 않음");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(1.5f);
            }

            // ---- 4-1) 선회까지 읽는 조준(2026-10-17 피드백 "플레이어를 더 노리게"): 같은 타로 계속 돌면 급강하 폭탄이 맞는다
            ship.SetEngineOrder(1f);
            ship.DevRudderOverride = 0.5f;
            yield return new WaitForSeconds(3f);
            {
                string line = null; bool turnHit = false;
                yield return Strike(diveBomber, false, false, (l, t, h, tr) => { line = l; turnHit = h; });
                ship.DevRudderOverride = 0.5f;
                _report.AppendLine($"- 급강하폭격기 · 같은 타로 계속 선회: {line}");
                if (!turnHit) Fail("선회를 유지하는 함선을 급강하폭격기가 맞히지 못함(선회 예측 안 됨)");
                CombatDevTools.ClearBattlefield();
            }
            ship.DevRudderOverride = 0f;
            yield return new WaitForSeconds(2f);

            // ---- 4-2) 편대: 뇌격기 3기 · 급강하 2기 — 함께 날고 함께 확정, 어뢰 부채
            var spawnGroup = typeof(EnemySpawner).GetMethod("Spawn", Inst);
            {
                Ww2BattleLog.Get(Ww2BattleLog.TorpedoBomber);
                var lead = spawnGroup?.Invoke(EnemySpawner.Instance, new object[] { torpedoBomber }) as Ww2TorpedoBomber;
                yield return null;
                var flight = Object.FindObjectsByType<Ww2TorpedoBomber>(FindObjectsSortMode.None).Where(b => b.IsAlive).ToList();
                float maxGap = 0f, watchStart = Time.time;
                while (Time.time - watchStart < 25f && lead != null && lead.IsAlive && !lead.IsCommitted)
                {
                    if (Time.time - watchStart > 4f)
                        foreach (var b in flight) if (b != lead && b.IsAlive) maxGap = Mathf.Max(maxGap, Vector3.Distance(b.transform.position, lead.transform.position));
                    yield return null;
                }
                var committed = flight.Where(b => b.IsAlive && b.IsCommitted).ToList();
                var headings = committed.Select(b => b.CommittedHeading).ToList();
                float fan = 0f;
                for (int i = 0; i < headings.Count; i++) for (int j = i + 1; j < headings.Count; j++) fan = Mathf.Max(fan, Vector3.Angle(headings[i], headings[j]));
                float meetSpread = committed.Count >= 2 ? committed.Max(a => committed.Max(b => Vector3.Distance(a.DangerPoint, b.DangerPoint))) : 0f;
                yield return new WaitForSeconds(1.2f);
                yield return ScreenShot("ww2_torpedo_flight");
                int torps0 = ActiveTorpedoes();
                float wait = Time.time;
                while (Time.time - wait < 6f && committed.Any(b => b.IsAlive && !b.HasReleased)) yield return null;
                int dropped = ActiveTorpedoes() - torps0;
                _report.AppendLine($"- 뇌격기 편대: {flight.Count}기 · 접근 중 편대장과 최대 거리 {maxGap:0}m · 함께 확정 {committed.Count}기 · 방향 차이 최대 {fan:0.0}° · 노리는 곳 간격 최대 {meetSpread:0}m · 어뢰 {dropped}발");
                if (flight.Count != 3) Fail("뇌격기가 3기 편대로 나오지 않음");
                if (maxGap > 30f) Fail("뇌격기 편대가 흩어져 날아옴");
                if (committed.Count != 3) Fail("뇌격기 편대가 함께 확정하지 않음");
                if (meetSpread < 10f) Fail("뇌격기 편대 어뢰가 부채로 벌어지지 않음");
                if (dropped < 3) Fail("뇌격기 편대가 어뢰 3발을 떨어뜨리지 않음");
                // 겉모습(Codex v31): 편대 자리마다 꼬리 번호가 다른 모델 하나만 켜짐
                var shown = flight.Select(b =>
                {
                    var vs = Get<GameObject[]>(b, "variants");
                    if (vs == null || vs.Length == 0) return "없음";
                    var on = vs.Where(v => v != null && v.activeSelf).ToList();
                    return on.Count == 1 ? on[0].name : $"켜짐{on.Count}";
                }).ToList();
                _report.AppendLine($"- 뇌격기 편대 겉모습: {string.Join(", ", shown)}");
                if (shown.Distinct().Count() != flight.Count || shown.Any(x => x == "없음" || x.StartsWith("켜짐"))) Fail("뇌격기 편대 3기가 서로 다른 꼬리 번호 모델로 보이지 않음");
                yield return new WaitForSeconds(4f);
                CombatDevTools.ClearBattlefield();
            }
            {
                var lead = spawnGroup?.Invoke(EnemySpawner.Instance, new object[] { diveBomber }) as Ww2DiveBomber;
                yield return null;
                var pair = Object.FindObjectsByType<Ww2DiveBomber>(FindObjectsSortMode.None).Where(b => b.IsAlive).ToList();
                float watchStart = Time.time;
                while (Time.time - watchStart < 25f && lead != null && lead.IsAlive && !lead.IsCommitted) yield return null;
                var dove = pair.Where(b => b.IsAlive && b.IsCommitted).ToList();
                float gapPoints = dove.Count == 2 ? Vector3.Distance(dove[0].StrikePoint, dove[1].StrikePoint) : 0f;
                float gapTime = dove.Count == 2 ? Mathf.Abs(dove[0].SecondsToRelease - dove[1].SecondsToRelease) : 0f;
                _report.AppendLine($"- 급강하 편대: {pair.Count}기 · 함께 강하 {dove.Count}기 · 착탄점 간격 {gapPoints:0}m · 투하 시차 {gapTime:0.0}초");
                if (pair.Count != 2 || dove.Count != 2) Fail("급강하폭격기가 2기 편대로 함께 강하하지 않음");
                yield return new WaitForSeconds(4f);
                CombatDevTools.ClearBattlefield();
            }
            var bossDef = def.Enemies.First(e => e.Id == "ww2_boss_gunnery_cruiser");
            var torpBoat = def.Enemies.FirstOrDefault(e => e.Id == "ww2_enemy_torpedo_boat");
            _report.AppendLine($"- 데이터: 순양함 체력 {bossDef.MaxHp:0}(900 +20%) · 뇌격기 편대 {torpedoBomber.GroupSize} · 급강하 편대 {diveBomber.GroupSize} · 어뢰정 {Yes(torpBoat != null)}");
            if (!Mathf.Approximately(bossDef.MaxHp, 1080f) || torpedoBomber.GroupSize != 3 || diveBomber.GroupSize != 2 || torpBoat == null) Fail("플레이 피드백 데이터(보스 체력·편대·어뢰정)가 반영되지 않음");

            // 어뢰정 모델(Codex v31): WWII 전용 프리팹 · 양현 고정 어뢰관 2기가 선수 쪽 · 선수 기관총
            if (torpBoat != null)
            {
                fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
                var tb = EnemySpawner.Instance.SpawnAt(torpBoat, ship.transform.position + fwd * 22f, Quaternion.LookRotation(-fwd)) as TorpedoBoat;
                yield return new WaitForSeconds(0.5f);
                if (tb != null) yield return CloseShot("ww2_torpedo_boat_close", tb.transform, 9f);
                var tbTubes = tb != null ? Get<Transform[]>(tb, "launchPoints") : null;
                int forward = tbTubes?.Count(t => t != null && tb.transform.InverseTransformPoint(t.position).z > 0.5f) ?? 0;
                int port = tbTubes?.Count(t => t != null && tb.transform.InverseTransformPoint(t.position).x < -0.3f) ?? 0;
                int stbd = tbTubes?.Count(t => t != null && tb.transform.InverseTransformPoint(t.position).x > 0.3f) ?? 0;
                var gunTurret = tb != null ? tb.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "TurretPivot") : null;
                string prefabName = torpBoat.Prefab != null ? torpBoat.Prefab.name : "없음";
                _report.AppendLine($"- 어뢰정 모델: 프리팹 {prefabName} · 어뢰관 {tbTubes?.Length ?? 0}(앞쪽 {forward} · 좌 {port} · 우 {stbd}) · 선수 기관총 {Yes(gunTurret != null)}");
                if (prefabName != "ENE_Ww2TorpedoBoat" || tbTubes == null || tbTubes.Length != 2 || forward != 2 || port != 1 || stbd != 1 || gunTurret == null)
                    Fail("WWII 어뢰정이 Codex 모델·양현 어뢰관 2기·선수 기관총으로 연결되지 않음");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(1f);
            }

            // 무장 켠 채: 양용포가 진입 기체로 돌리는가
            foreach (var b in off) if (b != null) b.enabled = true;
            BridgeMachineGun.DevHoldFire = false;
            int airMode = 0, cancelled0 = Ww2BattleLog.Get(Ww2BattleLog.TorpedoBomber).Cancelled + Ww2BattleLog.Get(Ww2BattleLog.DiveBomber).Cancelled;
            int released0 = Ww2BattleLog.Get(Ww2BattleLog.TorpedoBomber).Released + Ww2BattleLog.Get(Ww2BattleLog.DiveBomber).Released;
            fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize(); right = Vector3.Cross(Vector3.up, fwd);
            EnemySpawner.Instance.SpawnAt(torpedoBomber, ship.transform.position - right * 45f, Quaternion.LookRotation(right));
            EnemySpawner.Instance.SpawnAt(diveBomber, ship.transform.position + right * 40f, Quaternion.LookRotation(-right));
            float watch = Time.time;
            while (Time.time - watch < 20f && AirStrikes.Active.Count > 0)
            {
                if (guns.Any(g => g.AirMode)) airMode++;
                yield return null;
            }
            int cancelled = Ww2BattleLog.Get(Ww2BattleLog.TorpedoBomber).Cancelled + Ww2BattleLog.Get(Ww2BattleLog.DiveBomber).Cancelled - cancelled0;
            int releasedArmed = Ww2BattleLog.Get(Ww2BattleLog.TorpedoBomber).Released + Ww2BattleLog.Get(Ww2BattleLog.DiveBomber).Released - released0;
            _report.AppendLine($"- 무장 켬 · 뇌격기+급강하 1기씩: 양용포 대공 전환 {airMode}프레임 · 투하 전 격추 {cancelled} · 투하 {releasedArmed} · 대공포 {aa.RoundsFired}발");
            if (airMode == 0) Fail("양용포가 공격해 오는 항공기로 돌리지 않음");
            CombatDevTools.ClearBattlefield();

            // ---- 5) 보스(함선을 세워 두고 — 전속이면 느린 순양함이 사거리 밖으로 떨어진다)
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            yield return new WaitForSeconds(4f);
            off = DisableWeapons(null);
            BridgeMachineGun.DevHoldFire = true;
            fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var cruiser = EnemySpawner.Instance.SpawnAt(boss, ship.transform.position + fwd * 40f, Quaternion.LookRotation(-fwd));
            yield return new WaitForSeconds(1f);
            var bar = Object.FindFirstObjectByType<BossBarUI>();
            float bossWait = Time.time;
            int markers = 0;
            while (Time.time - bossWait < 14f && markers == 0)
            {
                markers = Object.FindObjectsByType<ImpactMarker>(FindObjectsSortMode.None).Count(m => m.isActiveAndEnabled);
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
            yield return ScreenShot("ww2_boss_salvo");
            if (cruiser != null) yield return CloseShot("ww2_cruiser_close", cruiser.transform, 22f);
            var boss1 = cruiser as Ww2GunneryBoss;
            if (boss1 != null)
            {
                var ts = Get<Transform[]>(boss1, "turrets");
                var ms = Get<Transform[]>(boss1, "muzzles");
                var sbT = new System.Text.StringBuilder();
                foreach (var t in ts ?? new Transform[0])
                {
                    if (t == null) { sbT.Append(" null"); continue; }
                    var mz = ms?.FirstOrDefault(m => m != null && m.IsChildOf(t));
                    Vector3 barrel = mz != null ? mz.position - t.position : t.forward; barrel.y = 0f;
                    Vector3 toP = ship.transform.position - t.position; toP.y = 0f;
                    sbT.Append($" {t.name} 포신↔기함 {Vector3.Angle(barrel, toP):0}° 회전 {t.localEulerAngles:F0}");
                }
                _report.AppendLine($"  - 진단: 포탑 {ts?.Length ?? 0} · 포구 {ms?.Length ?? 0} ·{sbT} · 거리 {Vector3.Distance(cruiser.transform.position, ship.transform.position):0} · 남은 타이머 {boss1.NextAttack.seconds:0.0}");
                // 2026-10-18 버그: 순양함 포탑이 옆으로 눕던 것
                var rests = Get<Quaternion[]>(boss1, "_turretRest");
                float maxTilt = 0f;
                for (int i = 0; ts != null && rests != null && i < ts.Length && i < rests.Length; i++)
                    if (ts[i] != null) maxTilt = Mathf.Max(maxTilt, Ww2Tilt(ts[i], rests[i], boss1.transform));
                _report.AppendLine($"- 순양함 포탑 기울어짐 최대 {maxTilt:0.0}°");
                if (rests == null || maxTilt > 2f) Fail("순양함 포탑이 돌면서 옆으로 기울어짐");
            }
            _report.AppendLine($"- 순양함: 일제 사격 {boss1?.SalvosFired ?? 0}회 · 경고 원 {markers} · 보스 막대 {Yes(bar != null && bar.Boss == cruiser)} · 단계 \"{boss1?.PhaseName}\"");
            if (boss1 == null || boss1.SalvosFired == 0 || markers == 0) Fail("순양함 주포 일제 사격에 경고 원이 없음");
            if (bar == null || bar.Boss != cruiser) Fail("보스 막대가 순양함을 잡지 않음");
            director.enabled = true;   // 보스 격침 → 승리는 스테이지 진행이 듣는다(규칙 검사 동안 꺼 두었다)
            cruiser?.TakeDamage(new DamageInfo(99999f, cruiser.transform.position, Vector3.down, DamageSource.Torpedo));
            yield return new WaitForSecondsRealtime(1f);
            _report.AppendLine($"- 순양함 격침 → 상태 {gm.State}");
            if (gm.State != GameState.Victory) Fail("보스 격침 뒤 승리가 아님");
            yield return new WaitForSecondsRealtime(0.5f);
            var resultUi = Object.FindFirstObjectByType<ResultScreenUI>();
            string overview = resultUi != null ? Get<TMPro.TMP_Text>(resultUi, "_overview")?.text ?? "" : "";
            _report.AppendLine($"- 결과 화면 오른쪽: \"{overview.Replace("\n", " / ")}\"");
            if (!overview.Contains("공습") || overview.Contains("적 미사일")) Fail("WWII 결과 화면에 공격 단계 기록이 없음");
            yield return ScreenShot("ww2_victory");

            // ---- 6) 현대 기록 보존
            bool runKept = File.Exists(runPath) && File.ReadAllText(runPath) == sentinel;
            string modernAfter = File.Exists(modernProfilePath) ? File.ReadAllText(modernProfilePath) : null;
            string ww2ProfilePath = PlayerProfile.PathFor(Ruleset.Ww2);
            string ww2Profile = File.Exists(ww2ProfilePath) ? File.ReadAllText(ww2ProfilePath) : "";
            _report.AppendLine($"- 승리 뒤: 현대 이어하기 파일 그대로 {Yes(runKept)} · 현대 프로필 그대로 {Yes(modernAfter == modernProfile)} · WWII 프로필 따로({Path.GetFileName(ww2ProfilePath)}) 발견 {Yes(ww2Profile.Contains("ww2_enemy_gunboat"))}");
            if (!runKept) Fail("WWII 시험 전투가 현대 이어하기 기록을 지우거나 바꿈");
            if (modernAfter != modernProfile) Fail("WWII 시험 전투가 현대 프로필을 바꿈");
            if (!ww2Profile.Contains("ww2_enemy_gunboat") || ww2Profile.Contains("\"ene_fastboat\"")) Fail("WWII 프로필이 따로 기록되지 않음");
            _report.AppendLine($"- 공격 기록: {Ww2BattleLog.Describe().Replace("\n", " / ")}");
            if (exceptions > 0) Fail($"예외 {exceptions}건");
            Application.logMessageReceived -= OnLog;
            File.Delete(runPath);
            BridgeMachineGun.DevHoldFire = false;
            foreach (var b in off) if (b != null) b.enabled = true;
            if (field != null) Game.World.IslandField.Disabled = false;
        }

        /// <summary>쉬는 자세(restLocal)에서 위를 향하던 축이 지금 선체 위와 몇 도 어긋났나(수평 선회만 했으면 0).</summary>
        private static float Ww2Tilt(Transform node, Quaternion restLocal, Transform hull)
        {
            Quaternion restWorld = (node.parent != null ? node.parent.rotation : Quaternion.identity) * restLocal;
            Vector3 upInNode = Quaternion.Inverse(restWorld) * hull.up;
            return Vector3.Angle(node.rotation * upInNode, hull.up);
        }

        /// <summary>
        /// WWII 시험 전투 한 판(-ww2BattleOnly): 기본 장비 그대로, 간단한 조타 로봇이 끝까지 싸운다 — 기본 장비만으로 깰 수 있는지,
        /// 무기별 피해·피격 원인·공격 단계(명중·회피·투하 전 격추)를 기록한다(판정이 아니라 균형 자료 — 실패는 예외·진행 멈춤만).
        /// 로봇: 전령기 3/4 · 가끔 좌우로 · 공습/포격 예고가 가까우면 속력·타를 바꿔 피함 · 현측 정횡 표적(보스·포격함 먼저)을 앞질러 어뢰 ·
        /// 선체 50% 아래면 응급 수리 · 정비 화면은 성장 카드를 고르고(없으면 그냥) 닫는다.
        /// </summary>
        private IEnumerator Ww2BattleCheck()
        {
            _report.AppendLine("\n## WWII 시험 전투 — 한 판(조타 로봇)");
            int exceptions = 0;
            void OnLog(string m, string st, LogType t) { if (t == LogType.Exception) { exceptions++; if (exceptions <= 5) _report.AppendLine($"  - 예외: {m}"); } }
            Application.logMessageReceived += OnLog;

            // 함종(패치 C1): -ww2Hull cruiser 이면 순양함 버튼(없으면 구축함)
            var args = System.Environment.GetCommandLineArgs();
            int hullArg = System.Array.IndexOf(args, "-ww2Hull");
            string hullName = hullArg >= 0 && hullArg < args.Length - 1 ? args[hullArg + 1] : null;
            var button = hullName != null ? FindButton("WW2 hull " + hullName) : FindButton("WW2 test start");
            if (button == null) { Fail(hullName != null ? $"항구에 WWII 함종 버튼 '{hullName}'이 없음" : "항구에 WWII 시험 전투 버튼이 없음"); yield break; }
            _report.AppendLine($"- 함종: {hullName ?? "destroyer"}");
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var control = Ww2TorpedoControl.Instance;
            var refitController = Object.FindFirstObjectByType<RefitController>(FindObjectsInactive.Include);
            var refitUi = Object.FindFirstObjectByType<RefitUI>(FindObjectsInactive.Include);
            if (!Ruleset.IsWw2 || gm.State != GameState.Playing || control == null) { Fail("WWII 시험 전투로 들어가지 못함"); yield break; }
            _report.AppendLine($"- 출항: {ship.Hull?.name ?? "함종 수치 없음"} · 최고속력 {ship.BaseMaxSpeed:0.0}m/s · 선회 {ship.BaseTurnRate:0}°/초 · 선체 {ship.HullMaxHp:0} · 장갑 {ship.HullArmor * 100f:0}% · 탑재 {ship.Grid.Tonnage:0.#}/{ship.Grid.TonnageBudget:0.#}t");
            yield return CloseShot("ww2_battle_ship_close", ship.transform, 18f);
            Time.timeScale = TimeScale;
            Vector3 home = ship.transform.position;
            float started = Time.time, nextWeave = 0f, evadeUntil = 0f, nextTorpedo = 0f, nextShot = 30f, nextMainSalvo = 0f;
            int refits = 0, evasions = 0, salvos = 0, mainSalvos = 0;
            var mainGuns = Ww2SalvoControl.Instance;
            EnemyController holdTarget = null;
            float holdUntil = 0f;
            var offered = new HashSet<string>();
            string phase = "";
            void OnPhase(int s, int i, int n, string title) => phase = $"{i}/{n} {title}";
            GameEvents.PhaseStarted += OnPhase;

            while (!gm.IsRunOver && Time.time - started < 720f)
            {
                if (gm.State == GameState.Refit)
                {
                    refits++;
                    var cards = refitUi != null ? Get<List<RefitCard>>(refitUi, "_cards") : null;
                    int growth = -1;
                    if (cards != null)
                        for (int i = 0; i < cards.Count; i++)
                        {
                            var c = cards[i];
                            offered.Add(c.Kind == RefitCardKind.Install ? c.Module?.Id : c.Kind == RefitCardKind.Growth ? $"성장:{c.Stat}" : c.Kind.ToString());
                            if (growth < 0 && c.Kind == RefitCardKind.Growth) growth = i;
                        }
                    if (growth >= 0) Call(refitUi, "ChooseCard", growth);
                    yield return null;
                    if (gm.State == GameState.Refit && refitController != null) Call(refitController, "Resume");
                    yield return null;
                    continue;
                }
                if (gm.State != GameState.Playing) { yield return null; continue; }

                Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 pos = ship.transform.position;
                Vector3 predicted = pos + fwd * ship.CurrentSpeed * 2.5f;

                // 예고된 공격이 예상 위치 가까이면 피한다
                bool danger = false;
                foreach (var s in AirStrikes.Active)
                    if (s != null && s.IsCommitted && !s.HasReleased && s.SecondsToRelease < 3f &&
                        (s is Ww2DiveBomber ? Vector3.Distance(s.StrikePoint, predicted) < 9f : true)) danger = true;
                foreach (var m in Object.FindObjectsByType<ImpactMarker>(FindObjectsSortMode.None))
                    if (m.isActiveAndEnabled && Vector3.Distance(m.transform.position, predicted) < 8f) danger = true;
                foreach (var t in Torpedo.Active)
                    if (t != null && Vector3.Distance(t.transform.position, pos) < 30f) danger = true;
                if (danger && Time.time > evadeUntil)
                {
                    evasions++;
                    evadeUntil = Time.time + 3f;
                    ship.SetEngineOrder(ship.ThrottleInput > 0.6f ? 0.25f : 1f);
                    ship.DevRudderOverride = Random.value < 0.5f ? -1f : 1f;
                }
                else if (Time.time > evadeUntil)
                {
                    ship.SetEngineOrder(0.75f);
                    if (Time.time > nextWeave)
                    {
                        nextWeave = Time.time + 6f;
                        Vector3 toHome = home - pos; toHome.y = 0f;
                        float steer = toHome.magnitude > 110f ? Mathf.Sign(Vector3.SignedAngle(fwd, toHome, Vector3.up)) : Random.Range(-0.5f, 0.5f);
                        ship.DevRudderOverride = steer;
                    }
                }

                // 어뢰: 정횡 쪽 표적을 앞질러
                if (Time.time > nextTorpedo)
                {
                    nextTorpedo = Time.time + 0.4f;
                    foreach (var side in new[] { Ww2Side.Port, Ww2Side.Starboard })
                    {
                        var (loaded, _, _, mounts) = control.Status(side);
                        if (loaded == 0 || mounts == 0) continue;
                        var mount = control.Mounts.FirstOrDefault(x => x.IsOpen(side) && x.Operational);
                        if (mount == null) continue;
                        Vector3 origin = mount.LaunchOrigin(side);
                        EnemyController best = null; float bestScore = float.MaxValue; Vector3 bestDir = Vector3.zero;
                        foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
                        {
                            if (t is not EnemyController e || !e.IsAlive) continue;
                            Vector3 p = e.transform.position, v = e.MoveVelocity;
                            for (int k = 0; k < 2; k++) { float tt = Vector3.Distance(origin, p) / Mathf.Max(1f, mount.Stats.ProjectileSpeed); p = e.transform.position + v * tt; }
                            Vector3 dir = p - origin; dir.y = 0f;
                            float dist = dir.magnitude;
                            if (dist < 14f || dist > mount.Stats.Range * 0.8f) continue;
                            if (!mount.ClampToArc(side, dir, out _, out _)) continue;
                            float value = e.Definition != null ? e.Definition.Rank == EnemyRank.Boss ? 0.2f : e.Definition.MaxHp >= 30f ? 0.5f : 1.5f : 1f;
                            float score = dist * value;
                            if (score < bestScore) { bestScore = score; best = e; bestDir = dir; }
                        }
                        if (best != null && (best.Definition.MaxHp >= 30f || best.Definition.Rank == EnemyRank.Boss) && control.TryFire(side, bestDir, out _)) salvos++;
                    }
                }

                // 주포 일제(전함, 패치 C3a): 사람처럼 — 큰 수상함(보스 먼저)을 겨누고 누르고 있다가(자동 사격 보류, 장전·선회를 기다림)
                // 모든 주포가 준비되거나 6초가 지나면 뗀다
                if (mainGuns != null && mainGuns.Guns.Count > 0)
                {
                    if (holdTarget != null)
                    {
                        Vector3 tp = holdTarget.transform.position;
                        bool allReady = mainGuns.Guns.All(g => g == null || !g.Operational || g.CanSalvoAt(tp, out _));
                        if (!holdTarget.IsAlive || Vector3.Distance(tp, pos) > 58f) { mainGuns.DevHold(null); holdTarget = null; nextMainSalvo = Time.time + 2f; }
                        else if (allReady || Time.time > holdUntil)
                        {
                            if (mainGuns.DevSalvo(tp) > 0) mainSalvos++;
                            holdTarget = null;
                            nextMainSalvo = Time.time + 3f;
                        }
                        else mainGuns.DevHold(tp);
                    }
                    else if (Time.time > nextMainSalvo)
                    {
                        nextMainSalvo = Time.time + 1f;
                        EnemyController best = null; float bestScore = float.MaxValue;
                        foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
                        {
                            if (t is not EnemyController e || !e.IsAlive || e.Definition == null) continue;
                            if (e.Definition.Rank != EnemyRank.Boss && e.Definition.MaxHp < 30f) continue;   // 큰 배만(작은 배는 자동 사격·양용포에)
                            float dist = Vector3.Distance(e.transform.position, pos);
                            if (dist < 14f || dist > 52f) continue;
                            float score = dist * (e.Definition.Rank == EnemyRank.Boss ? 0.2f : 1f);
                            if (score < bestScore) { bestScore = score; best = e; }
                        }
                        if (best != null) { holdTarget = best; holdUntil = Time.time + 6f; mainGuns.DevHold(best.transform.position); }
                    }
                }

                // 응급 수리
                if (ship.HullHp < ship.HullMaxHp * 0.5f) GameEvents.RaiseSkillRequested(ActiveSkillId.Repair);
                if (Time.time - started > nextShot) { nextShot += 60f; yield return ScreenShot($"ww2_battle_{(int)(Time.time - started)}"); }
                yield return null;
            }
            GameEvents.PhaseStarted -= OnPhase;
            ship.DevRudderOverride = null;
            Time.timeScale = 1f;

            float seconds = Time.time - started;
            _report.AppendLine($"- 결과: {gm.State} · {seconds:0}초 · 마지막 구간 {phase} · 선체 {ship.HullHp:0}/{ship.HullMaxHp:0} · 격침 {gm.TotalKills} · 정비 {refits}회 · 회피 조함 {evasions}회 · 로봇 어뢰 일제 발사 {salvos}회" + (mainSalvos > 0 ? $" · 주포 일제 {mainSalvos}회" : ""));
            _report.AppendLine($"- 정비 카드로 나온 것: {string.Join(", ", offered)}");
            if (offered.Any(id => id != null && (id.StartsWith("mod_") || id.Contains("Fleet")))) Fail("WWII 정비 카드에 현대 블록·편대 카드가 나옴");
            _report.AppendLine("- 무기별 피해(적에게 실제로 깎은 체력):");
            foreach (var e in RunReport.Ranked()) _report.AppendLine($"  - {e.Key}: {e.Damage:0} · 격침 {e.Kills}");
            _report.AppendLine("- 선체 피해 출처:");
            foreach (var kv in ship.HullDamageByCause.OrderByDescending(x => x.Value)) _report.AppendLine($"  - {kv.Key}: {kv.Value:0}");
            _report.AppendLine("- 공격 단계:");
            foreach (var a in Ww2BattleLog.All) _report.AppendLine($"  - {a.Name}: 시도 {a.Committed} · 발사/투하 {a.Released} · 투하 전 격추 {a.Cancelled} · 명중 {a.Hits} · 빗나감 {a.Misses}");
            foreach (var kv in CombatStats.Weapons) _report.AppendLine($"  - {kv.Key}: 발사 {kv.Value.Fired} · 명중 {kv.Value.Hits}");
            yield return ScreenShot("ww2_battle_end");
            if (!gm.IsRunOver) _report.AppendLine("- 참고: 12분 안에 끝나지 않음(보스가 남음)");
            if (exceptions > 0) Fail($"예외 {exceptions}건");
            Application.logMessageReceived -= OnLog;
        }
    }
}
#endif
