using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 바다 물결 텍스처(2026-11-01). 실제 바다 파도 스펙트럼(필립스 스펙트럼, Tessendorf 방식의 FFT)으로
    /// 이음매 없이 반복되는 파도 한 장(256², 한 장 = 64m 기준)을 시작할 때 한 번 계산해 바다 셰이더(Naval/Ocean)에 전역으로 넘긴다.
    ///   R·G = 수면 기울기(x·z, 0.5 = 평평), B = 높이(0~1), A = 파도가 꺾이는 곳의 거품(0~1, 자코비안이 작은 곳).
    /// 셰이더는 이것을 크기·방향이 다른 세 겹으로 흘려 잔물결·물빛 명암·마루 거품을 만든다.
    /// 외부 텍스처·코드 없이 직접 계산한다(라이선스 없음).
    /// 움직이는 파도(2026-11-01): 같은 스펙트럼을 시간에 따라(심해 분산 ω = √(gk)) 12초 한 바퀴 48장으로 계산해
    /// 텍스처 배열(_NavalOceanWavesAnim)로 넘긴다 — 파도마다 ω를 12초 주기에 맞춰 반올림해 이음매 없이 되풀이된다.
    /// 계산(약 1초)은 시작할 때 작업 스레드에서 하고, 끝나기 전에는 정지 한 장을 쓴다. 기울기는 높이 차분, 거품은 마루의 오목함(라플라시안)으로.
    /// 정지 한 장(_NavalOceanWaves, 자코비안 거품)은 항적 셰이더가 계속 쓴다.
    /// </summary>
    public static class OceanWaveTexture
    {
        private const int N = 256;
        private const float Patch = 64f;      // m
        private const float Wind = 9f;        // m/s
        private const float Choppy = 1.3f;
        private const float FoamArea = 0.09f; // 거품이 되는 면적 비율

        private static readonly int TexId = Shader.PropertyToID("_NavalOceanWaves");
        private static readonly int OnId = Shader.PropertyToID("_NavalOceanWavesOn");
        private static readonly int AnimTexId = Shader.PropertyToID("_NavalOceanWavesAnim");
        private static readonly int AnimOnId = Shader.PropertyToID("_NavalOceanAnimOn");
        private static readonly int AnimId = Shader.PropertyToID("_NavalOceanAnim");

        public const int Frames = 48;
        public const float Loop = 12f;   // s

        public static Texture2D Texture { get; private set; }
        /// <summary>움직이는 파도(48장 텍스처 배열). 계산이 끝나기 전에는 null.</summary>
        public static Texture2DArray Animated { get; private set; }
        /// <summary>검증용: 움직이는 파도 계산 시간(ms, 작업 스레드).</summary>
        public static float AnimBuildMs { get; private set; }
        private static System.Threading.Tasks.Task<Color32[][]> s_frames;
        /// <summary>검증용: 계산 시간(ms).</summary>
        public static float BuildMs { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Texture == null)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Texture = Build();
                BuildMs = (float)sw.Elapsed.TotalMilliseconds;
            }
            Shader.SetGlobalTexture(TexId, Texture);
            Shader.SetGlobalFloat(OnId, 1f);
            if (Animated != null) { Publish(); return; }
            Shader.SetGlobalFloat(AnimOnId, 0f);
            if (s_frames == null)
            {
                s_frames = System.Threading.Tasks.Task.Run(() =>
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var frames = BuildFrames();
                    AnimBuildMs = (float)sw.Elapsed.TotalMilliseconds;
                    return frames;
                });
                var go = new GameObject("Ocean wave frames");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                go.AddComponent<FrameUploader>();
            }
        }

        private static void Publish()
        {
            Shader.SetGlobalTexture(AnimTexId, Animated);
            Shader.SetGlobalVector(AnimId, new Vector4(Frames, Loop, 0f, 0f));
            Shader.SetGlobalFloat(AnimOnId, 1f);
        }

        /// <summary>작업 스레드 계산이 끝나면 메인 스레드에서 텍스처 배열로 올리고 스스로 사라진다.</summary>
        private sealed class FrameUploader : MonoBehaviour
        {
            private void Update()
            {
                if (s_frames == null || !s_frames.IsCompleted) return;
                if (s_frames.IsFaulted) { Debug.LogWarning($"[OceanWaveTexture] 움직이는 파도 계산 실패: {s_frames.Exception?.GetBaseException().Message}"); Destroy(gameObject); return; }
                var frames = s_frames.Result;
                var arr = new Texture2DArray(N, N, frames.Length, TextureFormat.RGBA32, true, true)
                {
                    name = "Ocean waves (FFT, animated)",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4,
                };
                for (int f = 0; f < frames.Length; f++) arr.SetPixels32(frames[f], f, 0);
                arr.Apply(true, true);
                Animated = arr;
                Publish();
                Destroy(gameObject);
            }
        }

        /// <summary>필립스 스펙트럼 h0(k)와 파수(같은 시드 — 정지 한 장과 움직이는 파도가 같은 바다).</summary>
        private static void Spectrum(out float[] hRe, out float[] hIm, out float[] kxA, out float[] kzA, out float[] kLen)
        {
            var rng = new System.Random(19310);
            int n2 = N * N;
            hRe = new float[n2]; hIm = new float[n2];
            kxA = new float[n2]; kzA = new float[n2]; kLen = new float[n2];
            Vector2 wind = new Vector2(0.86f, 0.51f);
            float L = Wind * Wind / 9.81f;
            const float small = 0.08f;   // 이보다 작은 물결은 누른다(m)
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int i = z * N + x;
                int nx = x < N / 2 ? x : x - N, nz = z < N / 2 ? z : z - N;   // FFT 순서의 주파수
                float kx = 2f * Mathf.PI * nx / Patch, kz = 2f * Mathf.PI * nz / Patch;
                float k = Mathf.Sqrt(kx * kx + kz * kz);
                kxA[i] = kx; kzA[i] = kz; kLen[i] = k;
                if (k < 1e-6f) continue;
                float kw = (kx * wind.x + kz * wind.y) / k;
                float p = Mathf.Exp(-1f / (k * L * k * L)) / (k * k * k * k) * kw * kw * Mathf.Exp(-k * k * small * small);
                if (kw < 0f) p *= 0.07f;   // 바람 반대 방향 물결은 약하게
                float amp = Mathf.Sqrt(p * 0.5f);
                hRe[i] = (float)Gauss(rng) * amp;
                hIm[i] = (float)Gauss(rng) * amp;
            }
        }

        /// <summary>
        /// 움직이는 파도 48장(작업 스레드에서 — Unity API를 쓰지 않는다). 장마다 h(k,t) = h0(k)e^{iωt} + conj(h0(−k))e^{−iωt}를
        /// 역 FFT 한 번으로 높이로 바꾸고, 기울기는 높이 차분, 거품은 마루의 오목함 상위 9%를 흐려서 쓴다. 정규화는 첫 장 기준.
        /// </summary>
        private static Color32[][] BuildFrames()
        {
            Spectrum(out var h0r, out var h0i, out _, out _, out var kLen);
            int n2 = N * N;
            float w0 = 2f * Mathf.PI / Loop;
            var omega = new float[n2];
            for (int i = 0; i < n2; i++) omega[i] = Mathf.Round(Mathf.Sqrt(9.81f * kLen[i]) / w0) * w0;   // 12초에 정수 바퀴

            var heights = new float[Frames][];
            for (int f = 0; f < Frames; f++)
            {
                float t = f * Loop / Frames;
                var re = new float[n2]; var im = new float[n2];
                for (int z = 0; z < N; z++)
                for (int x = 0; x < N; x++)
                {
                    int i = z * N + x;
                    int j = ((N - z) % N) * N + (N - x) % N;   // −k
                    float c = Mathf.Cos(omega[i] * t), s = Mathf.Sin(omega[i] * t);
                    re[i] = h0r[i] * c - h0i[i] * s + h0r[j] * c - h0i[j] * s;
                    im[i] = h0r[i] * s + h0i[i] * c - h0i[j] * c - h0r[j] * s;
                }
                Fft2D(re, im);
                heights[f] = re;
            }

            float dx = Patch / N;
            float Slope(float[] h, int x, int z, bool alongX) => alongX
                ? (h[z * N + (x + 1) % N] - h[z * N + (x + N - 1) % N]) / (2f * dx)
                : (h[((z + 1) % N) * N + x] - h[((z + N - 1) % N) * N + x]) / (2f * dx);
            float Concave(float[] h, int x, int z) =>
                4f * h[z * N + x] - h[z * N + (x + 1) % N] - h[z * N + (x + N - 1) % N] - h[((z + 1) % N) * N + x] - h[((z + N - 1) % N) * N + x];

            // 정규화 기준(첫 장)
            var h0 = heights[0];
            double sVar = 0, hVar = 0;
            var conc = new float[n2];
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int i = z * N + x;
                float a = Slope(h0, x, z, true), b = Slope(h0, x, z, false);
                sVar += a * a + b * b; hVar += h0[i] * h0[i];
                conc[i] = Concave(h0, x, z);
            }
            float sStd = Mathf.Sqrt((float)(sVar / (2 * n2))), hStd = Mathf.Sqrt((float)(hVar / n2));
            var sorted = (float[])conc.Clone(); System.Array.Sort(sorted);
            float cThr = sorted[(int)(n2 * (1f - FoamArea))], cMax = sorted[n2 - 1];

            var result = new Color32[Frames][];
            for (int f = 0; f < Frames; f++)
            {
                var h = heights[f];
                var foam = new float[n2];
                for (int z = 0; z < N; z++)
                for (int x = 0; x < N; x++)
                    foam[z * N + x] = Mathf.Clamp01((Concave(h, x, z) - cThr) / Mathf.Max(1e-6f, (cMax - cThr) * 0.45f));
                foam = Blur(Blur(foam));
                var px = new Color32[n2];
                for (int z = 0; z < N; z++)
                for (int x = 0; x < N; x++)
                {
                    int i = z * N + x;
                    float gx = Mathf.Clamp(Slope(h, x, z, true) / (sStd * 3f), -1f, 1f);
                    float gz = Mathf.Clamp(Slope(h, x, z, false) / (sStd * 3f), -1f, 1f);
                    float hh = Mathf.Clamp01(0.5f + h[i] / (hStd * 5f));
                    px[i] = new Color32((byte)Mathf.RoundToInt((gx * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((gz * 0.5f + 0.5f) * 255f),
                                        (byte)Mathf.RoundToInt(hh * 255f), (byte)Mathf.RoundToInt(foam[i] * 255f));
                }
                result[f] = px;
            }
            return result;
        }

        private static Texture2D Build()
        {
            int n2 = N * N;
            Spectrum(out var hRe, out var hIm, out var kxA, out var kzA, out var kLen);

            // 높이 · 기울기 · 꺾임(자코비안)용 미분을 공간으로
            float[] Field(System.Func<int, (float re, float im)> mul)
            {
                var re = new float[n2]; var im = new float[n2];
                for (int i = 0; i < n2; i++)
                {
                    var (mr, mi) = mul(i);
                    re[i] = hRe[i] * mr - hIm[i] * mi;
                    im[i] = hRe[i] * mi + hIm[i] * mr;
                }
                Fft2D(re, im);
                return re;
            }
            var height = Field(i => (1f, 0f));
            var sx = Field(i => (0f, kxA[i]));                      // i·kx·h
            var sz = Field(i => (0f, kzA[i]));
            var dxx = Field(i => (kLen[i] > 0 ? kxA[i] * kxA[i] / kLen[i] : 0f, 0f));
            var dzz = Field(i => (kLen[i] > 0 ? kzA[i] * kzA[i] / kLen[i] : 0f, 0f));
            var dxz = Field(i => (kLen[i] > 0 ? kxA[i] * kzA[i] / kLen[i] : 0f, 0f));

            // 정규화 기준
            float hMin = float.MaxValue, hMax = float.MinValue; double sVar = 0;
            for (int i = 0; i < n2; i++) { hMin = Mathf.Min(hMin, height[i]); hMax = Mathf.Max(hMax, height[i]); sVar += sx[i] * sx[i] + sz[i] * sz[i]; }
            float sStd = Mathf.Sqrt((float)(sVar / (2 * n2)));
            float hStd = 0f; for (int i = 0; i < n2; i++) hStd += height[i] * height[i]; hStd = Mathf.Sqrt(hStd / n2);
            float lambda = Choppy / Mathf.Max(1e-6f, sStd * 3f) * 0.35f;

            // 자코비안: 마루가 모이는 쪽 부호를 고른다(거품 자리 평균 높이가 높은 쪽)
            float[] Jacobian(float lam)
            {
                var j = new float[n2];
                for (int i = 0; i < n2; i++)
                    j[i] = (1f + lam * dxx[i]) * (1f + lam * dzz[i]) - lam * lam * dxz[i] * dxz[i];
                return j;
            }
            float CrestHeight(float[] j)
            {
                var sorted = (float[])j.Clone(); System.Array.Sort(sorted);
                float thr = sorted[(int)(n2 * FoamArea)];
                double sum = 0; int c = 0;
                for (int i = 0; i < n2; i++) if (j[i] <= thr) { sum += height[i]; c++; }
                return c > 0 ? (float)(sum / c) : 0f;
            }
            var jA = Jacobian(lambda); var jB = Jacobian(-lambda);
            var jac = CrestHeight(jA) >= CrestHeight(jB) ? jA : jB;
            var sortedJ = (float[])jac.Clone(); System.Array.Sort(sortedJ);
            float jThr = sortedJ[(int)(n2 * FoamArea)], jMin = sortedJ[0];
            var foam = new float[n2];
            for (int i = 0; i < n2; i++) foam[i] = Mathf.Clamp01((jThr - jac[i]) / Mathf.Max(1e-5f, (jThr - jMin) * 0.45f));
            foam = Blur(Blur(foam));

            var px = new Color32[n2];
            for (int i = 0; i < n2; i++)
            {
                float gx = Mathf.Clamp(sx[i] / (sStd * 3f), -1f, 1f);
                float gz = Mathf.Clamp(sz[i] / (sStd * 3f), -1f, 1f);
                float h = Mathf.Clamp01(0.5f + height[i] / (hStd * 5f));
                px[i] = new Color32((byte)Mathf.RoundToInt((gx * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((gz * 0.5f + 0.5f) * 255f),
                                    (byte)Mathf.RoundToInt(h * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(foam[i]) * 255f));
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true, true)
            {
                name = "Ocean waves (FFT)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static float[] Blur(float[] src)
        {
            var dst = new float[src.Length];
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                float s = 0f;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    s += src[((z + dz + N) % N) * N + (x + dx + N) % N];
                dst[z * N + x] = s / 9f;
            }
            return dst;
        }

        private static double Gauss(System.Random r)
        {
            double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
            return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
        }

        /// <summary>2차원 역 FFT(제자리, 크기 N×N, 정규화 없음 — 정규화는 쓰는 쪽 표준편차로 한다).</summary>
        private static void Fft2D(float[] re, float[] im)
        {
            var rr = new float[N]; var ri = new float[N];
            for (int z = 0; z < N; z++)
            {
                for (int x = 0; x < N; x++) { rr[x] = re[z * N + x]; ri[x] = im[z * N + x]; }
                Fft1D(rr, ri);
                for (int x = 0; x < N; x++) { re[z * N + x] = rr[x]; im[z * N + x] = ri[x]; }
            }
            for (int x = 0; x < N; x++)
            {
                for (int z = 0; z < N; z++) { rr[z] = re[z * N + x]; ri[z] = im[z * N + x]; }
                Fft1D(rr, ri);
                for (int z = 0; z < N; z++) { re[z * N + x] = rr[z]; im[z * N + x] = ri[z]; }
            }
        }

        private static void Fft1D(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = 2.0 * System.Math.PI / len;   // 역변환(+)
                float wr = (float)System.Math.Cos(ang), wi = (float)System.Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float cr = 1f, ci = 0f;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        float tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        float ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
        }
    }
}
