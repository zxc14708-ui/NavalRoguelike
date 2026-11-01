// 바다 수면(URP). 격자 무늬 대신 실제 바다처럼 보이게 한다.
//   - 파도: 방향·파장이 다른 사인파 5개(심해 분산 w = √(gk))를 합쳐 기울기로 법선을 만든다. 메시는 평면 그대로(위에서 보는 카메라라 높이 변위는 거의 안 보임).
//   - 잔물결: 흐르는 값 노이즈 두 겹의 기울기.
//   - 색: 함선·섬의 저채도 미니어처 재질과 어울리는 회청색 바탕과 마루, 절제한 하늘 반사와 흰 파도.
//   - 모든 무늬가 월드 좌표에 붙어 있어 카메라가 따라가도 배가 움직이는 것이 보인다(예전 격자의 역할).
//   - 주광 그림자를 받는다(함선 그림자가 수면에 떨어짐).
//   - 구름 그림자: 해 쿠키를 읽어 그늘진 수면을 은은하게 어둡게 한다(_CloudShade 0.4 → 그늘 약 −20%). 배·섬은 URP 조명 쿠키로 따로 어두워진다.
//   - 물빛 산란은 높은 마루에만 옅게 남긴다. 실제 알파 투명은 아니며 수면 깊이·항적 그리기 순서를 유지한다.
//   - 스타일(2026-11-01): 섬·함선의 단순한 면과 어울리게 잔물결은 두 겹(큰 물결 위주)만, 거품·해안 거품은 가장자리가 또렷한 덩어리,
//     하늘 반사는 은은하게. 움직임은 물결 텍스처(실제 파도 스펙트럼)를 그대로 쓴다.
//   - 물결 텍스처(2026-11-01, OceanWaveTexture — 필립스 스펙트럼 FFT로 직접 계산): 두 겹으로 흘려 잔물결 법선·물빛 명암(높이)·
//     파도가 꺾이는 마루 거품을 만든다. 하늘 반사는 수평선 쪽 밝은 하늘 + 반사 방향 300m 위 구름층(비친 구름), 프레넬로 비스듬할수록 강하다.
//   - 해안 물빛(시험, 전역 _NavalShoreFx가 1일 때만): ShoreWaterMask가 구운 해안 거리(_NavalShoreMask, 0~32m)로
//     해안 20m 안을 얕은 물빛 + 흐르는 물 밑 빛 무늬, 9m 안을 해안 거품 + 밀려오는 물결 줄로 그린다(Boat Attack Water 방식 참고).
Shader "Naval/Ocean"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (0.14, 0.22, 0.30, 1)
        _ScatterColor ("Crest scatter", Color) = (0.30, 0.39, 0.48, 1)
        _SkyColor ("Sky reflection", Color) = (0.42, 0.50, 0.60, 1)
        _FoamColor ("Whitecap foam", Color) = (0.92, 0.95, 0.97, 1)
        _TransmissionColor ("Soft water scattering tint", Color) = (0.29, 0.38, 0.47, 1)
        _Translucency ("Translucent look (scattering, not alpha)", Range(0, 1)) = 0.05
        _WaveSpeed ("Wave speed", Range(0, 3)) = 1
        _NormalStrength ("Wave normal strength", Range(0, 3)) = 0.85
        _RippleStrength ("Ripple strength", Range(0, 1)) = 0.46
        _Specular ("Sun glint", Range(0, 6)) = 0.60
        _Glossiness ("Glint sharpness", Range(16, 4096)) = 650
        _Whitecaps ("Whitecaps", Range(0, 1)) = 0.22
        _Reflection ("Sky reflection amount", Range(0, 1)) = 0.26
        _ShadowStrength ("Shadow darkness", Range(0, 1)) = 0.055
        _GridStrength ("Reference grid (20m)", Range(0, 0.3)) = 0
        _CloudShade ("Cloud shadow on water", Range(0, 1)) = 0.4
        _ShallowColor ("Shallow water (shore fx)", Color) = (0.25, 0.47, 0.49, 1)
        _Caustics ("Shallow caustics (shore fx)", Range(0, 1)) = 0.3
        _ShoreFoam ("Shore foam (shore fx)", Range(0, 1)) = 0.5
        _WaveTexStrength ("Wave texture normal", Range(0, 2)) = 1
        _WaveTexScale ("Wave texture tile (m)", Range(8, 128)) = 38
        _CloudReflection ("Reflected clouds", Range(0, 1)) = 0.55
        _CrestFoam ("Crest foam (wave texture)", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;
            half4 _ScatterColor;
            half4 _SkyColor;
            half4 _FoamColor;
            half4 _TransmissionColor;
            float _Translucency;
            float _WaveSpeed;
            float _NormalStrength;
            float _RippleStrength;
            float _Specular;
            float _Glossiness;
            float _Whitecaps;
            float _Reflection;
            float _ShadowStrength;
            float _GridStrength;
            float _CloudShade;
            half4 _ShallowColor;
            float _Caustics;
            float _ShoreFoam;
            float _WaveTexStrength;
            float _WaveTexScale;
            float _CloudReflection;
            float _CrestFoam;
        CBUFFER_END

        // 물결 텍스처(OceanWaveTexture가 전역으로 넘김): RG 기울기 · B 높이 · A 마루 거품
        TEXTURE2D(_NavalOceanWaves);
        SAMPLER(sampler_NavalOceanWaves);
        float _NavalOceanWavesOn;
        // 움직이는 파도(OceanWaveTexture가 작업 스레드 계산을 마치면 넘김): 48장 · 12초 한 바퀴
        TEXTURE2D_ARRAY(_NavalOceanWavesAnim);
        SAMPLER(sampler_NavalOceanWavesAnim);
        float _NavalOceanAnimOn;
        float4 _NavalOceanAnim;   // x 장 수, y 한 바퀴(초)

        // 움직이는 파도 한 겹: 이웃한 두 장을 시간 비율로 섞는다. phase로 겹마다 시작을 어긋낸다.
        float4 SampleWaveAnim(float2 uv, float time, float phase)
        {
            float fr = frac(time / _NavalOceanAnim.y + phase) * _NavalOceanAnim.x;
            float i0 = floor(fr);
            float i1 = fmod(i0 + 1.0, _NavalOceanAnim.x);
            float4 a = SAMPLE_TEXTURE2D_ARRAY(_NavalOceanWavesAnim, sampler_NavalOceanWavesAnim, uv, i0);
            float4 b = SAMPLE_TEXTURE2D_ARRAY(_NavalOceanWavesAnim, sampler_NavalOceanWavesAnim, uv, i1);
            return lerp(a, b, fr - i0);
        }

        // 해안 물빛(ShoreWaterMask가 전역으로 넘김)
        TEXTURE2D(_NavalShoreMask);
        SAMPLER(sampler_NavalShoreMask);
        float4 _NavalShoreRect;   // xy = 무늬 최소 xz, zw = 1/크기
        float _NavalShoreFx;

        // 정수 해시: 먼 좌표에서도 무늬가 뭉개지지 않는다
        float Hash21(float2 p)
        {
            uint2 q = (uint2)(int2)floor(p);
            q *= uint2(1597334673u, 3812015801u);
            uint n = (q.x ^ q.y) * 1597334673u;
            return n * (1.0 / 4294967295.0);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            // 5차 보간(기울기까지 매끄럽게) — 3차 보간은 격자 칸 경계가 잔물결·얼룩에 네모로 비쳤다(2026-11-01)
            float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // 격자 방향이 드러나지 않는 부드러운 노이즈: 돌린 좌표의 값 노이즈 두 겹 평균(대비는 조금 되살린다)
        float SoftNoise(float2 p)
        {
            float2 q = float2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8) * 1.37 + 31.7;
            return saturate((ValueNoise(p) + ValueNoise(q)) * 0.5 * 1.35 - 0.175);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // 방향 d, 파장 L(m), 진폭 A(m)의 파도 하나를 높이와 기울기에 더한다
            void AddWave(float2 xz, float t, float2 d, float L, float A, inout float h, inout float2 grad)
            {
                d = normalize(d);
                float k = 6.2831853 / L;
                float w = sqrt(9.8 * k);
                float ph = k * dot(d, xz) - w * t;
                h += A * sin(ph);
                grad += A * k * cos(ph) * d;
            }

            // 노이즈 기울기(중앙 차분)
            float2 NoiseGrad(float2 p)
            {
                const float e = 0.08;
                float nx = ValueNoise(p + float2(e, 0)) - ValueNoise(p - float2(e, 0));
                float ny = ValueNoise(p + float2(0, e)) - ValueNoise(p - float2(0, e));
                return float2(nx, ny) / (2.0 * e);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float t = _Time.y * _WaveSpeed;

                // 저주파 흐름으로 파도 좌표를 뒤틀어 긴 직선 띠와 규칙적인 교차무늬를 분산한다.
                float2 warp = float2(ValueNoise(xz * 0.031 + float2(t * 0.012, 8.4)),
                                     ValueNoise(xz * 0.043 + float2(-13.7, t * 0.009)));
                float2 waves = xz + (warp - 0.5) * 6.0;

                // --- 파도(큰 것 → 작은 것)
                float h = 0;
                float2 grad = 0;
                AddWave(waves, t, float2(0.80, 0.60), 23.0, 0.28, h, grad);
                AddWave(waves * 1.013, t, float2(-0.42, 0.91), 13.0, 0.16, h, grad);
                AddWave(waves * 0.987, t, float2(0.95, -0.31), 8.5, 0.09, h, grad);
                AddWave(xz + warp, t, float2(0.22, 0.98), 5.3, 0.05, h, grad);
                AddWave(xz - warp, t, float2(-0.70, -0.71), 3.4, 0.03, h, grad);
                float h01 = saturate(h / 0.61 * 0.5 + 0.5);

                // --- 잔물결(흐르는 노이즈 두 겹)
                float2 ripple = NoiseGrad(xz * 0.55 + float2(t * 0.35, t * 0.12)) * 0.55
                              + NoiseGrad(xz * 1.45 - float2(t * 0.22, t * 0.48)) * 0.25;
                grad += ripple * _RippleStrength * 0.22;

                // --- 물결 텍스처 세 겹(크기·방향·흐름이 다름). 있으면 사인파·노이즈 잔물결은 바탕으로만 남긴다.
                float waveFoam = 0.0;
                if (_NavalOceanWavesOn > 0.5)
                {
                    float s = 1.0 / _WaveTexScale;
                    float2 r2 = float2(xz.x * 0.80 - xz.y * 0.60, xz.x * 0.60 + xz.y * 0.80);
                    float2 uv1 = xz * s + float2(0.0107, 0.0064) * t * 0.4;
                    float2 uv2 = r2 * s * 2.71 + float2(-0.0151, 0.0093) * t * 0.4;
                    float4 w1, w2;
                    if (_NavalOceanAnimOn > 0.5)
                    {
                        // 파도가 실제로 진행하고 모양이 바뀐다. 작게 줄인 겹일수록 빨리(심해 분산: 크기 1/s배면 시간 1/√s배)
                        w1 = SampleWaveAnim(uv1, t * 1.3, 0.0);
                        w2 = SampleWaveAnim(uv2, t * 2.1, 0.37);
                    }
                    else
                    {
                        w1 = SAMPLE_TEXTURE2D(_NavalOceanWaves, sampler_NavalOceanWaves, uv1);
                        w2 = SAMPLE_TEXTURE2D(_NavalOceanWaves, sampler_NavalOceanWaves, uv2);
                    }
                    // 돌린 겹의 기울기는 월드 방향으로 되돌린다
                    float2 g1 = w1.rg * 2.0 - 1.0;
                    float2 q2 = w2.rg * 2.0 - 1.0;
                    float2 g2 = float2(q2.x * 0.80 + q2.y * 0.60, -q2.x * 0.60 + q2.y * 0.80);
                    // 멀리서 보면 작은 겹이 자글자글해 보이므로 카메라에서 멀수록 줄인다(쿼터뷰 ≈ 70~90m)
                    float camDist = distance(_WorldSpaceCameraPos, i.positionWS);
                    float detail = lerp(1.0, 0.35, saturate((camDist - 25.0) / 70.0));
                    // 사인파·노이즈 잔물결은 바탕으로만 아주 조금(노이즈 잔물결이 자글자글함의 원인)
                    grad = grad * 0.2 + (g1 * 0.6 + g2 * 0.25 * detail) * _WaveTexStrength * 0.55;
                    h01 = lerp(h01, w1.b * 0.7 + w2.b * 0.3, 0.75);
                    waveFoam = saturate(w1.a + w2.a * 0.5);
                }

                float3 n = normalize(float3(-grad.x * _NormalStrength, 1.0, -grad.y * _NormalStrength));
                float3 v = normalize(_WorldSpaceCameraPos - i.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                float nl = saturate(dot(n, light.direction));

                // 구름 그림자(해 쿠키). 1 = 맑음, 작을수록 그늘.
                float cloud = 1.0;
                #if defined(_LIGHT_COOKIES)
                    cloud = SampleMainLightCookie(i.positionWS).r;
                #endif
                light.color *= cloud;

                // 서로 다른 규모의 흐름과 작은 물결 얼룩을 색에도 반영한다.
                // 법선만 흔들 때보다 쿼터뷰에서 파도 움직임이 읽히고, 일정한 줄무늬가 줄어든다.
                // 얼룩(big·streak)은 약하게, 파도 높이의 부드러운 명암이 주가 되게 — 얼룩이 크면 물이 아니라 무늬 천처럼 보인다
                float big = SoftNoise(xz * 0.011 + 17.3) * 0.6 + SoftNoise(xz * 0.037 - 5.1) * 0.4;
                float2 streakUV = float2(dot(xz, float2(0.86, 0.51)) * 0.43,
                                         dot(xz, float2(-0.51, 0.86)) * 0.69);
                float streak = SoftNoise(streakUV + warp * 1.7 + float2(t * 0.09, -t * 0.04));
                float shade = saturate(0.30 + (h01 - 0.5) * 0.22 + (big - 0.5) * 0.08 + (streak - 0.5) * 0.08);
                float3 water = lerp(_DeepColor.rgb, _ScatterColor.rgb, shade);
                water *= 0.90 + 0.10 * nl;

                // --- 해안 물빛(시험): 해안 거리 shoreDist(m), 32 = 먼 바다
                float shoreDist = 32.0;
                if (_NavalShoreFx > 0.5)
                {
                    float2 suv = (xz - _NavalShoreRect.xy) * _NavalShoreRect.zw;
                    if (all(suv > 0.0) && all(suv < 1.0))
                        shoreDist = SAMPLE_TEXTURE2D(_NavalShoreMask, sampler_NavalShoreMask, suv).r * 32.0;
                }
                float shallow = 1.0 - smoothstep(0.0, 20.0, shoreDist);
                if (shallow > 0.0)
                {
                    water = lerp(water, _ShallowColor.rgb * (0.9 + 0.1 * nl), shallow * 0.62);
                    // 물 밑 빛 무늬: 서로 다르게 흐르는 노이즈 둘이 겹치는 가는 선(해안 가까울수록만 진하게)
                    float2 cuv = xz * 0.42 + warp * 2.0;
                    float c1 = ValueNoise(cuv + float2(t * 0.19, t * 0.11));
                    float c2 = ValueNoise(cuv * 1.31 - float2(t * 0.14, -t * 0.17));
                    float caustic = pow(saturate(1.0 - abs(c1 - c2) * 7.0), 5.0);
                    water += caustic * shallow * shallow * shallow * _Caustics * 0.22 * (0.4 + 0.6 * cloud) * light.shadowAttenuation;
                }

                // --- 하늘 반사(프레넬): 수평선 쪽은 밝고 위로 갈수록 짙은 하늘 + 비친 구름
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(n, v)), 5.0);
                float3 rdir = reflect(-v, n);
                float3 skyCol = lerp(_SkyColor.rgb * 1.18, _SkyColor.rgb * float3(0.78, 0.86, 1.0), saturate(rdir.y * 1.4));
                if (_CloudReflection > 0.0 && rdir.y > 0.04)
                {
                    // 반사 방향으로 300m 위 구름층을 본다(구름 그림자와 따로 천천히 흐름)
                    float2 cp = i.positionWS.xz + rdir.xz * (300.0 / rdir.y) + float2(t * 2.2, t * 1.3);
                    float cn = SoftNoise(cp * 0.0042) * 0.65 + SoftNoise(cp * 0.013 + 7.1) * 0.35;
                    float cm = smoothstep(0.48, 0.72, cn) * saturate((rdir.y - 0.04) * 6.0);
                    skyCol = lerp(skyCol, float3(0.93, 0.95, 0.97), cm * _CloudReflection);
                }
                float skyScatter = saturate(_Reflection * (0.45 + 1.6 * fresnel));
                float3 col = lerp(water, skyCol, skyScatter);

                // --- 높은 물결 마루의 저채도 회청색 산란
                // 밝은 산란층을 수면 전체에 깔지 않는다. 기존 무늬만 재사용해 불규칙한 마루를 고른다.
                float softCrest = smoothstep(0.64, 0.89, h01) * smoothstep(0.48, 0.76, streak);
                // 물 속 산란: 파도 앞면(해를 향하고 높은 곳)으로 빛이 비쳐 보이게 넓고 부드럽게 — 맑은 물 느낌
                float throughWave = smoothstep(0.42, 0.92, h01) * (0.55 + 0.45 * nl);
                float scattering = saturate(_Translucency) * (softCrest * 0.40 + throughWave * 0.30);
                col = lerp(col, _TransmissionColor.rgb, scattering);
                col = lerp(col, _ScatterColor.rgb, softCrest * 0.22);

                // --- 해 반짝임
                float3 hv = normalize(light.direction + v);
                float spec = pow(saturate(dot(n, hv)), _Glossiness) * _Specular;
                col += spec * light.color * light.shadowAttenuation;

                // --- 마루의 흰 파도(드문드문, 천천히 흘러감)
                float foamNoise = SoftNoise(xz * 0.45 + float2(t * 0.05, -t * 0.03)) * SoftNoise(xz * 1.9 - float2(t * 0.11, t * 0.07));
                float caps = saturate((h01 - (1.0 - _Whitecaps * 0.4)) * 4.0) * smoothstep(0.26, 0.52, foamNoise);
                caps *= lerp(1.0, 0.12, step(0.5, _NavalOceanWavesOn));   // 물결 텍스처가 있으면 마루 거품이 주가 된다
                col = lerp(col, _FoamColor.rgb * (0.90 + 0.10 * nl), caps * 0.40);

                // --- 마루 거품(물결 텍스처): 파도가 꺾이는 곳, 넓은 무늬로 거품 많은 곳·적은 곳을 나눈다
                if (waveFoam > 0.0)
                {
                    // 가장자리가 또렷한 덩어리 거품(그림체에 맞춤) — 흐릿한 점이 흩어지면 노이즈처럼 보인다
                    float foamPatch = smoothstep(0.45, 0.75, SoftNoise(xz * 0.018 + float2(t * 0.006, -t * 0.004)));
                    float crest = smoothstep(0.58, 0.66, waveFoam * (0.55 + 0.45 * foamPatch)) * _CrestFoam;
                    col = lerp(col, _FoamColor.rgb * (0.9 + 0.1 * nl), crest * 0.9);
                }

                // --- 해안 거품(시험): 해안에 붙은 띠 + 바다 쪽에서 밀려오는 물결 줄
                if (shoreDist < 10.0)
                {
                    float fn = ValueNoise(xz * 0.8 + float2(t * 0.09, -t * 0.06)) * 0.6 + ValueNoise(xz * 2.3 - t * 0.15) * 0.4;
                    // 해안에 붙은 또렷한 흰 파도선(굵기가 노이즈로 들쭉날쭉) + 바다 쪽에서 밀려오는 가는 줄
                    float edge = 1.2 + fn * 1.8;
                    float band = 1.0 - smoothstep(edge - 0.25, edge + 0.25, shoreDist);
                    float lap = sin(shoreDist * 1.15 - t * 1.7 + fn * 2.0) * 0.5 + 0.5;
                    float lines = (1.0 - smoothstep(3.0, 8.0, shoreDist)) * smoothstep(0.90, 0.95, lap) * step(0.42, fn);
                    col = lerp(col, _FoamColor.rgb * (0.9 + 0.1 * nl), saturate(band + lines * 0.6) * _ShoreFoam);
                }

                // --- 선택: 20m 기준 격자(속도감 보조, 기본 0)
                if (_GridStrength > 0)
                {
                    float2 g = abs(frac(xz / 20.0 + 0.5) - 0.5) * 20.0;
                    float gridLine = 1.0 - saturate(min(g.x, g.y) / 0.15);
                    col = lerp(col, col * 1.6 + 0.02, gridLine * _GridStrength);
                }

                // 그림자: 밝은 물빛을 유지하면서 함선의 접지감과 구름 흐름만 약하게 남긴다.
                col *= lerp(1.0 - _ShadowStrength, 1.0, light.shadowAttenuation);
                col *= lerp(1.0, cloud, _CloudShade);

                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag

            float4 DNVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            // 수면 법선 = 위
            half4 DNFrag() : SV_Target { return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
