// Dark rounded card on a quad: soft top-lit fill + thin border. Corner radius / border are fractions of the
// quad's shorter side (read from the object scale), so one material fits every card size.
Shader "Maimai/Panel"
{
    Properties
    {
        _Color ("Fill", Color) = (0.085,0.085,0.09,0.94)
        _Border ("Border", Color) = (1,1,1,0.10)
        _Radius ("Corner radius (x short side)", Range(0, 0.5)) = 0.12
        _Width ("Border width (x short side)", Range(0, 0.1)) = 0.012
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _Border;
            float _Radius, _Width;

            struct v2f { float4 pos : SV_POSITION; float2 p : TEXCOORD0; float2 hs : TEXCOORD1; float v : TEXCOORD2; };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 size = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                o.hs = size * 0.5;
                o.p = (v.texcoord.xy - 0.5) * size;
                o.v = v.texcoord.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float m = min(i.hs.x, i.hs.y) * 2;
                float R = _Radius * m;
                float2 q = abs(i.p) - (i.hs - R);
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - R; // < 0 inside
                float aa = fwidth(d) * 1.2;
                float inside = 1 - smoothstep(-aa, 0, d);
                float rim = inside * (1 - smoothstep(-_Width * m - aa, -_Width * m, d));
                fixed3 fill = _Color.rgb * (0.85 + 0.35 * i.v);          // lit from the top like the reference cards
                fixed3 col = lerp(fill, _Border.rgb, rim * _Border.a);
                return fixed4(col, inside * _Color.a);
            }
            ENDCG
        }
    }
}
