// Funções compartilhadas pelos shaders do tema espacial do Lane Dash.
// Tudo procedural: nenhuma textura é lida, só matemática barata (ruído de valor + SDFs).
#ifndef LD_COMMON_INCLUDED
#define LD_COMMON_INCLUDED

#include "UnityCG.cginc"

sampler2D _MainTex;   // exigido pelo SpriteRenderer, não é usado
float _LD_Scroll;     // distância percorrida (unidades do mundo)
float _LD_Speed;      // velocidade normalizada 0..1
float4 _LD_Player;    // posição da nave (xy)
float _LD_CamY;       // altura da câmera (ela se move entre menu e partida)

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    fixed4 color : COLOR;
};

struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    fixed4 color : COLOR;
    float2 wpos : TEXCOORD1;
};

v2f vert(appdata v)
{
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    o.color = v.color;
    o.wpos = mul(unity_ObjectToWorld, v.vertex).xy;
    return o;
}

// Perspectiva falsa: a pista afunila para o alto da tela (1 embaixo, ~0.62 no topo).
// Mesma fórmula de SpaceTheme.Perspective no C#, que posiciona os itens.
float LDPersp(float worldY)
{
    return clamp(1.0 - (worldY - _LD_CamY + 8.0) * 0.021, 0.6, 1.05);
}

// Coordenada local do quad em -1..1
float2 Local(float2 uv) { return uv * 2.0 - 1.0; }

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 hash22(float2 p)
{
    float n = hash21(p);
    return float2(n, hash21(p + n + 19.19));
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 3 oitavas: suficiente para leitura em tela de celular e barato
float fbm3(float2 p)
{
    float s = 0.5 * vnoise(p);
    p = p * 2.03 + 17.1;
    s += 0.25 * vnoise(p);
    p = p * 2.01 + 31.7;
    s += 0.125 * vnoise(p);
    return s / 0.875;
}

// SDFs (Inigo Quilez)
float sdEllipseApprox(float2 p, float2 r)
{
    float k0 = length(p / r);
    float k1 = length(p / (r * r));
    return k0 * (k0 - 1.0) / max(k1, 1e-4);
}

float sdTriangle(float2 p, float2 p0, float2 p1, float2 p2)
{
    float2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
    float2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
    float2 pq0 = v0 - e0 * saturate(dot(v0, e0) / dot(e0, e0));
    float2 pq1 = v1 - e1 * saturate(dot(v1, e1) / dot(e1, e1));
    float2 pq2 = v2 - e2 * saturate(dot(v2, e2) / dot(e2, e2));
    float s = sign(e0.x * e2.y - e0.y * e2.x);
    float2 d = min(min(float2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                       float2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                       float2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
    return -sqrt(d.x) * sign(d.y);
}

float sdHexagon(float2 p, float r)
{
    const float3 k = float3(-0.866025404, 0.5, 0.577350269);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= float2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

float smin(float a, float b, float k)
{
    float h = saturate(0.5 + 0.5 * (b - a) / k);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

#endif
