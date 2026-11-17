#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Core;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>-shoreOnly: 해안 물빛 시험(ShoreWaterMask · Naval/Ocean) 켬/끔 비교.</summary>
        public bool ShoreOnly;

        /// <summary>
        /// 해안 물빛 시험(2026-11-01): 절차형 섬과 Codex 모델 섬 하나씩 옆으로 배를 옮겨,
        /// 같은 순간(시간 멈춤)을 해안 물빛 끔/켬으로 게임 화면과 해안 가까이에서 찍는다(`shore_*_off/on.png`).
        /// </summary>
        private IEnumerator ShoreCheck()
        {
            _report.AppendLine("\n## 해안 물빛(시험 — 얕은 물빛 · 물 밑 빛 무늬 · 해안 거품)");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            DisableWeapons(null);
            yield return new WaitForSeconds(2f);

            var mask = Game.View.ShoreWaterMask.Instance;
            if (mask == null) { Fail("ShoreWaterMask가 없음"); yield break; }

            // 같은 장면으로 다시 비교할 수 있게 섬 배치를 고정 시드로 — 절차형 섬과 Codex 모델 섬이 둘 다 있는 첫 시드
            var field = Game.World.IslandField.Instance;
            if (field != null)
            {
                for (int seed = 20261101; seed < 20261101 + 30; seed++)
                {
                    field.Regenerate(seed);
                    yield return new WaitForSeconds(0.3f);
                    bool proc = false, model = false;
                    foreach (var i in Game.World.Islands.All) { if (i.Bank) continue; if (i.Art != null) model = true; else proc = true; }
                    if (proc && model) { _report.AppendLine($"- 섬 배치 시드 {seed}(절차형·모델 섬 모두 있음)"); break; }
                }
            }

            foreach (bool art in new[] { false, true })
            {
                // 배에서 가까운 섬(절차형 / Codex 모델)을 고른다 — 수로 기슭은 뺀다
                Game.World.IslandInfo pick = null;
                float best = float.MaxValue;
                foreach (var i in Game.World.Islands.All)
                {
                    if (i.Bank || (i.Art != null) != art || i.Rim == null) continue;
                    float d = Vector3.Distance(i.Center, ship.transform.position);
                    if (d < best) { best = d; pick = i; }
                }
                string label = art ? "model" : "procedural";
                if (pick == null) { _report.AppendLine($"- {(art ? "Codex 모델" : "절차형")} 섬 없음 — 건너뜀"); continue; }

                // 섬 남쪽(카메라 쪽) 해안에서 바다로 28m — 배를 섬과 나란히 둔다
                Vector3 dir = Vector3.back;
                Vector2 shore2 = pick.Rim[0];
                float far = float.MinValue;
                foreach (var r in pick.Rim) if (-r.y > far) { far = -r.y; shore2 = r; }
                var shore = new Vector3(shore2.x, 0f, shore2.y);
                var pos = shore + dir * 30f;
                pos.y = ship.transform.position.y;
                ship.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.right));
                if (ship.TryGetComponent<Rigidbody>(out var body)) { body.position = pos; body.rotation = ship.transform.rotation; }
                Time.timeScale = 1f;
                yield return new WaitForSeconds(1.5f);   // 카메라가 따라오고 주변 섬이 생길 시간

                var mark = new GameObject("Shore shot").transform;
                mark.position = shore + dir * 6f;
                mark.rotation = Quaternion.LookRotation(Vector3.right);

                Time.timeScale = 0f;   // 끔/켬을 같은 순간으로
                Game.View.ShoreWaterMask.Enabled = false;
                yield return null;
                yield return Shot($"shore_{label}_off");
                yield return CloseShot($"shore_{label}_close_off", mark, 34f);
                Game.View.ShoreWaterMask.Enabled = true;
                mask.BakeNow();
                yield return null;
                yield return Shot($"shore_{label}_on");
                yield return CloseShot($"shore_{label}_close_on", mark, 34f);
                Time.timeScale = TimeScale;
                _report.AppendLine($"- {(art ? $"Codex 모델 섬 {pick.Art}" : "절차형 섬")}(반경 {pick.Radius:0}m): 구운 섬 {mask.BakedIslands} · 해안 20m 안 텍셀 {mask.ShallowFraction:P1} · `shore_{label}_off/on.png`, `shore_{label}_close_off/on.png`");
                if (mask.BakedIslands == 0 || mask.ShallowFraction <= 0f) Fail("해안 거리 텍스처에 섬이 구워지지 않음");
                Destroy(mark.gameObject);
            }
            _report.AppendLine($"- 물결 텍스처(FFT): {(Game.View.OceanWaveTexture.Texture != null ? $"{Game.View.OceanWaveTexture.Texture.width}² · 계산 {Game.View.OceanWaveTexture.BuildMs:0}ms" : "없음")}");
            if (Game.View.OceanWaveTexture.Texture == null) Fail("물결 텍스처가 만들어지지 않음");
            // 움직이는 파도: 작업 스레드 계산이 끝나길 기다린 뒤, 배 옆을 0.25초 간격 12장(움직임 확인용)
            Time.timeScale = 1f;
            for (float w = 0f; w < 8f && Game.View.OceanWaveTexture.Animated == null; w += 0.25f) yield return new WaitForSeconds(0.25f);
            var anim = Game.View.OceanWaveTexture.Animated;
            _report.AppendLine($"- 움직이는 파도: {(anim != null ? $"{anim.depth}장 · 한 바퀴 {Game.View.OceanWaveTexture.Loop:0}초 · 계산 {Game.View.OceanWaveTexture.AnimBuildMs:0}ms(작업 스레드)" : "없음")} · `sea_anim_00~11.png`");
            if (anim == null) Fail("움직이는 파도가 만들어지지 않음");
            for (int f = 0; f < 12; f++)
            {
                yield return CloseShot($"sea_anim_{f:00}", ship.transform, 12f);
                yield return new WaitForSeconds(0.25f);
            }
            // 배 옆 수면(물빛·잔물결·흰 물거품) 가까이
            Time.timeScale = 0f;
            yield return CloseShot("sea_ship_close", ship.transform, 12f);
            // 구름 그림자를 끈 같은 장면(얼룩이 구름 그늘인지 바다 무늬인지 가르기)
            var clouds = Game.View.CloudShadows.Instance;
            if (clouds != null)
            {
                clouds.enabled = false;
                yield return null;
                yield return Shot("sea_noclouds");
                clouds.enabled = true;
                yield return null;
                yield return Shot("sea_clouds");
            }
            // 항적: 섬을 치우고 전속 직진 → 전타 선회, 게임 화면과 선미 가까이
            Time.timeScale = TimeScale;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            var wake = ship.GetComponent<Game.View.ShipWake>();
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(9f);
            Time.timeScale = 0f;
            yield return Shot("wake_straight");
            var sternMark = new GameObject("Wake shot").transform;
            sternMark.SetPositionAndRotation(ship.transform.position - ship.transform.forward * 14f, ship.transform.rotation);
            yield return CloseShot("wake_straight_close", sternMark, 20f);
            float straightChurn = wake != null ? wake.Churn : 0f;
            Time.timeScale = TimeScale;
            ship.DevRudderOverride = 1f;
            float turnChurn = 0f;
            for (float tt = 0f; tt < 6f; tt += 0.25f) { yield return new WaitForSeconds(0.25f); turnChurn = Mathf.Max(turnChurn, wake != null ? wake.Churn : 0f); }
            Time.timeScale = 0f;
            yield return Shot("wake_turn");
            sternMark.SetPositionAndRotation(ship.transform.position - ship.transform.forward * 8f, ship.transform.rotation);
            yield return CloseShot("wake_turn_close", sternMark, 22f);
            Destroy(sternMark.gameObject);
            _report.AppendLine($"- 항적: 직진 휘저음 {straightChurn:0.00} · 선회 최대 {turnChurn:0.00} · 켈빈 점 {wake?.KelvinPoints} · 물살 점 {wake?.WashPoints} · `wake_straight(_close).png`, `wake_turn(_close).png`");
            if (wake == null || wake.WashPoints < 10) Fail("항속 중 항적이 남지 않음");
            if (turnChurn <= straightChurn + 0.2f) Fail("선회 중 물살이 더 휘저어지지 않음");
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);

            Game.View.ShoreWaterMask.Enabled = false;
            Time.timeScale = TimeScale;
        }
    }
}
#endif
