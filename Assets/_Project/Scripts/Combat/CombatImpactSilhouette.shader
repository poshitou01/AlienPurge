Shader "AlienPurge/Combat/ImpactSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer tint", Color) = (1,1,1,1)
    }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _RendererColor;
            CBUFFER_END
            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Output Vert(Input v) { Output o; o.position=TransformObjectToHClip(v.position.xyz); o.uv=v.uv; o.color=v.color*_Color*_RendererColor; return o; }
            half4 Frag(Output v) : SV_Target { return half4(v.color.rgb, SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv).a*v.color.a); }
            ENDHLSL
        }
    }
}
