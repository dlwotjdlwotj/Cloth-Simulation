Shader "Custom/Cloth"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _GridColor ("Grid Color", Color) = (0.72, 0.72, 0.74, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _ShowPattern ("Show Pattern", Float) = 0
        _ShowGrid ("Show Grid", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                SHADOW_COORDS(3)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _GridColor;
            half _Smoothness;
            float _ShowPattern;
            float _ShowGrid;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                n *= facing >= 0 ? 1 : -1;

                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 h = normalize(l + v);

                float ndotl = saturate(dot(n, l));
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                float3 diffuse = _LightColor0.rgb * ndotl * atten;
                float specPow = exp2(8.0 * _Smoothness + 1.0);
                float spec = pow(saturate(dot(n, h)), specPow) * _Smoothness * atten;

                float3 col = albedo * (ambient + diffuse) + _LightColor0.rgb * spec;

                if (_ShowGrid > 0.5)
                {
                    float gx = abs(frac(i.uv.x * 24) - 0.5);
                    float gy = abs(frac(i.uv.y * 24) - 0.5);
                    float gridAmt = (gx < 0.04 || gy < 0.04) ? 0.35 : 0;
                    col = lerp(col, _GridColor.rgb, gridAmt);
                }

                return fixed4(col, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
