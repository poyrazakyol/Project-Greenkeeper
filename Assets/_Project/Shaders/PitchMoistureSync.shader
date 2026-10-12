// Visual moisture sync (M.3 fix): copies the CPU moisture grid (uploaded as a
// 212x140 linear R8 texture holding raw 0-100 bytes) into RT_GrassState.B.
// ColorMask B leaves the mower's fine height (R), health (G) and damage (A)
// untouched. Bilinear sampling of the coarse grid gives soft cell edges.
// Plain built-in blit shader — driven by Graphics.Blit.
Shader "Greenkeeper/PitchMoistureSync"
{
    Properties
    {
        _MainTex ("Moisture (raw 0-100 bytes in R)", 2D) = "black" {}
        _ByteScale ("Byte Scale", Float) = 2.55
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        ColorMask B
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _ByteScale; // 255/100: sampled r = m/255, target b = m/100

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float moisture01 = saturate(tex2D(_MainTex, i.uv).r * _ByteScale);
                return float4(0.0, 0.0, moisture01, 0.0);
            }
            ENDCG
        }
    }
}
