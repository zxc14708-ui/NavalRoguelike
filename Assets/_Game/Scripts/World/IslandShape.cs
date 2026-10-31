using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 절차형 섬의 해안선 모양(2026-10-31): 섬 로컬 방향(라디안, +X에서 반시계 — Lathe·파도 거품과 같은 기준)마다 반경 배율.
    ///   - 타원: 길쭉함(무인도 1~1.7배, 바위섬 1~1.35배)과 방향. 넓이는 원과 비슷하게 맞춘다.
    ///   - 굴곡: FastNoiseLite(OpenSimplex2 · FBm 2겹)를 각도로 만든 원 위의 점에서 읽어 곶·만 2~5개(이음매 없음).
    /// 섬 콜라이더가 볼록 메시라 굴곡은 작게(±10~20%) 둔다 — 만 안쪽에서는 콜라이더가 해안보다 조금 바다 쪽으로 나온다.
    /// 같은 난수면 같은 모양(판 시드 재현).
    /// </summary>
    public sealed class IslandShape
    {
        private const int Samples = 72;
        private readonly float[] _table = new float[Samples];

        /// <summary>가장 먼 방향의 배율(간격 판정용 외접 반경).</summary>
        public float Max { get; }
        /// <summary>길쭉함(긴 축 / 짧은 축).</summary>
        public float Aspect { get; }

        public IslandShape(System.Random rng, bool rocky)
        {
            var noise = new FastNoiseLite(rng.Next());
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(2);
            noise.SetFrequency(1f);

            Aspect = 1f + (float)rng.NextDouble() * (rocky ? 0.35f : 0.7f);
            float tilt = (float)rng.NextDouble() * Mathf.PI;
            float amp = (rocky ? 0.12f : 0.1f) + (float)rng.NextDouble() * 0.08f;
            float freq = 1.1f + (float)rng.NextDouble() * 0.9f;
            float norm = 1f / Mathf.Sqrt(Aspect);

            float max = 0f;
            for (int i = 0; i < Samples; i++)
            {
                float a = i * Mathf.PI * 2f / Samples;
                float c = Mathf.Cos(a - tilt), s = Mathf.Sin(a - tilt);
                float ellipse = norm / Mathf.Sqrt(c * c / (Aspect * Aspect) + s * s);
                float n = noise.GetNoise(Mathf.Cos(a) * freq, Mathf.Sin(a) * freq);
                _table[i] = ellipse * (1f + amp * n);
                max = Mathf.Max(max, _table[i]);
            }
            Max = max;
        }

        /// <summary>방향 a(라디안)의 반경 배율.</summary>
        public float At(float a)
        {
            float t = Mathf.Repeat(a, Mathf.PI * 2f) / (Mathf.PI * 2f) * Samples;
            int i = Mathf.FloorToInt(t) % Samples;
            return Mathf.Lerp(_table[i], _table[(i + 1) % Samples], t - Mathf.Floor(t));
        }
    }
}
