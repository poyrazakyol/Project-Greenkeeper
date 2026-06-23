// Day-end paint fade (M.2 fix): subtracts a constant from RT_Paint.r so the
// painted lines fade at the same per-day rate as the CPU PaintCoverage
// authority (TDD §3.4 morning bulk update). Done as a full-screen blit
// instead of re-seeding from the coarse CPU grid, which would blur the
// high-resolution lines. Plain built-in blit shader — driven by Graphics.Blit.
Shader "Greenkeeper/PitchPaintFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _FadeAmount ("Fade Amount", Float) = 0
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _FadeAmount;

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.r = saturate(c.r - _FadeAmount);
                return c;
            }
            ENDCG
        }
    }
}
