// Pitch ground shader: the far-LOD half of the grass system (TDD §3.3 —
// beyond the blade bands the ground itself shows grass color by sampling the
// same RT_GrassState, so stripes/tints read at distance). Uses the shared
// color globals pushed by GrassRenderer so blades and ground match.
// RT_Paint sampling (line paint) arrives with M.2.
Shader "Greenkeeper/PitchGround"
{
    Properties
    {
        _SoilDarken ("Soil Darken", Range(0.4, 1)) = 0.82
        _DamageColor ("Damaged Soil Color", Color) = (0.35, 0.26, 0.16, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "GroundForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassStateRt);
            SAMPLER(sampler_GrassStateRt);
            float4 _PitchUvTransform;
            half4 _GrassHealthyColor;
            half4 _GrassDryColor;
            half _GrassMoistureDarken;

            half _SoilDarken;
            half4 _DamageColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 pitchUv = input.positionWS.xz * _PitchUvTransform.zw + _PitchUvTransform.xy;
                half4 state = SAMPLE_TEXTURE2D(_GrassStateRt, sampler_GrassStateRt, pitchUv);

                half3 albedo = lerp(_GrassDryColor.rgb, _GrassHealthyColor.rgb, state.g);
                albedo *= lerp(1.0, _GrassMoistureDarken, state.b);
                albedo *= _SoilDarken; // ground reads darker than blade tips
                albedo = lerp(albedo, _DamageColor.rgb, state.a); // mud where damaged

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half halfLambert = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                half3 lighting = mainLight.color * halfLambert + SampleSH(normalWS);

                return half4(albedo * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
