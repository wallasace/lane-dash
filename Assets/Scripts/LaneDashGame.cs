using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Lane Dash: toque na metade esquerda/direita da tela para trocar de pista.
// Desvie dos blocos vermelhos e pegue as moedas. A velocidade aumenta com o tempo.
public class LaneDashGame : MonoBehaviour, IGame
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<LaneDashGame>() == null)
            new GameObject("LaneDashGame").AddComponent<LaneDashGame>();
    }

    class Item { public Transform t; public bool coin; }

    readonly float[] lanes = { -2.2f, 0f, 2.2f };
    const float PlayerY = -7f;
    const float SpawnY = 12f;
    const float HitDist = 0.85f;
    const float MenuSpeed = 4f;
    const float CamSize = 10f;
    const float MenuCamY = -4f;          // no menu o jogador sobe para o meio da tela (prévia das pistas)
    const float DashTop = 14f, DashBottom = -16f;
    const int TrailDots = 4;

    // Arcade neon discreto
    static readonly Color Cyan = new Color(0.3f, 0.88f, 1f);
    static readonly Color Red = new Color(1f, 0.3f, 0.32f);
    static readonly Color CoinGold = new Color(1f, 0.76f, 0.2f);
    static readonly Color CoinDark = new Color(0.92f, 0.55f, 0.1f);
    static readonly Color CoinLight = new Color(1f, 0.87f, 0.42f);
    static readonly Color RoadColor = new Color(0.1f, 0.18f, 0.32f, 0.5f);
    static readonly Color DashColor = new Color(0.75f, 0.82f, 0.95f, 0.45f);

    Camera cam;
    Sprite square, circle, glow, block;
    Transform player;
    readonly SpriteRenderer[] trail = new SpriteRenderer[TrailDots];
    int lane = 1, coins, startFrame;
    float speed, distance, elapsed, nextSpawnAt, camTargetY;
    bool dead;
    readonly List<Item> items = new List<Item>();
    readonly List<Transform> dashes = new List<Transform>();

    int Score => (int)(distance / 2f) + coins * 10;

    void Start()
    {
        Application.targetFrameRate = 60;
        cam = Util.SetupCamera(CamSize);
        square = Util.Square();
        circle = Util.Circle();
        glow = Util.Glow();
        block = Util.Block();

        BuildDotGrid();
        BuildRoad();
        BuildPlayer();

        Shell.Create(this, new ShellTheme
        {
            title = "Lane Dash",
            accentLastWord = true,
            tagline = "Dodge. Collect. Go further.",
            scoreLabel = "SCORE",
            resultLabel = "POINTS",
            menuHint = "Tap left or right",
            controlsHint = true,
            pausePhrase = "Ready to continue?",
            english = true,
            author = "wallasace",
            xHandle = "wall_as_ace",
            gitHubUser = "wallasace",
            accent = Cyan,
            highlight = Shell.Gold,
            buildBrand = BuildBrand,
        });
        // Clima de corrida: Mi menor - Dó - Sol - Ré, 140 bpm
        Music.Play(new Music.Style
        {
            bpm = 140,
            chordRoots = new[] { 52, 48, 55, 50 },
            minor = new[] { true, false, false, false },
            seed = 21,
            busyHats = true,
            volume = 0.3f,
        });
    }

    // ---------- Visual ----------

    // Fundo "dot grid" (CSS --dot-bg / --dot-color / --dot-size 1px / --dot-space 22px):
    // cor sólida com um ponto quadrado de 1px a cada 22px, fixo na tela. Substitui o fundo do Shell.
    const int DotSpace = 22;                 // --dot-space (em "px" da textura)
    const float DotsPerScreenHeight = 36f;   // quantas células cabem na altura da tela
    static readonly Color DotBg = new Color(0.035f, 0.055f, 0.11f);    // --dot-bg: azul-marinho profundo
    static readonly Color DotFg = new Color(0.27f, 0.36f, 0.52f);      // --dot-color: azul acinzentado
    RawImage dotGrid;
    Vector2Int dotGridScreen;

    void BuildDotGrid()
    {
        cam.backgroundColor = DotBg;
        var tex = new Texture2D(DotSpace, DotSpace) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var px = new Color[DotSpace * DotSpace];
        for (int i = 0; i < px.Length; i++) px[i] = DotBg;
        px[0] = DotFg; // o canto da célula é o ponto, como na máscara do CSS
        tex.SetPixels(px);
        tex.Apply();

        var go = new GameObject("DotGrid");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 49f;
        canvas.sortingOrder = -99; // por cima do fundo do Shell (-100), que fica escondido

        dotGrid = new GameObject("Dots").AddComponent<RawImage>();
        dotGrid.transform.SetParent(go.transform, false);
        dotGrid.texture = tex;
        dotGrid.color = Color.white; // opaco: cobre o fundo padrão do Shell
        dotGrid.raycastTarget = false;
        var rt = dotGrid.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        UpdateDotGridTiling();
    }

    // Repete a textura para que as células fiquem quadradas em qualquer proporção de tela
    void UpdateDotGridTiling()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size == dotGridScreen) return;
        dotGridScreen = size;
        float cell = Mathf.Max(8f, size.y / DotsPerScreenHeight);
        float w = size.x / cell, h = size.y / cell;
        dotGrid.uvRect = new Rect(-w / 2f + 0.5f, -h / 2f + 0.5f, w, h); // centraliza a grade
    }

    void BuildRoad()
    {
        // Faixa das pistas um pouco mais clara que o fundo, com bordas finas
        Util.MakeSprite("Road", square, RoadColor, new Vector3(0, -2f, 0), new Vector2(6.8f, 60f), -5);
        foreach (float x in new[] { -3.4f, 3.4f })
            Util.MakeSprite("Edge", square, new Color(Shell.MutedBlue.r, Shell.MutedBlue.g, Shell.MutedBlue.b, 0.2f),
                new Vector3(x, -2f, 0), new Vector2(0.05f, 60f), -4);

        // Linhas tracejadas entre as pistas; andam para dar sensação de velocidade
        int count = (int)((DashTop - DashBottom) / 2f);
        foreach (float x in new[] { -1.1f, 1.1f })
            for (int i = 0; i < count; i++)
                dashes.Add(Util.MakeSprite("Dash", square, DashColor,
                    new Vector3(x, DashBottom + i * 2f, 0), new Vector2(0.07f, 0.9f), -3).transform);
    }

    void BuildPlayer()
    {
        var core = Util.MakeSprite("Player", circle, Cyan, new Vector3(0, PlayerY, 0), Vector2.one, 3);
        player = core.transform;
        Child(player, "Glow", glow, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f), Vector3.zero, 2.6f, 2);
        Child(player, "Shine", circle, new Color(1f, 1f, 1f, 0.45f), new Vector3(-0.15f, 0.15f, 0), 0.35f, 4);

        // Rastro curto: bolinhas que seguem o jogador com atraso
        for (int k = 0; k < TrailDots; k++)
            trail[k] = Util.MakeSprite("Trail", circle, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f - k * 0.11f),
                new Vector3(0, PlayerY - 0.55f * (k + 1), 0), Vector2.one * (0.36f - k * 0.06f), 1);
    }

    static SpriteRenderer Child(Transform parent, string name, Sprite s, Color c, Vector3 localPos, float scale, int order)
    {
        var sr = Util.MakeSprite(name, s, c, Vector3.zero, Vector2.one * scale, order);
        sr.transform.SetParent(parent, false);
        sr.transform.localPosition = localPos;
        return sr;
    }

    // Marca do menu: duas pistas, linha tracejada e o jogador
    void BuildBrand(RectTransform root)
    {
        Image Img(Sprite s, Color c, Vector2 pos, Vector2 size, float rot = 0f, float ppu = 1f)
        {
            var img = new GameObject("Mark").AddComponent<Image>();
            img.transform.SetParent(root, false);
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            if (s == Util.Rounded()) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppu; }
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
            return img;
        }

        var lineColor = new Color(Shell.MutedBlue.r, Shell.MutedBlue.g, Shell.MutedBlue.b, 0.75f);
        Img(Util.Rounded(), lineColor, new Vector2(-78, 0), new Vector2(22, 150), 5f, 3f);
        Img(Util.Rounded(), lineColor, new Vector2(78, 0), new Vector2(22, 150), -5f, 3f);
        for (int i = 0; i < 3; i++)
            Img(Util.Rounded(), Shell.Ivory, new Vector2(0, 66 - i * 26), new Vector2(8, 16), 0f, 6f);
        Img(glow, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), new Vector2(0, -38), new Vector2(190, 190));
        Img(circle, Cyan, new Vector2(0, -38), new Vector2(76, 76));
    }

    // ---------- Fluxo ----------

    public void ResetGame()
    {
        foreach (var it in items) if (it.t) Destroy(it.t.gameObject);
        items.Clear();
        lane = 1; coins = 0; distance = 0; elapsed = 0; speed = 7f; nextSpawnAt = 4f;
        dead = false;
        player.gameObject.SetActive(true);
        player.position = new Vector3(0, PlayerY, 0);
        for (int k = 0; k < TrailDots; k++)
        {
            trail[k].gameObject.SetActive(true);
            trail[k].transform.position = new Vector3(0, PlayerY - 0.55f * (k + 1), 0);
        }
        camTargetY = MenuCamY;
        cam.transform.position = new Vector3(0, MenuCamY, -10);
    }

    public void Begin()
    {
        startFrame = Time.frameCount;
        camTargetY = 0f;
    }

    void Update()
    {
        RefreshBacklightsIfChanged();
        UpdateDotGridTiling();
        var cp = cam.transform.position;
        cp.y = Mathf.Lerp(cp.y, camTargetY, 4f * Time.deltaTime);
        cam.transform.position = cp;

        // No menu as faixas continuam andando devagar, como "tela viva"
        if (!Shell.I.Playing)
        {
            if (!dead) MoveDashes(MenuSpeed * Time.deltaTime);
            return;
        }

        if (Time.frameCount != startFrame) HandleInput();

        elapsed += Time.deltaTime;
        speed = Mathf.Min(7f + elapsed * 0.25f, 20f);
        float step = speed * Time.deltaTime;
        distance += step;

        var pp = player.position;
        pp.x = Mathf.Lerp(pp.x, lanes[lane], 18f * Time.deltaTime);
        player.position = pp;
        UpdateTrail(pp.x);

        MoveDashes(step);

        if (distance >= nextSpawnAt)
        {
            SpawnRow();
            // Linhas ficam mais próximas com o tempo, mas sempre dá pra reagir
            nextSpawnAt = distance + Mathf.Max(5f, 9f - elapsed * 0.05f);
        }

        MoveItems(step, pp);
        if (Shell.I.Playing) Shell.I.SetScore(Score, false);
    }

    void UpdateTrail(float playerX)
    {
        for (int k = 0; k < TrailDots; k++)
        {
            var p = trail[k].transform.position;
            p.x = Mathf.Lerp(p.x, playerX, 14f / (k + 2) * Time.deltaTime);
            trail[k].transform.position = p;
        }
    }

    void MoveDashes(float step)
    {
        foreach (var d in dashes)
        {
            var p = d.position; p.y -= step;
            if (p.y < DashBottom) p.y += DashTop - DashBottom;
            d.position = p;
        }
    }

    void HandleInput()
    {
        int move = 0;
        if (Util.PointerDown()) move = Input.mousePosition.x < Screen.width / 2f ? -1 : 1;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) move = -1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) move = 1;
        if (move == 0) return;
        int target = Mathf.Clamp(lane + move, 0, lanes.Length - 1);
        if (target != lane)
        {
            lane = target;
            Sfx.Play("move", Random.Range(0.95f, 1.05f), 0.7f);
        }
        else
        {
            // Já está na pista da borda: "toc" na lateral, como no Stack Tower
            Sfx.Play("bounce", Random.Range(0.95f, 1.05f), 0.6f);
            Juice.Pop(player, 0.12f, 0.12f);
        }
    }

    void SpawnRow()
    {
        // Bloqueia 1 ou 2 pistas (nunca as 3) e às vezes põe uma moeda numa pista livre
        int blocked = Random.value < Mathf.Clamp01(elapsed / 40f) ? 2 : 1;
        var order = new List<int> { 0, 1, 2 };
        for (int i = 0; i < order.Count; i++)
        {
            int j = Random.Range(i, order.Count);
            (order[i], order[j]) = (order[j], order[i]);
        }
        for (int i = 0; i < blocked; i++) AddObstacle(lanes[order[i]]);
        if (Random.value < 0.6f) AddCoin(lanes[order[blocked]]);
    }

    // Quadrado vermelho com relevo, sombra e brilho leve
    void AddObstacle(float x)
    {
        const float size = 1.4f;
        var sr = Util.MakeSprite("Obstacle", block, Red, new Vector3(x, SpawnY, 0), Vector2.one, 2);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(size, size);
        var shadow = Child(sr.transform, "Shadow", block, new Color(0f, 0f, 0.05f, 0.4f), new Vector3(0.08f, -0.14f, 0), 1f, 1);
        shadow.drawMode = SpriteDrawMode.Sliced;
        shadow.size = new Vector2(size, size);
        // Mesmo "backlight" dos blocos do Stack Tower (Tools > Ajustes Visuais)
        var light = Child(sr.transform, "Backlight", null, Red, Vector3.zero, 1f, 0);
        light.drawMode = SpriteDrawMode.Sliced;
        ApplyBacklight(light, size);
        items.Add(new Item { t = sr.transform, coin = false });
    }

    static void ApplyBacklight(SpriteRenderer light, float size)
    {
        var s = VisualSettings.I;
        light.sprite = Util.Backlight(s.blockGlowBlur);
        light.color = new Color(Red.r, Red.g, Red.b, s.blockGlowAlpha);
        float side = size * s.blockGlowScale + s.blockGlowBlur * 2f;
        light.size = new Vector2(side, side);
        light.transform.localPosition = new Vector3(0, -s.blockGlowDrop, 0);
    }

    // Reaplica o brilho quando os ajustes mudam no Inspector
    int settingsVersion = -1;

    void RefreshBacklightsIfChanged()
    {
        if (settingsVersion == VisualSettings.Version) return;
        settingsVersion = VisualSettings.Version;
        foreach (var it in items)
            if (!it.coin && it.t) ApplyBacklight(it.t.Find("Backlight").GetComponent<SpriteRenderer>(), 1.4f);
    }

    // Moeda: disco dourado com anel interno e brilho
    void AddCoin(float x)
    {
        var sr = Util.MakeSprite("Coin", circle, CoinGold, new Vector3(x, SpawnY, 0), Vector2.one * 0.75f, 2);
        Child(sr.transform, "Glow", glow, new Color(CoinGold.r, CoinGold.g, CoinGold.b, 0.3f), Vector3.zero, 2.2f, 1);
        Child(sr.transform, "Ring", circle, CoinDark, Vector3.zero, 0.74f, 3);
        Child(sr.transform, "Face", circle, CoinLight, Vector3.zero, 0.56f, 4);
        items.Add(new Item { t = sr.transform, coin = true });
    }

    void MoveItems(float step, Vector3 playerPos)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            var p = it.t.position; p.y -= step;
            it.t.position = p;

            if (it.coin)
            {
                float pulse = 0.75f + Mathf.Sin(Time.time * 8f) * 0.06f;
                it.t.localScale = new Vector3(pulse, pulse, 1);
            }

            bool hit = Mathf.Abs(p.x - playerPos.x) < HitDist && Mathf.Abs(p.y - playerPos.y) < HitDist;
            if (hit && it.coin)
            {
                coins++;
                Sfx.Play("coin");
                Juice.Burst(p, CoinGold, 10, 5f, 0.18f);
                Juice.Pop(player, 0.25f, 0.15f);
                Shell.I.SetScore(Score);
                Shell.I.ShowMessage("+10", 0.5f, 0.27f);
                Destroy(it.t.gameObject);
                items.RemoveAt(i);
            }
            else if (hit)
            {
                Die();
                return;
            }
            else if (p.y < -13f)
            {
                Destroy(it.t.gameObject);
                items.RemoveAt(i);
            }
        }
    }

    void Die()
    {
        dead = true;
        Sfx.Play("lose");
        Juice.Shake(0.5f, 0.45f);
        Juice.Burst(player.position, Cyan, 24, 9f, 0.3f);
        player.gameObject.SetActive(false);
        foreach (var t in trail) t.gameObject.SetActive(false);
        Shell.I.GameOver(Score);
    }
}
