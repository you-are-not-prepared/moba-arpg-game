// Pulsing glow ring on the ground (buff auras). Additive; draw it on a flat disc whose UVs map 0..1 across it
// (a Unity cylinder's caps do).
Shader "Moba/GroundRing"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.3, 0.25, 1)
        _PulseSpeed ("Pulse Speed", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundRing"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float r = distance(input.uv, float2(0.5, 0.5)) * 2.0;
                float ring = smoothstep(0.7, 0.88, r) * (1.0 - smoothstep(0.9, 1.0, r));
                float fill = saturate(1.0 - r) * 0.2;
                float pulse = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);
                return half4(_Color.rgb, saturate((ring + fill) * _Color.a * pulse));
            }
            ENDHLSL
        }
    }
}
