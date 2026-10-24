using System.Collections.Generic;
using UnityEngine;

// Skin espacial do Lane Dash. Tudo é desenhado por shaders procedurais (Assets/Resources/LDShaders):
// cada elemento é um quad branco com um material próprio. Sprites com o mesmo material são
// agrupados num único draw call, então 10 asteroides custam o mesmo que 1.
// Efeitos (VFX) usam um pool fixo de partículas de 4 tipos: brilho, anel, faísca esticada e fumaça.
public class SpaceTheme
{
    enum Kind { Glow, Ring, Streak, Smoke }

    class Particle
    {
        public SpriteRenderer sr;
        public Kind kind;
        public Vector2 vel;
        public Color color;
        public float life, maxLife, size, grow, drag, stretch, alpha;
    }

    public static readonly Color Accent = new Color(0.25f, 0.95f, 1f);
    static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
    static readonly Color Fire = new Color(1f, 0.45f, 0.12f);
    static readonly Color Smoke = new Color(0.1f, 0.07f, 0.09f);

    static readonly int ScrollId = Shader.PropertyToID("_LD_Scroll");
    static readonly int SpeedId = Shader.PropertyToID("_LD_Speed");
    static readonly int PlayerId = Shader.PropertyToID("_LD_Player");
    static readonly int CamYId = Shader.PropertyToID("_LD_CamY");
    static readonly int SizeId = Shader.PropertyToID("_Size");

    const int PoolSize = 220;
    // Quad da nave: mais alto que largo para caber o jato; a nave fica na parte de cima
    const float ShipWidth = 2.1f, ShipHeight = 3.7f, ShipOffsetY = -0.8f;   // nave maior: é o foco da tela

    readonly Transform root;
    readonly Camera cam;
    readonly Sprite quad;
    readonly SpriteRenderer background, actionLines;
    readonly Material rockMat, gemMat, rockReflMat, gemReflMat;
    readonly Material[] fxMats = new Material[4];
    readonly List<Particle> pool = new List<Particle>();
    // Reflexos no piso metálico: cópias mais fracas dos objetos, deslocadas em direção à câmera
    readonly List<(Transform obj, Transform refl, float offset)> reflections = new List<(Transform, Transform, float)>();
    readonly SpriteRenderer shipRefl;
    const float ReflStrength = 0.25f;
    int nextParticle;
    float trailTimer, bgAspect;

    public SpriteRenderer Ship { get; }

