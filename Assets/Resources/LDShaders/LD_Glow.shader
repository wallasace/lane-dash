// Partícula aditiva: brilho radial com núcleo quente. Faz o papel de bloom sem pós-processamento.
// color.a controla a intensidade (usado para o fade da partícula).
Shader "LaneDash/Glow"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off ZWrite Off
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "LDCommon.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = Local(i.uv);
                float r2 = dot(p, p);
                float3 c = i.color.rgb * exp(-r2 * 6.0) + exp(-r2 * 40.0) * 0.5;
                return fixed4(c * i.color.a, 0);
            }
            ENDCG
        }
    }
}
