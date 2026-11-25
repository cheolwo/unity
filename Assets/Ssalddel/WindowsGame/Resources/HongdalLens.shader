// 현실감 시험의 렌즈(HongdalLens.cs, 2026-11-24). 0 문턱 · 내려 받기, 1 내려 받기(4탭), 2 올리며 더하기(가산), 3 합치기(흐림 · 번짐 · 색 보정).
// _Lens = (번짐 세기, 위아래 흐림 세기, 또렷한 띠 가운데, 색 보정 세기).
Shader "Hongdal/Lens"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _Blur1;
    sampler2D _Blur2;
    float _Threshold;
    float4 _Lens;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box4(float2 uv, float spread)
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(-1.0, -1.0, 1.0, 1.0) * spread;
        return (tex2D(_MainTex, uv + d.xy).rgb + tex2D(_MainTex, uv + d.zy).rgb + tex2D(_MainTex, uv + d.xw).rgb + tex2D(_MainTex, uv + d.zw).rgb) * 0.25;
    }

    half4 FragPrefilter(v2f i) : SV_Target
    {
        half3 c = Box4(i.uv, 1.0);
        half bright = max(c.r, max(c.g, c.b));
        half knee = 0.12;
        half soft = clamp(bright - _Threshold + knee, 0.0, 2.0 * knee);
        soft = soft * soft / (4.0 * knee + 1e-4);
        half keep = max(soft, bright - _Threshold) / max(bright, 1e-4);
        return half4(c * keep, 1.0);
    }

    half4 FragDown(v2f i) : SV_Target
    {
        return half4(Box4(i.uv, 1.0), 1.0);
    }

    half4 FragUp(v2f i) : SV_Target
    {
        return half4(Box4(i.uv, 0.5), 1.0);
    }

    half4 FragFinal(v2f i) : SV_Target
    {
        float2 other = i.uv;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0.0) other.y = 1.0 - other.y;
        #endif
        half3 c = tex2D(_MainTex, i.uv).rgb;
        // 위아래 흐림: 가운데 띠에서 멀수록 1/2 → 1/4 그림으로
        float band = abs(other.y - _Lens.z);
        float blur = smoothstep(0.16, 0.50, band) * _Lens.y;
        half3 near = tex2D(_Blur1, other).rgb;
        half3 far = tex2D(_Blur2, other).rgb;
        c = lerp(c, lerp(near, far, saturate(blur * 1.7 - 0.5)), saturate(blur * 1.8));
        // 번짐
        c += tex2D(_BloomTex, other).rgb * _Lens.x;
        // 색 보정
        half3 g = saturate(c);
        half luma = dot(g, half3(0.299, 0.587, 0.114));
        g = saturate(lerp(luma.xxx, g, 1.10));
        g = lerp(g, g * g * (3.0 - 2.0 * g), 0.32);
        float2 v = other - 0.5;
        g *= 1.0 - 0.30 * dot(v, v);
        c = lerp(c, g, _Lens.w);
        return half4(c, 1.0);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragDown
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragUp
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragFinal
            ENDCG
        }
    }
}
