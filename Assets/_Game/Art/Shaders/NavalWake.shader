// 배가 지나간 물살(항적). ShipWake가 만드는 띠 메시에 입힌다.
//   정점: 색 알파 = 세기(속력 × 남은 수명), uv.x = 띠 가로(0 좌 ~ 1 우), uv.y = 띠를 따라간 거리(m)
//   _Style 0 = 켈빈 항적(선수에서 V자로 벌어지는 두 줄의 흰 물결 — ShipWake가 두 줄만 만든다)
//   _Style 1 = 추진기 물살(선미 뒤로 곧게 이어지는 거품 띠, 가운데가 가장 희고 흐트러짐)
// 거품은 월드 좌표 노이즈로 쪼개 흘러가게 해 한 장짜리 띠로 보이지 않게 한다.
Shader "Naval/Wake"
{
    Properties
    {
        _FoamColor ("Foam", Color) = (0.9, 0.96, 1, 0.85)
        _Style ("Style (0 Kelvin arms, 1 propeller wash)", Float) = 0
        _EdgeSharpness ("Arm sharpness", Range(2, 30)) = 9
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
            CBUFFER_END

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
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float t = _Time.y;
                float u = i.uv.x;
                float along = i.uv.y;

                // 거품을 쪼개는 노이즈(흘러감)
                float n1 = ValueNoise(xz * 1.35 + float2(t * 0.35, -t * 0.2));
                float n2 = ValueNoise(xz * 3.6 - float2(t * 0.55, t * 0.3));
                float noise = n1 * 0.62 + n2 * 0.38;

                float a;
                if (_Style < 0.5)
                {
                    // 켈빈 항적의 한 줄: 가운데가 가장 희고, 줄을 따라 물결 마루처럼 끊어진다
                    float c = 1.0 - abs(u * 2.0 - 1.0);
                    float stripe = pow(c, 1.6);
                    float cusps = 0.55 + 0.45 * sin(along * 1.7 + noise * 3.0);
                    a = stripe * cusps * smoothstep(0.22, 0.62, noise * 0.8 + stripe * 0.3);
                }
                else
                {
                    // 추진기 물살: 가운데 거품 띠, 가장자리로 갈수록 성기게
                    float c = 1.0 - abs(u * 2.0 - 1.0);
                    float core = smoothstep(0.0, 0.85, c);
                    a = core * smoothstep(0.28, 0.72, noise * 0.85 + core * 0.35);
                }

                a = saturate(a * i.alpha) * _FoamColor.a;

                // 거품은 해를 받는 쪽이 조금 더 밝다
                Light light = GetMainLight();
                half3 col = _FoamColor.rgb * (0.75 + 0.25 * saturate(light.direction.y)) * light.color;
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
