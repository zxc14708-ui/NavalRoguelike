#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Game.UI;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 시작 화면 해군기지(비행장 전투기 경로·계단 현상, 2026-10-11).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 이륙 전투기(Fighter1) 지상 경로가 평행 유도로에 서 있는 초계기(P3C_Taxi, 섬 좌표 (−470, −262))를 지나지 않는지.
        /// 2) 계단 현상: 같은 화면(개관 시점, 회전 멈춤)을 설정만 바꿔 1920×1080으로 찍고(렌더 텍스처를 UI로 줄여 붙이는 실제 경로)
        ///    활주로 부분을 잘라 3배로 키워 저장한다. 설정별 렌더 텍스처 크기·MSAA·프레임 시간을 적는다.
        /// </summary>
        private IEnumerator HarborAACheck()
        {
            _report.AppendLine("\n## 시작 화면 비행장(전투기 경로·계단 현상)");

            // ---- 1) 경로
            var parked = new Vector2(-470f, -262f);
            float minOld = MinGroundDistance(new[] { new Vector3(60, 4.6f, -185), new Vector3(60, 4.6f, -262), new Vector3(-740, 4.6f, -262), new Vector3(-760, 4.6f, -330), new Vector3(400, 4.6f, -330) }, parked);
            float minNew = MinGroundDistance(NavalBaseMenu.FighterRoute, parked);
            _report.AppendLine($"- 전투기 지상 경로와 유도로 위 초계기(−470, −262)의 최소 거리: 예전 {minOld:0} m → 지금 {minNew:0} m (초계기 날개폭 약 30 m)");
            if (minNew < 40f) Fail("전투기가 유도로 위 초계기를 지나감");

            // 초계기도 움직인다(2026-10-11): 두 항공기를 같은 시계로 88초 동안 0.05초마다 놓아 보고 가장 가까운 거리
            float fTotal = 0f, pTotal = 0f;
            var fTimes = new[] { 6f, 8f, 4f, 8f, 7f, 6f, 12f, 6f, 9f, 8f, 4f, 5f, 5f };
            foreach (var x in fTimes) fTotal += x;
            foreach (var x in NavalBaseMenu.P3CTimes) pTotal += x;
            float minGround = float.MaxValue, minAny = float.MaxValue, atGround = 0f;
            for (float clock = 0f; clock < 88f; clock += 0.05f)
            {
                Vector3 f = RoutePoint(NavalBaseMenu.FighterRoute, fTimes, (clock + 5f) % fTotal);
                Vector3 q = RoutePoint(NavalBaseMenu.P3CRoute, NavalBaseMenu.P3CTimes, (clock + 3f) % pTotal);
                float d = Vector3.Distance(f, q);
                if (d < minAny) minAny = d;
                if (f.y < 15f && q.y < 15f)
                {
                    float g = Vector2.Distance(new Vector2(f.x, f.z), new Vector2(q.x, q.z));
                    if (g < minGround) { minGround = g; atGround = clock; }
                }
            }
            _report.AppendLine($"- 움직이는 초계기: 주기 {pTotal:0}초(전투기 {fTotal:0}초) · 둘 다 지상일 때 최소 거리 {minGround:0} m(시각 {atGround:0.0}) · 언제나 최소 거리 {minAny:0} m");
            if (!Mathf.Approximately(pTotal, fTotal)) Fail("초계기와 전투기 주기가 달라 시간이 어긋남");
            if (minGround < 45f || minAny < 40f) Fail("초계기와 전투기가 너무 가까워짐");

            // ---- 2) 계단 현상
            var canvasGo = new GameObject("HarborAA canvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var menu = NavalBaseMenu.Create(canvasGo.transform);
            if (menu == null) { Fail("시작 화면 해군기지를 만들지 못함"); Destroy(canvasGo); yield break; }
            foreach (var t in canvasGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
            // 떼어 낸 초계기: 따로 있고 움직이며, 합친 메시에는 그 자리에 삼각형이 남지 않았는지
            Transform islandRoot = null;
            foreach (Transform child in menu.ViewCamera.transform.parent)
                if (child.Find("NavalAirStation") != null) { islandRoot = child; break; }
            var p3c = islandRoot != null ? islandRoot.Find("NavalAirStation/P3C_Taxi") : null;
            int leftover = 0;
            if (islandRoot != null)
            {
                var box = new Bounds(new Vector3(-471.5f, 9.4f, -262f), new Vector3(42f, 11.2f, 33f));
                var combined = islandRoot.Find("Combined");
                if (combined != null)
                    foreach (var mf in combined.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                        var m = islandRoot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                        var v = mf.sharedMesh.vertices; var tri = mf.sharedMesh.triangles;
                        for (int i = 0; i < tri.Length; i += 3)
                            if (box.Contains(m.MultiplyPoint3x4(v[tri[i]])) && box.Contains(m.MultiplyPoint3x4(v[tri[i + 1]])) && box.Contains(m.MultiplyPoint3x4(v[tri[i + 2]]))) leftover++;
                    }
            }
            Vector3 p0 = p3c != null ? p3c.localPosition : Vector3.zero;
            float watch = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - watch < 3f) { menu.RenderNow(); yield return null; }
            Vector3 p1 = p3c != null ? p3c.localPosition : Vector3.zero;
            _report.AppendLine($"- 떼어 낸 초계기: 있음 {Yes(p3c != null)} · 부품 {(p3c != null ? p3c.GetComponentsInChildren<Renderer>().Length : 0)}개 · 합친 메시에 남은 초계기 삼각형 {leftover}개 · 3초 동안 위치 {p0} → {p1}");
            if (p3c == null) Fail("시작 화면에 P3C_Taxi가 없음(Naval/Art/Detach Taxiing P-3C를 실행해야 함)");
            if (leftover > 0) Fail("합친 메시에 멈춰 선 초계기가 남아 있음");
            float settle = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - settle < 4f) { menu.RenderNow(); yield return null; }   // 개관 시점에 자리 잡기
            menu.Paused = true;

            float ss0 = NavalBaseMenu.Supersample; int msaa0 = NavalBaseMenu.Msaa; var aa0 = NavalBaseMenu.PostAntialiasing;
            var variants = new (string name, float ss, int msaa, AntialiasingMode aa)[]
            {
                ("before", 1f, 1, AntialiasingMode.None),                      // 예전 실제 상태(URP MSAA 꺼짐 → 렌더 텍스처 MSAA 무시)
                ("msaa8", 1f, 8, AntialiasingMode.None),
                ("msaa8_smaa", 1f, 8, AntialiasingMode.SubpixelMorphologicalAntiAliasing),
                ("ss15_msaa_smaa", 1.5f, 8, AntialiasingMode.SubpixelMorphologicalAntiAliasing),
                ("ss2_msaa_smaa", 2f, 8, AntialiasingMode.SubpixelMorphologicalAntiAliasing),
                ("ss15_taa", 1.5f, 1, AntialiasingMode.TemporalAntiAliasing),
            };
            foreach (var v in variants)
            {
                NavalBaseMenu.Supersample = v.ss; NavalBaseMenu.Msaa = v.msaa; NavalBaseMenu.PostAntialiasing = v.aa;
                menu.ApplyAntialiasing();
                // 프레임 시간(시작 화면 카메라가 매 프레임 그린다) + TAA 수렴
                float t0 = Time.realtimeSinceStartup; int frames = 0;
                // 배치 모드 플레이어는 카메라를 저절로 그리지 않는다 — 매 프레임 직접 그린다(TAA는 이전 프레임을 쌓는다)
                float renderMs = 0f;
                while (Time.realtimeSinceStartup - t0 < 1.5f)
                {
                    float r0 = Time.realtimeSinceStartup;
                    menu.RenderNow();
                    renderMs += Time.realtimeSinceStartup - r0;
                    frames++;
                    yield return null;
                }
                float ms = renderMs * 1000f / Mathf.Max(1, frames);
                yield return new WaitForEndOfFrame();
                menu.RenderNow();
                var rt = menu.ViewCamera.targetTexture;
                var tex = Downscale(ReadTexture(rt), 1920, 1080);
                // 활주로 중앙(섬 좌표 (−50, 4.6, −300))이 보이는 곳을 중심으로 480×270 잘라 3배
                Vector3 vp = menu.ViewCamera.WorldToViewportPoint(IslandPoint(menu, new Vector3(-50f, 4.6f, -300f)));
                var crop = Crop(tex, Mathf.RoundToInt(vp.x * 1920), Mathf.RoundToInt(vp.y * 1080), 480, 270, 3);
                File.WriteAllBytes(Path.Combine(OutputDirectory, $"harbor_aa_{v.name}.png"), crop.EncodeToPNG());
                if (v.name == "before" || v.name == "ss15_msaa_smaa")
                    File.WriteAllBytes(Path.Combine(OutputDirectory, $"harbor_full_{v.name}.png"), tex.EncodeToPNG());
                _report.AppendLine($"- {v.name}: 렌더 텍스처 {rt.width}×{rt.height} · MSAA {rt.antiAliasing} · 후처리 AA {v.aa} · 그리기 {ms:0.0} ms(CPU 측) (활주로 중심 화면 {vp.x:0.00},{vp.y:0.00})");
                Destroy(tex); Destroy(crop);
            }
            NavalBaseMenu.Supersample = ss0; NavalBaseMenu.Msaa = msaa0; NavalBaseMenu.PostAntialiasing = aa0;
            Destroy(canvasGo);   // 시작 화면 정리(조명·URP 설정 복원은 NavalBaseMenu.OnDisable)
            yield return null;
        }

        /// <summary>섬 좌표 → 월드(시작 화면 무대 아래 NavalAirStation이 있는 섬 루트 기준).</summary>
        private static Vector3 IslandPoint(NavalBaseMenu menu, Vector3 local)
        {
            var stage = menu.ViewCamera.transform.parent;
            foreach (Transform child in stage)
                if (child.Find("NavalAirStation") != null) return child.TransformPoint(local);
            return stage.TransformPoint(local);
        }

        /// <summary>NavalBaseMenu.LateUpdate와 같은 방식으로 경로 위 위치(섬 좌표).</summary>
        private static Vector3 RoutePoint(Vector3[] points, float[] times, float time)
        {
            int segment = 0;
            while (time >= times[segment] && segment < times.Length - 1) { time -= times[segment]; segment++; }
            return Vector3.Lerp(points[segment], points[(segment + 1) % points.Length], time / times[segment]);
        }

        private static float MinGroundDistance(Vector3[] route, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < route.Length; i++)
            {
                var a = route[i]; var b = route[(i + 1) % route.Length];
                if (a.y > 10f || b.y > 10f) continue;   // 하늘 구간은 제외
                Vector2 a2 = new(a.x, a.z), b2 = new(b.x, b.z), ab = b2 - a2;
                float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p - a2, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a2 + ab * t));
            }
            return best;
        }

        /// <summary>렌더 텍스처를 읽는다(MSAA는 MSAA 없는 임시 텍스처로 옮기며 풀린다).</summary>
        private static Texture2D ReadTexture(RenderTexture src)
        {
            var tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, tmp);
            var prevActive = RenderTexture.active;
            RenderTexture.active = tmp;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(tmp);
            return tex;
        }

        /// <summary>화면 크기로 줄인다(넓이 평균 — 화면의 밉맵 축소와 비슷). 원본 텍스처는 지운다.</summary>
        private static Texture2D Downscale(Texture2D src, int w, int h)
        {
            var px = src.GetPixels32();
            int sw = src.width, sh = src.height;
            var outPx = new Color32[w * h];
            float fx = (float)sw / w, fy = (float)sh / h;
            for (int y = 0; y < h; y++)
            {
                int y0 = Mathf.FloorToInt(y * fy), y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt((y + 1) * fy));
                for (int x = 0; x < w; x++)
                {
                    int x0 = Mathf.FloorToInt(x * fx), x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt((x + 1) * fx));
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int yy = y0; yy < y1 && yy < sh; yy++)
                        for (int xx = x0; xx < x1 && xx < sw; xx++)
                        { var c = px[yy * sw + xx]; r += c.r; g += c.g; b += c.b; n++; }
                    n = Mathf.Max(1, n);
                    outPx[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                }
            }
            Object.Destroy(src);
            var dst = new Texture2D(w, h, TextureFormat.RGB24, false);
            dst.SetPixels32(outPx);
            dst.Apply();
            return dst;
        }

        private static Texture2D Crop(Texture2D src, int cx, int cy, int w, int h, int scale)
        {
            int x0 = Mathf.Clamp(cx - w / 2, 0, src.width - w), y0 = Mathf.Clamp(cy - h / 2, 0, src.height - h);
            var px = src.GetPixels(x0, y0, w, h);
            var dst = new Texture2D(w * scale, h * scale, TextureFormat.RGB24, false);
            var outPx = new Color[w * scale * h * scale];
            for (int y = 0; y < h * scale; y++)
                for (int x = 0; x < w * scale; x++)
                    outPx[y * w * scale + x] = px[(y / scale) * w + x / scale];
            dst.SetPixels(outPx);
            dst.Apply();
            return dst;
        }
    }
}
#endif
