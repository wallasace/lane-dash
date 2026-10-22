// Pista (fiel ao concept), PBR estilizado, com perspectiva falsa (afunila para o alto; ver LDPersp):
// - Piso: placas metálicas escuras de cantos chanfrados, muito desgaste e arranhões, partes
//   molhadas refletindo o neon, grelhas perto das bordas, linhas azuis transversais e
//   faixas ciano longas com espaços curtos.
// - Paredes: faixa de neon magenta grossa junto ao piso, placas de blindagem anguladas com
//   chanfros iluminados de magenta, luzes laranja, segunda faixa de neon por fora e muito
//   brilho vazando para o piso e para o espaço.
// Tudo rola com _LD_Scroll. Normais analíticas (sem amostras extras), barato para tela cheia.
Shader "LaneDash/Track"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "LDCommon.cginc"
            #include "LDPBR.cginc"

            static const float FloorHalf = 3.25;  // meia largura do piso (coordenada da pista)
            static const float WallW = 1.5;       // largura de cada parede
            static const float LaneSep = 1.1;     // x das faixas tracejadas
            static const float3 Cyan = float3(0.2, 0.9, 1.0);
            static const float3 Magenta = float3(1.0, 0.16, 0.78);
            static const float3 Pink = float3(1.0, 0.75, 0.95);
            static const float3 Amber = float3(1.0, 0.45, 0.1);

            // Distância até a borda de uma célula retangular e direção do chanfro (normal)
            float2 Cell(float2 uv, float2 size, out float2 slope, out float2 id)
            {
                float2 g = uv / size;
                id = floor(g);
                float2 f = frac(g);
                slope = float2(f.x < 0.5 ? -1.0 : 1.0, f.y < 0.5 ? -1.0 : 1.0);
                return min(f, 1.0 - f) * size;
            }

            // Faixa de neon: núcleo quase branco, corpo magenta e brilho largo
            float3 NeonBand(float dist, float width, float pulse)
            {
                return Pink * smoothstep(width * 0.45, width * 0.15, dist) * 0.6
                     + Magenta * smoothstep(width, width * 0.4, dist) * (1.6 + pulse)
                     + Magenta * exp(-dist / width * 1.6) * 0.7;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float y = i.wpos.y;
                float persp = LDPersp(y);
                float x = i.wpos.x / persp;     // x na pista (corrigido pela perspectiva)
                float ys = y + _LD_Scroll;
                float ax = abs(x);
                float sx = x < 0.0 ? -1.0 : 1.0;
                float t = _Time.y;
                float3 rgb = 0;
                float alpha = 0;

                float fade = lerp(0.3, 1.0, smoothstep(11.0, 2.0, y - _LD_CamY));
                float pp = (frac(ys * 0.06 + t * 0.12) - 0.5) * 6.0;
                float pulse = exp(-pp * pp) * 1.4;   // pulsos de energia no neon

                if (ax < FloorHalf)
                {
                    // ---------- Piso ----------
                    float2 slope, id;
                    float2 dist = Cell(float2(x + FloorHalf, ys), float2(1.083, 2.4), slope, id);
                    // Cantos chanfrados (placas "octogonais")
                    float corner = (dist.x + dist.y - 0.22) * 0.707;
                    float e = min(min(dist.x, dist.y), corner);
                    float seam = smoothstep(0.03, 0.01, e);
                    float bevel = smoothstep(0.09, 0.025, e) * (1.0 - seam);
                    float2 tilt = corner < min(dist.x, dist.y) ? slope * 0.707 : (dist.x < dist.y ? float2(slope.x, 0) : float2(0, slope.y));
                    float3 N = normalize(float3(tilt * bevel * 1.2, 1.0));

                    float ph = hash21(id + 3.7);
                    float grime = fbm3(float2(x * 1.6, ys * 0.7) + ph * 5.0);
                    float scratch = smoothstep(0.025, 0.0, abs(vnoise(float2(x * 9.0 + ys * 0.8, ys * 0.35) + ph * 9.0) - 0.5))
                                  * step(0.55, vnoise(float2(x * 2.0, ys * 0.5) + 4.0));
                    float3 albedo = float3(0.045, 0.05, 0.065) * (0.85 + ph * 0.3) * lerp(1.0, 0.7, grime * grime);
                    albedo += scratch * 0.02;
                    albedo *= lerp(0.75, 1.0, smoothstep(0.3, 0.6, ax));   // pista do meio um pouco mais escura

                    float wet = smoothstep(0.42, 0.62, vnoise(float2(x * 0.7, ys * 0.15) + 11.0));
                    float rough = lerp(0.6, 0.1, wet);
                    float ao = 1.0 - seam * 0.85;

                    // Grelhas de ventilação perto das bordas
                    float ventZone = step(2.45, ax) * step(ax, 2.95) * step(0.15, frac(ys / 7.0)) * step(frac(ys / 7.0), 0.5);
                    ao *= 1.0 - ventZone * step(0.45, frac(ys * 9.0)) * 0.7;

                    float3 col = Shade(N, albedo, 0.75, rough, ao, 0);

                    // Reflexos do neon (fortes perto das paredes, alongados no sentido da pista)
                    float shine = lerp(0.3, 1.0, wet);
                    float streak = 0.55 + 0.45 * vnoise(float2(x * 3.0, ys * 0.3));
                    col += Magenta * exp(-(FloorHalf - ax) * 1.8) * 0.3 * shine * streak;
                    col += Magenta * exp(-(FloorHalf - ax) * 6.0) * 0.3;
                    float laneD = abs(ax - LaneSep);
                    col += Cyan * exp(-laneD * 4.0) * 0.12 * shine * streak;
                    float2 rp = (float2(x, y) - _LD_Player.xy - float2(0, -1.0)) * float2(1.4, 0.45);
                    col += Cyan * exp(-dot(rp, rp) * 1.5) * 0.5 * shine;

                    // Faixas: traços longos com espaços curtos, núcleo branco e brilho
                    float dash = smoothstep(0.42, 0.38, abs(frac(ys * 0.45) - 0.5));
                    float lw = laneD * persp;
                    // Guias, não protagonistas: brilho contido para não competir com nave, perigos e cristais
                    col += Cyan * smoothstep(0.06, 0.03, lw) * dash * 0.4 + float3(0.85, 1, 1) * smoothstep(0.025, 0.0, lw) * dash * 0.12;
                    col += Cyan * exp(-lw * 12.0) * 0.25 * (0.4 + 0.6 * dash);

                    // Linhas azuis transversais e emendas de placa com filete azul fraco
                    float hl = abs(frac(ys / 7.0 + 0.5) - 0.5) * 7.0;
                    col += float3(0.15, 0.35, 1.0) * exp(-hl * 25.0) * 0.25;
                    col += float3(0.1, 0.3, 0.9) * seam * 0.04;

                    // Setas discretas na pista do meio
                    float chev = abs(frac(ys * 0.35 - ax * 0.35) - 0.5);
                    col += Cyan * smoothstep(0.04, 0.0, abs(chev - 0.25)) * step(ax, 0.45) * 0.04 * step(frac(ys / 12.0), 0.3);

                    // Luzinhas laranja junto à parede
                    float2 lp = float2(ax - 3.05, frac(ys / 3.5 + 0.5) * 3.5 - 1.75);
                    col += Amber * exp(-dot(lp * float2(1.0, 0.3), lp * float2(1.0, 0.3)) * 700.0) * 0.9;

                    rgb = col;
                    alpha = 1.0;
                }
                else if (ax < FloorHalf + WallW)
                {
                    // ---------- Parede ----------
                    float u = (ax - FloorHalf) / WallW;   // 0 = junto ao piso, 1 = borda externa
                    float3 albedo = float3(0.08, 0.07, 0.11);
                    float rough = 0.35;
                    float3 N = float3(0, 0, 1);
                    float ao = 1.0;
                    float3 emit = 0;
                    float plate = 0.0;

                    if (u > 0.2 && u < 0.7)
                    {
                        // Placas de blindagem anguladas (emendas inclinadas), com chanfros
                        float2 slope, id;
                        float2 dist = Cell(float2(u - 0.2, ys + u * 1.2), float2(0.5, 3.4), slope, id);
                        dist.x *= WallW;
                        float corner = (dist.x + dist.y - 0.3) * 0.707;
                        float e = min(min(dist.x, dist.y), corner);
                        float seam = smoothstep(0.05, 0.02, e);
                        float bevel = smoothstep(0.16, 0.05, e) * (1.0 - seam);
                        float2 tilt = dist.x < dist.y ? float2(slope.x * sx, 0) : float2(0, slope.y);
                        N = normalize(float3(tilt * bevel * 1.3, 1.0));
                        ao = 1.0 - seam * 0.85;
                        albedo *= 0.85 + hash21(id) * 0.4;
                        plate = bevel;
                        // Grelha em parte das placas
                        float vent = step(0.55, hash21(id + 1.3)) * step(0.3, frac((ys + u * 1.2) / 3.4)) * step(frac((ys + u * 1.2) / 3.4), 0.65);
                        ao *= 1.0 - vent * step(0.5, frac(ys * 10.0)) * 0.6;
                        // Luz laranja em algumas placas
                        float2 lp = float2((u - 0.6) * WallW, frac((ys + u * 1.2) / 3.4) * 3.4 - 0.5);
                        emit += Amber * exp(-dot(lp, lp) * 500.0) * step(0.5, hash21(id + 7.0)) * 2.5;
                    }
                    else if (u >= 0.82)
                    {
                        // Aba externa segmentada
                        float seg = frac(ys / 1.7);
                        ao = 1.0 - smoothstep(0.04, 0.0, min(seg, 1.0 - seg)) * 0.8;
                        N = normalize(float3(sx * 0.5, 0, 1.0));
                    }

                    float3 col = Shade(N, albedo, 0.7, rough, ao, 0) + emit;
                    // A blindagem reflete o neon: chanfros acesos de magenta
                    col += Magenta * plate * 0.45 + Magenta * 0.05;

                    // Faixa de neon interna (grossa) e externa (mais fina)
                    col += NeonBand(abs(u - 0.11) * WallW, 0.16, pulse) * 0.45;
                    col += NeonBand(abs(u - 0.76) * WallW, 0.09, pulse * 0.7) * 0.3;

                    float outer = smoothstep(1.0, 0.94, u);
                    rgb = col * outer;
                    alpha = outer;
                }

                // Brilho do neon que vaza para o espaço (aditivo)
                float spill = exp(-max(ax - (FloorHalf + WallW), 0.0) * 2.2) * step(FloorHalf + WallW * 0.94, ax);
                rgb += Magenta * spill * 0.25;

                // Foco: o cenário escurece com a distância da nave (holofote) e recua no geral;
                // embaixo da nave, uma poça de luz ciano a separa do piso
                float2 fp = (i.wpos - _LD_Player.xy) * float2(0.16, 0.075);
                rgb *= lerp(0.4, 1.0, exp(-dot(fp, fp))) * 0.85;
                float2 pool = (i.wpos - _LD_Player.xy - float2(0, -0.3)) * float2(0.75, 0.55);
                rgb += Cyan * exp(-dot(pool, pool) * 1.2) * 0.22 * alpha;

                rgb *= fade;
                alpha *= lerp(0.6, 1.0, fade);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
