// Procedural note shapes on a quad (uv 0..1). One material per shape/colour; no textures needed.
// _Shape: 0 ring (tap), 1 star (slide head), 2 hex ring (hold head), 3 bar (hold body), 4 touch (4 arrows + dot), 5 chevron, 6 judge ring
Shader "Maimai/NoteShape"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Shape ("Shape", Float) = 0
        _Thick ("Ring thickness", Range(0.05, 1)) = 0.28
        _Accent ("Ring progress colour", Color) = (1,0.35,0.21,1)
        _Progress ("Ring progress", Range(0, 1)) = 0
        _Spread ("Touch spread (per renderer)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _Accent;
            float _Shape, _Thick, _Progress, _Spread;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }

            float band(float d, float inner, float outer, float aa)
            {
                return smoothstep(inner - aa, inner, d) * (1 - smoothstep(outer, outer + aa, d));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                float aa = fwidth(r) * 1.5;
                float a = 0;
                fixed3 col = _Color.rgb;
                int s = (int)round(_Shape);
                if (s == 0)            // ring with a soft glow core
                    a = band(r, 1 - _Thick, 0.96, aa) + 0.12 * (1 - smoothstep(0.0, 1 - _Thick, r));
                else if (s == 1)       // 5-point star
                {
                    float th = atan2(p.x, p.y);
                    float rs = 0.42 + 0.54 * pow(0.5 + 0.5 * cos(5 * th), 2.2);
                    a = 1 - smoothstep(rs - aa, rs, r);
                }
                else if (s == 2)       // hold: hexagon ring pointed along the lane (+y), sides stretch with the quad's y scale
                {
                    float L = length(unity_ObjectToWorld._m01_m11_m21) / length(unity_ObjectToWorld._m00_m10_m20);
                    float2 q = float2(abs(p.x), max(abs(p.y * L) - (L - 1), 0));
                    float d = max(q.y * 0.866 + q.x * 0.5, q.x) * 1.04; // tip stays inside the quad
                    float ad = fwidth(d) * 1.5;
                    a = band(d, 0.9 - _Thick, 0.9, ad);
                }
                else if (s == 3)       // bar: full height, soft sides
                {
                    float ax = fwidth(p.x) * 1.5;
                    a = (1 - smoothstep(0.55, 0.55 + ax, abs(p.x))) * 0.85;
                }
                else if (s == 4)       // touch: 4 wedges slide in from _Spread 1 and lock into a diamond around the dot at 0 (hit time)
                {
                    float off = _Spread * 0.5;
                    float v1 = abs(p.y) - off, v2 = abs(p.x) - off;
                    float t1 = min(v1 - abs(p.x) - 0.05, 0.44 - v1);   // > 0 inside a wedge (tip toward the centre)
                    float t2 = min(v2 - abs(p.y) - 0.05, 0.44 - v2);
                    float tri = max(t1, t2), ad = fwidth(tri) * 1.5;
                    float fill = smoothstep(0, ad, tri);
                    col = lerp(fixed3(1, 1, 1), _Color.rgb, smoothstep(0.035, 0.035 + ad, tri)); // white rim
                    float dot = 1 - smoothstep(0.11 - aa, 0.11, r);
                    col = dot > fill ? fixed3(1, 1, 1) : col;
                    a = max(fill * 0.95, dot) * saturate((1 - _Spread) * 4); // fade in over the first quarter
                }
                else if (s == 6)       // dotted judge ring (128 dots, bigger at the 8 lanes), lit _Accent up to _Progress
                {
                    float u = frac(atan2(p.x, p.y) / 6.2831853 + 1);
                    float cell = u * 128, idx = floor(cell);
                    float sp = 6.2831853 * 0.975 / 128;
                    float2 q = float2((frac(cell) - 0.5) * sp, r - 0.975);
                    float dr = fmod(idx, 16) == 8 ? 0.0105 : 0.0062;
                    float dd = length(q);
                    a = 1 - smoothstep(dr - aa * 0.5, dr + aa * 0.5, dd);
                    bool lit = (idx + 0.5) / 128 <= _Progress;
                    col = lit ? _Accent.rgb : col;
                    a *= lit ? 1 : 0.55;
                    a += 0.015 * (1 - smoothstep(0.96 - aa, 0.96, r)); // faint inner fill
                }
                else                   // slide chevron pointing +y
                {
                    float d = abs(p.y - 0.35 + abs(p.x) * 0.9);
                    float ad = fwidth(d) * 1.5;
                    a = (1 - smoothstep(0.22, 0.22 + ad, d)) * step(abs(p.x), 0.85);
                }
                return fixed4(col, _Color.a * saturate(a));
            }
            ENDCG
        }
    }
}
