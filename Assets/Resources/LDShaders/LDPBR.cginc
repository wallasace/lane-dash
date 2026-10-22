// PBR estilizado compartilhado pelos shaders do tema espacial (nave, asteroides).
// GGX + Smith + Fresnel de Schlick, difuso em "degraus suaves" (cel shading leve),
// reflexo de um ambiente neon falso e luz de recorte (rim). Visão de cima: V = (0, 0, 1).
#ifndef LD_PBR_INCLUDED
#define LD_PBR_INCLUDED

static const float PI = 3.14159265;
static const float3 KeyDir = float3(-0.25, 0.6, 0.75);   // luz principal: fria, de cima

float D_GGX(float NoH, float a)
{
    float a2 = a * a;
    float d = NoH * NoH * (a2 - 1.0) + 1.0;
    return a2 / (PI * d * d);
}

float V_Smith(float NoV, float NoL, float a)
{
    return 0.5 / max(NoL * (NoV * (1.0 - a) + a) + NoV * (NoL * (1.0 - a) + a), 1e-4);
}

// Ambiente: céu escuro, reflexo ciano da pista embaixo, magenta dos trilhos nos lados
float3 Env(float3 R, float rough)
{
    float sharp = 1.0 - rough;
    float3 c = lerp(float3(0.02, 0.03, 0.06), float3(0.18, 0.26, 0.42), saturate(R.y * 0.5 + 0.5));
    c += float3(1.0, 0.25, 0.75) * pow(saturate(abs(R.x)), lerp(3.0, 10.0, sharp)) * 0.3 * sharp;   // reflexo do neon das paredes
    c += float3(0.2, 0.9, 1.0) * pow(saturate(-R.y), lerp(1.5, 6.0, sharp)) * 0.7;
    c += pow(saturate(dot(R, normalize(float3(-0.4, 0.6, 0.7)))), lerp(4.0, 80.0, sharp)) * sharp * 2.0;
    return c;
}

float3 Light(float3 N, float3 L, float3 lightCol, float3 albedo, float metal, float rough)
{
    float3 V = float3(0, 0, 1);
    float a = max(rough * rough, 0.02);
    float3 H = normalize(L + V);
    float NoL = saturate(dot(N, L)), NoV = saturate(N.z) + 1e-4;
    float3 f0 = lerp(0.04, albedo, metal);
    float3 F = f0 + (1.0 - f0) * pow(1.0 - saturate(dot(V, H)), 5.0);
    float3 spec = D_GGX(saturate(dot(N, H)), a) * V_Smith(NoV, NoL, a) * F;
    float ramp = smoothstep(0.0, 0.3, NoL) * 0.75 + NoL * 0.25;   // cel shading leve
    float3 diff = (1.0 - F) * (1.0 - metal) * albedo;
    // Limita picos de especular (luz rasante em chanfros finos gera pontos estourados)
    return (diff * ramp + min(spec * NoL * PI, 3.0)) * lightCol;
}

// rim = cor e intensidade da luz de recorte (ciano na nave, laranja nos asteroides)
float3 Shade(float3 N, float3 albedo, float metal, float rough, float ao, float3 rim)
{
    // Direção de luz única para a cena toda, vinda do próprio cenário:
    // luz fria de cima (principal), neon magenta das paredes pelas laterais e a pista ciano por baixo
    float3 col = Light(N, normalize(KeyDir), float3(0.85, 0.9, 1.0) * 1.5, albedo, metal, rough)
               + Light(N, normalize(float3(-0.9, 0.05, 0.35)), float3(1.0, 0.2, 0.75) * 0.45, albedo, metal, rough)
               + Light(N, normalize(float3(0.9, 0.05, 0.35)), float3(1.0, 0.2, 0.75) * 0.45, albedo, metal, rough)
               + Light(N, normalize(float3(0.0, -0.85, 0.45)), float3(0.2, 0.85, 1.0) * 0.8, albedo, metal, rough);
    float NoV = saturate(N.z);
    float3 f0 = lerp(0.04, albedo, metal);
    float3 Fr = f0 + (max(1.0 - rough, f0) - f0) * pow(1.0 - NoV, 5.0);
    col += Env(reflect(float3(0, 0, -1), N), rough) * Fr * ao;
    col += (1.0 - metal) * albedo * float3(0.06, 0.08, 0.13) * ao;
    col += rim * pow(1.0 - NoV, 4.0);
    return col * ao;
}

#endif
