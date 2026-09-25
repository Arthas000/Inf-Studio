Shader "InFalsusStudio/Background"
{
    Properties { _MainTex("Background",2D)="black"{} _Brightness("Brightness",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION;float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Brightness;
            CBUFFER_END
            Varyings vert(Attributes v){Varyings o;o.positionHCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 frag(Varyings v):SV_Target{return half4(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv).rgb*_Brightness,1);}
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Brightness;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
            fixed4 frag(v2f i):SV_Target{return fixed4(tex2D(_MainTex,i.uv).rgb*_Brightness,1);}
            ENDCG
        }
    }
}
