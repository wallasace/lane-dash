// Nave (fiel ao concept): caça em flecha com nariz longo, placas brancas na espinha,
// cockpit preto, asas grandes em flecha com bordo de ataque prateado, duas naceles largas
// nas laterais + motor central, aletas traseiras e jatos ciano longos.
// Iluminação PBR estilizada: GGX + Fresnel, metal/rugosidade por material, reflexo de um
// ambiente neon falso, rim ciano e difuso em "degraus suaves".
// O quad é mais alto que largo (1.7 x 3.0) para caber o jato; a nave fica na parte de cima.
// A cor de destaque vem da cor do vértice (SpriteRenderer.color).
Shader "LaneDash/Ship"
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

            // Proporção do quad (3.0 / 1.7) e deslocamento que põe o centro da nave em p.y = 0
            static const float Aspect = 1.765;
            static const float CenterShift = 0.765;

            // Bordo de ataque da asa: da raiz (0.12, 0.15) até a ponta (0.86, -0.50)
            static const float LeadSlope = 0.878;
            static const float LeadOffset = 0.255;

            float Fuselage(float2 p)
            {
                float nose = sdTriangle(p, float2(0, 0.98), float2(-0.1, 0.3), float2(0.1, 0.3));
                float mid = sdEllipseApprox(p - float2(0, 0.08), float2(0.115, 0.5));
                return smin(nose, mid, 0.06);
            }

            float Nacelle(float2 m) { return sdEllipseApprox(m - float2(0.2, -0.32), float2(0.1, 0.31)); }

            float ShipSDF(float2 p)
            {
                float2 m = float2(abs(p.x), p.y);
                float wing = sdTriangle(m, float2(0.11, 0.17), float2(0.86, -0.50), float2(0.2, -0.40));
                float canard = sdTriangle(m, float2(0.08, 0.42), float2(0.23, 0.22), float2(0.08, 0.25));
                float fin = sdTriangle(m, float2(0.24, -0.44), float2(0.38, -0.70), float2(0.23, -0.62));
                float tail = sdEllipseApprox(p - float2(0, -0.42), float2(0.075, 0.18));
                float d = smin(Fuselage(p), Nacelle(m), 0.05);
                d = smin(d, wing, 0.03);
                d = smin(d, canard, 0.02);
                d = smin(d, fin, 0.02);
                return smin(d, tail, 0.03);
            }

            // Altura: bevel nas bordas, fuselagem e naceles abauladas, asas com diedro
            float Height(float2 p)
            {
                float2 m = float2(abs(p.x), p.y);
                float bevel = smoothstep(0.0, 0.05, -ShipSDF(p));
                float dome = max(sqrt(saturate(-Fuselage(p) / 0.11)), sqrt(saturate(-Nacelle(m) / 0.1)) * 0.85);
                float dihedral = (1.0 - saturate(m.x)) * 0.3;
                return bevel * (0.25 + dihedral) + dome * 0.6;
            }


            // Jato: núcleo branco que vira ciano e some ao longo do comprimento
            float3 Exhaust(float2 p, float2 nozzle, float width, float len, float3 accent, float flick)
            {
                float2 q = p - nozzle;
                float t = saturate(-q.y / len);
                float w = width * (1.0 + t * 0.8);
                float beam = exp(-q.x * q.x / (w * w)) * pow(1.0 - t, 1.4) * smoothstep(0.03, -0.02, q.y) * flick;
                float core = exp(-q.x * q.x / (w * w * 0.1)) * pow(1.0 - t, 2.5) * smoothstep(0.03, -0.02, q.y);
                return accent * beam * 1.3 + core * float3(0.8, 0.97, 1.0) * 1.2;
            }

            // Bocal: anel ciano com miolo branco (visto de cima/trás)
            float3 Nozzle(float2 p, float2 c, float r, float3 accent)
            {
                float2 q = (p - c) * float2(1.0, 1.8);
                float d = length(q);
                return accent * exp(-abs(d - r) * 70.0) * 1.5 + float3(0.85, 0.98, 1.0) * exp(-d * d / (r * r * 0.35)) * 1.6;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 l = Local(i.uv);
                float2 p = float2(l.x, l.y * Aspect - CenterShift);
                float2 m = float2(abs(p.x), p.y);
                float ax = m.x;
                float3 accent = i.color.rgb;
                float d = ShipSDF(p);
                float aa = fwidth(d) * 1.2;
                float fill = smoothstep(aa, -aa, d);

                const float e = 0.008;
                float h = Height(p);
                float3 N = normalize(float3((h - Height(p + float2(e, 0))) / e * 0.12,
                                            (h - Height(p + float2(0, e))) / e * 0.12, 1.0));

                // ---------- Materiais ----------
                // Base: metal grafite escuro e brilhante (casco do concept)
                float3 albedo = lerp(float3(0.2, 0.21, 0.25), float3(0.34, 0.36, 0.41), saturate(0.5 + p.y * 0.6));
                float metal = 0.55;
                float rough = 0.42 + (vnoise(p * 24.0) - 0.5) * 0.1;
                float ao = 1.0;

                float fus = Fuselage(p);
                float nac = Nacelle(m);
                float lead = p.y + ax * LeadSlope - LeadOffset;   // 0 no bordo de ataque, negativo para dentro

                // Bordo de ataque prateado (faixa clara e lisa ao longo da frente da asa)
                float leadBand = smoothstep(-0.1, -0.06, lead) * step(0.12, ax) * step(0.0, nac);
                albedo = lerp(albedo, float3(0.8, 0.82, 0.86), leadBand);
                rough = lerp(rough, 0.16, leadBand);

                // Painel branco perto da raiz da asa
                float rootPanel = step(lead, -0.1) * smoothstep(0.36, 0.33, ax) * step(0.13, ax) * step(-0.28, p.y) * step(0.0, nac);
                albedo = lerp(albedo, float3(0.5, 0.52, 0.56), rootPanel);
                metal = lerp(metal, 0.1, rootPanel);
                rough = lerp(rough, 0.4, rootPanel);

                // Placas brancas na espinha, com cortes entre elas
                float plates = smoothstep(0.05, 0.04, ax) * step(-0.36, p.y) * step(p.y, 0.26) * step(0.12, frac(p.y * 8.0));
                albedo = lerp(albedo, float3(0.52, 0.54, 0.58), plates);
                metal = lerp(metal, 0.1, plates);
                rough = lerp(rough, 0.35, plates);

                // Linhas de painel e oclusão nas junções (asa com fuselagem e naceles)
                float seams = (step(0.94, frac(lead * 9.0)) * step(0.14, ax) * step(lead, -0.08) + step(frac(p.y * 8.0), 0.12) * plates);
                ao *= 1.0 - saturate(seams) * 0.55;
                float jd = min(fus, nac);   // fora da fuselagem/naceles: escurece perto da junção
                ao *= jd < 0.0 ? 1.0 : lerp(0.6, 1.0, smoothstep(0.0, 0.07, jd));

                float3 col = Shade(N, albedo, metal, rough, ao, float3(0.3, 0.9, 1.0) * 0.45);

                // Cockpit: vidro preto, longo e estreito, com reflexo forte
                float ck = sdEllipseApprox(p - float2(0, 0.46), float2(0.048, 0.17));
                float ckMask = smoothstep(0.008, -0.008, ck);
                float3 Ng = normalize(float3(p.x / 0.048 * 0.8, (p.y - 0.46) / 0.17 * 0.45, 1.0));
                col = lerp(col, Shade(Ng, float3(0.02, 0.03, 0.06), 0.0, 0.04, 1.0, float3(0.3, 0.9, 1.0) * 0.45) + accent * 0.05, ckMask);

                // Contorno neon e brilho externo
                col += accent * exp(-abs(d) * 60.0) * 1.9;
                float2 win = smoothstep(1.0, 0.75, abs(l));    // some antes da borda do quad
                float glow = exp(-max(d, 0.0) * 6.0) * 0.6 * (1.0 - fill) * win.x * win.y;

                // Bocais e jatos (dois grandes nas naceles + um central); crescem com a velocidade
                float3 noz = Nozzle(p, float2(-0.2, -0.6), 0.065, accent) + Nozzle(p, float2(0.2, -0.6), 0.065, accent)
                           + Nozzle(p, float2(0, -0.58), 0.045, accent);
                float flick = 0.88 + 0.12 * sin(_Time.y * 61.0) + 0.08 * sin(_Time.y * 37.0 + p.x * 9.0);
                float len = 1.5 + 1.0 * _LD_Speed;
                float3 jets = Exhaust(p, float2(-0.2, -0.62), 0.07, len, accent, flick)
                            + Exhaust(p, float2(0.2, -0.62), 0.07, len, accent, flick)
                            + Exhaust(p, float2(0, -0.6), 0.045, len * 0.85, accent, flick);
                jets *= (1.0 - fill * 0.95) * win.x * smoothstep(1.0, 0.8, -l.y);

                float3 rgb = col * fill + noz + accent * glow + jets;

                // Luzes de navegação nas pontas das asas
                float2 wt = float2(ax - 0.82, p.y + 0.47);
                float side = step(0.0, p.x);
                float blink = step(0.5, frac(_Time.y * 1.2 + side * 0.5));
                rgb += lerp(float3(1, 0.15, 0.1), float3(0.1, 1, 0.3), side) * exp(-dot(wt, wt) * 1500.0) * blink * 2.0;

                // Sombra escura em volta da nave: o jogador é sempre o elemento mais destacado
                float shadow = exp(-max(d, 0.0) * 9.0) * 0.6 * win.x * win.y;
                return fixed4(rgb, max(fill, shadow));
            }
            ENDCG
        }
    }
}
