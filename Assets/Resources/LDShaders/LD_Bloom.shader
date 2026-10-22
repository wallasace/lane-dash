// Bloom leve para mobile (dual Kawase) + compressão dos brilhos e vinheta. Usado por LDBloom.cs.
// Passes: 0 = pré-filtro com limiar (meia resolução), 1 = reduz, 2 = amplia (somando), 3 = composição final.
Shader "Hidden/LaneDash/Bloom"
{
    Properties { _MainTex ("", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    half _Threshold, _Knee, _Intensity, _Exposure, _Saturation, _Vignette;
    half4 _Tint;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    // Limiar suave: só o que é bem claro alimenta o bloom
    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold + _Knee, 0.0, 2.0 * _Knee);
        rq = rq * rq / (4.0 * _Knee + 1e-4);
        return c * max(rq, br - _Threshold) / max(br, 1e-4);
    }

    half4 fragPrefilter(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy * 0.5;
        half3 c = tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb + tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb
                + tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb + tex2D(_MainTex, i.uv + float2(d.x, d.y)).rgb;
        return half4(Prefilter(min(c * 0.25, 20.0)), 1);
    }

    half4 fragDown(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        half3 c = tex2D(_MainTex, i.uv).rgb * 4.0
                + tex2D(_MainTex, i.uv - d).rgb + tex2D(_MainTex, i.uv + d).rgb
                + tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb + tex2D(_MainTex, i.uv - float2(d.x, -d.y)).rgb;
        return half4(c / 8.0, 1);
    }

    half4 fragUp(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        half3 c = tex2D(_MainTex, i.uv + float2(-d.x * 2.0, 0)).rgb + tex2D(_MainTex, i.uv + float2(d.x * 2.0, 0)).rgb
                + tex2D(_MainTex, i.uv + float2(0, -d.y * 2.0)).rgb + tex2D(_MainTex, i.uv + float2(0, d.y * 2.0)).rgb
                + (tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb + tex2D(_MainTex, i.uv + float2(d.x, d.y)).rgb
                 + tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb + tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb) * 2.0;
        return half4(c / 12.0, 1);
    }

    // Compressão suave só acima de 0.8: mantém as cores do jogo e segura os brilhos fortes
    half3 SoftClip(half3 x)
    {
        half3 over = max(x - 0.8, 0.0);
        return min(x, 0.8) + over / (1.0 + over * 2.5);
    }

    half4 fragComposite(v2f i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        half3 b = tex2D(_BloomTex, i.uv).rgb;
        c += b * _Intensity * _Tint.rgb;
        c = SoftClip(c * _Exposure);
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(l.xxx, c, _Saturation);
        float2 v = (i.uv - 0.5) * float2(1.0, 0.8);
        c *= 1.0 - dot(v, v) * _Vignette;
        return half4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG }
    }
}
