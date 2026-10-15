Shader "AlienPurge/ArenaV021/ContinuousWall"
{
    Properties { _BaseMap ("Ivory containment wall", 2D) = "white" {} }
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
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv;output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 mirroredUV=1.0-abs(frac(input.uv*0.5)*2.0-1.0);
                half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,mirroredUV).rgb*input.color.rgb;
                float u=frac(input.uv.x);
                float joint=1.0-smoothstep(0.006,0.024,min(u,1.0-u));
                float bevel=1.0-smoothstep(0.018,0.07,min(input.uv.y,1.0-input.uv.y));
                color*=1.0-0.16*joint-0.12*bevel;
                return half4(color,1.0);
            }
            ENDHLSL
        }
    }
}
