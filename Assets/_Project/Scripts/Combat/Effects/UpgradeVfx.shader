Shader "AlienPurge/Combat/UpgradeVfx"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 position : POSITION; float3 uv : TEXCOORD0; float4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float3 uv : TEXCOORD0; float4 color : COLOR; };
            Output Vert(Input v) { Output o; o.position=TransformObjectToHClip(v.position.xyz); o.uv=v.uv; o.color=v.color; return o; }
            half4 Frag(Output v) : SV_Target
            {
                // Discs and ribbons share one material/draw call. Feather edges without a texture or bloom.
                float mask = v.uv.z < .5 ? saturate(1 - dot(v.uv.xy, v.uv.xy)) : saturate(1 - abs(v.uv.y));
                return half4(v.color.rgb, v.color.a * mask * mask);
            }
            ENDHLSL
        }
    }
}
