using UnityEngine;
using UnityEngine.UI;

// Mãozinha de tutorial (estilo playable): uma mão animada em loop mostra o gesto do jogo,
// tocando o lado esquerdo e depois o direito da tela, com onda de toque, a metade da tela
// correspondente acendendo e um texto curto. Aparece só no início da partida, com o jogo
// parado esperando o primeiro toque. Tudo desenhado por código, em tempo de execução.
public class TutorialHand : MonoBehaviour
{
    const float Cycle = 2.4f;          // segundos para tocar esquerda + direita

    RectTransform hand, ripple;
    Image handImg, rippleImg, leftGlow, rightGlow;
    Text label;
    CanvasGroup group;
    bool visible;
    float t, alpha;

    public static TutorialHand Create(string text)
    {
        var go = new GameObject("TutorialHand");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var th = go.AddComponent<TutorialHand>();
        th.Build(text);
        return th;
    }

    void Build(string text)
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        leftGlow = SideGlow(0f);
        rightGlow = SideGlow(1f);

        rippleImg = new GameObject("Ripple").AddComponent<Image>();
        rippleImg.transform.SetParent(transform, false);
        rippleImg.sprite = RingSprite();
        rippleImg.raycastTarget = false;
        ripple = rippleImg.rectTransform;
        ripple.anchorMin = ripple.anchorMax = new Vector2(0.5f, 0.5f);

        handImg = new GameObject("Hand").AddComponent<Image>();
        handImg.transform.SetParent(transform, false);
        handImg.sprite = HandSprite();
        handImg.raycastTarget = false;
        hand = handImg.rectTransform;
        hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
        hand.pivot = new Vector2(0.42f, 0.9f);   // a ponta do dedo é o ponto de toque
        hand.sizeDelta = new Vector2(230, 230);

