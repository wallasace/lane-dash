// Anel de onda de choque (explosão e coleta de cristal), aditivo.
Shader "LaneDash/Ring"
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
                float r = length(Local(i.uv));
                float ring = exp(-abs(r - 0.82) * 22.0) * smoothstep(1.0, 0.9, r);
                return fixed4(i.color.rgb * ring * i.color.a, 0);
            }
            ENDCG
        }
    }
}
