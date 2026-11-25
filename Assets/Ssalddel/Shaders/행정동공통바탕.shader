Shader "Ssalddel/PrivateAdministrativeDongCommonBase"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            // 기존 역세권 도형 shader와 같은 양면 표시로 원형 winding의 가시성을 보존한다.
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // 표현 전용 고정 명암이다. 실제 조명·그림자·날씨·업무 상태를 참조하지 않는다.
                half3 normal = normalize(input.normalWS);
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                normal *= dot(normal, viewDirection) < 0 ? -1 : 1;
                half main = saturate(dot(normal, normalize(half3(-0.65h,0.74h,0.15h))));
                half fill = saturate(dot(normal, normalize(half3(0.65h,0.45h,0.55h))));
                half studio = 0.45h + 0.50h * main + 0.05h * fill;
                half baseTone = lerp(0.91h,1.0h,saturate((input.positionWS.y-0.16h)/2.5h));
                return half4(input.color.rgb * studio * baseTone, input.color.a);
            }
            ENDHLSL
        }
    }
}
