using System.Collections.Generic;
using UnityEngine;

// Skin espacial do Lane Dash. Tudo é desenhado por shaders procedurais (Assets/Resources/LDShaders):
// cada elemento é um quad branco com um material próprio. Sprites com o mesmo material são
// agrupados num único draw call, então 10 asteroides custam o mesmo que 1.
// Sem pós-processamento: o "bloom" vem de halos e partículas aditivas, barato em mobile.
public class SpaceTheme
{
    class Particle
    {
        public SpriteRenderer sr;
        public Vector2 vel;
        public Color color;
        public float life, maxLife, size, grow, drag;
    }

    public static readonly Color Accent = new Color(0.25f, 0.95f, 1f);
    static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
    static readonly Color Fire = new Color(1f, 0.45f, 0.12f);

    static readonly int ScrollId = Shader.PropertyToID("_LD_Scroll");
    static readonly int SpeedId = Shader.PropertyToID("_LD_Speed");
    static readonly int PlayerId = Shader.PropertyToID("_LD_Player");
    static readonly int CamYId = Shader.PropertyToID("_LD_CamY");
    static readonly int SizeId = Shader.PropertyToID("_Size");

    const int PoolSize = 120;
    // Quad da nave: mais alto que largo para caber o jato; a nave fica na parte de cima
    const float ShipWidth = 2.1f, ShipHeight = 3.7f, ShipOffsetY = -0.8f;   // nave maior: é o foco da tela

    readonly Transform root;
    readonly Camera cam;
    readonly Sprite quad;
    readonly SpriteRenderer background;
    readonly Material rockMat, gemMat, glowMat, ringMat;
    readonly List<Particle> pool = new List<Particle>();
    int nextParticle;
    float trailTimer, bgAspect;

    public SpriteRenderer Ship { get; }

    public SpaceTheme(Camera cam, Transform playerRoot, Sprite quad)
    {
        this.cam = cam;
        this.quad = quad;
        root = new GameObject("SpaceTheme").transform;
        rockMat = Mat("LD_Asteroid");
        gemMat = Mat("LD_Crystal");
        glowMat = Mat("LD_Glow");
        ringMat = Mat("LD_Ring");

        // Fundo preso à câmera (por cima do fundo padrão do Shell, que fica escondido)
        background = Make("SpaceBackground", Mat("LD_Background"), Color.white, Vector3.zero, Vector2.one, -98);
        background.transform.SetParent(cam.transform, false);
        background.transform.localPosition = new Vector3(0, 0, 10f);
        FitBackground();

        // Piso (±3.3) + paredes (1.2 cada) + folga para o brilho do neon
        Make("SpaceTrack", Mat("LD_Track"), Color.white, new Vector3(0, -2f, 0), new Vector2(12f, 60f), -10);

        Ship = Make("Ship", Mat("LD_Ship"), Accent, Vector3.zero, new Vector2(ShipWidth, ShipHeight), 5);
        Ship.transform.SetParent(playerRoot, false);
        Ship.transform.localPosition = new Vector3(0, ShipOffsetY, 0);

        for (int i = 0; i < PoolSize; i++)
        {
            var sr = Make("FX", glowMat, Color.clear, Vector3.zero, Vector2.one, 10);
            sr.gameObject.SetActive(false);
            pool.Add(new Particle { sr = sr });
        }
    }

    static Material Mat(string name)
    {
        var s = Resources.Load<Shader>("LDShaders/" + name);
        if (!s)
        {
            Debug.LogError("Shader não encontrado: " + name);
            s = Shader.Find("Sprites/Default");
        }
        return new Material(s);
    }

    SpriteRenderer Make(string name, Material m, Color c, Vector3 pos, Vector2 scale, int order)
    {
        var sr = Util.MakeSprite(name, quad, c, pos, scale, order);
        sr.sharedMaterial = m;
        sr.transform.SetParent(root, true);
        return sr;
    }

    // Cobre a tela inteira em qualquer proporção (com folga para o tremor de câmera)
    void FitBackground()
    {
        if (Mathf.Approximately(bgAspect, cam.aspect)) return;
        bgAspect = cam.aspect;
        float h = cam.orthographicSize * 2f + 2f;
        var size = new Vector2(h * cam.aspect + 2f, h);
        background.transform.localScale = new Vector3(size.x, size.y, 1f);
        background.sharedMaterial.SetVector(SizeId, size);
    }

    // r,g = semente da forma; b = quanto magma
    public Transform Asteroid(Vector3 pos)
    {
        var seed = new Color(Random.value, Random.value, Random.value * 0.6f, 1);
        // Quad maior que a rocha (raio ~0.7) para caber a aura de fogo e os detritos
        var t = Make("Asteroid", rockMat, seed, pos, new Vector2(3.2f, 3.2f), 3).transform;
        t.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        return t;
    }

