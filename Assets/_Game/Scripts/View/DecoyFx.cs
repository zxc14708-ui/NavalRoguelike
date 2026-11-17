using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 기만체 연출(2026-10-04, 판정 없음). 실제 함정 기만체 발사처럼:
    ///   1) 발사: 발사기에서 주황 섬광 + 하얀 발사 연기 구름
    ///   2) 비행: 붉은 꼬리를 끄는 탄이 포물선으로 솟는다(옅은 흰 연기 꼬리)
    ///   3) 공중 폭발: 하얀 섬광·점광 → 하얀 플레어 불똥이 사방으로 퍼져 떨어지고, 은박(채프)이 반짝이며 천천히 흩날린다
    ///   4) 연소: 몇 초 동안 불타는 플레어가 계속 떨어진다(점점 주황으로)
    ///   5) 채프 구름(2026-11-09, 시인성 — "썼는지 잘 안 보인다"): 터진 자리에 희뿌연 구름이 크게 퍼지고, 기만체가 살아 있는 동안
    ///      구름이 계속 덧쌓이며 반짝이는 은박이 아주 천천히 흩날려 떨어진다. 바람을 따라 흐르고 끝으로 갈수록 옅어진다(Lingering).
    /// 모두 DecorFx의 공용 파티클(월드 공간)에 Emit만 한다 — 기만체가 풀로 돌아가도 이미 뿌린 효과는 남는다.
    /// </summary>
    public static class DecoyFx
    {
        private static readonly Color FlareWhite = new(1f, 0.96f, 0.84f, 1f);
        private static readonly Color FlareOrange = new(1f, 0.62f, 0.25f, 1f);
        private static readonly Color TrailRed = new(1f, 0.22f, 0.12f, 1f);

        /// <summary>발사 순간: 발사기 앞 섬광과 하얀 연기 구름(첨부 사진의 발사 연기).</summary>
        public static void Launch(Vector3 position, Vector3 velocity)
        {
            Vector3 dir = velocity.sqrMagnitude > 1e-4f ? velocity.normalized : Vector3.up;
            DecorFx.Emit(DecorFx.Flash, position, Vector3.zero, 0.12f, 1.8f, new Color(1f, 0.75f, 0.35f, 0.95f));
            for (int i = 0; i < 12; i++)
            {
                Vector3 r = Random.insideUnitSphere;
                Vector3 v = dir * Random.Range(1.5f, 4f) + r * 1.4f + Vector3.up * Random.Range(0.3f, 1.2f) + DecorFx.Wind * 0.4f;
                float w = Random.Range(0.82f, 0.95f);
                DecorFx.Emit(DecorFx.Smoke, position + r * 0.4f, v, Random.Range(2.4f, 3.8f), Random.Range(1.0f, 2.0f),
                    new Color(w, w, w, Random.Range(0.55f, 0.8f)));
            }
            for (int i = 0; i < 6; i++)
                DecorFx.Emit(DecorFx.Sparks, position, (dir + Random.insideUnitSphere * 0.5f) * Random.Range(4f, 9f),
                    Random.Range(0.15f, 0.3f), Random.Range(0.08f, 0.14f), FlareOrange);
            Explosions.FlashLight(position + Vector3.up * 0.4f, 0.3f, new Color(1f, 0.7f, 0.35f));
        }

        /// <summary>비행 중: 붉은 꼬리(늘어난 불똥)와 옅은 흰 연기. debt는 호출하는 쪽이 들고 있는 누적값.</summary>
        public static void Trail(Vector3 position, Vector3 velocity, float dt, ref float debt)
        {
            debt += dt * 45f;
            while (debt >= 1f)
            {
                debt -= 1f;
                DecorFx.Emit(DecorFx.Sparks, position, velocity * 0.9f, 0.1f, 0.2f, TrailRed);
                if (Random.value < 0.5f)
                    DecorFx.Emit(DecorFx.Smoke, position - velocity * 0.02f, Random.insideUnitSphere * 0.3f + DecorFx.Wind * 0.3f,
                        Random.Range(0.9f, 1.4f), Random.Range(0.35f, 0.55f), new Color(0.93f, 0.93f, 0.93f, 0.45f));
            }
        }

        /// <summary>공중 폭발: 하얀 섬광 → 플레어 불똥이 사방으로 퍼져 떨어지고 은박이 반짝이며 흩날린다.</summary>
        public static void Burst(Vector3 position, Vector3 flatDirection)
        {
            DecorFx.Emit(DecorFx.Flash, position, Vector3.zero, 0.16f, 4.2f, new Color(1f, 0.98f, 0.92f, 1f));
            DecorFx.Emit(DecorFx.Flash, position, Vector3.zero, 0.3f, 2.4f, new Color(1f, 0.85f, 0.55f, 0.9f));
            Explosions.FlashLight(position, 0.7f, new Color(1f, 0.93f, 0.78f));

            // 플레어 불똥: 사방(아래쪽으로 조금 치우쳐), 날아가던 방향으로 조금 더
            for (int i = 0; i < 48; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = d.y * 0.7f - 0.15f;
                Vector3 v = d.normalized * Random.Range(4f, 10f) + flatDirection * 2f;
                DecorFx.Emit(DecorFx.Sparks, position, v, Random.Range(1.0f, 1.9f), Random.Range(0.12f, 0.22f),
                    Color.Lerp(FlareWhite, FlareOrange, Random.value * 0.35f));
            }
            // 은박(채프): 작은 하얀 반짝이가 천천히 흩날린다
            for (int i = 0; i < 36; i++)
            {
                Vector3 v = Random.insideUnitSphere * 3.2f + Vector3.up * 0.6f + DecorFx.Wind * 0.5f;
                float w = Random.Range(0.85f, 1f);
                DecorFx.Emit(DecorFx.Spray, position + Random.insideUnitSphere * 0.6f, v, Random.Range(2.2f, 3.4f),
                    Random.Range(0.08f, 0.14f), new Color(w, w, w, 0.95f));
            }
            // 채프 구름: 터진 자리에서 크게 퍼지는 희뿌연 덩어리(오래 남는다)
            for (int i = 0; i < 18; i++)
            {
                float w = Random.Range(0.86f, 0.96f);
                Vector3 r = Random.insideUnitSphere;
                r.y *= 0.45f;
                DecorFx.Emit(DecorFx.Smoke, position + r * 1.2f, r * Random.Range(1.6f, 3.2f) + DecorFx.Wind * 0.5f,
                    Random.Range(5.5f, 8f), Random.Range(2.2f, 3.6f), new Color(w, w, w * 1.02f, Random.Range(0.5f, 0.7f)));
            }
            // 은박이 한꺼번에 퍼지는 반짝임
            for (int i = 0; i < 80; i++)
            {
                Vector3 v = Random.insideUnitSphere * 4.5f + Vector3.up * 0.4f + DecorFx.Wind * 0.5f;
                DecorFx.Emit(DecorFx.Chaff, position + Random.insideUnitSphere * 0.8f, v, Random.Range(3.5f, 6f),
                    Random.Range(0.12f, 0.2f), ChaffColor());
            }
        }

        /// <summary>
        /// 채프 구름이 남아 있는 동안(터진 뒤 ~ 기만체 수명 끝): 희뿌연 구름을 덧쌓고 은박 반짝이를 흩뿌린다.
        /// t01 = 남은 시간 진행(0 → 1, 끝으로 갈수록 적고 옅게). debt는 호출하는 쪽이 들고 있는 누적값.
        /// </summary>
        public static void Lingering(Vector3 position, float t01, float dt, ref float debt)
        {
            float fade = 1f - Mathf.Clamp01(t01);
            debt += dt * (3f + 22f * fade);
            while (debt >= 1f)
            {
                debt -= 1f;
                Vector3 r = Random.insideUnitSphere * 2.6f;
                r.y *= 0.4f;
                if (Random.value < 0.18f)
                {
                    float w = Random.Range(0.86f, 0.96f);
                    DecorFx.Emit(DecorFx.Smoke, position + r, Random.insideUnitSphere * 0.5f + DecorFx.Wind * 0.45f,
                        Random.Range(4f, 6f), Random.Range(1.8f, 3f), new Color(w, w, w * 1.02f, 0.38f * fade + 0.12f));
                }
                else
                {
                    var v = Random.insideUnitSphere * 0.8f + Vector3.down * Random.Range(0.2f, 0.6f) + DecorFx.Wind * 0.4f;
                    var c = ChaffColor();
                    c.a *= 0.4f + 0.6f * fade;
                    DecorFx.Emit(DecorFx.Chaff, position + r, v, Random.Range(2.5f, 4.5f), Random.Range(0.1f, 0.18f), c);
                }
            }
        }

        /// <summary>은박 한 조각: 대부분 은백색, 가끔 해를 받아 반짝(밝은 금빛).</summary>
        private static Color ChaffColor()
            => Random.value < 0.18f ? new Color(1f, 0.95f, 0.75f, 1f) : new Color(Random.Range(0.82f, 0.95f), Random.Range(0.86f, 0.97f), 1f, 0.95f);

        /// <summary>연소: 불타는 플레어가 계속 떨어진다. t01 = 연소 진행(0 → 1, 점점 주황으로 줄어든다).</summary>
        public static void Burning(Vector3 position, float t01, float dt, ref float debt)
        {
            debt += dt * Mathf.Lerp(22f, 6f, t01);
            while (debt >= 1f)
            {
                debt -= 1f;
                Vector3 v = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(-4.5f, -1.5f), Random.Range(-2.2f, 2.2f));
                DecorFx.Emit(DecorFx.Sparks, position + Random.insideUnitSphere * 1.2f, v, Random.Range(0.6f, 1.1f),
                    Random.Range(0.1f, 0.18f) * (1f - t01 * 0.5f), Color.Lerp(FlareWhite, FlareOrange, t01));
                if (Random.value < 0.25f)
                    DecorFx.Emit(DecorFx.Smoke, position + Random.insideUnitSphere, DecorFx.Wind * 0.4f + Vector3.up * 0.3f,
                        Random.Range(1.4f, 2.2f), Random.Range(0.5f, 0.9f), new Color(0.9f, 0.9f, 0.9f, 0.3f));
            }
        }
    }
}
