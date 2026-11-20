// 적 피격 번쩍임(2026-11-20 타격감). View/HitFlash가 맞은 적의 메시 위에 같은 메시를 한 겹 더 그려 잠깐 하얗게 더한다.
//   더하기 합성(원래 색 위에 빛을 얹는다) · 깊이 기록 없음 · 같은 깊이에서 이기도록 Offset.
//   가장자리(시선과 비스듬한 면)가 조금 더 밝아 입체감이 남는다. 세기는 _FlashColor(재질 블록)로 매 프레임 준다.
Shader "Naval/HitFlash"
{
    Properties
    {
        [HDR] _FlashColor ("Flash color", Color) = (0, 0, 0, 0)
        _Rim ("Rim emphasis", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "HitFlash"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FlashColor;
                half _Rim;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = _WorldSpaceCameraPos.xyz - p.positionWS;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half nv = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half k = lerp(1.0h, 0.55h + (1.0h - nv) * 0.9h, _Rim);
                return half4(_FlashColor.rgb * k, 0);
            }
            ENDHLSL
        }
    }
}
