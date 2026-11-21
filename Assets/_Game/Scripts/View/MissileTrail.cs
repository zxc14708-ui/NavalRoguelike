using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 미사일 · 로켓 연기 꼬리(2026-11-21 사용자: 더 사실적으로). 예전에는 흰 띠(TrailRenderer) 한 줄이었다.
    ///   - 지나온 길 0.25m마다 연기 뭉치 하나(서로 겹쳐 이어진 연기 기둥): 0.4~0.5m로 나와 수명 동안 5배로 부풀고, 뒤로 조금 밀리다 바람에 흘러 옅어진다.
    ///     적 미사일은 길게(2.4~3.2초), 우리 미사일 · 로켓은 짧게(1.5~2.1초 — 많이 쏘므로 화면을 덮지 않게).
    ///   - 노즐: 매 프레임 밝은 불꽃 한 점(흰빛이 도는 주황) — 꼬리 맨 앞이 달아올라 보인다.
    ///   - 밤에는 연기가 조금 어둡게(달빛에 비친 회색).
    /// 입자는 DecorFx.MissileSmoke · Fire를 모든 미사일이 나눠 쓴다.
    /// </summary>
    public static class MissileTrail
    {
        public const float Spacing = 0.25f;

        public struct State
        {
            public Vector3 Last;
            public bool Started;
            public float Carry;
        }

        public static void Tick(ref State s, Vector3 nozzle, Vector3 forward, bool enemy, float dt)
        {
            if (dt <= 0f) return;
            if (!s.Started) { s.Last = nozzle; s.Started = true; s.Carry = 0f; return; }
            Vector3 path = nozzle - s.Last;
            float dist = path.magnitude;
            if (dist > 60f) { s.Last = nozzle; return; }   // 순간 이동(풀 재사용)은 잇지 않는다

            float night = DayNight.Darkness01;
            float shade = Mathf.Lerp(0.86f, 0.58f, night);   // 밤에는 달빛에 비친 회색(너무 어두우면 꼬리가 안 보인다)
            Vector3 back = -forward;
            s.Carry += dist;
            int n = 0;
            while (s.Carry >= Spacing && n < 40)
            {
                s.Carry -= Spacing;
                float t = dist > 1e-4f ? 1f - s.Carry / dist : 1f;
                Vector3 at = Vector3.Lerp(s.Last, nozzle, Mathf.Clamp01(t)) + Random.insideUnitSphere * 0.06f;
                float g = shade * Random.Range(0.92f, 1.04f);
                float life = enemy ? Random.Range(2.4f, 3.2f) : Random.Range(1.5f, 2.1f);
                DecorFx.Emit(DecorFx.MissileSmoke, at, back * Random.Range(0.6f, 1.6f) + Random.insideUnitSphere * 0.35f + DecorFx.Wind * 0.35f,
                    life, Random.Range(0.55f, 0.75f), new Color(g, g, g * 1.04f, enemy ? 0.55f : 0.45f));
                n++;
            }
            s.Last = nozzle;

            // 노즐 불꽃(밤에는 조금 더 밝게)
            DecorFx.Emit(DecorFx.Fire, nozzle + back * 0.1f, back * 2.5f, Random.Range(0.04f, 0.07f), Random.Range(0.28f, 0.42f) * (1f + 0.3f * night),
                Color.Lerp(new Color(1f, 0.95f, 0.75f, 1f), new Color(1f, 0.55f, 0.18f, 0.95f), Random.value * 0.6f));
        }
    }
}
