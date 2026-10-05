// 바다 수면(URP). 격자 무늬 대신 실제 바다처럼 보이게 한다.
//   - 파도: 방향·파장이 다른 사인파 5개(심해 분산 w = √(gk))를 합쳐 기울기로 법선을 만든다. 메시는 평면 그대로(위에서 보는 카메라라 높이 변위는 거의 안 보임).
//   - 잔물결: 흐르는 값 노이즈 두 겹의 기울기.
//   - 색: 함선·섬의 저채도 미니어처 재질과 어울리는 회청색 바탕과 마루, 절제한 하늘 반사와 흰 파도.
//   - 모든 무늬가 월드 좌표에 붙어 있어 카메라가 따라가도 배가 움직이는 것이 보인다(예전 격자의 역할).
//   - 주광 그림자를 받는다(함선 그림자가 수면에 떨어짐).
//   - 구름 그림자: 쿠키는 유지하되 수면 감광만 약하게 적용한다. 배·섬의 조명은 바꾸지 않는다.
//   - 물빛 산란은 높은 마루에만 옅게 남긴다. 실제 알파 투명은 아니며 수면 깊이·항적 그리기 순서를 유지한다.
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
        _CloudShade ("Cloud shadow on water", Range(0, 1)) = 0.16
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
        CBUFFER_END

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
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
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
                float big = ValueNoise(xz * 0.011 + 17.3) * 0.6 + ValueNoise(xz * 0.037 - 5.1) * 0.4;
                float2 streakUV = float2(dot(xz, float2(0.86, 0.51)) * 0.43,
                                         dot(xz, float2(-0.51, 0.86)) * 0.69);
                float streak = ValueNoise(streakUV + warp * 1.7 + float2(t * 0.09, -t * 0.04));
                float shade = saturate(0.28 + (h01 - 0.5) * 0.38 + (big - 0.5) * 0.32 + (streak - 0.5) * 0.20);
                float3 water = lerp(_DeepColor.rgb, _ScatterColor.rgb, shade);
                water *= 0.94 + 0.06 * nl;

                // --- 하늘 반사(프레넬)
                // 전면을 하늘색으로 씻어내지 않고, 낮은 각도와 잔물결에만 하늘빛을 남긴다.
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(n, v)), 5.0);
                float skyScatter = saturate((0.025 + fresnel * 0.975) * _Reflection);
                float3 col = lerp(water, _SkyColor.rgb, skyScatter);

                // --- 높은 물결 마루의 저채도 회청색 산란
                // 밝은 산란층을 수면 전체에 깔지 않는다. 기존 무늬만 재사용해 불규칙한 마루를 고른다.
                float softCrest = smoothstep(0.64, 0.89, h01) * smoothstep(0.48, 0.76, streak);
                float scattering = saturate(_Translucency) * softCrest * 0.40;
                col = lerp(col, _TransmissionColor.rgb, scattering);
                col = lerp(col, _ScatterColor.rgb, softCrest * 0.22);

                // --- 해 반짝임
                float3 hv = normalize(light.direction + v);
                float spec = pow(saturate(dot(n, hv)), _Glossiness) * _Specular;
                col += spec * light.color * light.shadowAttenuation;

                // --- 마루의 흰 파도(드문드문, 천천히 흘러감)
                float foamNoise = ValueNoise(xz * 0.45 + float2(t * 0.05, -t * 0.03)) * ValueNoise(xz * 1.9 - float2(t * 0.11, t * 0.07));
                float caps = saturate((h01 - (1.0 - _Whitecaps * 0.4)) * 6.0) * smoothstep(0.3, 0.5, foamNoise);
                col = lerp(col, _FoamColor.rgb * (0.90 + 0.10 * nl), caps * 0.48);

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
