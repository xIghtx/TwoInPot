Shader "TwoInPot/PixelUnlit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _PixelTex ("Pixel Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_PixelTex); SAMPLER(sampler_PixelTex);
            float4 _Color;
            struct Input { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Output { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Output Vert(Input v)
            {
                SetUpSpriteInstanceProperties();
                Output o; o.positionCS = TransformObjectToHClip(UnityFlipSprite(v.positionOS, unity_SpriteProps.xy)); o.uv = v.uv; o.color = v.color * _Color * unity_SpriteColor; return o;
            }
            half4 Frag(Output i) : SV_Target { return SAMPLE_TEXTURE2D(_PixelTex, sampler_PixelTex, i.uv) * i.color; }
            ENDHLSL
        }
    }
}
