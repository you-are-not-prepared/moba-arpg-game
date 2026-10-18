// Glowing, see-through energy wall (the shield ultimate). Additive: a bright rim around the wall's edges and
// faint bands drifting upward through its body, so the blocking area reads clearly from the camera.
// Expects UV.x along the wall and UV.y from the ground (0) to the top (1).
Shader "Moba/ShieldWall"
{
    Properties
    {
        _Color ("Body Color", Color) = (0.35, 0.7, 1, 0.45)
        _EdgeColor ("Edge Color", Color) = (0.85, 0.95, 1, 1)
        _BandSpeed ("Band Speed", Float) = 1.5
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
            Name "ShieldWall"
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
                half4 _EdgeColor;
                float _BandSpeed;
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
                float2 uv = input.uv;
                float edgeX = min(uv.x, 1.0 - uv.x) * 10.0;
                float edgeY = min(uv.y, 1.0 - uv.y) * 7.0;
                float rim = saturate(1.0 - min(edgeX, edgeY));
                float bands = 0.5 + 0.5 * sin((uv.y * 6.0 - _Time.y * _BandSpeed) * 6.2831853);
                float body = _Color.a * (0.55 + 0.45 * bands) * (1.0 - uv.y * 0.35);
                half3 color = lerp(_Color.rgb, _EdgeColor.rgb, rim);
                return half4(color, saturate(body + rim * _EdgeColor.a));
            }
            ENDHLSL
        }
    }
}
