Shader "AlienPurge/ArenaV03/ContinuousAtlas"
{
    Properties
    {
        _BaseMap ("Shared Environment Atlas", 2D) = "white" {}
        _DetailMap ("Ground Microdetail", 2D) = "gray" {}
        _DetailAmount ("Ground Detail Amount", Range(0,0.3)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 detailUV:TEXCOORD1; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 detailUV:TEXCOORD1; float4 color:COLOR; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DetailMap_ST;
                float _DetailAmount;
            CBUFFER_END
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.detailUV = IN.detailUV;
                OUT.color = IN.color;
                return OUT;
            }
            half4 frag(Varyings IN):SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                if (_DetailAmount > 0.0001)
                {
                    float2 mirrorUV = 1.0 - abs(frac(IN.detailUV * .5) * 2.0 - 1.0);
                    half3 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, mirrorUV).rgb;
                    half gray = dot(detail, half3(.2126,.7152,.0722));
                    half amount = _DetailAmount * IN.color.a;
                    c.rgb *= 1.0 + (gray - .48) * amount;
                }
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
