// Instanced grass blade shader (TDD §3.3). Blade root positions are derived
// from the instance id with a per-tile hash stream — no per-instance CPU
// buffers. The vertex stage samples RT_GrassState (R: height -> blade scale,
// G: health / B: moisture -> color, A: damage -> blade cull) and applies a
// cheap idle sway. Lighting is per-vertex half-Lambert + ambient SH.
// RT_Pattern sampling (stripe bend) intentionally absent — arrives in F0.6.
Shader "Greenkeeper/GrassBlade"
{
    Properties
    {
        _BladeWidth ("Blade Width (m)", Float) = 0.014
        _BladeHeightAtMax ("Blade Height At Max State (m)", Float) = 0.09
        _HeightJitter ("Height Jitter", Range(0, 0.5)) = 0.2
        _WidthJitter ("Width Jitter", Range(0, 0.5)) = 0.25
        _RootDarken ("Root Darken", Range(0, 1)) = 0.55
        _DamageThreshold ("Damage Discard Threshold", Range(0, 1)) = 0.5
        _SwayAmp ("Sway Amplitude (m)", Float) = 0.008
        _SwayFreq ("Sway Frequency", Float) = 1.6
        _BendAmount ("Pattern Bend Amount", Range(0, 1)) = 0.35
        _BendNormalWeight ("Bend Normal Weight", Range(0, 1)) = 0.6
        _SpecStrength ("Specular Strength", Range(0, 1)) = 0.25
        _SpecPower ("Specular Power", Float) = 24
        _StripeAlbedoGain ("Stripe Albedo Gain", Range(0, 0.5)) = 0.18
        _LodTintOn ("LOD Debug Tint", Float) = 0
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
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Bound as globals by PitchRenderBridge / GrassRenderer.
            TEXTURE2D(_GrassStateRt);
            SAMPLER(sampler_GrassStateRt);
            TEXTURE2D(_PatternRt);
            SAMPLER(sampler_PatternRt);
            float4 _PitchUvTransform;   // uv = worldXZ * zw + xy
            half4 _GrassHealthyColor;
            half4 _GrassDryColor;
            half _GrassMoistureDarken;

            // Material-level (set once from GrassRenderSettingsSO).
            float _BladeWidth;
            float _BladeHeightAtMax;
            float _HeightJitter;
            float _WidthJitter;
            half _RootDarken;
            float _DamageThreshold;
            float _SwayAmp;
            float _SwayFreq;
            float _BendAmount;
            float _BendNormalWeight;
            float _SpecStrength;
            float _SpecPower;
            float _StripeAlbedoGain;
            float _LodTintOn;

            // Per-draw (MaterialPropertyBlock, one per tile).
            float4 _TileOriginSize;     // xy: tile origin XZ, zw: tile size
            float _TileSeed;
            float _WidthScale;
            half4 _LodTintColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR0;
            };

            float Hash01(uint n)
            {
                // PCG-style integer hash -> [0, 1)
                n = n * 747796405u + 2891336453u;
                n = ((n >> ((n >> 28) + 4u)) ^ n) * 277803737u;
                n = (n >> 22) ^ n;
                return n * (1.0 / 4294967296.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                uint seed = (uint)_TileSeed + input.instanceID * 8u;
                float hx = Hash01(seed);
                float hz = Hash01(seed + 1u);
                float hRot = Hash01(seed + 2u);
                float hHeight = Hash01(seed + 3u);
                float hWidth = Hash01(seed + 4u);
                float hPhase = Hash01(seed + 5u);

                float2 rootXz = _TileOriginSize.xy + float2(hx, hz) * _TileOriginSize.zw;
                float2 pitchUv = rootXz * _PitchUvTransform.zw + _PitchUvTransform.xy;
                float4 state = SAMPLE_TEXTURE2D_LOD(_GrassStateRt, sampler_GrassStateRt, pitchUv, 0);
                // Mow direction (F0.6): zero where never mowed -> upright blade.
                float2 bendDir = SAMPLE_TEXTURE2D_LOD(_PatternRt, sampler_PatternRt, pitchUv, 0).rg;

                float bladeHeight = state.r * _BladeHeightAtMax * (1.0 + (hHeight * 2.0 - 1.0) * _HeightJitter);
                float bladeWidth = _BladeWidth * _WidthScale * (1.0 + (hWidth * 2.0 - 1.0) * _WidthJitter);
                if (state.a > _DamageThreshold)
                {
                    // Bald spot: collapse the blade to a degenerate triangle.
                    bladeHeight = 0.0;
                    bladeWidth = 0.0;
                }

                float sinR, cosR;
                sincos(hRot * 6.2831853, sinR, cosR);
                float2 widthDirXz = float2(cosR, sinR);

                float heightFrac = input.uv.y;
                float sway = sin(_Time.y * _SwayFreq + hPhase * 6.2831853 + rootXz.x * 0.7 + rootXz.y * 0.5)
                    * _SwayAmp * heightFrac * heightFrac;

                // Geometric bend (F0.6): the tip leans along the mow direction;
                // sway rides on top of the static lean.
                float2 bendOffset = bendDir * (_BendAmount * bladeHeight * heightFrac * heightFrac);

                float3 positionWS;
                positionWS.xz = rootXz
                    + widthDirXz * (input.positionOS.x * bladeWidth)
                    + bendOffset
                    + float2(0.8, 0.6) * sway;
                positionWS.y = input.positionOS.y * bladeHeight;
                output.positionCS = TransformWorldToHClip(positionWS);

                // Per-vertex lighting, 4 verts per blade keeps it cheap. The
                // normal tilts AGAINST the bend (real lawn optics): the
                // specular/diffuse angle difference is what draws the stripes
                // (TDD §3.3 — the signature visual).
                float3 normalWS = lerp(float3(widthDirXz.y, 0.0, -widthDirXz.x), float3(0.0, 1.0, 0.0), 0.7);
                normalWS = normalize(normalWS + float3(-bendDir.x, 0.0, -bendDir.y) * _BendNormalWeight);

                Light mainLight = GetMainLight();
                half halfLambert = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half specular = pow(saturate(dot(normalWS, halfDir)), _SpecPower) * _SpecStrength;
                half3 lighting = mainLight.color * (halfLambert + specular) + SampleSH(normalWS);

                half3 albedo = lerp(_GrassDryColor.rgb, _GrassHealthyColor.rgb, state.g);
                albedo *= lerp(1.0, _GrassMoistureDarken, state.b);
                albedo *= lerp(_RootDarken, 1.0, heightFrac);

                // Direct albedo stripe term (F0.7 contrast polish): grass bent
                // away from the viewer reads lighter, toward reads darker —
                // controllable independently of the specular response.
                float2 viewXz = viewDirWS.xz / max(length(viewDirWS.xz), 1e-4);
                albedo *= 1.0 + _StripeAlbedoGain * dot(bendDir, viewXz);

                output.color = albedo * lighting;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = input.color;
                if (_LodTintOn > 0.5)
                {
                    color = lerp(color, _LodTintColor.rgb, 0.6);
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
