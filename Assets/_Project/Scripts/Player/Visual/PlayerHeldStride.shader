Shader "AlienPurge/Player/HeldStride"
{
    Properties
    {
        [PerRendererData] _MainTex("Held upper body",2D)="white"{}
        _Walking("Walking",Float)=0
        _Stride("Stride phase",Float)=0
        _Facing("Facing direction",Vector)=(1,0,0,0)
        _LegJoin("Leg root measured from top",Float)=.75
        [HideInInspector] _Color("Tint",Color)=(1,1,1,1)
        [HideInInspector] _RendererColor("Renderer tint",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _RendererColor, _Facing;
            float _Walking, _LegJoin, _Stride;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS);
                o.uv=v.uv;o.color=v.color*_Color*_RendererColor;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float y=1-i.uv.y;
                // A continuous deformation keeps the authored silhouette connected. The hips and
                // hands remain fixed, while the boots alternate their stride and lift without
                // blending in the unarmed walk frames (whose arm swing breaks the weapon grip).
                float weight=smoothstep(_LegJoin,.94,y)*_Walking;
                float side=smoothstep(.40,.60,i.uv.x)*2-1;
                float cycle=sin(_Stride*6.2831853),step=cycle*side;
                float2 uv=i.uv;
                uv.x-=_Facing.x*step*.028*weight;
                uv.y-=(_Facing.y*step*.034+abs(cycle)*.009)*weight;
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*i.color;
            }
            ENDHLSL
        }
    }
}
