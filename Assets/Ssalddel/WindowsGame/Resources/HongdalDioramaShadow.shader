// 2.5D 지도 시제품의 지면 그림자용. 스텐실로 겹친 그림자가 두 번 어두워지지 않게 한다.
// _Window: 역 중심 보기의 둥근 창(HongdalDiorama와 같다). 창 밖의 그림자는 옅어지다 사라진다. 반경이 0이면 창이 없다.
Shader "Hongdal/DioramaShadow"
{
    Properties
    {
        _Color ("Shadow", Color) = (0, 0, 0, 0.3)
        _Window ("Window", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Window;
            float4 _HongdalTier;      // 역 세 겹(HongdalDiorama와 같다). 낮아진 바깥 건물의 그림자는 옅게

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 ground : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.ground = vertex.xz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float outside = _Window.z > 0.0 ? saturate((distance(i.ground, _Window.xy) - _Window.z) / max(1.0, _Window.w)) : 0.0;
                fixed4 c = _Color;
                c.a *= 1.0 - outside;
                if (_Window.z > 0.0 && _HongdalTier.z > 0.0)
                    c.a *= 1.0 - 0.7 * _HongdalTier.z * smoothstep(_HongdalTier.y * 0.94, _HongdalTier.y * 1.12, distance(i.ground, _Window.xy));
                return c;
            }
            ENDCG
        }
    }
}
