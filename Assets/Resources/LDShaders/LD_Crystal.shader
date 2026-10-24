// Iluminação: PBR estilizado "pintado" (3 tons de diffuse, especular recortado, bevel pintado, rim ciano).
// Cristal de energia (a "moeda"), fiel ao concept: gema dourada alongada (prisma hexagonal com
// pontas em pirâmide) girando no próprio eixo, facetas com PBR estilizado e brilho interno,
// arestas claras, anel dourado em órbita (a metade de trás passa atrás da gema) e reflexo
// dourado na pista abaixo. O giro é feito no shader, então o quad não precisa ser achatado.
// color.g = fase do giro e da faísca, para os cristais não ficarem sincronizados.
// Quad de 1.2 x 2.0 unidades; p está em unidades do mundo.
Shader "LaneDash/Crystal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Refl ("Reflexo no piso (0 = objeto normal)", Float) = 0
    }
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

            static const float2 Half = float2(0.6, 1.0);
            static const float CenterY = 0.12;
            static const float R = 0.24;      // raio do prisma
            static const float Tip = 0.72;    // altura das pontas
            static const float Band = 0.12;   // meia altura do prisma central
            static const float3 Gold = float3(1.0, 0.62, 0.1);
            static const float3 GoldLight = float3(1.0, 0.92, 0.6);
            static const float2 RingR = float2(0.46, 0.12);

            float HalfWidth(float y) { return R * saturate((Tip - abs(y)) / (Tip - Band)); }

            float GemSDF(float2 q)
            {
                float2 a = abs(q);
                float k = (Tip - Band) / R;
                return max(a.x - R, (a.x * k + a.y - Tip) / sqrt(k * k + 1.0));
            }

            // Anel em órbita: distância até a elipse e se o ponto está na metade da frente
            float RingDist(float2 q, out float front)
            {
                float2 e = q / RingR;
                front = step(e.y, 0.0);
                return abs(length(e) - 1.0) * RingR.y;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 l = Local(i.uv);
                float2 p = l * Half;
                float t = _Time.y;
                float phase = i.color.g * 6.2832;
                float bob = sin(t * 2.0 + phase) * 0.04;     // flutua de leve
                float2 q = p - float2(0, CenterY + bob);
                float2 win = smoothstep(1.0, 0.8, abs(l));
                // Máscara radial: halo, sombra e reflexo somem antes da borda do quad (sem corte reto)
                float radial = smoothstep(1.0, 0.55, length(l));

                float d = GemSDF(q);
                float aa = fwidth(d) * 1.2;
                float fill = smoothstep(aa, -aa, d);

                float3 rgb = 0;
                float alpha = 0;

                // Reflexo dourado na pista e halo
                float refl = exp(-p.x * p.x * 45.0) * exp(-pow(max(-(p.y + 0.75), 0.0), 2.0) * 6.0) * smoothstep(-0.5, -0.8, p.y);
                rgb += Gold * refl * 0.5 * win.x * smoothstep(-1.0, -0.82, p.y);
                rgb += Gold * exp(-max(d, 0.0) * 6.5) * 1.0 * (1.0 - fill) * radial;
                // Sombra escura em volta: destaca o cristal do piso
                alpha = max(alpha, exp(-max(d, 0.0) * 10.0) * 0.5 * radial);

                // Metade de trás do anel (fica atrás da gema)
                float front;
                float rd = RingDist(q + float2(0, 0.02), front);
                float ring = smoothstep(0.012, 0.004, rd) * win.x;
                float ringGlow = exp(-rd * 60.0) * 0.5 * win.x;
                rgb += (Gold * 1.6 * ring + Gold * ringGlow) * (1.0 - front);

                if (d < 0.02)
                {
                    // Giro: acha qual face do prisma hexagonal está na frente neste x
                    float hw = max(HalfWidth(q.y), 1e-3);
                    float s = clamp(q.x / hw, -1.0, 1.0);
                    float a = t * 1.6 + phase;
                    float3 n = float3(0, 0, 1);
                    float edge = 1.0;
                    float faceId = 0.0;
                    for (int k = 0; k < 6; k++)
                    {
                        float v0 = a + k * 1.0472;
                        float th = v0 + 0.5236;
                        float x0 = sin(v0), x1 = sin(v0 + 1.0472);
                        if (cos(th) > 0.0 && s >= min(x0, x1) && s <= max(x0, x1))
                        {
                            n = float3(sin(th), 0, cos(th));
                            edge = min(abs(s - x0), abs(s - x1)) * hw;
                            faceId = k;
                        }
                    }
                    // Pirâmides: as faces de cima/baixo inclinam para fora
                    float region = q.y > Band ? 1.0 : (q.y < -Band ? -1.0 : 0.0);
                    float3 N = normalize(float3(n.x * 0.85, region * 0.55, n.z * 0.85 + 0.15));

                    // ---------- Proposta: PBR estilizado "pintado" ----------
                    float3 V = float3(0, 0, 1);
                    float3 L = normalize(KeyDir);
                    float NoV = saturate(dot(N, V));
                    float lit = dot(N, L);
                    // Difuso em 3 tons pintados (sombra, meio-tom, luz) com transição curta
                    float3 dark = float3(0.32, 0.08, 0.01);
                    float3 mid = float3(0.95, 0.42, 0.04);
                    float3 light = float3(1.0, 0.8, 0.28);
                    float3 col = lerp(dark, mid, smoothstep(0.02, 0.1, lit));
                    col = lerp(col, light, smoothstep(0.5, 0.58, lit));
                    // Gradiente pintado: mais claro em cima, mais quente embaixo
                    col *= lerp(0.8, 1.15, saturate(q.y * 0.7 + 0.5));
                    float tipFade = saturate((abs(q.y) - Band) / (Tip - Band));
                    col *= lerp(1.0, 0.8, tipFade);

                    // Núcleo emissivo (laranja -> amarelo no centro)
                    float core = pow(1.0 - abs(s), 3.0) * (1.0 - tipFade * 0.7);
                    col += lerp(float3(1.0, 0.45, 0.05), float3(1.0, 0.9, 0.5), core) * core * 0.45;

                    // Especular com forma definida (GGX "recortado")
                    float3 H = normalize(L + V);
                    float g = D_GGX(saturate(dot(N, H)), 0.18) * 0.05;
                    col = lerp(col, float3(1.0, 0.97, 0.85), smoothstep(0.55, 0.65, g) * 0.9);
                    // Fresnel com o ambiente (sutil, tingido)
                    float fres = pow(1.0 - NoV, 3.0);
                    col += lerp(Env(reflect(-V, N), 0.2), light, 0.5) * fres * 0.35;
                    // Rim complementar ciano, do lado oposto à luz (vem da pista)
                    col += float3(0.2, 0.85, 1.0) * smoothstep(0.1, 0.6, -lit) * pow(1.0 - NoV, 1.5) * 0.7;

                    // Bevel pintado: só as arestas voltadas para a luz brilham
                    float edges = exp(-edge * 150.0) * smoothstep(0.0, 0.5, lit)
                                + exp(-abs(abs(q.y) - Band) * 150.0) * 0.6 + exp(-abs(d) * 110.0) * 0.8;
                    col += float3(1.0, 0.93, 0.7) * saturate(edges) * 0.9;
                    // Faixa de luz que sobe pela gema
                    float sweep = q.y - (frac(t * 0.5 + phase) * 2.0 - 1.0);
                    col += GoldLight * exp(-sweep * sweep * 60.0) * 0.6;

                    rgb = lerp(rgb, col, fill);
                    alpha = max(alpha, fill);
                }

                // Metade da frente do anel e faíscas correndo por ele
                rgb += (GoldLight * 1.4 * ring + Gold * ringGlow) * front;
                alpha = max(alpha, ring * front);
                for (int j = 0; j < 3; j++)
                {
                    float ang = t * 1.8 + phase + j * 2.094;
                    float2 sp = float2(cos(ang), sin(ang)) * RingR - float2(0, 0.02);
                    float vis = sin(ang) < 0.0 ? 1.0 : 1.0 - fill;
                    float2 dp = q - sp;
                    rgb += GoldLight * exp(-dot(dp, dp) * 3000.0) * 2.0 * vis;
                }

                // Faísca em cruz perto da ponta
                float2 sq = q - float2(0.07, 0.42);
                float star = (exp(-abs(sq.x) * 60.0) * exp(-abs(sq.y) * 9.0) + exp(-abs(sq.y) * 60.0) * exp(-abs(sq.x) * 9.0))
                           * pow(0.5 + 0.5 * sin(t * 3.0 + phase * 3.0), 4.0);
                rgb += GoldLight * star * 1.5;

                return Reflected(rgb, alpha, l.y);
            }
            ENDCG
        }
    }
}
