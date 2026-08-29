Shader "Hidden/SimGrid"
{
    Properties
    {
        _Color     ("Grid Color", Color)             = (0.26, 0.59, 0.98, 0.35)
        _CellSize  ("Cell Size", Float)              = 1.0
        _Thickness ("Line Thickness (world units)", Float) = 0.04
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _CellSize;
            float  _Thickness;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float cell = max(_CellSize, 1e-4);

                // world-space distance to the nearest grid line (lines at multiples of cellSize)
                float2 f = abs(frac(i.world / cell - 0.5) - 0.5) * cell;
                float  d = min(f.x, f.y);

                float aa       = fwidth(d);
                float lineMask = 1.0 - smoothstep(_Thickness - aa, _Thickness + aa, d);

                // fade out when a cell spans less than ~3-6 screen pixels (kills moire)
                float px   = cell / max(fwidth(i.world.x), fwidth(i.world.y));
                float fade = saturate((px - 3.0) / 3.0);

                fixed4 c = _Color;
                c.a *= lineMask * fade;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}