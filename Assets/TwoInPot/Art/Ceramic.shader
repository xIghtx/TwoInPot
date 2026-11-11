Shader "TwoInPot/Ceramic"
{
    Properties { _BaseColor ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varying { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varying Vert(Input v)
            {
                Varying o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varying i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half diffuse = .55h + .45h * saturate(dot(n, normalize(float3(-.5, .8, -1))));
                half glint = pow(saturate(dot(n, normalize(float3(-.35, .5, -1)))), 24) * .18h;
                return half4(_BaseColor.rgb * diffuse + glint, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
