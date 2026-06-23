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
            TEXTURE2D(_PatternRt);
            SAMPLER(sampler_PatternRt);
            TEXTURE2D(_PaintRt);
            SAMPLER(sampler_PaintRt);
            float4 _PitchUvTransform;
            half4 _GrassHealthyColor;
            half4 _GrassDryColor;
            half _GrassMoistureDarken;
            // Stripe params pushed as globals by GrassRenderer (F0.6).
            half _GroundStripeContrast;
            half _GroundSpecStrength;
            half _GroundSpecPower;
            half _GroundBendNormalWeight;
            half _GroundStripeAlbedoGain;

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

                // Far-LOD stripes (F0.6/TDD §3.3): beyond the blade bands the
                // stripes are read from this texture pass. Same bent-normal
                // model as GrassBlade so the band transition stays coherent.
                float2 patternDir = SAMPLE_TEXTURE2D(_PatternRt, sampler_PatternRt, pitchUv).rg;
                float2 bendDir = patternDir * _GroundStripeContrast;

                float3 normalWS = normalize(input.normalWS);
                normalWS = normalize(normalWS + float3(-bendDir.x, 0.0, -bendDir.y) * _GroundBendNormalWeight);

                Light mainLight = GetMainLight();
                half halfLambert = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half specular = pow(saturate(dot(normalWS, halfDir)), _GroundSpecPower)
                    * _GroundSpecStrength * length(bendDir);
                half3 lighting = mainLight.color * (halfLambert + specular) + SampleSH(normalWS);

                // Direct albedo stripe term, same model as GrassBlade (F0.7).
                float2 viewXz = viewDirWS.xz / max(length(viewDirWS.xz), 1e-4);
                albedo *= 1.0 + _GroundStripeAlbedoGain * dot(patternDir, viewXz);

                half3 lit = albedo * lighting;

                // Paint lines (M.2): RT_Paint.r is coverage; overlay white,
                // lit by the same main light so lines sit in the scene.
                half paint = SAMPLE_TEXTURE2D(_PaintRt, sampler_PaintRt, pitchUv).r;
                half3 paintColor = half3(1.0, 1.0, 1.0) * (halfLambert + 0.15);
                lit = lerp(lit, paintColor, paint);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
