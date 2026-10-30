// The glow of boss tiers above 5 (issue #11). It is drawn as an extra material over the boss's own ones, so it only
// adds light: a rim that follows the outline of the body, a tint of the body's own texture, and a band of light
// that sweeps over it. Everything pulses in the shader, so the mod only sets the values when a boss spawns.
// Built into Bundles/tierlook.bundle by Assets/Editor/BuildTierLook.cs; see CONTRIBUTING.md.
Shader "CustomBossChallenge/TierGlow"
{
    Properties
    {
        _BaseMap ("Body texture", 2D) = "white" {}
        _TierColor ("Tier colour", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Range(0, 2)) = 0.5
        _RimPower ("Rim width (higher is thinner)", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim", Range(0, 4)) = 1.5
        _FillStrength ("Tint of the body", Range(0, 1)) = 0.25
        _ShimmerStrength ("Sweeping band", Range(0, 2)) = 0.4
        _PulseAmplitude ("Pulse amplitude", Range(0, 1)) = 0.2
        _PulsePeriod ("Seconds per pulse", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TierGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _TierColor;
                half _Strength;
                half _RimPower;
                half _RimStrength;
                half _FillStrength;
                half _ShimmerStrength;
                half _PulseAmplitude;
                float _PulsePeriod;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The game's camera is orthographic; this gives the right view direction for both kinds
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normalize(input.normalWS), view));
                half rim = pow(1 - facing, _RimPower) * _RimStrength;

                half3 body = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half fill = dot(body, half3(0.299, 0.587, 0.114)) * _FillStrength;

                // A soft diagonal band that crosses the boss about every three seconds
                half band = frac((input.positionWS.x + input.positionWS.z) * 0.01 - _Time.y * 0.33);
                half shimmer = smoothstep(0.4, 0.5, band) * (1 - smoothstep(0.5, 0.6, band)) * _ShimmerStrength;

                half pulse = 1 + _PulseAmplitude * sin(_Time.y * 6.2831853 / max(_PulsePeriod, 0.05));
                return half4(_TierColor.rgb * (rim + fill + shimmer) * _Strength * pulse, 1);
            }
            ENDHLSL
        }
    }
}
