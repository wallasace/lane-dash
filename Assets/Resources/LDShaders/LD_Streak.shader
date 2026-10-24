// Faísca esticada (aditiva, HDR para o bloom): núcleo branco fino e corpo colorido que some nas pontas.
// O quad é girado na direção da velocidade e esticado no eixo y pelo sistema de partículas.
// color.a = intensidade (fade da partícula).
Shader "LaneDash/Streak"
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
                // mais grossa na frente (y > 0), afinando para trás
                float w = lerp(0.25, 1.0, saturate(p.y * 0.5 + 0.5));
                float across = exp(-p.x * p.x / (0.12 * w * w));
                float along = smoothstep(1.0, 0.6, abs(p.y)) * saturate(p.y * 0.5 + 0.7);
                float core = exp(-p.x * p.x / (0.015 * w * w)) * along;
                float3 c = i.color.rgb * across * along * 2.0 + core * 1.5;
                return fixed4(c * i.color.a, 0);
            }
            ENDCG
        }
    }
}
