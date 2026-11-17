// 폭발 불덩이(파티클). DecorFx.BlastFire가 쓰는 재질(Resources/Effects/MAT_BlastFire).
//   정점 색: rgb = 불빛(수명에 따라 노랑 → 주황 → 검붉게), 알파 = 남은 정도(1 = 막 터짐, 0 = 다 탐).
//   균일하게 흐려지는 대신 노이즈를 따라 갈라지며 녹아 사라지고(디졸브), 녹는 가장자리가 타오르듯 빛난다.
//   원 모양과 노이즈를 셰이더에서 계산해 텍스처가 필요 없다. 파티클마다 회전이 달라 무늬가 겹치지 않는다.
Shader "Naval/BlastFire"
{
    Properties
    {
        [HDR] _EdgeColor ("Burning edge", Color) = (2.0, 0.75, 0.2, 1)
        _EdgeWidth ("Burning edge width", Range(0.01, 0.4)) = 0.09
        _StartCut ("Ragged outline at start", Range(0, 0.5)) = 0.16
        _Softness ("Dissolve softness", Range(0.005, 0.3)) = 0.04
        _NoiseScale ("Noise scale", Range(1, 12)) = 4.2
        _NoiseAmount ("Noise amount", Range(0, 1)) = 0.62
        _CoreBoost ("Core brightness", Range(0, 2)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "BlastFire"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EdgeColor;
                half _EdgeWidth;
                half _StartCut;
                half _Softness;
                half _NoiseScale;
                half _NoiseAmount;
                half _CoreBoost;
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
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.color = v.color;
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // 몸통: 가운데가 단단하고 바깥은 노이즈로 너덜너덜하다(0~1)
                float2 c = i.uv * 2.0 - 1.0;
                half shape = saturate(1.0 - dot(c, c));
                float2 np = i.uv * _NoiseScale;
                half n = ValueNoise(np) * 0.62 + ValueNoise(np * 2.3 + 7.1) * 0.38;
                half body = saturate(shape * (1.0 - _NoiseAmount + _NoiseAmount * n * 2.0));

                // 디졸브: 남은 정도(정점 알파)가 줄수록 문턱이 올라가 얇은 곳부터 사라진다.
                // 막 터졌을 때도 바깥의 얇은 곳은 잘라 둥근 원판이 아니라 울퉁불퉁한 덩이로 시작한다.
                half progress = 1.0 - i.color.a;
                half threshold = lerp(_StartCut, 1.0 + _Softness, progress);
                half alpha = smoothstep(threshold - _Softness, threshold + _Softness, body);
                clip(alpha - 0.004);

                // 녹는 가장자리는 갈라지기 시작하면서부터 타오르듯, 두꺼운 속은 더 밝게, 속에도 노이즈 명암
                half edge = (1.0 - smoothstep(threshold, threshold + _EdgeWidth, body)) * saturate(progress * 3.0);
                half3 col = i.color.rgb * (1.0 + _CoreBoost * saturate(body - threshold)) * lerp(0.72, 1.15, n);
                col += _EdgeColor.rgb * edge * (1.0 - progress * 0.6);

                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
