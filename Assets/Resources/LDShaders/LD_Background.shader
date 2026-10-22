// Fundo do tema espacial, baseado em duas referências CSS:
// - "Pure CSS Parallax Pixel Stars": degradê radial vindo de baixo (#1B2735 -> #090A0F) e três camadas
//   de estrelas quadradas (1, 2 e 3 px) em parallax;
// - "Only CSS Shooting Star": estrelas cadentes azuis na diagonal, com cauda que cresce e encolhe
//   e um brilho em cruz na ponta.
// Tudo calculado no shader (sem textura). O quad fica preso à câmera; as medidas são em "px"
// da tela de referência 1080x1920, para ter o mesmo tamanho em qualquer celular.
Shader "LaneDash/Background"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Tamanho do quad (unidades)", Vector) = (12, 22, 0, 0)
    }
    SubShader
    {
        // Fila Transparent para respeitar o sortingOrder (fica por cima do fundo padrão do Shell)
        Tags { "Queue"="Transparent" "RenderType"="Opaque" "PreviewType"="Plane" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "LDCommon.cginc"

            float4 _Size;

            static const float PxPerUnit = 96.0;   // 1920 px de referência / 20 unidades de altura
            static const float3 Top = float3(0.106, 0.153, 0.208);    // #1B2735
            static const float3 Bottom = float3(0.035, 0.039, 0.059); // #090A0F
            static const float3 Blue = float3(0.373, 0.569, 1.0);     // rgba(95, 145, 255)

            // Uma estrela quadrada por célula, em posição aleatória (mesma densidade do CSS:
            // 700 / 200 / 100 estrelas a cada 2000x2000 px, com "px" = 2 px de referência)
            float PixelStars(float2 px, float cell, float size, float scroll, float seed)
            {
                px.y += scroll;
                float2 c = floor(px / cell);
                float2 pos = hash22(c + seed) * (cell - size * 2.0) + size;
                float2 d = abs(px - c * cell - pos);
                float on = step(max(d.x, d.y), size * 0.5);
                float tw = 0.75 + 0.25 * sin(_Time.y * (1.0 + hash21(c + seed + 3.1) * 3.0) + c.x);
                return on * tw;
            }

            // "ease-in-out" do CSS, aproximado
            float Ease(float t) { return t * t * (3.0 - 2.0 * t); }

            // Traço de 2 px centrado na ponta, mais forte no meio (transparente-azul-transparente)
            float Sparkle(float2 d, float2 axis, float len)
            {
                float along = dot(d, axis);
                float perp = abs(dot(d, float2(-axis.y, axis.x)));
                return saturate(1.0 - abs(along) / max(len * 0.5, 1e-3)) * smoothstep(1.5, 0.5, perp);
            }

            float3 ShootingStars(float2 px, float2 screen)
            {
                const float Period = 3.0;      // $shooting-time
                const float Travel = 450.0;    // translateX(300px), maior na tela do celular
                const float TailMax = 150.0;   // largura máxima da cauda (100px)
                const float ShineMax = 45.0;   // largura máxima do brilho em cruz (30px)
                float2 dir = normalize(float2(1.0, -1.0)); // diagonal para baixo e à direita (rotateZ 45°)
                float2 nrm = float2(-dir.y, dir.x);
                float3 col = 0;

                for (int i = 0; i < 6; i++)
                {
                    float fi = i;
                    float t = (_Time.y + hash21(float2(fi, 9.7)) * Period * 3.0) / Period;
                    float cycle = floor(t);
                    float ph = frac(t);
                    float2 h = hash22(float2(fi * 7.1, cycle));
                    if (h.x > 0.55) continue;   // nem todo ciclo tem estrela: fica mais natural

                    float2 start = float2(h.y, hash21(float2(cycle, fi)) * 0.6 + 0.35) * screen;
                    float2 head = start + dir * Travel * Ease(ph);
                    float tail = TailMax * (ph < 0.3 ? Ease(ph / 0.3) : 1.0 - Ease((ph - 0.3) / 0.7));
                    float shine = ShineMax * (1.0 - abs(ph * 2.0 - 1.0));

                    float2 d = px - head;
                    float along = dot(d, dir);
                    float perp = abs(dot(d, nrm));
                    // Cauda: azul na ponta até transparente atrás, com drop-shadow azul
                    float fade = 1.0 - saturate(-along / max(tail, 1e-3));
                    float inTail = step(along, 0.0) * step(-along, tail);
                    float core = smoothstep(1.5, 0.5, perp) * fade * inTail;
                    float halo = exp(-perp / 4.0) * 0.45 * fade * inTail;
                    // Brilho em cruz (::before / ::after girados ±45° em relação à cauda)
                    float cross = Sparkle(d, normalize(dir + nrm), shine) + Sparkle(d, normalize(dir - nrm), shine);
                    col += Blue * (core + halo + cross * 0.9) + cross * 0.25;
                }
                return col;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screen = _Size.xy * PxPerUnit;
                float2 px = i.uv * screen;

                // radial-gradient(ellipse at bottom, #1B2735 0%, #090A0F 100%)
                float2 e = (i.uv - float2(0.5, 0.0)) / float2(0.7071, 1.4142);
                float3 col = lerp(Top, Bottom, saturate(length(e)));

                // Parallax: além da deriva lenta do CSS, as camadas correm com a velocidade do jogo
                float run = _LD_Scroll * PxPerUnit;
                col += PixelStars(px, 150.0, 2.0, _Time.y * 40.0 + run * 0.10, 1.0) * 0.8
                     + PixelStars(px, 283.0, 4.0, _Time.y * 20.0 + run * 0.06, 17.0) * 0.9
                     + PixelStars(px, 400.0, 6.0, _Time.y * 13.3 + run * 0.035, 41.0);

                col += ShootingStars(px, screen);
                return fixed4(col * 0.6, 1);   // fundo recuado: é palco, não protagonista
            }
            ENDCG
        }
    }
}