    public Transform Crystal(Vector3 pos) =>
        Make("Crystal", gemMat, new Color(1, Random.value, 1, 1), pos, new Vector2(1.4f, 2.33f), 4).transform;

    // Chamado todo frame: valores globais dos shaders, partículas e rastro dos motores.
    // scroll = distância visual percorrida; speed01 = velocidade normalizada (0..1).
    public void Tick(float dt, float scroll, float speed01, float worldSpeed, bool trail)
    {
        FitBackground();
        Shader.SetGlobalFloat(ScrollId, scroll);
        Shader.SetGlobalFloat(SpeedId, speed01);
        Shader.SetGlobalFloat(CamYId, cam.transform.position.y);
        var sp = Ship.transform.parent.position;
        Shader.SetGlobalVector(PlayerId, new Vector4(sp.x, sp.y, 0, 0));

        if (trail && Ship.gameObject.activeInHierarchy)
        {
            trailTimer -= dt;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.02f;
                // Faíscas saindo dos três bocais (o jato contínuo é desenhado no shader da nave)
                float w = 0.19f * Ship.transform.lossyScale.x / ShipWidth;
                for (float side = -1f; side <= 1f; side += 1f)
                    Emit(sp + new Vector3(side * w, -0.75f, 0), new Vector2(Random.Range(-0.3f, 0.3f), -worldSpeed * 0.6f),
                        Accent, 0.3f, 0.3f, -0.6f, 0f);
            }
        }

        foreach (var p in pool)
        {
            if (!p.sr.gameObject.activeSelf) continue;
            p.life -= dt;
            if (p.life <= 0f) { p.sr.gameObject.SetActive(false); continue; }
            p.vel *= 1f / (1f + p.drag * dt);
            var t = p.sr.transform;
            t.position += (Vector3)(p.vel * dt);
            float k = p.life / p.maxLife;
            float s = Mathf.Max(0.01f, p.size + p.grow * (1f - k));
            t.localScale = new Vector3(s, s, 1);
            var c = p.color; c.a = k * k;
            p.sr.color = c;
        }
    }

    // Perspectiva falsa da pista (mesma fórmula de LDPersp em LDCommon.cginc): 1 embaixo, ~0.75 no topo
    public static float Perspective(float worldY, float camY) =>
        Mathf.Clamp(1f - (worldY - camY + 8f) * 0.0135f, 0.7f, 1.05f);

    // Inclina e "achata" a nave ao trocar de pista, imitando uma rolagem 3D. bank: -1..1
    public void Bank(float bank)
    {
        // Gira em torno do centro da nave (o quad é deslocado para baixo por causa do jato)
        var rot = Quaternion.Euler(0, 0, -bank * 14f);
        Ship.transform.localRotation = rot;
        Ship.transform.localPosition = rot * new Vector3(0, ShipOffsetY, 0);
        Ship.transform.localScale = new Vector3(ShipWidth * (1f - Mathf.Abs(bank) * 0.25f), ShipHeight, 1f);
    }

    void Emit(Vector3 pos, Vector2 vel, Color c, float size, float life, float grow, float drag, bool ring = false)
    {
        var p = pool[nextParticle];
        nextParticle = (nextParticle + 1) % pool.Count;
        p.sr.sharedMaterial = ring ? ringMat : glowMat;
        p.sr.sortingOrder = ring ? 11 : 10;
        p.sr.transform.position = pos;
        p.vel = vel; p.color = c; p.size = size; p.grow = grow; p.drag = drag;
        p.life = p.maxLife = life;
        p.sr.gameObject.SetActive(true);
    }

    void Burst(Vector3 pos, Color c, int count, float speed, float size, float life)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Random.insideUnitCircle.normalized * speed * Random.Range(0.3f, 1f);
            Emit(pos, dir, c, size * Random.Range(0.5f, 1.2f), life * Random.Range(0.6f, 1f), -size * 0.5f, 3f);
        }
    }

    public void Pickup(Vector3 pos)
    {
        Burst(pos, Gold, 12, 7f, 0.5f, 0.4f);
        Emit(pos, Vector2.zero, Gold, 0.6f, 0.3f, 2.2f, 0f, true);
    }

    public void Explode(Vector3 pos)
    {
        Emit(pos, Vector2.zero, Color.white, 3f, 0.25f, 3f, 0f);
        Burst(pos, Fire, 40, 14f, 0.8f, 0.9f);
        Burst(pos, Accent, 20, 9f, 0.5f, 0.7f);
        Emit(pos, Vector2.zero, Fire, 1f, 0.5f, 7f, 0f, true);
        Emit(pos, Vector2.zero, Accent, 0.6f, 0.7f, 5f, 0f, true);
    }
}
