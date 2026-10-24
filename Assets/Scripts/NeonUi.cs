using UnityEngine;
using UnityEngine.UI;

// Estilo de interface "HUD neon" para a skin espacial: aplicado por cima das telas do Shell.
// Troca cantos arredondados por chanfrados, contornos por neon ciano com detalhes magenta,
// clareia os textos num branco-azulado e emoldura o placar como um HUD de nave.
// Tudo gerado por código (sprites 9-slice criados em tempo de execução).
public static class NeonUi
{
    public static readonly Color Cyan = new Color(0.3f, 0.92f, 1f);
    public static readonly Color Magenta = new Color(1f, 0.25f, 0.8f);
    static readonly Color Panel = new Color(0.02f, 0.025f, 0.07f, 0.9f);
    static readonly Color Ghost = new Color(0.04f, 0.09f, 0.18f, 0.6f);
    static readonly Color Dim = new Color(0.03f, 0.0f, 0.07f, 0.72f);
    static readonly Color TextCool = new Color(0.88f, 0.95f, 1f);
    static readonly Color TextDim = new Color(0.5f, 0.72f, 0.92f);

    const int SpriteSize = 64, Cut = 18;
    static Sprite fill, line;

    // Retângulo com dois cantos cortados na diagonal (superior esquerdo e inferior direito)
    public static Sprite Chamfer() => fill ? fill : (fill = MakeChamfer(false));
    public static Sprite ChamferOutline() => line ? line : (line = MakeChamfer(true));

    static Sprite MakeChamfer(bool outline)
    {
        const int s = SpriteSize;
        const float thickness = 3f;
        var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                // Distância (positiva para dentro) até cada borda, incluindo as duas diagonais
                float d = Mathf.Min(Mathf.Min(px, s - px), Mathf.Min(py, s - py));
                d = Mathf.Min(d, (px + (s - py) - Cut) * 0.7071f);   // canto superior esquerdo
                d = Mathf.Min(d, ((s - px) + py - Cut) * 0.7071f);   // canto inferior direito
                float a = Mathf.Clamp01(d);
                if (outline) a *= Mathf.Clamp01(thickness - d + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        float b = Cut + 4;
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    static bool Near(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.06f;

    // Reestiliza tudo que o Shell criou dentro de root
    public static void Apply(Transform root, Text title, Text score, Transform hud)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == Util.Rounded())
            {
                img.sprite = Chamfer();
                img.pixelsPerUnitMultiplier = 1.4f;
                if (img.name == "Card")
                {
                    img.color = Panel;
                    CornerAccents(img.rectTransform);
                }
                else if (img.color.a < 0.9f) img.color = Ghost;   // botões secundários e selos
            }
            else if (img.sprite == Util.RoundedOutline())
            {
                img.sprite = ChamferOutline();
                img.pixelsPerUnitMultiplier = 1.4f;
                img.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f);
            }
            else if (img.sprite == null && img.name == "Panel" && img.color.a > 0f)
                img.color = Dim;
        }

        foreach (var t in root.GetComponentsInChildren<Text>(true))
        {
            if (Near(t.color, Shell.Ivory)) t.color = TextCool;
            else if (Near(t.color, Shell.MutedBlue)) t.color = TextDim;
        }

        if (title) Glow(title.gameObject, Cyan, 0.35f, 3f);
        if (score) { Glow(score.gameObject, new Color(1f, 0.7f, 0.15f), 0.4f, 4f); ScoreFrame(hud, score); }
    }

    // Brilho falso em volta do texto (contornos translúcidos), que o bloom não alcança na UI
    static void Glow(GameObject go, Color c, float alpha, float dist)
    {
        var o1 = go.AddComponent<Outline>();
        o1.effectColor = new Color(c.r, c.g, c.b, alpha);
        o1.effectDistance = new Vector2(dist, dist);
        var o2 = go.AddComponent<Outline>();
        o2.effectColor = new Color(c.r, c.g, c.b, alpha * 0.5f);
        o2.effectDistance = new Vector2(dist * 2f, dist * 2f);
    }

    // Detalhes magenta nos cantos chanfrados dos painéis
    static void CornerAccents(RectTransform card)
    {
        Bar(card, new Vector2(0f, 1f), new Vector2(Cut + 50, -6), new Vector2(90, 5), Magenta);
        Bar(card, new Vector2(1f, 0f), new Vector2(-Cut - 50, 6), new Vector2(90, 5), Magenta);
        Bar(card, new Vector2(1f, 1f), new Vector2(-30, -6), new Vector2(36, 5), new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f));
    }

    static Image Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
    {
        var img = new GameObject("Accent").AddComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    // Moldura de HUD do placar (como no concept): contorno chanfrado, marcas laterais e sublinhado dourado
    static void ScoreFrame(Transform hud, Text score)
    {
        var anchor = score.rectTransform.anchorMin;
        var frame = new GameObject("ScoreFrame").AddComponent<RectTransform>();
        frame.SetParent(hud, false);
        frame.SetSiblingIndex(0);   // atrás dos textos
        frame.anchorMin = frame.anchorMax = anchor;
        frame.anchoredPosition = new Vector2(0, 10);
        frame.sizeDelta = new Vector2(560, 230);

        var back = new GameObject("Back").AddComponent<Image>();
        back.transform.SetParent(frame, false);
        back.sprite = Chamfer();
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 1.4f;
        back.color = new Color(0.02f, 0.03f, 0.08f, 0.55f);
        back.raycastTarget = false;
        Stretch(back.rectTransform);

        var edge = new GameObject("Edge").AddComponent<Image>();
        edge.transform.SetParent(frame, false);
        edge.sprite = ChamferOutline();
        edge.type = Image.Type.Sliced;
        edge.pixelsPerUnitMultiplier = 1.4f;
        edge.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f);
        edge.raycastTarget = false;
        Stretch(edge.rectTransform);

        // Marcas "≡" dos dois lados e sublinhado dourado
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 3; k++)
                Bar(frame, new Vector2(0.5f, 0.5f), new Vector2(side * (300 + k * 0), -20 + k * 14), new Vector2(34 - k * 8, 4),
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f));
        Bar(frame, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(170, 6), new Color(1f, 0.72f, 0.2f, 0.95f));
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
