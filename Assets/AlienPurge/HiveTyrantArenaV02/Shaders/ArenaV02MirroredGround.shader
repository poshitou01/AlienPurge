Shader "AlienPurge/ArenaV02/MirroredGround"
{
    Properties
    {
        _BaseMap ("Matte research floor", 2D) = "white" {}
        _FloorSize ("Ellipse width / height", Vector) = (48,30,0,0)
        _DetailContrast ("Surface detail contrast", Range(0,1)) = 0.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _FloorSize;
                float _DetailContrast;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 localXY : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.localXY = input.positionOS.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Continuous at every integer UV edge. No dependence on matching painted edges.
                float2 mirroredUV = 1.0 - abs(frac(input.uv * 0.5) * 2.0 - 1.0);
                half3 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, mirroredUV).rgb;
                half3 color = lerp(half3(0.48, 0.48, 0.46), sampled, _DetailContrast);
                float radial = length(input.localXY * 2.0 / _FloorSize.xy);
                float lineA = 1.0 - smoothstep(0.0015, 0.004, abs(radial - 0.88));
                float lineB = 1.0 - smoothstep(0.0015, 0.004, abs(radial - 0.975));
                color *= 1.0 - 0.14 * max(lineA, lineB);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
