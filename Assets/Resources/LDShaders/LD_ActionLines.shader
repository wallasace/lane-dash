// Linhas de ação (estilo anime/mangá) por cima da tela: traços finos vindo das bordas em direção
// ao centro, piscando a cada poucos quadros. Aparecem e ficam mais densas, longas e rápidas
// conforme a velocidade do jogo (_LD_Speed, 0..1). Aditivo e barato: um quad preso à câmera.
Shader "LaneDash/ActionLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Tamanho do quad (unidades)", Vector) = (12, 22, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off ZWrite Off ZTest Always
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "LDCommon.cginc"

            float4 _Size;
            static const float TAU = 6.2831853;

            fixed4 frag(v2f i) : SV_Target
            {
                float speed = smoothstep(0.12, 1.0, _LD_Speed);
                if (speed <= 0.0) return 0;

                // Centro do efeito um pouco abaixo do meio (perto da nave), em unidades do mundo
                float2 p = (i.uv - float2(0.5, 0.42)) * _Size.xy;
                float r = length(p);
                float a = atan2(p.y, p.x) / TAU + 0.5;

                // Cada fatia angular pode ter uma linha; o sorteio muda a cada ~1/14 s (efeito "piscando")
                const float Bins = 140.0;
                float bin = floor(a * Bins);
                float frame = floor(_Time.y * lerp(10.0, 18.0, speed));
                float h = hash21(float2(bin, frame));
                float h2 = hash21(float2(bin + 31.0, frame));
                float density = lerp(0.12, 0.45, speed);
                if (h > density) return 0;

                // Linha fina: mais grossa na borda, afinando em direção ao centro
                float across = abs(frac(a * Bins) - 0.5) * 2.0;
                float width = lerp(0.18, 0.55, saturate((r - 4.0) / 8.0)) * (0.6 + h2 * 0.8);
                float stroke = smoothstep(width, width * 0.3, across);

                // Começa longe do centro (não cobre a nave) e avança mais com a velocidade
                float inner = lerp(9.0, 5.5, speed) + h2 * 2.5;
                float reach = smoothstep(inner, inner + 1.5, r);

                float3 col = lerp(float3(0.7, 0.9, 1.0), float3(1.0, 1.0, 1.0), h2);
                return fixed4(col * stroke * reach * speed * 0.55, 0);
            }
            ENDCG
        }
    }
}