        label = Util.MakeText(transform, 0.19f, 0, 46, Util.Weight.Bold, 0.18f);
        label.text = text;
        label.color = new Color(0.88f, 0.95f, 1f);
        var glow = label.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(0.3f, 0.92f, 1f, 0.35f);
        glow.effectDistance = new Vector2(3, 3);
    }

    Image SideGlow(float x)
    {
        var img = new GameObject("SideGlow").AddComponent<Image>();
        img.transform.SetParent(transform, false);
        img.sprite = Util.HGradient();
        img.raycastTarget = false;
        img.color = new Color(0.3f, 0.92f, 1f, 0f);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(x == 0f ? 0f : 0.5f, 0f);
        rt.anchorMax = new Vector2(x == 0f ? 0.5f : 1f, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = new Vector3(x == 0f ? 1f : -1f, 1f, 1f);   // brilho mais forte na borda da tela
        return img;
    }

    // Mostra no início da partida (o jogo fica parado esperando o primeiro toque)
    public void Show()
    {
        visible = true;
        t = 0f;
    }

    // Primeiro toque do jogador, fim da partida ou volta ao menu
    public void Hide() => visible = false;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool paused = Shell.I && Shell.I.Current == Shell.State.Paused;   // some enquanto o painel de pausa está aberto
        alpha = Mathf.MoveTowards(alpha, visible && !paused ? 1f : 0f, dt * 5f);
        group.alpha = alpha;
        if (alpha <= 0f) return;

        t += dt;
        float c = (t % Cycle) / Cycle * 2f;      // 0..2: primeira metade esquerda, segunda direita
        bool right = c >= 1f;
        float k = c % 1f;                         // progresso dentro do toque

        var target = new Vector2(right ? 270f : -270f, -430f);
        var rest = new Vector2(right ? 330f : -330f, -620f);
        // Chega (0-0.35), aperta (0.35-0.5), segura e solta (0.5-0.75), volta a descansar
        float approach = Ease(Mathf.Clamp01(k / 0.35f));
        float leave = Ease(Mathf.Clamp01((k - 0.75f) / 0.25f));
        hand.anchoredPosition = Vector2.Lerp(Vector2.Lerp(rest, target, approach), rest, leave);
        float press = k > 0.35f && k < 0.6f ? Mathf.Sin((k - 0.35f) / 0.25f * Mathf.PI) : 0f;
        hand.localScale = Vector3.one * (1f - press * 0.15f);
        hand.localRotation = Quaternion.Euler(0, 0, right ? -12f : 12f);

        // Onda de toque saindo da ponta do dedo
        float rk = Mathf.Clamp01((k - 0.42f) / 0.45f);
        ripple.anchoredPosition = target;
        ripple.sizeDelta = Vector2.one * Mathf.Lerp(40f, 260f, rk);
        rippleImg.color = new Color(0.3f, 0.92f, 1f, k > 0.42f ? (1f - rk) * 0.9f : 0f);

        // A metade tocada acende de leve
        float lit = k > 0.35f ? Mathf.Clamp01(1f - (k - 0.35f) / 0.5f) * 0.22f : 0f;
        leftGlow.color = new Color(0.3f, 0.92f, 1f, right ? 0f : lit);
        rightGlow.color = new Color(0.3f, 0.92f, 1f, right ? lit : 0f);

        label.color = new Color(0.88f, 0.95f, 1f, 0.75f + 0.25f * Mathf.Sin(t * 4f));
    }

    static float Ease(float x) => x * x * (3f - 2f * x);

    // ---------- Sprites gerados ----------

    static Sprite handSprite, ringSprite;

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        var pa = p - a; var ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    static float Box(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * r;
        return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
    }

    // Luva de desenho animado apontando para cima (o clássico "dedinho" de tutorial):
    // indicador esticado com unha, três dedos dobrados com vincos entre eles, polegar dobrado
    // por cima, punho neon, sombreado suave com luz do alto à esquerda e sombra projetada.
    // Cada parte é uma camada com o próprio contorno, por isso os vincos aparecem sozinhos.
    static readonly Color Ink = new Color(0.06f, 0.08f, 0.15f, 1f);
    static readonly Color Glove = new Color(1f, 1f, 1f, 1f);
    static readonly Color GloveShade = new Color(0.78f, 0.83f, 0.94f, 1f);
    static readonly Color Cuff = new Color(0.25f, 0.88f, 1f, 1f);

    static float Palm(Vector2 p) => Box(p, new Vector2(0.57f, 0.36f), new Vector2(0.2f, 0.15f), 0.1f);
    static float Index(Vector2 p) => Capsule(p, new Vector2(0.42f, 0.42f), new Vector2(0.42f, 0.8f), 0.088f);
    static float Middle(Vector2 p) => Box(p, new Vector2(0.56f, 0.5f), new Vector2(0.064f, 0.085f), 0.06f);
    static float Ring(Vector2 p) => Box(p, new Vector2(0.68f, 0.48f), new Vector2(0.058f, 0.078f), 0.055f);
    static float Pinky(Vector2 p) => Box(p, new Vector2(0.785f, 0.445f), new Vector2(0.05f, 0.065f), 0.048f);
    static float Thumb(Vector2 p) => Capsule(p, new Vector2(0.34f, 0.33f), new Vector2(0.55f, 0.41f), 0.066f);
    static float Wrist(Vector2 p) => Box(p, new Vector2(0.58f, 0.19f), new Vector2(0.19f, 0.065f), 0.035f);
    static float Nail(Vector2 p) => Box(p, new Vector2(0.42f, 0.785f), new Vector2(0.036f, 0.032f), 0.03f);

    static float All(Vector2 p) =>
        Mathf.Min(Mathf.Min(Mathf.Min(Palm(p), Index(p)), Mathf.Min(Middle(p), Ring(p))),
                  Mathf.Min(Mathf.Min(Pinky(p), Thumb(p)), Wrist(p)));

    // Desenha uma camada por cima de dst: preenchimento sombreado + contorno escuro
    static Color Layer(Color dst, Vector2 p, System.Func<Vector2, float> sdf, Color lit, Color shade, float px)
    {
        float d = sdf(p);
        if (d > px * 2f) return dst;
        const float e = 0.004f;
        var g = new Vector2(sdf(p + new Vector2(e, 0)) - d, sdf(p + new Vector2(0, e)) - d).normalized;   // normal para fora
        float rim = Mathf.Clamp01(1f + d / 0.07f);                       // 1 na borda, 0 no miolo
        float toLight = Vector2.Dot(g, new Vector2(-0.7071f, 0.7071f));  // borda virada para o alto à esquerda
        var body = Color.Lerp(lit, shade, rim * Mathf.Clamp01(-toLight) * 0.9f + (1f - p.y) * 0.12f);
        body = Color.Lerp(body, Color.white, rim * Mathf.Clamp01(toLight) * 0.25f);
        const float ink = 0.016f;
        float outline = Mathf.Clamp01((d + ink) / px + 0.5f);            // faixa escura junto à borda
        var c = Color.Lerp(body, Ink, outline);
        float cover = Mathf.Clamp01(-d / px + 0.5f);
        return Color.Lerp(dst, new Color(c.r, c.g, c.b, 1f), cover);
    }

    static Sprite HandSprite()
    {
        if (handSprite) return handSprite;
        const int s = 256;
        float px = 1f / s;
        var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                var p = new Vector2((x + 0.5f) / s, (y + 0.5f) / s);
                // Sombra projetada, macia e deslocada para baixo à direita
                float sh = All(p - new Vector2(0.025f, -0.03f));
                var c = new Color(0f, 0f, 0.05f, Mathf.Clamp01(1f - (sh + 0.01f) / 0.035f) * 0.35f);

                c = Layer(c, p, Wrist, Cuff, new Color(0.08f, 0.5f, 0.75f, 1f), px);
                c = Layer(c, p, Palm, Glove, GloveShade, px);
                c = Layer(c, p, Pinky, Glove, GloveShade, px);
                c = Layer(c, p, Ring, Glove, GloveShade, px);
                c = Layer(c, p, Middle, Glove, GloveShade, px);
                c = Layer(c, p, Index, Glove, GloveShade, px);
                c = Layer(c, p, Thumb, Glove, GloveShade, px);

                // Unha do indicador e faixa de brilho no punho neon
                float nail = Nail(p);
                if (nail < 0f) c = Color.Lerp(c, new Color(0.9f, 0.93f, 1f, 1f), Mathf.Clamp01(-nail / px) * 0.6f);
                if (Wrist(p) < -0.016f && Mathf.Abs(p.y - 0.215f) < 0.012f) c = Color.Lerp(c, Color.white, 0.55f);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        handSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.42f, 0.9f), 100);
        return handSprite;
    }

    static Sprite RingSprite()
    {
        if (ringSprite) return ringSprite;
        const int s = 128;
        var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = new Vector2(x + 0.5f - s / 2f, y + 0.5f - s / 2f).magnitude / (s / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.1f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100);
        return ringSprite;
    }
}
