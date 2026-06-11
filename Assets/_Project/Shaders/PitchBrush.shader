// Brush stamp shader (TDD §3.2): draws one quad-stamp into a pitch RT via
// CommandBuffer, camera-independent. The vertex stage places the unit quad in
// the target's NDC from UV-space parameters; the fragment stage produces a
// radial falloff disc. One generic alpha-blended mode for F0.3; channel-masked
// variants arrive with the tools that need them (F0.5 mowing, M.2 paint).
Shader "Greenkeeper/PitchBrush"
{
    Properties
    {
        _CenterUv ("Center UV", Vector) = (0.5, 0.5, 0, 0)
        _RadiusUv ("Radius UV", Vector) = (0.05, 0.05, 0, 0)
        _Rotation ("Rotation (rad)", Float) = 0
        _Value ("Value", Color) = (1, 1, 1, 1)
        _Hardness ("Hardness", Range(0, 1)) = 0.5
        _FlipY ("Flip Y", Float) = 0
        // Matches UnityEngine.Rendering.ColorWriteMask (R=8 G=4 B=2 A=1, All=15).
        // ShaderLab state reads this from the MATERIAL, not from property
        // blocks — the render bridge keeps one material per (mask, blend op).
        _ColorMask ("Color Mask", Float) = 15
        // UnityEngine.Rendering.BlendOp (Add=0, Min=3). Min = lower-only stamp.
        _BlendOp ("Blend Op", Float) = 0
        // Set to 1 together with BlendOp Min: outside the falloff the fragment
        // emits 1 (Min-neutral) instead of relying on alpha, because Min/Max
        // blend ops ignore blend factors.
        _MinNeutral ("Min Neutral", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Name "PitchBrushStamp"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            BlendOp [_BlendOp]
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 _CenterUv;
            float4 _RadiusUv;
            float _Rotation;
            float4 _Value;
            float _Hardness;
            float _FlipY;
            float _MinNeutral;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0; // -1..1 across the stamp quad
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float s = sin(_Rotation);
                float c = cos(_Rotation);
                float2 p = input.positionOS.xy; // -0.5..0.5 unit quad
                float2 rotated = float2(p.x * c - p.y * s, p.x * s + p.y * c);
                float2 uv = _CenterUv.xy + rotated * _RadiusUv.xy * 2.0;

                float2 ndc = uv * 2.0 - 1.0;
                if (_FlipY > 0.5)
                {
                    ndc.y = -ndc.y;
                }
                output.positionCS = float4(ndc, 0.0, 1.0);
                output.local = input.uv * 2.0 - 1.0;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float dist = length(input.local);
                // hardness 1 -> hard-edged disc, hardness 0 -> soft falloff from center
                float edge = saturate((1.0 - dist) / max(1.0 - _Hardness, 0.0001));
                if (_MinNeutral > 0.5)
                {
                    // Min blend ignores factors: fade toward the neutral 1
                    // ourselves so the quad outside the disc is a no-op.
                    return float4(lerp(float3(1.0, 1.0, 1.0), _Value.rgb, edge * _Value.a), 1.0);
                }
                return float4(_Value.rgb, edge * _Value.a);
            }
            ENDHLSL
        }
    }
}
