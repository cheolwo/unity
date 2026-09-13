Shader "Ssalddel/SagajeongMiniature"
{
    Properties
    {
        _Color ("모형 색", Color) = (1,1,1,1)
        _VertexColorWeight ("Vertex 색 사용", Range(0,1)) = 0
        _WindowColor ("창 색", Color) = (.08,.19,.25,1)
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }
        Pass
        {
            Name "SagajeongForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _WindowColor;
                half _VertexColorWeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 surface : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 surface : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.color = input.color;
                output.surface = input.surface;
                output.shadowCoord = GetShadowCoord(position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                normal *= dot(normal, viewDirection) < 0 ? -1 : 1;
                Light mainLight = GetMainLight(input.shadowCoord);
                half diffuse = saturate(dot(normal, mainLight.direction));
                half3 ambient = max(SampleSH(normal), half3(.12h, .14h, .16h));
                half3 direct = mainLight.color * (.24h + diffuse * .76h)
                    * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                half4 vertexTint = lerp(half4(1,1,1,1), input.color, saturate(_VertexColorWeight));
                half3 baseColor = _Color.rgb * vertexTint.rgb;

                // surface.x == 1인 외벽만 world-space 격자를 사용한다.
                // 창 하나마다 별도 quad를 만들지 않아 확대/축소 때 renderer와 vertex 예산이 고정된다.
                half wall = 1 - saturate(abs(input.surface.x - 1) * 8);
                half vertical = 1 - saturate(abs(normal.y) * 5);
                float horizontal = abs(normal.x) > abs(normal.z) ? input.positionWS.z : input.positionWS.x;
                float floorHeight = lerp(3.2, 2.75, saturate(input.surface.y / 3));
                float columnWidth = lerp(3.0, 2.35, saturate(input.surface.y / 3));
                float row = frac((input.positionWS.y - .45) / floorHeight);
                float column = frac(horizontal / columnWidth);
                half window = wall * vertical
                    * step(.17, column) * step(column, .83)
                    * step(.20, row) * step(row, .66)
                    * step(.72, input.positionWS.y);
                baseColor = lerp(baseColor, _WindowColor.rgb, window * .78h);

                // 미세한 모형 표면 변화만 더하며 운영 날씨나 권위 상태를 읽지 않는다.
                half grain = 1 + .012h * sin(input.positionWS.x * .21) * sin(input.positionWS.z * .19);
                half contact = lerp(.84h, 1, saturate(input.positionWS.y / 3));
                half3 lit = baseColor * (ambient + direct) * grain * contact;
                return half4(lit, _Color.a * vertexTint.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack Off
}
