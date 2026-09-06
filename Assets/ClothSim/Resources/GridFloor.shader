Shader "Custom/GridFloor"
{
    Properties
    {
        _BgColor ("Background", Color) = (0.18, 0.18, 0.19, 1)
        _GridColor ("Grid", Color) = (0.32, 0.32, 0.34, 1)
        _MajorColor ("Major Grid", Color) = (0.42, 0.42, 0.45, 1)
        _Spacing ("Spacing", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            fixed4 _BgColor, _GridColor, _MajorColor;
            float _Spacing;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float gridLine(float2 p, float spacing)
            {
                float2 g = abs(frac(p / spacing) - 0.5);
                return (min(g.x, g.y) < 0.02) ? 1 : 0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.worldPos.xz;
                float minor = gridLine(p, _Spacing);
                float major = gridLine(p, _Spacing * 5);
                float fade = saturate(1.2 - length(p) * 0.018);
                fixed3 col = _BgColor.rgb;
                col = lerp(col, _GridColor.rgb, minor * fade);
                col = lerp(col, _MajorColor.rgb, major * fade);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
