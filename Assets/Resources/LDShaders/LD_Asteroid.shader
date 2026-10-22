// Asteroide (fiel ao concept): aglomerado de blocos de rocha preta, cada um com volume próprio,
// e lava incandescente brilhando nas fendas fundas entre eles (amarelo no fundo, laranja e
// vermelho nas bordas). A lava ilumina as faces dos blocos voltadas para as fendas.
// Em volta: aura de fogo, detritos de rocha orbitando e brasas.
// PBR estilizado (LDPBR.cginc): rocha dielétrica pouco brilhante, rim laranja forte.
// A cor do vértice é a semente: r,g = forma; b = calor extra da lava.
// O quad (2.8 unidades) é maior que a rocha para caber a aura e os detritos; p está em unidades do mundo.
Shader "LaneDash/Asteroid"
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

            static const float HalfQuad = 1.4;
            static const int Lumps = 12;
            static const float3 LavaRed = float3(0.85, 0.1, 0.02);
            static const float3 LavaOrange = float3(1.0, 0.4, 0.04);
            static const float3 LavaYellow = float3(1.0, 0.85, 0.4);

            float smax(float a, float b, float k) { return -smin(-a, -b, k); }

            // Blocos: esferas de tamanhos variados agrupadas.
            // x = altura do bloco mais alto, y = distância da silhueta, z = altura do segundo bloco
            // (onde x e z quase empatam, dois blocos se encontram: é a fenda com lava)
            float3 Rock(float2 p, float seed)
            {
                float h1 = 0.0, h2 = 0.0, dist = 10.0;
                for (int k = 0; k < Lumps; k++)
                {
                    float fk = k;
                    float2 c = k == 0 ? float2(0, 0) : (hash22(float2(fk, seed)) - 0.5) * float2(0.85, 0.8);
                    float r = (k == 0 ? 0.32 : 0.16 + 0.15 * hash21(float2(seed, fk * 3.1)));
                    float2 q = (p - c) / r;
                    // topo achatado e borda íngreme, deformado por ruído: bloco de rocha facetado
                    float dd = dot(q, q) + (vnoise(q * 1.7 + fk * 4.0 + seed) - 0.5) * 0.4 + (vnoise(q * 4.5 + fk * 7.0) - 0.5) * 0.22;
                    float lump = pow(saturate(1.0 - dd), 0.28) * r * (0.8 + 0.4 * hash21(float2(fk, seed + 2.0)));
                    if (lump > h1) { h2 = h1; h1 = lump; } else h2 = max(h2, lump);
                    dist = smin(dist, length(p - c) - r, 0.04);
                }
                dist += (vnoise(p * 9.0 + seed) - 0.5) * 0.05;   // contorno áspero
                return float3(h1 + fbm3(p * 10.0 + seed) * 0.02 * step(0.001, h1), dist, h2);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 l = Local(i.uv);
                float2 p = l * HalfQuad;
                float seed = i.color.r * 37.0 + i.color.g * 11.0;
                float heat = i.color.b;
                float t = _Time.y;
                float2 win = smoothstep(1.0, 0.8, abs(l));

                float3 rk = Rock(p, seed);
                float d = rk.y;
                float aa = fwidth(d) * 1.5;
                float fill = smoothstep(aa, -aa, d);
                float3 rgb = 0;
                float alpha = 0;

                if (d < 0.05)
                {
                    const float e = 0.008;
                    float h = rk.x;
                    float hx = Rock(p + float2(e, 0), seed).x;
                    float hy = Rock(p + float2(0, e), seed).x;
                    float3 N = normalize(float3((h - hx) / e, (h - hy) / e, 1.0));

                    // Fendas: dentro da silhueta mas com pouca altura (entre os blocos)
                    float crevice = max(smoothstep(0.05, 0.0, h - rk.z) * step(0.001, rk.z), smoothstep(0.05, 0.0, h)) * smoothstep(0.02, -0.04, d);
                    float pulse = 0.8 + 0.2 * sin(t * 3.0 + seed + p.x * 4.0);
                    float hot = (0.75 + heat * 0.5) * pulse;
                    float lava = crevice * hot;
                    float3 lavaCol = lerp(LavaRed, LavaOrange, saturate(lava * 1.4));
                    lavaCol = lerp(lavaCol, LavaYellow, smoothstep(0.7, 1.0, lava));

                    // Rocha preta, áspera, com leve variação
                    float3 albedo = lerp(float3(0.025, 0.022, 0.024), float3(0.09, 0.08, 0.08), fbm3(p * 6.0 + seed));
                    float ao = lerp(0.45, 1.0, smoothstep(0.0, 0.12, h));
                    float3 col = Shade(N, albedo, 0.0, 0.72, ao, LavaOrange * 0.7);

                    // Luz da lava nas faces dos blocos voltadas para as fendas (bounce quente)
                    float bounce = exp(-(h - rk.z) * 25.0) * step(0.001, rk.z) * (1.0 - crevice) + exp(-h * 22.0) * (1.0 - crevice);
                    col += LavaOrange * bounce * 1.5 * hot;
                    // Pequenas brasas dentro da rocha (veios finos)
                    float vein = 0.0;
                    col += LavaOrange * vein * hot;
                    col = lerp(col, lavaCol * 3.4, saturate(lava * 1.2));

                    rgb = col * fill;
                    alpha = fill;
                }

                // Aura de fogo (ruído pela direção, sem emenda), subindo para fora
                float2 fdir = p / max(length(p), 1e-4);
                float flame = vnoise(fdir * 3.0 + seed + float2(length(p) * 4.0 - t * 2.5, 0));
                float aura = exp(-max(d, 0.0) * 5.5) * (0.3 + flame * flame * 1.6) * (1.0 - fill) * win.x * win.y;
                rgb += lerp(LavaRed, LavaOrange, flame) * aura * 1.25;

                // Detritos: pedaços de rocha orbitando, com rim laranja
                for (int k = 0; k < 8; k++)
                {
                    float hk = hash21(float2(k, seed));
                    float ang = hk * 6.2832 + k * 0.8 + t * (0.2 + hk * 0.25) * (k % 2 == 0 ? 1.0 : -1.0);
                    float2 c = float2(cos(ang), sin(ang)) * (0.82 + hk * 0.38);
                    float r = 0.03 + 0.06 * hash21(float2(seed, k + 3.0));
                    float2 dp = p - c;
                    float dd = length(dp) - r * (0.8 + 0.4 * vnoise(dp / r * 1.3 + k));
                    float df = smoothstep(0.008, -0.008, dd) * win.x * win.y;
                    float2 q = dp / r;
                    float3 nk = normalize(float3(q, sqrt(saturate(1.0 - dot(q, q) * 0.8)) + 0.2));
                    float3 rock = Shade(nk, float3(0.05, 0.045, 0.045), 0.0, 0.6, 1.0, LavaOrange * 0.9);
                    // lado voltado para o asteroide recebe a luz da lava
                    rock += LavaOrange * saturate(dot(nk.xy, -normalize(c))) * 0.6;
                    rgb = lerp(rgb, rock, df);
                    alpha = max(alpha, df);
                }

                // Brasas soltas
                for (int b = 0; b < 6; b++)
                {
                    float hb = hash21(float2(seed, b * 7.0));
                    float life = frac(t * (0.35 + hb * 0.3) + hb);
                    float ang = hb * 6.2832 + b;
                    float2 c = float2(cos(ang), sin(ang)) * (0.65 + life * 0.6);
                    float2 dp = p - c;
                    rgb += LavaYellow * exp(-dot(dp, dp) * 2500.0) * (1.0 - life) * 1.5 * win.x * win.y;
                }

                // Legibilidade: contorno laranja no limite da rocha e sombra escura em volta,
                // que separa o perigo do piso (o brilho aditivo fica por cima da sombra)
                rgb += LavaOrange * exp(-abs(d) * 45.0) * 1.2 * win.x * win.y;
                // Anel de alerta vermelho pulsando rápido: lê como "perigo" mesmo de relance
                float warn = 0.55 + 0.45 * sin(t * 9.0 + seed);
                rgb += float3(1.0, 0.12, 0.04) * exp(-abs(d - 0.09) * 38.0) * warn * 1.3 * win.x * win.y;
                alpha = max(alpha, exp(-max(d, 0.0) * 4.0) * 0.6 * win.x * win.y);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
