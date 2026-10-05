// 격침 자리의 기름띠(장식). ShipWreck이 수면 바로 위 사각형에 입힌다.
//   uv(0~1) 가운데가 중심. 가장자리는 월드 좌표 노이즈로 들쭉날쭉하게, 안쪽은 얼룩지게.
//   검고 매끈한 막 위에 무지갯빛(두께에 따라 색이 바뀌는 간섭색)이 얇게 돌고, 해 반짝임을 받는다.
//   _Fade(0~1)는 렌더러마다 MaterialPropertyBlock으로 준다(퍼지며 나타났다가 옅어짐).
Shader "Naval/Slick"
{
    Properties
    {
        _SlickColor ("Oil", Color) = (0.035, 0.04, 0.045, 0.78)
        _Sheen ("Iridescent sheen", Range(0, 1)) = 0.16
        _Fade ("Fade", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-70" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Slick"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SlickColor;
                float _Sheen;
                float _Fade;
                float _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fogFactor : TEXCOORD2;
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

            float3 Hue(float h)
            {
                return saturate(abs(frac(h + float3(0.0, 0.6666667, 0.3333333)) * 6.0 - 3.0) - 1.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float t = _Time.y;
                float2 c = i.uv * 2.0 - 1.0;
                float r = length(c);

                // 들쭉날쭉한 가장자리(각도·월드 좌표 노이즈), 안쪽 얼룩
                float2 s = float2(_Seed * 7.13, _Seed * 3.71);
                float edge = ValueNoise(c * 2.3 + s) * 0.6 + ValueNoise(c * 5.1 - s) * 0.4;
                float shape = 1.0 - smoothstep(0.42, 0.95, r + (edge - 0.5) * 0.55);
                float patch = ValueNoise(xz * 0.35 + s + t * 0.03) * 0.65 + ValueNoise(xz * 1.1 - s) * 0.35;
                float body = shape * lerp(0.55, 1.0, smoothstep(0.25, 0.7, patch));

                // 간섭색: 두께(노이즈)와 보는 각도에 따라 색이 돈다. 가장자리가 얇아 더 밝다.
                float3 v = normalize(_WorldSpaceCameraPos - i.positionWS);
                float thick = ValueNoise(xz * 0.8 + s * 1.7 + float2(t * 0.05, -t * 0.03));
                float3 rainbow = lerp(0.45, Hue(thick * 1.6 + v.y * 0.6 + r * 0.5), 0.55);   // 채도를 낮춰 은은하게
                float sheenMask = smoothstep(0.35, 0.8, thick) * (0.5 + 0.5 * smoothstep(0.3, 0.9, r));

                float3 col = _SlickColor.rgb + rainbow * _Sheen * sheenMask * 0.35;

                // 기름막은 물보다 매끈하다: 해 반짝임을 조금
                Light light = GetMainLight();
                float3 h = normalize(light.direction + v);
                col += pow(saturate(h.y), 220.0) * 0.6 * light.color;

                float a = saturate(body * _SlickColor.a * _Fade);
                col = MixFog(col, i.fogFactor);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
