#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;
using Game.UI;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 시작 화면 군항 운영 연출(Codex HarborOperations_v1 2026-11-20 · HarborDistricts_v2 예인정 2척 2026-11-21).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 시작 화면 군항을 만들고 운영 덧붙임이 붙었는지, 170초 주기 동안 화물선 · 예인정 · 예인줄 · 크레인 컨테이너가 제대로 움직이고
        /// 제자리로 돌아오는지, 일시정지 중에는 멈추는지 확인한다. 장면별 근접 사진(크레인 하역 · 출항 예인 · 입항 · 비행장 · 개관).
        /// </summary>
        private IEnumerator HarborOpsCheck()
        {
            _report.AppendLine("\n## 시작 화면 군항 운영(2026-11-20)");
            var canvasGo = new GameObject("HarborOps canvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.layer = 5;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var menu = NavalBaseMenu.Create(canvasGo.transform);
            if (menu == null) { Fail("시작 화면 군항을 만들지 못함"); Destroy(canvasGo); yield break; }
            foreach (var t in canvasGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
            yield return null;

            var ops = menu.Operations;
            bool ready = ops != null && ops.Ready;
            int renderers = ops != null ? ops.GetComponentsInChildren<Renderer>(true).Length : 0;
            int wrongLayer = 0;
            if (ops != null)
                foreach (var t in ops.GetComponentsInChildren<Transform>(true))
                    if (t.gameObject.layer != menu.ViewCamera.gameObject.layer && ((1 << t.gameObject.layer) & menu.ViewCamera.cullingMask) == 0) wrongLayer++;
            _report.AppendLine($"- 운영 덧붙임: 붙음 {Yes(ops != null)} · 준비 {Yes(ready)}(화물 부두 메시 자르기 성공) · 렌더러 {renderers}개 · 카메라에 안 보이는 레이어 {wrongLayer}개");
            if (!ready) { Fail("군항 운영 덧붙임이 붙지 않음(Resources/Environment/HarborOperations 또는 화물 부두 메시 불일치)"); Destroy(canvasGo); yield break; }
            if (wrongLayer > 0) Fail("운영 덧붙임 일부가 시작 화면 카메라 레이어가 아님");

            float s = ops.transform.lossyScale.x;
            var ship = ops.CargoShip; var tug = ops.Tug; var sternTug = ops.SternTug;
            if (sternTug == null) { Fail("함미 예인정이 없음(HarborDistricts_v2)"); Destroy(canvasGo); yield break; }
            Vector3 rest = ship.localPosition, tugRest = tug.localPosition, sternRest = sternTug.localPosition;

            // ---- 1) 주기: 0.5초마다 놓아 보고 가장 멀리 간 거리 · 예인줄 · 끝에서 제자리
            float far = 0f, tugGap = float.MaxValue, sternGap = float.MaxValue, pairGap = float.MaxValue; bool towSeen = false; int maxLines = 0;
            for (float t = 0f; t < HarborOperations.CycleSeconds; t += 0.5f)
            {
                ops.SetTime(t);
                far = Mathf.Max(far, Vector3.Distance(ship.localPosition, rest));
                if (t > 30f && t < 130f)
                {
                    tugGap = Mathf.Min(tugGap, Vector3.Distance(Flat(ship.localPosition), Flat(tug.localPosition)));
                    sternGap = Mathf.Min(sternGap, Vector3.Distance(Flat(ship.localPosition), Flat(sternTug.localPosition)));
                }
                pairGap = Mathf.Min(pairGap, Vector3.Distance(Flat(tug.localPosition), Flat(sternTug.localPosition)));
                if (ops.TowlineVisible) towSeen = true;
                maxLines = Mathf.Max(maxLines, ops.ActiveTowlines);
            }
            ops.SetTime(HarborOperations.CycleSeconds - 0.01f);
            float back = Vector3.Distance(ship.localPosition, rest), tugBack = Vector3.Distance(tug.localPosition, tugRest);
            float sternBack = Vector3.Distance(sternTug.localPosition, sternRest);
            _report.AppendLine($"- 170초 주기: 화물선 최대 이동 {far:0} m · 예인줄 보임 {Yes(towSeen)}(동시 최대 {maxLines}줄) · 예인 중 화물선과 최소 수평 거리 함수 예인정 {tugGap:0.0} m · 함미 예인정 {sternGap:0.0} m · 예인정끼리 {pairGap:0.0} m · 끝에서 화물선 {back:0.00} m · 함수 {tugBack:0.00} m · 함미 {sternBack:0.00} m 차이");
            if (far < 150f) Fail("화물선이 출항하지 않음");
            if (!towSeen) Fail("예인줄이 한 번도 보이지 않음");
            if (tugGap < 8f || sternGap < 8f) Fail("예인정이 화물선과 겹침");
            if (pairGap < 6f) Fail("예인정 두 척이 서로 겹침");
            if (maxLines < 2) Fail("함수 · 함미 예인줄이 동시에 걸리지 않음");
            if (back > 1f || tugBack > 1f || sternBack > 1f) Fail("한 주기 뒤 화물선 · 예인정이 제자리로 돌아오지 않음");

            // ---- 2) 일시정지 · 배경 정지: dt 0이면 그대로
            ops.SetTime(40f);
            Vector3 before = ship.localPosition;
            menu.Paused = true;
            for (int i = 0; i < 10; i++) { menu.RenderNow(); yield return null; }
            float pausedMove = Vector3.Distance(before, ship.localPosition);
            menu.Paused = false;
            for (int i = 0; i < 10; i++) { menu.RenderNow(); yield return null; }
            float runMove = Vector3.Distance(before, ship.localPosition);
            _report.AppendLine($"- 일시정지 10프레임 이동 {pausedMove:0.000} m · 다시 움직임 10프레임 {runMove:0.00} m");
            if (pausedMove > 0.001f) Fail("일시정지 중에도 화물선이 움직임");
            if (runMove <= 0.001f) Fail("일시정지를 풀어도 화물선이 움직이지 않음");

            // ---- 3) 장면별 근접 사진(시각을 정해 놓고 멈춘 채 찍는다)
            menu.Paused = true;
            var heli = PrivateField<Transform>(ops, "helicopter");
            var truck = PrivateField<Transform>(ops, "truck");
            Vector3 eye = new Vector3(-0.55f, 0.6f, -0.58f).normalized;
            ops.SetTime(8f);  yield return HarborShot(menu, "harbor_ops_crane", ship.position + Vector3.up * 10f * s, eye * 170f * s);
            ops.SetTime(4f);  yield return HarborShot(menu, "harbor_ops_tug_berths", tug.position, eye * 150f * s);
            ops.SetTime(42f); yield return HarborShot(menu, "harbor_ops_tow_out", ship.position, eye * 260f * s);
            ops.SetTime(120f); yield return HarborShot(menu, "harbor_ops_inbound", ship.position, eye * 260f * s);
            if (truck != null) { ops.SetTime(60f); yield return HarborShot(menu, "harbor_ops_yard", truck.position, eye * 120f * s); }
            if (heli != null) { ops.SetTime(4f); yield return HarborShot(menu, "harbor_ops_airfield", heli.position, eye * 140f * s); }
            ops.SetTime(42f);
            for (int i = 0; i < 3; i++) { menu.RenderNow(); yield return null; }
            yield return new WaitForEndOfFrame();
            menu.RenderNow();
            var rt = menu.ViewCamera.targetTexture;
            if (rt != null)
            {
                var tex = Downscale(ReadTexture(rt), 1920, 1080);
                File.WriteAllBytes(Path.Combine(OutputDirectory, "harbor_ops_overview.png"), tex.EncodeToPNG());
                Destroy(tex);
            }
            menu.Paused = false;
            Destroy(canvasGo);
            yield return null;
        }

        /// <summary>시작 화면 카메라 설정을 복사한 카메라로 한 장(1280×720).</summary>
        private IEnumerator HarborShot(NavalBaseMenu menu, string name, Vector3 focus, Vector3 offset)
        {
            yield return new WaitForEndOfFrame();
            var go = new GameObject("HarborShotCam");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(menu.ViewCamera);
            cam.fieldOfView = 30f;
            cam.transform.position = focus + offset;
            cam.transform.LookAt(focus);
            cam.nearClipPlane = Mathf.Max(0.1f, offset.magnitude * 0.02f);
            cam.farClipPlane = offset.magnitude * 6f + 50f;
            const int w = 1280, h = 720;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
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
