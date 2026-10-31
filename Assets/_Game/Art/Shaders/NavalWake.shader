// 배가 지나간 물살(항적). ShipWake가 만드는 띠 메시에 입힌다.
//   정점: 색 알파 = 세기(속력 × 남은 수명), uv.x = 띠 가로(0 좌 ~ 1 우), uv.y = 띠를 따라간 거리(m)
//   _Style 0 = 켈빈 항적(선수에서 V자로 벌어지는 두 줄의 흰 물결 — ShipWake가 두 줄만 만든다)
//   _Style 1 = 추진기 물살(선미 뒤로 곧게 이어지는 거품 띠, 가운데가 가장 희고 흐트러짐)
// 거품은 월드 좌표 노이즈로 쪼개 흘러가게 해 한 장짜리 띠로 보이지 않게 한다.
// 2026-11-01: 정점 색 r = 남은 수명, g = 휘저음(선회·가속). 거품은 바다 물결 텍스처(_NavalOceanWaves)로도 쪼개고,
//   오래될수록 성기게 흩어진다. 추진기 물살은 거품 둘레에 공기 섞인 밝은 청록 물빛(_AeratedColor)을 깔고, 휘저을수록 거품이 짙다.
Shader "Naval/Wake"
{
    Properties
    {
        _FoamColor ("Foam", Color) = (0.9, 0.96, 1, 0.85)
        _Style ("Style (0 Kelvin arms, 1 propeller wash)", Float) = 0
        _EdgeSharpness ("Arm sharpness", Range(2, 30)) = 9
        _AeratedColor ("Aerated water", Color) = (0.34, 0.66, 0.63, 0.3)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Wake"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FoamColor;
                float _Style;
                float _EdgeSharpness;
                half4 _AeratedColor;
            CBUFFER_END

            TEXTURE2D(_NavalOceanWaves);
            SAMPLER(sampler_NavalOceanWaves);
            float _NavalOceanWavesOn;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half alpha : TEXCOORD2;
                half2 lifeChurn : TEXCOORD4;
                float fogFactor : TEXCOORD3;
            };

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
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = input.uv;
                o.alpha = input.color.a;
                o.lifeChurn = input.color.rg;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float t = _Time.y;
                float u = i.uv.x;
                float along = i.uv.y;

                float life = i.lifeChurn.x;     // 1 = 막 생김
                float churn = i.lifeChurn.y;

                // 거품을 쪼개는 노이즈(흘러감) + 바다 물결 텍스처의 거품 무늬
                float n1 = ValueNoise(xz * 1.35 + float2(t * 0.35, -t * 0.2));
                float n2 = ValueNoise(xz * 3.6 - float2(t * 0.55, t * 0.3));
                float noise = n1 * 0.62 + n2 * 0.38;
                if (_NavalOceanWavesOn > 0.5)
                {
                    float4 w = SAMPLE_TEXTURE2D(_NavalOceanWaves, sampler_NavalOceanWaves, xz / 9.5 + float2(t * 0.013, -t * 0.009));
                    noise = noise * 0.65 + (w.b * 0.6 + w.a * 0.4) * 0.35 + 0.02;
                }
                // 오래될수록 거품이 성기게(문턱이 올라감)
                float breakup = (1.0 - life) * 0.32;

                float a;
                float aerated = 0.0;
                if (_Style < 0.5)
                {
                    // 켈빈 항적의 한 줄: 가운데가 가장 희고, 줄을 따라 물결 마루처럼 끊어진다
                    float c = 1.0 - abs(u * 2.0 - 1.0);
                    float stripe = pow(c, 1.6);
                    float cusps = 0.55 + 0.45 * sin(along * 1.7 + noise * 3.0);
                    a = stripe * cusps * smoothstep(0.22 + breakup, 0.62 + breakup, noise * 0.8 + stripe * 0.3);
                    aerated = stripe * 0.35;
                }
                else
                {
                    // 추진기 물살: 가운데 거품 띠, 가장자리로 갈수록 성기게. 휘저을수록 거품이 짙고 넓다.
                    float c = 1.0 - abs(u * 2.0 - 1.0);
                    float core = smoothstep(0.0, 0.85 - churn * 0.35, c);
                    // 거품은 덩어리로 끊기게(노이즈 비중을 크게) — 띠 전체를 칠하면 페인트 줄처럼 보인다
                    float th = 0.36 + breakup - churn * 0.12;
                    a = core * smoothstep(th, th + 0.26, noise * 0.95 + core * 0.15);
                    // 공기 섞인 물빛: 가운데에 부드럽게, 가장자리로 흐려지며 오래될수록 맑아진다(물빛과 같은 결로 얼룩지게)
                    aerated = smoothstep(0.0, 0.9, c) * (0.25 + 0.75 * life) * (0.45 + 0.55 * noise);
                }

                float foamA = saturate(a * i.alpha) * _FoamColor.a;
                float aerA = saturate(aerated * i.alpha) * _AeratedColor.a;

                // 거품은 해를 받는 쪽이 조금 더 밝다
                Light light = GetMainLight();
                float lit = (0.75 + 0.25 * saturate(light.direction.y));
                half3 foamCol = _FoamColor.rgb * lit * light.color;
                half3 aerCol = _AeratedColor.rgb * lit * light.color;
                // 공기 섞인 물빛 위에 거품을 얹는다
                a = saturate(foamA + aerA * (1.0 - foamA));
                half3 col = (foamCol * foamA + aerCol * aerA * (1.0 - foamA)) / max(a, 1e-4);
                #if defined(_LIGHT_COOKIES)
                    col *= lerp(1.0, SampleMainLightCookie(i.positionWS).r, 0.7);   // 구름 그림자
                #endif
                col = MixFog(col, i.fogFactor);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
