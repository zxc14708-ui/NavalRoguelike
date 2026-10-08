#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Progression;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 레이더·탄약·점사·근접방어·방어 분류·탄약고 보급 검사.</summary>
    public partial class CombatVerificationRunner
    {
        // ------------------------------------------------------------ 레이더 화면

        /// <summary>
        /// 레이더 화면 검사: 스윕이 함교 안테나 모델의 실제 회전과 같은 속도·방위로 도는지,
        /// 탐지거리 안 표적만 찍히는지, 표적이 사라지면 에코가 한 바퀴 안에 사라지는지 보고 UI 포함 스크린샷을 남긴다.
        /// </summary>
        private IEnumerator RadarCheck()
        {
            _report.AppendLine("\n## 레이더 화면");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(1f);

            var scope = Game.UI.RadarScopeUI.Instance;
            if (scope == null) { Fail("레이더 화면(RadarScopeUI)이 생성되지 않음"); yield break; }
            if (!scope.Online) { Fail("레이더 화면이 NO RADAR 상태"); yield break; }

            var source = scope.Source;
            var sourceBehaviour = source as Component;
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var antennaField = sourceBehaviour == null ? null :
                sourceBehaviour.GetType().GetField("radarAntenna", Flags) ?? sourceBehaviour.GetType().GetField("antenna", Flags);
            var antenna = antennaField?.GetValue(sourceBehaviour) as Transform;
            _report.AppendLine($"- 스윕 기준: {sourceBehaviour?.GetType().Name}, 안테나 {source.AntennaRpm:0} rpm (한 바퀴 {60f / source.AntennaRpm:0.0}초), 표시 거리 {scope.DisplayRange:0}");

            // 1) 안테나 모델 회전 vs 화면 스윕: 속도와 방위
            float sweepTurn = 0f, antennaTurn = 0f, maxOffset = 0f, sumOffset = 0f;
            int samples = 0;
            yield return new WaitForEndOfFrame();   // 안테나(Update)와 스윕(LateUpdate)이 모두 갱신된 뒤에 읽는다
            float prevSweep = scope.SweepBearing;
            // 안테나 모델에서 월드 수평에 가장 가까운 로컬 축을 골라 그 방위 변화를 잰다(스윕 계산과 무관한 독립 측정)
            Vector3 localAxis = Vector3.forward;
            if (antenna != null)
            {
                float best = float.MaxValue;
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                {
                    float y = Mathf.Abs(antenna.TransformDirection(axis).y);
                    if (y < best) { best = y; localAxis = axis; }
                }
            }
            float AntennaYaw() { var d = antenna.TransformDirection(localAxis); return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
            float prevAntennaYaw = antenna != null ? AntennaYaw() : 0f;
            float start = Time.time;
            while (Time.time - start < 6f)
            {
                yield return new WaitForEndOfFrame();
                float sweep = scope.SweepBearing;
                sweepTurn += Mathf.DeltaAngle(prevSweep, sweep);
                prevSweep = sweep;

                if (antenna != null)
                {
                    // 모델이 수평면에서 돈 양(함선 선회분 포함)
                    float yaw = AntennaYaw();
                    antennaTurn += Mathf.DeltaAngle(prevAntennaYaw, yaw);
                    prevAntennaYaw = yaw;
                }

                float offset = Mathf.Abs(Mathf.DeltaAngle(sweep, source.AntennaBearing));
                maxOffset = Mathf.Max(maxOffset, offset);
                sumOffset += offset;
                samples++;
            }
            float elapsed = Time.time - start;
            float sweepRpm = sweepTurn / elapsed / 6f;
            _report.AppendLine($"- {elapsed:0.0}초 측정: 화면 스윕 {sweepRpm:0.0} rpm" +
                               (antenna != null ? $", 함교 안테나 모델 {antennaTurn / elapsed / 6f:0.0} rpm" : ", 안테나 Transform 없음(적산 방식)") +
                               $", 스윕-안테나 방위 차 평균 {sumOffset / Mathf.Max(1, samples):0.0}° / 최대 {maxOffset:0.0}°");
            if (Mathf.Abs(sweepRpm - source.AntennaRpm) > 1f) Fail($"스윕 {sweepRpm:0.0} rpm ≠ 안테나 {source.AntennaRpm:0} rpm");
            if (antenna != null && Mathf.Abs(antennaTurn - sweepTurn) > Mathf.Abs(sweepTurn) * 0.02f) Fail($"안테나 모델 회전 {antennaTurn:0}° vs 스윕 {sweepTurn:0}° 불일치");
            if (maxOffset > 1f) Fail($"스윕이 안테나 방위와 최대 {maxOffset:0.0}° 어긋남");

            // 2) 탐지거리 안(20·28)은 찍히고 밖(범위+8)은 안 찍힘
            float range = scope.DisplayRange;
            var frozen = new List<EnemyController>();
            frozen.AddRange(CombatDevTools.SpawnRing("ene_fastboat", 1, 20f, 45f));
            frozen.AddRange(CombatDevTools.SpawnRing("ene_missileboat", 1, 28f, 200f));
            frozen.AddRange(CombatDevTools.SpawnRing("ene_fastboat", 1, range + 8f, 300f));
            foreach (var e in frozen) e.DevFrozen = true;
            var weapons = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not IRadarSource) { r.enabled = false; weapons.Add(r); }   // 표적이 격침되지 않게

            float period = 60f / source.AntennaRpm;
            yield return new WaitForSeconds(period * 1.2f);
            int contacts = scope.CountEchoes(false);
            _report.AppendLine($"- 멈춘 표적 20·28(범위 안)과 {range + 8f:0}(범위 밖): 한 바퀴 뒤 표적 에코 {contacts}개, 클러터 포함 {scope.CountEchoes(true)}개");
            if (contacts != 2) Fail($"범위 안 표적 2척인데 에코 {contacts}개");
            yield return RadarShot("radar_static");

            // 3) 표적이 사라지면 에코는 한 바퀴 안에 사라짐
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(period * 1.05f);
            int left = scope.CountEchoes(false);
            _report.AppendLine($"- 표적 제거 후 한 바퀴 뒤 남은 표적 에코: {left}");
            if (left != 0) Fail($"표적이 사라진 뒤에도 에코 {left}개가 남음");
            foreach (var w in weapons) if (w != null) w.enabled = true;

            // 4) 교전 장면: 고속정 무리 + 드론 + 미사일
            CombatDevTools.SpawnRing("ene_fastboat", 6, 30f, 20f);
            CombatDevTools.SpawnRing("ene_drone", 2, 30f, 100f);
            CombatDevTools.SpawnRing("ene_missileboat", 4, 66f, 40f);   // 화면 밖 표식(마름모)
            CombatDevTools.MissileVolley(4, 34f, 250f, damageOverride: 0.2f);
            yield return new WaitForSeconds(period * 1.3f);
            yield return RadarShot("radar_combat");
            yield return new WaitForSeconds(period);
            yield return RadarShot("radar_combat_2");

            // 5) 전용 레이더(52) 장착 후 거리 눈금
            CombatDevTools.ClearBattlefield();
            CombatDevTools.InstallTestLoadout();
            yield return new WaitForSeconds(1.5f);
            _report.AppendLine($"- 시험 무장(전용 레이더) 장착 후 표시 거리 {scope.DisplayRange:0}, 스윕 기준 {(scope.Source as Component)?.GetType().Name}");
            CombatDevTools.SpawnRing("ene_missileboat", 3, 46f, 30f);
            CombatDevTools.SpawnRing("ene_fastboat", 4, 38f, 160f);
            yield return new WaitForSeconds(period * 1.5f);
            yield return RadarShot("radar_long_range");
            CombatDevTools.ClearBattlefield();
        }

        /// <summary>오버레이 캔버스를 잠시 카메라 캔버스로 바꿔 UI까지 그리고, 레이더 부분을 잘라 저장한다.</summary>
        /// <summary>정비 화면 전체(카드·선택 패널·함선 뷰)를 찍는다.</summary>
        private IEnumerator RefitShot(Game.UI.RefitUI refit, string name)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var root = Get<GameObject>(refit, "root");
            var canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (cam == null || canvas == null) yield break;

            const int w = 1920, h = 1080;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var prevMode = canvas.renderMode;
            var prevCam = canvas.worldCamera;
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.5f;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);

            // 함선 뷰만(정비 카메라가 보는 그대로, UI 없이)
            var shipCam = Get<Camera>(refit, "shipCamera");
            if (shipCam == null) yield break;
            var srt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var prevShipTarget = shipCam.targetTexture;
            shipCam.targetTexture = srt;
            shipCam.Render();
            RenderTexture.active = srt;
            var stex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            stex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            stex.Apply();
            shipCam.targetTexture = prevShipTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(srt);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + "_ship.png"), stex.EncodeToPNG());
            Destroy(stex);
        }

        private IEnumerator RadarShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var scope = Game.UI.RadarScopeUI.Instance;
            if (cam == null || scope == null) yield break;
            var canvas = scope.transform.parent != null ? scope.transform.parent.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null) yield break;

            const int w = 1920, h = 1080;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var prevMode = canvas.renderMode;
            var group = scope.GetComponentInParent<CanvasGroup>();
            float prevAlpha = group != null ? group.alpha : 1f;

            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.5f;
            if (group != null) group.alpha = 1f;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var full = new Texture2D(w, h, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            full.Apply();

            // 왼쪽 아래 HUD 기둥(함 현황 + 레이더)을 잘라낸다
            var corners = new Vector3[4];
            ((RectTransform)scope.transform).GetWorldCorners(corners);
            Vector3 topRight = cam.WorldToScreenPoint(corners[2]);
            var weaponPanel = Object.FindFirstObjectByType<Game.UI.WeaponStatusPanelUI>();
            if (weaponPanel != null)
            {
                ((RectTransform)weaponPanel.transform).GetWorldCorners(corners);
                topRight.x = Mathf.Max(topRight.x, cam.WorldToScreenPoint(corners[2]).x);
            }
            var hudView = Object.FindFirstObjectByType<Game.UI.HUDView>();
            var levelPanel = hudView != null ? FindChild(hudView.transform, "Level bar") as RectTransform : null;
            if (levelPanel != null)
            {
                levelPanel.GetWorldCorners(corners);
                Vector3 tr = cam.WorldToScreenPoint(corners[2]);
                topRight = new Vector3(Mathf.Max(topRight.x, tr.x), Mathf.Max(topRight.y, tr.y), 0f);
            }
            // 왼쪽 끝: 하단 덩어리가 가운데로 옮겨졌으므로 레이더 왼쪽 아래 모서리부터
            ((RectTransform)scope.transform).GetWorldCorners(corners);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(cam.WorldToScreenPoint(corners[0]).x) - 12, 0, w - 64);
            int cw = Mathf.Clamp(Mathf.CeilToInt(topRight.x) + 12 - x0, 64, w - x0);
            int ch = Mathf.Clamp(Mathf.CeilToInt(topRight.y) + 12, 64, h);
            var crop = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x0, 0, cw, ch));
            crop.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            canvas.renderMode = prevMode;
            if (group != null) group.alpha = prevAlpha;

            File.WriteAllBytes(Path.Combine(OutputDirectory, name + "_full.png"), full.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), crop.EncodeToPNG());
            Destroy(full);
            Destroy(crop);
        }

        // ------------------------------------------------------------ 탄약

        private static List<(string name, IAmmoUser user)> AmmoUsers()
        {
            var result = new List<(string, IAmmoUser)>();
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (ship == null || ship.Grid == null) return result;
            foreach (var m in ship.Grid.Modules)
                if (m?.Runtime is IAmmoUser u && u.Ammo != null && !m.IsDestroyed)
                    result.Add((m.Definition.DisplayName, u));
            return result;
        }

        private void AppendAmmo(string label)
        {
            var sb = new StringBuilder();
            foreach (var (name, u) in AmmoUsers())
                sb.Append(u.Ammo.Infinite ? $"{name} 무한 · " : $"{name} {u.Ammo.Current}/{u.Ammo.Capacity}({u.Ammo.Status}) · ");
            _report.AppendLine($"- 탄약 {label}: {sb}");
        }

        // ------------------------------------------------------------ VLS 덮개 열림(8셀)

        /// <summary>
        /// VLS 8셀(2026-10-08): 셀이 8개인지, 쏠 때 덮개가 먼저 다 열리고 그 뒤에 미사일이 나가는지,
        /// 1번 셀부터 8번까지 차례로 쏘는지, 8발을 다 쓰면 전량 재장전하고 다시 1번부터 쏘는지,
        /// 덮개가 원래 자리로 정확히 닫히는지 본다. 열린 순간을 가까이서 찍는다.
        /// </summary>
        private IEnumerator VlsHatchCheck()
        {
            _report.AppendLine("\n## VLS 8셀 덮개·순서·재장전");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = TimeScale;
            CombatDevTools.ClearBattlefield();
            int placed = ResetLoadout(M("mod_radar"), M("mod_vls"));
            yield return new WaitForSeconds(0.5f);
            var vls = GameManager.Instance.Player.GetComponentInChildren<VlsModule>();
            if (vls == null) { Fail($"VLS를 설치하지 못함(설치 {placed}개)"); yield break; }
            vls.Ammo.Refill();
            // 셀이 적을 때 대형만 노리는 규칙 때문에 미사일정에 마지막 셀을 아끼지 않게(이 검사는 순서·재장전만 본다)
            Put(vls, "targetRule", VlsModule.TargetRule.AnyTarget);

            var lids = PrivateField<Transform[]>(vls, "hatchLids");
            var points = PrivateField<Transform[]>(vls, "hatches");
            int cells = points != null ? points.Length : 0;
            _report.AppendLine($"- 셀(발사점) {cells}개 · 덮개 {(lids != null ? lids.Length : 0)}개 · 탄약 {vls.Ammo.Capacity}셀 · " +
                               $"보급 방식 {(vls.Ammo.IsFullReloadMode ? "전량 재장전" : "조금씩")} {vls.Ammo.Interval:0.#}초 · 열림 연출 {Yes(vls.HasHatchAnimation)}");
            if (cells != 8 || lids == null || lids.Length != 8 || !vls.HasHatchAnimation) { Fail("VLS가 8셀·덮개 연출이 아님"); yield break; }
            if (!vls.Ammo.IsFullReloadMode) Fail("VLS가 전량 재장전 방식이 아님");
            var closedPos = new Vector3[8];
            var closedRot = new Quaternion[8];
            for (int i = 0; i < 8; i++) { closedPos[i] = vls.transform.InverseTransformPoint(lids[i].position); closedRot[i] = Quaternion.Inverse(vls.transform.rotation) * lids[i].rotation; }

            // 시작 화면 구성이 바뀌어 출항 절차를 못 거쳤으면 스포너가 함선을 모른다 — 직접 알려 준다
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", GameManager.Instance.Player.transform);
            List<EnemyController> SpawnTargets()
            {
                var list = CombatDevTools.SpawnRing("ene_missileboat", 12, 36f);
                foreach (var e in list) e.DevFrozen = true;
                return list;
            }
            var boats = SpawnTargets();

            float start = Time.time, firstOpen = -1f, firstLaunch = -1f, maxOpen = 0f, maxLift = 0f;
            int maxOpenCount = 0, shots = 0, seen = 0;
            var order = new List<int>();
            while (Time.time - start < 40f && vls.LaunchCount < 8)
            {
                if (firstOpen < 0f && vls.OpenHatchCount > 0) firstOpen = Time.time - start;
                if (firstLaunch < 0f && vls.LaunchCount > 0) firstLaunch = Time.time - start;
                if (vls.LaunchCount > seen) { seen = vls.LaunchCount; order.Add(vls.LastCell + 1); }
                maxOpen = Mathf.Max(maxOpen, vls.MaxHatchOpen);
                maxOpenCount = Mathf.Max(maxOpenCount, vls.OpenHatchCount);
                for (int i = 0; i < 8; i++)
                    maxLift = Mathf.Max(maxLift, vls.transform.InverseTransformPoint(lids[i].position).y - closedPos[i].y);
                if (shots < 2 && vls.LaunchCount > shots && vls.MaxHatchOpen > 0.99f)
                {
                    shots++;
                    yield return CloseShot($"vls_hatch_{shots}", vls.transform, 7f);
                }
                if (boats.FindAll(e => e != null && e.IsAlive).Count < 3) boats = SpawnTargets();
                yield return null;
            }
            if (vls.LaunchCount > seen) order.Add(vls.LastCell + 1);
            _report.AppendLine($"- 처음 덮개 열림 {firstOpen:0.00}초 → 첫 발사 {firstLaunch:0.00}초(덮개가 열린 뒤 {(firstLaunch - firstOpen):0.00}초)");
            _report.AppendLine($"- 덮개 최대 열림 {maxOpen:0.00} · 동시에 열린 셀 최대 {maxOpenCount}개 · 덮개 중심이 들린 높이 최대 {maxLift:0.00} m");
            _report.AppendLine($"- 발사 셀 순서: {string.Join(" → ", order)}");
            if (firstOpen < 0f || firstLaunch < 0f) Fail("덮개가 열리지 않았거나 미사일이 나가지 않음");
            else if (firstLaunch - firstOpen < 0.12f) Fail("덮개가 다 열리기 전에 미사일이 나감");
            if (maxOpen < 0.99f || maxLift < 0.1f) Fail("덮개가 충분히 열리지 않음");
            bool inOrder = order.Count == 8;
            for (int i = 0; inOrder && i < 8; i++) inOrder = order[i] == i + 1;
            if (!inOrder) Fail("1번부터 8번 셀까지 차례로 쏘지 않음");

            // 전량 재장전 → 다시 1번 셀부터
            yield return null;
            _report.AppendLine($"- 8발 뒤: 남은 셀 {vls.Ammo.Current}/{vls.Ammo.Capacity} · 재장전 중 {Yes(vls.Ammo.IsReloading)} · 남은 시간 {vls.Ammo.SecondsToNext:0.#}초");
            if (!vls.Ammo.IsReloading) Fail("8발을 다 쓴 뒤 재장전에 들어가지 않음");
            float reloadStart = Time.time;
            int during = vls.LaunchCount;
            while (vls.Ammo.IsReloading && Time.time - reloadStart < 90f)
            {
                if (boats.FindAll(e => e != null && e.IsAlive).Count < 3) boats = SpawnTargets();
                yield return null;
            }
            float reloadTook = Time.time - reloadStart;
            bool firedWhileReloading = vls.LaunchCount != during;
            float waitStart = Time.time;
            while (vls.LaunchCount == during && Time.time - waitStart < 10f)
            {
                if (boats.FindAll(e => e != null && e.IsAlive).Count < 3) boats = SpawnTargets();
                yield return null;
            }
            _report.AppendLine($"- 재장전 {reloadTook:0.#}초(재장전 중 발사 {Yes(firedWhileReloading)}) → 다음 발사 셀 {vls.LastCell + 1}번 · 셀 {vls.Ammo.Current + (vls.LaunchCount > during ? 1 : 0)}/{vls.Ammo.Capacity}에서 시작");
            if (firedWhileReloading) Fail("재장전 중에 발사함");
            if (vls.LaunchCount == during || vls.LastCell != 0) Fail("재장전 뒤 1번 셀부터 다시 쏘지 않음");

            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(2.5f);
            float worstPos = 0f, worstRot = 0f;
            for (int i = 0; i < 8; i++)
            {
                worstPos = Mathf.Max(worstPos, Vector3.Distance(vls.transform.InverseTransformPoint(lids[i].position), closedPos[i]));
                worstRot = Mathf.Max(worstRot, Quaternion.Angle(Quaternion.Inverse(vls.transform.rotation) * lids[i].rotation, closedRot[i]));
            }
            _report.AppendLine($"- 다 쏜 뒤: 열린 셀 {vls.OpenHatchCount}개 · 덮개 원위치 오차 {worstPos * 1000f:0.0} mm / {worstRot:0.00}°");
            if (vls.OpenHatchCount != 0 || worstPos > 0.002f || worstRot > 0.2f) Fail("덮개가 원래 자리로 닫히지 않음");
            yield return CloseShot("vls_hatch_closed", vls.transform, 7f);
        }

        /// <summary>
        /// 탄약 검사(1차): 모든 무기가 탄약 데이터를 읽었는지, 쏘면 줄고 보급되는지,
        /// VLS가 셀 수 + 보급량보다 더 쏘지 않는지, CIWS가 비면 재장전하는지 본다. UI 포함 스크린샷.
        /// </summary>
        private IEnumerator AmmoCheck()
        {
            _report.AppendLine("\n## 탄약");
            CombatDevTools.ClearBattlefield();
            CombatDevTools.InstallTestLoadout();
            yield return new WaitForSeconds(1f);

            var users = AmmoUsers();
            foreach (var (name, u) in users)
                if (u.Ammo.Infinite) Fail($"{name}: 탄약 데이터가 없어 무한 탄약");
            AppendAmmo("시작");

            // 1) 고속정 무리: 기관포·76mm·로켓 소모
            CombatStats.Reset();
            CombatDevTools.SpawnRing("ene_fastboat", 10, 40f);
            float start = Time.time;
            int minCiws = int.MaxValue;
            while (Time.time - start < 25f) yield return null;
            AppendAmmo("고속정 10척 25초 후");
            AppendStats();
            foreach (var (name, u) in AmmoUsers())
                if (u.Ammo.Current < 0 || u.Ammo.Current > u.Ammo.Capacity) Fail($"{name}: 탄약 {u.Ammo.Current}/{u.Ammo.Capacity} 범위 밖");
            yield return RadarShot("ammo_fastpack");

            // 2) VLS 소진: 가치 높은 표적(미사일정) 여러 척을 세워 두고 쏘게 한다
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            foreach (var (_, u) in AmmoUsers()) u.Ammo.Refill();
            CombatStats.Reset();
            // VLS 한 문만 남기고 끈다(검증을 이어 돌리면 여러 문이 표적을 나눠 셀이 비지 않는다)
            var launchers = AmmoUsers().FindAll(x => x.user is VlsModule);
            for (int i = 1; i < launchers.Count; i++) ((Behaviour)launchers[i].user).enabled = false;
            var disabledVls = launchers.GetRange(Mathf.Min(1, launchers.Count), Mathf.Max(0, launchers.Count - 1));
            if (launchers.Count > 1) launchers = launchers.GetRange(0, 1);
            int vlsCells = 0;
            foreach (var l in launchers) vlsCells += l.user.Ammo.Capacity;
            var frozen = CombatDevTools.SpawnRing("ene_missileboat", vlsCells + 6, 42f);   // 셀보다 많아야 셀이 빈다
            foreach (var e in frozen) e.DevFrozen = true;
            start = Time.time;
            int minCells = int.MaxValue;
            while (Time.time - start < 40f)
            {
                foreach (var l in launchers) minCells = Mathf.Min(minCells, l.user.Ammo.Current);
                yield return null;
            }
            // 미사일정(가치 3)만 있으면 VLS는 셀 25%(8셀 → 2셀)를 대형·보스용으로 남겨야 한다
            int reserve = launchers.Count > 0 ? Mathf.CeilToInt(launchers[0].user.Ammo.Capacity * 0.25f) : 0;
            float elapsed = Time.time - start;
            int vlsFired = CombatStats.Weapons.TryGetValue("VLS", out var ve) ? ve.Fired : 0;
            float interval = launchers.Count > 0 ? launchers[0].user.Ammo.Interval : 15f;
            int allowed = vlsCells + launchers.Count * (Mathf.FloorToInt(elapsed / interval) + 1);
            _report.AppendLine($"- VLS {launchers.Count}문: 셀 {vlsCells}, 미사일정 {frozen.Count}척, {elapsed:0}초 동안 {vlsFired}발 발사(허용 최대 {allowed}), 최저 셀 {minCells} (예비 {reserve}셀 유지 기대)");
            foreach (var d in disabledVls) ((Behaviour)d.user).enabled = true;
            if (launchers.Count == 0) Fail("VLS가 설치되지 않음");
            else
            {
                if (vlsFired > allowed) Fail($"VLS가 셀+보급보다 많이 쏨({vlsFired} > {allowed})");
                if (minCells < reserve) Fail($"VLS가 가치 3 표적에 예비 셀까지 씀(최저 {minCells} < {reserve})");
                if (minCells > reserve) Fail($"VLS가 쓸 수 있는 셀을 남김(최저 {minCells} > 예비 {reserve})");
            }
            AppendAmmo("미사일정 40초 후");
            yield return RadarShot("ammo_vls_empty");

            // 3) CIWS 소진: 드론 무리 뒤에 미사일 일제사격
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            foreach (var (_, u) in AmmoUsers()) u.Ammo.Refill();
            var weapons = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not CiwsModule && r is not IRadarSource) { r.enabled = false; weapons.Add(r); }   // CIWS 혼자 막게
            CombatStats.Reset();
            var ciws = AmmoUsers().Find(x => x.user is CiwsModule).user;
            bool reloadSeen = false;
            start = Time.time;
            CombatDevTools.SpawnRing("ene_drone", 10, 30f);
            bool volleyFired = false;
            while (Time.time - start < 30f)
            {
                if (!volleyFired && Time.time - start > 12f) { CombatDevTools.MissileVolley(6, 40f, damageOverride: 0.2f); volleyFired = true; }
                if (ciws != null)
                {
                    minCiws = Mathf.Min(minCiws, ciws.Ammo.Current);
                    if (ciws.Ammo.IsReloading) reloadSeen = true;
                }
                yield return null;
            }
            foreach (var w in weapons) if (w != null) w.enabled = true;
            _report.AppendLine($"- CIWS 단독(드론 10 → 12초 뒤 미사일 6): 최저 탄약 {(minCiws == int.MaxValue ? -1 : minCiws)}, 재장전 관측 {(reloadSeen ? "예" : "아니오")}");
            AppendStats();
            if (ciws == null) Fail("CIWS가 없음");
            AppendAmmo("CIWS 시험 후");
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ CIWS 점사

        /// <summary>
        /// CIWS 한 문이 미사일 1발·드론 1대를 처리하는 데 드는 시간·탄약·점사 수를 여러 번 잰다.
        /// 점사는 최소 시간만큼은 끝까지 쏘므로, 한 번 교전에 최소 점사 탄약 이상이 들어야 한다.
        /// </summary>
        private IEnumerator BurstCheck()
        {
            _report.AppendLine("\n## CIWS 점사");
            CiwsModule ciws = null;
            foreach (var c in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None))
                if (c.Instance != null && c.enabled && !c.Instance.IsDestroyed) { ciws = c; break; }
            if (ciws == null) { Fail("CIWS 없음"); yield break; }

            var ship = GameManager.Instance.Player;
            const int Trials = 6;
            foreach (bool drone in new[] { false, true })
            {
                int kills = 0, engaged = 0;
                float timeSum = 0f, ammoSum = 0f, burstSum = 0f;
                for (int trial = 0; trial < Trials; trial++)
                {
                    CombatDevTools.ClearBattlefield();
                    yield return new WaitForSeconds(0.5f);
                    var disabled = new List<Behaviour>();
                    foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                        if (r.enabled && r != ciws && r is not IRadarSource) { r.enabled = false; disabled.Add(r); }
                    ciws.Ammo.Refill();
                    CombatStats.Reset();
                    int ammo0 = ciws.Ammo.Current, bursts0 = ciws.BurstCount, shots0 = ciws.ShotsFired;

                    float angle = -20f + 40f * trial / (Trials - 1);
                    ITargetable target = null;
                    if (drone)
                    {
                        var list = CombatDevTools.SpawnRing("ene_drone", 1, 26f, angle);
                        if (list.Count > 0) target = list[0] as ITargetable;
                    }
                    else CombatDevTools.MissileVolley(1, 30f, angle, damageOverride: 0.2f);
                    yield return null;
                    if (target == null)
                    {
                        var kind = drone ? TargetKind.Aircraft : TargetKind.Missile;
                        var reg = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                        if (reg.Count > 0) target = reg[0];
                    }

                    float start = Time.time, firstShot = -1f, gone = -1f, lastDist = 999f;
                    while (Time.time - start < 20f)
                    {
                        if (firstShot < 0f && ciws.ShotsFired != shots0) firstShot = Time.time;
                        bool alive = target != null && target.IsAlive && target.Transform != null;
                        if (alive) lastDist = Vector3.Distance(target.Transform.position, ship.transform.position);
                        else if (gone < 0f) gone = Time.time;
                        if (gone > 0f && !ciws.InBurst && Time.time - gone > 0.3f) break;
                        yield return null;
                    }

                    bool killed = drone
                        ? gone > 0f && lastDist > 4f
                        : CombatStats.Threats.TryGetValue(ThreatOutcome.Intercepted, out var ie) && ie.Count > 0;
                    if (firstShot > 0f)
                    {
                        engaged++;
                        ammoSum += ammo0 - ciws.Ammo.Current;
                        burstSum += ciws.BurstCount - bursts0;
                        if (killed) { kills++; timeSum += gone - firstShot; }
                    }
                    foreach (var d in disabled) if (d != null) d.enabled = true;
                }

                string name = drone ? "드론" : "미사일";
                float avgAmmo = engaged > 0 ? ammoSum / engaged : 0f;
                _report.AppendLine($"- {name} 1개 × {Trials}회: 격추 {kills}/{Trials} · 첫 발부터 격추까지 평균 {(kills > 0 ? timeSum / kills : 0f):0.00}초 · " +
                                   $"교전당 탄약 {avgAmmo:0} · 점사 {(engaged > 0 ? burstSum / engaged : 0f):0.0}회");
                if (kills == 0) Fail($"CIWS가 {name}을 한 번도 격추하지 못함");
                if (engaged > 0 && avgAmmo < 18f) Fail($"{name} 교전당 탄약 {avgAmmo:0} — 최소 점사가 지켜지지 않음");
            }
            _report.AppendLine($"- 점사 평균 탄약 {ciws.AverageBurstAmmo:0} (누적 {ciws.BurstCount}회)");
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ 방어(2차)

        /// <summary>CIWS만 켜 두고 미사일을 쏘아 요격·피격을 센다. 방향은 사격 금지 구역이 없는 앞쪽 부채꼴로 모은다.</summary>
        private IEnumerator CiwsOnly(string label, int missiles, float spreadDeg, List<(string, int, int)> results)
        {
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            var disabled = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not CiwsModule && r is not IRadarSource) { r.enabled = false; disabled.Add(r); }
            foreach (var (_, u) in AmmoUsers()) u.Ammo.Refill();
            CombatStats.Reset();

            // 앞쪽(±spread/2)에서 동시에 날아오게: 한 발씩 각도를 나눠 쏜다
            for (int i = 0; i < missiles; i++)
            {
                float a = missiles == 1 ? 0f : -spreadDeg * 0.5f + spreadDeg * i / (missiles - 1);
                CombatDevTools.MissileVolley(1, 30f, a, damageOverride: 0.2f);
            }
            var mounts = new List<CiwsModule>();
            foreach (var c in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None)) if (c.Instance != null && c.enabled) mounts.Add(c);
            var shots0 = new List<int>();
            foreach (var c in mounts) shots0.Add(c.ShotsFired);
            var bursts0 = new List<int>();
            foreach (var c in mounts) bursts0.Add(c.BurstCount);
            var reasons = new Dictionary<string, int>();
            float start = Time.time;
            while (Time.time - start < 12f && TargetRegistry.Get(TargetKind.Missile).Count > 0)
            {
                for (int i = 0; i < mounts.Count; i++)
                {
                    string key = $"#{i}:{(mounts[i].HasTarget ? (string.IsNullOrEmpty(mounts[i].LastBlockReason) ? "사격" : mounts[i].LastBlockReason) : "표적 없음")}";
                    reasons[key] = reasons.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            var diag = new StringBuilder();
            for (int i = 0; i < mounts.Count; i++) diag.Append($"#{i} {mounts[i].ShotsFired - shots0[i]}발 · 점사 {mounts[i].BurstCount - bursts0[i]}회 · ");
            foreach (var kv in reasons) diag.Append($"{kv.Key} {kv.Value}f · ");

            int intercepted = CombatStats.Threats.TryGetValue(ThreatOutcome.Intercepted, out var ie) ? ie.Count : 0;
            int hit = CombatStats.Threats.TryGetValue(ThreatOutcome.HitShip, out var he) ? he.Count : 0;
            int fired = CombatStats.Weapons.TryGetValue("CIWS", out var we) ? we.Fired : 0;
            _report.AppendLine($"- {label}: 미사일 {missiles}발 → 요격 {intercepted} · 피격 {hit} (CIWS {fired}발)");
            _report.AppendLine($"  - 문별: {diag}");
            results.Add((label, intercepted, hit));
            foreach (var d in disabled) if (d != null) d.enabled = true;
        }

        /// <summary>
        /// 2차 검사: CIWS 교전 절차·분담(1문 vs 2문), 미사일정 연발, VLS 표적 가치 규칙, 분류별 효율 데이터.
        /// </summary>
        private IEnumerator DefenseCheck()
        {
            _report.AppendLine("\n## 방어·표적 분류 (2차)");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(1f);

            // 1) 분류 데이터
            var sb = new StringBuilder();
            foreach (var id in new[] { "ene_fastboat", "ene_missileboat", "ene_submarine", "ene_boss", "ene_drone", "ene_fighter" })
            {
                var def = CombatDevTools.FindEnemy(id);
                if (def == null) continue;
                sb.Append($"{def.DisplayName} {def.Category}/{def.TargetValue} · ");
                if (def.Category == TargetCategory.Unspecified) Fail($"{id}: 표적 분류가 비어 있음");
            }
            _report.AppendLine($"- 분류/가치: {sb}");

            yield return BurstCheck();

            // 2) CIWS 1문
            var results = new List<(string, int, int)>();
            int ciwsCount = 0;
            foreach (var r in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None)) if (r.Instance != null) ciwsCount++;
            _report.AppendLine($"- 설치된 CIWS {ciwsCount}문");
            yield return CiwsOnly($"CIWS {ciwsCount}문 · 2발", 2, 20f, results);
            yield return CiwsOnly($"CIWS {ciwsCount}문 · 6발 포화", 6, 60f, results);

            // 3) CIWS 한 문 추가
            bool added = CombatDevTools.InstallModule("mod_ciws");
            yield return new WaitForSeconds(0.5f);
            int ciwsAfter = 0;
            foreach (var r in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None)) if (r.Instance != null) ciwsAfter++;
            yield return CiwsOnly($"CIWS {ciwsAfter}문 · 6발 포화", 6, 60f, results);
            if (!added) Fail("CIWS 추가 설치 실패");

            if (results.Count == 3)
            {
                if (results[0].Item2 == 0) Fail("CIWS가 2발 중 하나도 요격하지 못함");
                if (results[1].Item3 == 0) _report.AppendLine("  - 참고: CIWS 1문이 6발 포화를 모두 막음(포화 돌파가 일어나지 않음)");
                if (results[2].Item2 < results[1].Item2) _report.AppendLine("  - 참고: CIWS를 늘렸는데 요격 수가 늘지 않음");
            }

            // 4) 미사일정 연발: 한 척이 한 번에 몇 발 쏘는지
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            var disabled = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not IRadarSource) { r.enabled = false; disabled.Add(r); }
            var boat = CombatDevTools.SpawnRing("ene_missileboat", 1, 34f);
            int maxInFlight = 0;
            float start = Time.time;
            while (Time.time - start < 8f)
            {
                maxInFlight = Mathf.Max(maxInFlight, TargetRegistry.Get(TargetKind.Missile).Count);
                yield return null;
            }
            foreach (var d in disabled) if (d != null) d.enabled = true;
            var boatDef = CombatDevTools.FindEnemy("ene_missileboat");
            int salvo = boatDef != null ? boatDef.SalvoSize : 1;
            _report.AppendLine($"- 미사일정 1척: 연발 {salvo}발 설정, 동시에 날아간 미사일 최대 {maxInFlight}발");
            if (maxInFlight < salvo) Fail($"미사일정 연발이 {salvo}발인데 동시 비행 {maxInFlight}발");
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ 탄약고·웨이브(3차)

        /// <summary>
        /// 3차 검사: 탄약고 근접 보너스(용량·보급), 여러 개 겹쳐도 하나만 적용, 파괴 시 유폭과 보너스 해제,
        /// 스테이지 웨이브의 위협 혼합, 보스 호위 스폰.
        /// </summary>
        private IEnumerator LogisticsCheck()
        {
            _report.AppendLine("\n## 탄약고·웨이브 (3차)");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);

            // 1) 기관포 한 문을 기준으로
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            AutocannonModule gun = null;
            if (ship != null && ship.Grid != null)
                foreach (var m in ship.Grid.Modules)
                    if (m?.Runtime is AutocannonModule a && !m.IsDestroyed) { gun = a; break; }
            if (gun == null) { Fail("기관포가 없어 탄약고 검사를 못 함"); yield break; }

            int baseCap = gun.Ammo.Capacity;
            float baseInterval = gun.Ammo.Interval;

            var mag = CombatDevTools.InstallModuleNear("mod_magazine", gun.Instance.Origin, 1);
            yield return null;
            int magCap = gun.Ammo.Capacity;
            float magInterval = gun.Ammo.Interval;
            _report.AppendLine($"- 기관포 {gun.Instance.Origin}: 탄약고 없음 {baseCap}발·보급 {baseInterval:0.0}초 → 탄약고 {(mag != null ? mag.Origin.ToString() : "설치 실패")} 옆 {magCap}발·보급 {magInterval:0.0}초");
            if (mag == null) { Fail("기관포 옆에 탄약고를 설치하지 못함"); yield break; }
            if (magCap != Mathf.RoundToInt(baseCap * 1.5f)) Fail($"탄약고 용량 보너스가 +50%가 아님({baseCap} → {magCap})");
            if (Mathf.Abs(magInterval - baseInterval / 1.25f) > 0.05f) Fail($"탄약고 보급 보너스가 25%가 아님({baseInterval:0.00} → {magInterval:0.00})");

            // 2) 두 번째 탄약고: 겹쳐도 하나만
            var mag2 = CombatDevTools.InstallModuleNear("mod_magazine", gun.Instance.Origin, 2);
            yield return null;
            _report.AppendLine($"- 탄약고 2개 겹침: {gun.Ammo.Capacity}발 (하나만 적용 기대 {magCap})");
            if (mag2 != null && gun.Ammo.Capacity != magCap) Fail("탄약고 보너스가 겹쳐 쌓임");

            // 3) CIWS는 탄약고 보너스를 받지 않는다
            CiwsModule ciws = null;
            foreach (var m in ship.Grid.Modules) if (m?.Runtime is CiwsModule c && !m.IsDestroyed) { ciws = c; break; }
            if (ciws != null)
            {
                var near = CombatDevTools.InstallModuleNear("mod_magazine", ciws.Instance.Origin, 1);
                yield return null;
                _report.AppendLine($"- CIWS 옆 탄약고({(near != null ? "설치" : "자리 없음")}): CIWS 탄약 {ciws.Ammo.Capacity}발 (보너스 없음 기대 {ciws.Definition.Stats.MagazineCapacity})");
                if (near != null && ciws.Ammo.Capacity != ciws.Definition.Stats.MagazineCapacity) Fail("CIWS가 탄약고 보너스를 받음");
                if (near != null) CombatDevTools.RemoveModule(near);   // 기관포 검사에 섞이지 않게
            }

            // 4) 유폭: 두 번째 탄약고를 먼저 치우고(연쇄 방지) 첫 탄약고를 파괴
            if (mag2 != null) CombatDevTools.RemoveModule(mag2);
            yield return null;
            float gunHpBefore = gun.Instance.Hp;
            float hullBefore = ship.HullHp;
            mag.TakeDamage(99999f);
            float gunHpAfter = gun.Instance.Hp;
            var cook = MagazineModule.LastCookOff;
            yield return null;
            _report.AppendLine($"- 유폭: 피해 모듈 {cook.damaged}개, 파괴 {cook.destroyed}개, 옆 기관포 HP {gunHpBefore:0} → {gunHpAfter:0}, 선체 {hullBefore - ship.HullHp:0} 피해, 기관포 탄약 용량 {gun.Ammo.Capacity}발");
            if (cook.damaged == 0) Fail("유폭 피해를 받은 모듈이 없음");
            if (gunHpAfter >= gunHpBefore) Fail("탄약고 옆 기관포가 유폭 피해를 받지 않음");
            if (!gun.Instance.IsDestroyed && gun.Ammo.Capacity != baseCap) Fail($"탄약고가 부서졌는데 보너스가 남음({gun.Ammo.Capacity})");
            yield return Shot("cookoff");

            // 5) 웨이브 구성
            var director = Object.FindFirstObjectByType<StageDirector>(FindObjectsInactive.Include);
            var stages = director != null
                ? typeof(StageDirector).GetField("stages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(director) as Game.Data.RoundSet[]
                : null;
            if (stages == null) { Fail("StageDirector 스테이지를 읽지 못함"); yield break; }

            Game.Data.RoundSet.Round bossRound = default;
            bool hasBossRound = false;
            for (int s = 0; s < stages.Length; s++)
            {
                if (stages[s] == null) continue;
                _report.AppendLine($"- 스테이지 {s + 1}:");
                for (int i = 0; i < stages[s].Count; i++)
                {
                    var r = stages[s].Get(i);
                    var kinds = new StringBuilder();
                    var categories = new HashSet<TargetCategory>();
                    if (r.Entries != null)
                        foreach (var e in r.Entries)
                            if (e.Enemy != null && e.Weight > 0f)
                            {
                                kinds.Append($"{e.Enemy.DisplayName} {e.Weight:0.#} · ");
                                categories.Add(e.Enemy.Category);
                            }
                    string boss = r.Boss != null ? $" + 보스 {r.Boss.DisplayName}" + (r.Escort != null ? $"·호위 {r.Escort.DisplayName}×{r.EscortCount}" : "") : "";
                    _report.AppendLine($"  - {i + 1}. {r.Title}: {kinds}{boss}");
                    if (s == 0 && i >= 1 && categories.Count < 2) Fail($"스테이지 1 {i + 1}구간 '{r.Title}'이 한 종류 위협뿐");
                    if (r.Boss != null)
                    {
                        if (r.Escort == null || r.EscortCount <= 0) Fail($"스테이지 {s + 1} 보스 구간에 호위가 없음");
                        if (s == 0) { bossRound = r; hasBossRound = true; }
                    }
                }
            }

            // 6) 보스 호위 실제 스폰
            if (hasBossRound && EnemySpawner.Instance != null && ship != null)
            {
                CombatDevTools.ClearBattlefield();
                yield return null;
                int before = CountAliveEnemies();
                EnemySpawner.Instance.SetPhase(bossRound, ship.transform);
                yield return new WaitForSeconds(0.3f);
                int spawned = CountAliveEnemies() - before;
                _report.AppendLine($"- 보스 구간 시작: 보스 1 + 호위 {bossRound.EscortCount} 기대, 실제 {spawned}척");
                if (spawned != 1 + bossRound.EscortCount) Fail($"보스 구간 스폰 수 {spawned} ≠ {1 + bossRound.EscortCount}");
                yield return Shot("boss_escort");
                CombatDevTools.ClearBattlefield();
            }
        }
    }
}
#endif
