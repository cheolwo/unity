Shader "Ssalddel/PrivateNeighborhoodRealism"
{
    Properties
    {
        _Color ("기존 표현 색", Color) = (1,1,1,1)
        _BaseColor ("호환 색", Color) = (1,1,1,1)
        _VertexColorWeight ("기존 Vertex 색 사용", Range(0,1)) = 0
        _WindowColor ("기존 창 색", Color) = (.08,.19,.25,1)
        _GlassWeight ("관측 창 표현", Range(0,1)) = 0
        _ConcreteMap ("절차적 콘크리트 시안", 2D) = "white" {}
        _AsphaltMap ("절차적 도로 시안", 2D) = "white" {}
        _RoofMap ("절차적 지붕 시안", 2D) = "white" {}
        _ConcreteNormal ("절차적 콘크리트 normal", 2D) = "bump" {}
        _AsphaltNormal ("절차적 도로 normal", 2D) = "bump" {}
        _RoofNormal ("절차적 지붕 normal", 2D) = "bump" {}
        _NormalStrength ("비실측 미세 normal", Range(0,.5)) = .16
        _Cull ("Cull", Float) = 0
        _Cutoff ("Cutoff", Float) = .5
        _BaseMap ("ShadowCaster 호환", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "PrivateRealismForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _BaseColor;
                half4 _WindowColor;
                half _VertexColorWeight;
                half _GlassWeight;
                half _NormalStrength;
            CBUFFER_END
            TEXTURE2D(_ConcreteMap); SAMPLER(sampler_ConcreteMap);
            TEXTURE2D(_AsphaltMap); SAMPLER(sampler_AsphaltMap);
            TEXTURE2D(_RoofMap); SAMPLER(sampler_RoofMap);
            TEXTURE2D(_ConcreteNormal); SAMPLER(sampler_ConcreteNormal);
            TEXTURE2D(_AsphaltNormal); SAMPLER(sampler_AsphaltNormal);
            TEXTURE2D(_RoofNormal); SAMPLER(sampler_RoofNormal);

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
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
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
                half4 vertexTint = lerp(half4(1,1,1,1), input.color, saturate(_VertexColorWeight));
                half3 baseColor = _Color.rgb * vertexTint.rgb;

                // 원래 모형명암.shader의 창 분류·배치식을 그대로 보존한다.
                // 기존 가상 격자이며 실제 층수나 창 개수의 관측값이 아니다.
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
                window = max(window, _GlassWeight);
                baseColor = lerp(baseColor, _WindowColor.rgb, window * .78h);

                float2 worldUv = abs(normal.y) > .7 ? input.positionWS.xz
                    : abs(normal.x) > abs(normal.z) ? input.positionWS.zy : input.positionWS.xy;
                half horizontalSurface = step(.7, abs(normal.y));
                half roof = horizontalSurface * (1 - saturate(abs(input.surface.x - 2) * 8));
                half road = horizontalSurface * (1 - roof) * step(dot(baseColor, half3(.2126,.7152,.0722)), .34);
                half3 textureTint = SAMPLE_TEXTURE2D(_ConcreteMap, sampler_ConcreteMap, worldUv / 1.7).rgb;
                textureTint = lerp(textureTint, SAMPLE_TEXTURE2D(_AsphaltMap, sampler_AsphaltMap, worldUv / 1.1).rgb, road);
                textureTint = lerp(textureTint, SAMPLE_TEXTURE2D(_RoofMap, sampler_RoofMap, worldUv / 1.4).rgb, roof);
                baseColor *= lerp(half3(1,1,1), textureTint * 2, 1 - window * .95h);

                // normal은 메모리상 RGB 코드 생성 자료다. 원본 사진·실제 표면 스캔을 사용하지 않는다.
                half3 bump = SAMPLE_TEXTURE2D(_ConcreteNormal, sampler_ConcreteNormal, worldUv / 1.7).rgb * 2 - 1;
                bump = lerp(bump, SAMPLE_TEXTURE2D(_AsphaltNormal, sampler_AsphaltNormal, worldUv / 1.1).rgb * 2 - 1, road);
                bump = lerp(bump, SAMPLE_TEXTURE2D(_RoofNormal, sampler_RoofNormal, worldUv / 1.4).rgb * 2 - 1, roof);
                half3 tangent = abs(normal.y) > .7 ? half3(1,0,0)
                    : abs(normal.x) > abs(normal.z) ? half3(0,0,1) : half3(1,0,0);
                half3 bitangent = normalize(cross(normal, tangent));
                normal = normalize(normal + (tangent * bump.x + bitangent * bump.y)
                    * _NormalStrength * (1 - window * .94h));

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = viewDirection;
                lighting.shadowCoord = input.shadowCoord;
                lighting.vertexLighting = VertexLighting(input.positionWS, normal);
                lighting.bakedGI = SampleSH(normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = baseColor;
                surface.metallic = window * .08h;
                surface.specular = half3(.04,.04,.04);
                surface.smoothness = lerp(lerp(.19h,.12h,road), .84h, window);
                surface.normalTS = half3(0,0,1);
                surface.occlusion = lerp(.86h,1,saturate(input.positionWS.y / 3));
                surface.alpha = 1;
                half4 result = UniversalFragmentPBR(lighting, surface);
                result.a = _Color.a * vertexTint.a;
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack Off
}
