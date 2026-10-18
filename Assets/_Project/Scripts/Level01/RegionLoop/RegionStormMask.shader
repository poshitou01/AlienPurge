Shader "AlienPurge/Level01/RegionStormMask"
{
    Properties { _Color("Tint", Color) = (.16,.23,.42,.22) _SafeZone("Native safe zone", Vector) = (0,0,4,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _SafeZone;
            CBUFFER_END
            Varyings vert(Attributes i)
            {
                Varyings o; float3 world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world); o.world = world.xy; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float distanceFromSafety = distance(i.world, _SafeZone.xy) - _SafeZone.z;
                clip(distanceFromSafety);
                // Wind bands make danger legible on bright concrete without concealing bullets/enemies.
                float bands = .9 + .1 * sin(i.world.x * .8 + i.world.y * .4 - _Time.y * 2);
                return half4(_Color.rgb, _Color.a * bands * saturate(distanceFromSafety * 2));
            }
            ENDHLSL
        }
    }
}
