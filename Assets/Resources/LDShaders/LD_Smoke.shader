// Fumaça: puff macio com borda irregular (ruído), escurece o que está atrás e tem um leve brilho
// quente por dentro enquanto é jovem. color.rgb = cor, color.a = opacidade (fade).
// A borda gira e muda com a semente da posição para os puffs não ficarem iguais.
Shader "LaneDash/Smoke"
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
            #include "LDCommon.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = Local(i.uv);
                float r = length(p);
                float seed = dot(floor(i.wpos * 3.0), float2(1.7, 9.2));
                float n = fbm3(p * 2.5 + seed);
                float shape = smoothstep(1.0, 0.35, r + (n - 0.5) * 0.6);
                float a = shape * i.color.a;
                // miolo levemente aceso (fogo dentro da fumaça)
                float3 c = i.color.rgb * (0.6 + n * 0.6) + float3(1.0, 0.4, 0.1) * exp(-r * r * 6.0) * i.color.a * 0.6;
                return fixed4(c * a, a);
            }
            ENDCG
        }
    }
}
