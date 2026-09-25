Shader "InFalsusStudio/VertexTransparent"
{
    Properties { _FogColor("Fog", Color)=(0.016,0.027,0.042,1) _FogStart("Fog Start", Float)=32 _FogEnd("Fog End", Float)=76 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor; float _FogStart; float _FogEnd;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float worldZ : TEXCOORD0; };
            Varyings Vert(Attributes v) {
                Varyings o; float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world); o.color=v.color; o.worldZ=world.z; return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                float fog=smoothstep(_FogStart,max(_FogStart+0.01,_FogEnd),i.worldZ);
                half4 color=i.color; color.a *= 1.0 - fog; return color;
            }
            ENDHLSL
        }
    }
    // Legacy fallback is not the primary target. The delivered demo is intended for Unity 6 URP.
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _FogColor;float _FogStart;float _FogEnd;
            struct Varyings { float4 position:SV_POSITION;float4 color:COLOR;float worldZ:TEXCOORD0; };
            Varyings Vert(float4 p:POSITION,float4 color:COLOR) { Varyings o;o.position=UnityObjectToClipPos(p);o.color=color;o.worldZ=mul(unity_ObjectToWorld,p).z;return o; }
            half4 Frag(Varyings i):SV_Target {float fog=smoothstep(_FogStart,max(_FogStart+0.01,_FogEnd),i.worldZ);half4 color=i.color;color.a *= 1.0 - fog; return color;}
            ENDCG
        }
    }
}
