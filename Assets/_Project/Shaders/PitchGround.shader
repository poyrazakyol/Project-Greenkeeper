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
            // Moisture dose visuals (M.3, shared globals).
            half _PuddleThreshold;
            half _DryThreshold;
            half4 _MoistureDryColor;
            half _WetStart;
            half _WetFull;
            half _WetDarken;
            half _WetSaturation;
            half _WetGloss;
            half _WetSheenPower;
            half4 _PuddleWaterColor;
            half _PuddleOpacity;
            half4 _PuddleSkyColor;
            half _PuddleSpecular;
            half _PuddleGlintPower;
            half _PuddleReflection;
            half _PuddleRipple;
            float _PuddleNoiseScale;
            half _PuddleCoverageAtThreshold;
            half _PuddleCoverageAtFull;
            half _PuddleCoverageExponent;

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth 2D value noise in [0,1]; breaks standing water into
            // patches instead of a hard disc.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 pitchUv = input.positionWS.xz * _PitchUvTransform.zw + _PitchUvTransform.xy;
                half4 state = SAMPLE_TEXTURE2D(_GrassStateRt, sampler_GrassStateRt, pitchUv);

                // Moisture (B) mirrors the CPU grid (PitchRenderBridge sync).
                // Wet ramp completes at the puddle threshold, so the whole
                // >threshold area reads fully wet; the noise below only decides
                // where water actually stands.
                half moisture = state.b;
                half wet = smoothstep(_WetStart, _WetFull, moisture);

                half3 albedo = lerp(_GrassDryColor.rgb, _GrassHealthyColor.rgb, state.g);
                albedo *= lerp(1.0, _GrassMoistureDarken, moisture);
                albedo *= lerp(1.0, _WetDarken, wet); // watered ground reads clearly darker
                albedo *= _SoilDarken; // ground reads darker than blade tips
                albedo = lerp(albedo, _DamageColor.rgb, state.a); // mud where damaged

                // Wet soil/grass base: deeper, more saturated (never lighter).
                half luma = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = max(lerp(luma.xxx, albedo, 1.0 + _WetSaturation * wet), 0.0);

                // Dry tint, soft ramp below the dry threshold.
                half dry = 1.0 - smoothstep(_DryThreshold * 0.5, _DryThreshold, moisture);
                albedo = lerp(albedo, _MoistureDryColor.rgb, dry * 0.7);

                // Standing water (moisture > puddle threshold): the covered
                // fraction follows the SO coverage curve — scattered patches at
                // the threshold, a near-continuous sheet toward 100 — and the
                // noise decides where inside the area the water sits.
                half overflow01 = saturate((moisture - _PuddleThreshold) / max(1.0 - _PuddleThreshold, 0.001));
                half coverage = lerp(_PuddleCoverageAtThreshold, _PuddleCoverageAtFull,
                    pow(overflow01, _PuddleCoverageExponent));
                // Approximate inverse of the noise distribution: covered
                // fraction -> noise cutoff (noise mostly spans ~0.15-0.85).
                half cutoff = lerp(0.85, 0.15, coverage);
                half presence = smoothstep(_PuddleThreshold, _PuddleThreshold + 0.02, moisture);
                float2 noisePos = input.positionWS.xz / _PuddleNoiseScale;
                half noise = ValueNoise(noisePos) * 0.65 + ValueNoise(noisePos * 2.3 + 17.0) * 0.35;
                half puddle = smoothstep(cutoff, cutoff + 0.06, noise) * presence;

                // Soaked rim just outside each patch: darker wet soil, so the
                // water edge reads as a waterline instead of a cut-out.
                half rim = saturate(smoothstep(cutoff - 0.15, cutoff, noise) * presence - puddle);
                albedo *= lerp(1.0, 0.7, rim);

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
                // Stripe specular + a broad wet sheen; both multiply albedo,
                // so wet ground darkens/glosses but does not whiten.
                half nDotH = saturate(dot(normalWS, halfDir));
                half specular = pow(nDotH, _GroundSpecPower) * _GroundSpecStrength * length(bendDir)
                    + pow(nDotH, _WetSheenPower) * wet * _WetGloss;
                half3 lighting = mainLight.color * (halfLambert + specular) + SampleSH(normalWS);

                // Direct albedo stripe term, same model as GrassBlade (F0.7).
                float2 viewXz = viewDirWS.xz / max(length(viewDirWS.xz), 1e-4);
                albedo *= 1.0 + _GroundStripeAlbedoGain * dot(patternDir, viewXz);

                half3 lit = albedo * lighting;

                // Water surface: shallow, slightly transparent water over the
                // soaked ground. The body is the ground seen through the water,
                // pulled toward the dark deep tone by _PuddleOpacity. Sky
                // reflection is pure fresnel (faint when looking down, strong
                // at grazing angles) and sun glints appear only on ripple
                // crests — dark water with sparkle, never a white sheet.
                float2 ripplePos = input.positionWS.xz / max(_PuddleNoiseScale * 0.35, 0.05);
                float t = _Time.y;
                float rippleA = ValueNoise(ripplePos + float2(t * 0.35, 0.0));
                float rippleB = ValueNoise(ripplePos * 1.3 + float2(31.0, t * 0.3));
                float3 waterN = normalize(float3((rippleA - 0.5) * _PuddleRipple, 1.0, (rippleB - 0.5) * _PuddleRipple));
                half crest = smoothstep(0.62, 0.8, (rippleA + rippleB) * 0.5);

                half waterLambert = saturate(dot(waterN, mainLight.direction)) * 0.5 + 0.5;
                half3 deep = _PuddleWaterColor.rgb * (mainLight.color * waterLambert + SampleSH(waterN));
                half3 body = lerp(lit * 0.8, deep, _PuddleOpacity);
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(waterN, viewDirWS)), 5.0);
                half3 sky = _PuddleSkyColor.rgb * (0.7 + 0.3 * SampleSH(reflect(-viewDirWS, waterN)));
                half glint = pow(saturate(dot(waterN, halfDir)), _PuddleGlintPower) * _PuddleSpecular * crest;
                half3 waterLit = lerp(body, sky, saturate(fresnel * _PuddleReflection))
                    + mainLight.color * glint;
                lit = lerp(lit, waterLit, puddle);

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