    public SpaceTheme(Camera cam, Transform playerRoot, Sprite quad)
    {
        this.cam = cam;
        this.quad = quad;
        root = new GameObject("SpaceTheme").transform;
        // Bloom na câmera: o brilho do neon, jatos, lava e cristais vaza para a cena toda
        if (!cam.GetComponent<LDBloom>()) cam.gameObject.AddComponent<LDBloom>();

        rockMat = Mat("LD_Asteroid");
        gemMat = Mat("LD_Crystal");
        rockReflMat = ReflMat(rockMat);
        gemReflMat = ReflMat(gemMat);
        fxMats[(int)Kind.Glow] = Mat("LD_Glow");
        fxMats[(int)Kind.Ring] = Mat("LD_Ring");
        fxMats[(int)Kind.Streak] = Mat("LD_Streak");
        fxMats[(int)Kind.Smoke] = Mat("LD_Smoke");

        // Fundo preso à câmera (por cima do fundo padrão do Shell, que fica escondido)
        background = Make("SpaceBackground", Mat("LD_Background"), Color.white, Vector3.zero, Vector2.one, -98);
        background.transform.SetParent(cam.transform, false);
        background.transform.localPosition = new Vector3(0, 0, 10f);

        // Linhas de ação por cima de tudo, presas à câmera; a intensidade segue a velocidade (_LD_Speed)
        actionLines = Make("ActionLines", Mat("LD_ActionLines"), Color.white, Vector3.zero, Vector2.one, 60);
        actionLines.transform.SetParent(cam.transform, false);
        actionLines.transform.localPosition = new Vector3(0, 0, 1f);
        FitBackground();

        // Piso (±3.3) + paredes (1.2 cada) + folga para o brilho do neon
        Make("SpaceTrack", Mat("LD_Track"), Color.white, new Vector3(0, -2f, 0), new Vector2(12f, 60f), -10);

        Ship = Make("Ship", Mat("LD_Ship"), Accent, Vector3.zero, new Vector2(ShipWidth, ShipHeight), 5);
        Ship.transform.SetParent(playerRoot, false);
        Ship.transform.localPosition = new Vector3(0, ShipOffsetY, 0);
        shipRefl = Make("ShipReflection", ReflMat(Ship.sharedMaterial), Accent, Vector3.zero, Vector2.one, 1);

        for (int i = 0; i < PoolSize; i++)
        {
            var sr = Make("FX", fxMats[0], Color.clear, Vector3.zero, Vector2.one, 10);
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

    static Material ReflMat(Material src)
    {
        var m = new Material(src);
        m.SetFloat("_Refl", ReflStrength);
        return m;
    }

    // Cria o reflexo de um objeto no piso (mesma sprite e semente, material em modo reflexo)
    void AddReflection(SpriteRenderer obj, Material reflMat, float offset)
    {
        var r = Make(obj.name + "Reflection", reflMat, obj.color, obj.transform.position, Vector2.one, 1);
        r.transform.SetParent(obj.transform, false);
        r.transform.localScale = Vector3.one;
        reflections.Add((obj.transform, r.transform, offset));
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
        actionLines.transform.localScale = background.transform.localScale;
        actionLines.sharedMaterial.SetVector(SizeId, size);
    }

    // r,g = semente da forma; b = quanto magma
    public Transform Asteroid(Vector3 pos)
    {
        var seed = new Color(Random.value, Random.value, Random.value * 0.6f, 1);
        // Quad maior que a rocha (raio ~0.7) para caber a aura de fogo e os detritos
        var sr = Make("Asteroid", rockMat, seed, pos, new Vector2(3.2f, 3.2f), 3);
        sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        AddReflection(sr, rockReflMat, 0.9f);
        return sr.transform;
    }

    public Transform Crystal(Vector3 pos)
    {
        var sr = Make("Crystal", gemMat, new Color(1, Random.value, 1, 1), pos, new Vector2(1.4f, 2.33f), 4);
        AddReflection(sr, gemReflMat, 1.1f);
        return sr.transform;
    }

    // Chamado todo frame: valores globais dos shaders, partículas, rastro dos motores e linhas de velocidade.
    // scroll = distância visual percorrida; speed01 = velocidade normalizada (0..1).
    public void Tick(float dt, float scroll, float speed01, float worldSpeed, bool trail)
    {
        FitBackground();
        Shader.SetGlobalFloat(ScrollId, scroll);
        Shader.SetGlobalFloat(SpeedId, speed01);
        Shader.SetGlobalFloat(CamYId, cam.transform.position.y);
        var sp = Ship.transform.parent.position;
        Shader.SetGlobalVector(PlayerId, new Vector4(sp.x, sp.y, 0, 0));
        UpdateReflections();

        if (trail && Ship.gameObject.activeInHierarchy)
        {
            // Faíscas esticadas saindo dos três bocais (o jato contínuo é desenhado no shader da nave)
            trailTimer -= dt;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.035f;
                float side = Random.Range(-1, 2) * 0.2f;
                Emit(Kind.Streak, sp + new Vector3(side, -0.8f, 0), new Vector2(Random.Range(-0.4f, 0.4f), -worldSpeed * 0.5f - 4f),
                    Accent, 0.12f, 0.25f, 0f, 1f, 3f, 0.8f);
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
            float k = p.life / p.maxLife;           // 1 = acabou de nascer, 0 = vai sumir
            float s = Mathf.Max(0.01f, p.size + p.grow * (1f - k));
            var c = p.color;
            switch (p.kind)
            {
                case Kind.Streak:
                    // Gira na direção do movimento e estica com a velocidade
                    float speed = p.vel.magnitude;
                    if (speed > 0.01f) t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(p.vel.y, p.vel.x) * Mathf.Rad2Deg - 90f);
                    t.localScale = new Vector3(s, s * Mathf.Max(1f, p.stretch * Mathf.Min(speed, 30f) * 0.08f + 1f), 1);
                    c.a = k * p.alpha;
                    break;
                case Kind.Smoke:
                    t.localScale = new Vector3(s, s, 1);
                    c.a = Mathf.Min(1f, (1f - k) * 8f) * k * p.alpha;   // entra rápido, some devagar
                    break;
                default:
                    t.localScale = new Vector3(s, s, 1);
                    c.a = k * k * p.alpha;
                    break;
            }
            p.sr.color = c;
        }
    }

    // Reflexos seguem o objeto sempre deslocados para baixo na tela (mesmo com a rocha girando)
    void UpdateReflections()
    {
        for (int i = reflections.Count - 1; i >= 0; i--)
        {
            var (obj, refl, offset) = reflections[i];
            if (!obj) { reflections.RemoveAt(i); continue; }
            refl.position = obj.position + new Vector3(0, -offset, 0.01f);
        }
        var ship = Ship.transform;
        shipRefl.gameObject.SetActive(ship.gameObject.activeInHierarchy);
        shipRefl.transform.SetPositionAndRotation(ship.position + new Vector3(0, -0.7f, 0.01f), ship.rotation);
        shipRefl.transform.localScale = ship.lossyScale;
    }

    // Inclina e "achata" a nave ao trocar de pista, imitando uma rolagem 3D. bank: -1..1
    public void Bank(float bank)
    {
        // Gira em torno do centro da nave (o quad é deslocado para baixo por causa do jato)
        var rot = Quaternion.Euler(0, 0, -bank * 14f);
        Ship.transform.localRotation = rot;
        Ship.transform.localPosition = rot * new Vector3(0, ShipOffsetY, 0);
        Ship.transform.localScale = new Vector3(ShipWidth * (1f - Mathf.Abs(bank) * 0.25f), ShipHeight, 1f);
    }

    void Emit(Kind kind, Vector3 pos, Vector2 vel, Color c, float size, float life, float grow, float drag,
        float stretch = 0f, float alpha = 1f)
    {
        var p = pool[nextParticle];
        nextParticle = (nextParticle + 1) % pool.Count;
        p.kind = kind;
        p.sr.sharedMaterial = fxMats[(int)kind];
        p.sr.sortingOrder = kind == Kind.Smoke ? 9 : kind == Kind.Glow ? 10 : kind == Kind.Streak ? 11 : 12;
        p.sr.transform.position = pos;
        p.sr.transform.rotation = Quaternion.identity;
        p.vel = vel; p.color = c; p.size = size; p.grow = grow; p.drag = drag;
        p.stretch = stretch; p.alpha = alpha;
        p.life = p.maxLife = life;
        p.sr.gameObject.SetActive(true);
    }

    void Sparks(Vector3 pos, Color c, int count, float speed, float size, float life, float stretch = 1.5f)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Random.insideUnitCircle.normalized * speed * Random.Range(0.35f, 1f);
            Emit(Kind.Streak, pos, dir, c, size * Random.Range(0.6f, 1.2f), life * Random.Range(0.6f, 1f), 0f, 4f, stretch);
        }
    }

    // Coleta do cristal: clarão, anel, faíscas douradas esticadas e estrelinhas que sobem
    public void Pickup(Vector3 pos)
    {
        Emit(Kind.Glow, pos, Vector2.zero, new Color(1f, 0.9f, 0.6f), 3.2f, 0.2f, 1f, 0f);
        Emit(Kind.Ring, pos, Vector2.zero, Gold, 0.6f, 0.4f, 4.5f, 0f);
        Sparks(pos, Gold, 24, 12f, 0.22f, 0.4f, 2f);
        for (int i = 0; i < 8; i++)
            Emit(Kind.Glow, pos + (Vector3)(Random.insideUnitCircle * 0.5f), new Vector2(Random.Range(-1f, 1f), Random.Range(1.5f, 3.5f)),
                Gold, Random.Range(0.3f, 0.5f), Random.Range(0.5f, 0.8f), -0.15f, 1.5f);
    }

    // Troca de pista: "puff" do propulsor lateral, jogando faíscas para o lado oposto ao movimento
    public void LaneChange(int dir)
    {
        var sp = Ship.transform.parent.position + new Vector3(-dir * 0.55f, -0.25f, 0);
        Emit(Kind.Glow, sp, new Vector2(-dir * 2f, 0), Accent, 1.1f, 0.18f, 0.5f, 6f, 0f, 0.9f);
        for (int i = 0; i < 6; i++)
            Emit(Kind.Streak, sp, new Vector2(-dir * Random.Range(5f, 9f), Random.Range(-3f, 1f)), Accent,
                0.12f, Random.Range(0.15f, 0.25f), 0f, 6f, 1.6f, 1f);
    }

    // Explosão em camadas: clarão, bola de fogo, ondas de choque, faíscas, fumaça e estilhaços
    public void Explode(Vector3 pos)
    {
        Emit(Kind.Glow, pos, Vector2.zero, Color.white, 9f, 0.16f, 2f, 0f);                    // clarão
        Emit(Kind.Glow, pos, Vector2.zero, Fire, 5f, 0.6f, -2.5f, 0f);                         // bola de fogo
        Emit(Kind.Glow, pos, Vector2.zero, new Color(1f, 0.8f, 0.4f), 2.5f, 0.35f, -1.5f, 0f);  // núcleo quente
        Emit(Kind.Ring, pos, Vector2.zero, Fire, 1f, 0.5f, 12f, 0f);                           // onda de choque
        Emit(Kind.Ring, pos, Vector2.zero, Accent, 0.6f, 0.7f, 8f, 0f);
        Sparks(pos, Fire, 40, 24f, 0.22f, 0.7f, 2.5f);                                         // faíscas quentes
        Sparks(pos, Accent, 22, 17f, 0.16f, 0.6f, 2.5f);                                       // pedaços da nave
        for (int i = 0; i < 18; i++)                                                           // fumaça
        {
            var v = Random.insideUnitCircle * Random.Range(0.5f, 3f);
            Emit(Kind.Smoke, pos + (Vector3)(v * 0.2f), v, Smoke, Random.Range(1f, 1.6f), Random.Range(1.1f, 1.7f),
                Random.Range(2.5f, 4f), 1.2f, 0f, 0.9f);
        }
        for (int i = 0; i < 10; i++)                                                           // estilhaços escuros
        {
            var v = Random.insideUnitCircle.normalized * Random.Range(6f, 12f);
            Emit(Kind.Smoke, pos, v, new Color(0.05f, 0.04f, 0.05f), Random.Range(0.2f, 0.35f), Random.Range(0.6f, 0.9f),
                -0.08f, 2f, 0f, 1f);
        }
        for (int i = 0; i < 20; i++)                                                           // brasas
            Emit(Kind.Glow, pos, Random.insideUnitCircle * Random.Range(3f, 9f), Fire, Random.Range(0.25f, 0.45f),
                Random.Range(0.6f, 1.1f), -0.1f, 1.5f);
    }
}
