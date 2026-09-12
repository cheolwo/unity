Shader "Ssalddel/ObservationPrimitive"
{
    Properties { _Color ("Role Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 vertex : SV_POSITION; float3 normal : TEXCOORD0; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // 표현 전용 고정 명암: 전역 조명·날씨·업무 상태에 관여하지 않는다.
                float light = saturate(dot(normalize(i.normal), normalize(float3(-0.35, 0.85, -0.4))));
                return fixed4(max(_Color.rgb, 0.045) * (0.55 + 0.45 * light), _Color.a);
            }
            ENDCG
        }
    }
}
