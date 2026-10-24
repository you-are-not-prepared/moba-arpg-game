// Melee swing arc (see MeleeSwing). Additive. The mesh's UV.x runs along the arc (0 = where the swing starts,
// 1 = where it ends) and UV.y across the blade; _Progress (0..1, per renderer) moves a bright leading edge along
// the arc with a fading trail behind it, then fades the whole slash out. Brightest towards the blade's outer edge.
Shader "Moba/MeleeSlash"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.95, 0.8, 0.9)
        _Progress ("Progress", Range(0, 1)) = 1
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
            Name "MeleeSlash"
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
                float _Progress;
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
                // The leading edge reaches the end of the arc at 70% progress; the trail behind it is 80% of the arc.
                float head = _Progress / 0.7;
                float behind = head - input.uv.x;
                float ahead = step(0.0, behind);
                float trail = ahead * saturate(1.0 - behind / 0.8);
                float edge = ahead * exp(-behind * 14.0);
                float across = sin(input.uv.y * 3.14159) * (0.45 + 0.55 * input.uv.y);
                float fadeOut = 1.0 - smoothstep(0.7, 1.0, _Progress);
                float glow = (trail * 0.9 + edge * 0.9) * across * fadeOut;
                return half4(_Color.rgb * (1.0 + edge), saturate(glow * _Color.a * 1.6));
            }
            ENDHLSL
        }
    }
}
