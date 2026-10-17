Shader "AlienPurge/Level01ConnectedV01/Atlas"
{
    Properties
    {
        [MainTexture] _BaseMap("Atlas",2D)="white"{}
        _DetailMap("Ground detail",2D)="gray"{}
        _Cell("Atlas cell x y width height",Vector)=(0,0,1,1)
        _Color("Tint",Color)=(1,1,1,1)
        _Focus("Focused outline",Range(0,1))=0
        _OutlinePixels("Outline width",Float)=2
        _DetailAmount("Ground microdetail",Range(0,.2))=0
        _WorldRepeat("World texture period",Float)=8
        _FloorMode("World floor sampling",Float)=0
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags{"LightMode"="Universal2D"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);SAMPLER(sampler_DetailMap);
            float4 _BaseMap_TexelSize;
            CBUFFER_START(UnityPerMaterial)
            float4 _Cell,_Color;
            float _Focus,_OutlinePixels,_DetailAmount,_WorldRepeat,_FloorMode;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;};
            Varyings vert(Attributes input)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.world=TransformObjectToWorld(input.positionOS.xyz).xy;o.uv=input.uv;return o;
            }
            float2 mirrorUV(float2 uv){return 1-abs(frac(uv*.5)*2-1);}
            float atlasAlpha(float2 uv)
            {
                float2 lo=_Cell.xy+_BaseMap_TexelSize.xy*.5;
                float2 hi=_Cell.xy+_Cell.zw-_BaseMap_TexelSize.xy*.5;
                if(any(uv<lo)||any(uv>hi))return 0;
                return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=_Cell.xy+i.uv*_Cell.zw;
                if(_FloorMode>.5)uv=mirrorUV(i.world/max(_WorldRepeat,.1));
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_Color;
                if(_DetailAmount>.0001)
                {
                    half d=SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,mirrorUV(i.world/6)).r;
                    c.rgb*=1+(d-.5)*_DetailAmount;
                }
                if(_Focus>.001 && _FloorMode<.5)
                {
                    float2 d=_BaseMap_TexelSize.xy*_OutlinePixels;
                    float a=max(max(atlasAlpha(uv+float2(d.x,0)),atlasAlpha(uv-float2(d.x,0))),
                        max(atlasAlpha(uv+float2(0,d.y)),atlasAlpha(uv-float2(0,d.y))));
                    a=max(a,max(max(atlasAlpha(uv+d),atlasAlpha(uv-d)),
                        max(atlasAlpha(uv+float2(d.x,-d.y)),atlasAlpha(uv+float2(-d.x,d.y)))));
                    float edge=saturate(a-c.a)*_Focus;
                    float outAlpha=max(c.a,edge);
                    c.rgb=lerp(half3(.95,.98,1),c.rgb,c.a/max(outAlpha,.0001));c.a=outAlpha;
                }
                return c;
            }
            ENDHLSL
        }
    }
}
