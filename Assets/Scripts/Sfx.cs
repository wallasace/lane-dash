using System.Collections.Generic;
using UnityEngine;

// Efeitos sonoros sintetizados por código, sem arquivos de áudio.
// Cada som é montado em camadas (tons com envelope, ruído filtrado, "corpo" grave) e passa por
// um reverb curto, para soar mais encorpado que bipes de onda quadrada.
// Para trocar por arquivos .wav reais, basta substituir o clip no dicionário.
public static class Sfx
{
    const int Rate = 44100;

    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static AudioSource[] sources;
    static int next;

    public static bool Muted;

    public static void Init(GameObject host)
    {
        Muted = PlayerPrefs.GetInt("muted", 0) == 1;
        sources = new AudioSource[8];
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = host.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
            sources[i].ignoreListenerPause = true;
        }
        // Clips criados em runtime somem ao sair do Play: recria se estiverem inválidos
        if (clips.TryGetValue("click", out var existing) && existing != null) return;
        clips.Clear();

        clips["click"] = Click();
        clips["place"] = Place();
        clips["perfect"] = Chime(new[] { 880f, 1320f }, 0.5f, 0.08f);
        clips["coin"] = Chime(new[] { 1318.5f, 1975.5f, 2637f }, 0.55f, 0.045f);
        clips["bounce"] = Thunk();
        clips["move"] = Whoosh(0.22f, 900f, 3200f, 0.45f);
        clips["lose"] = Explosion();
        clips["record"] = Fanfare();
    }

    public static void Play(string name, float pitch = 1f, float volume = 1f)
    {
        if (Muted || sources == null || !clips.TryGetValue(name, out var clip) || clip == null) return;
        var s = sources[next];
        next = (next + 1) % sources.Length;
        s.pitch = pitch;
        s.volume = volume;
        s.clip = clip;
        s.Play();
    }

    // ---------- Sons ----------

    // Clique de UI: estalo curto e macio
    static AudioClip Click()
    {
        var b = Buf(0.06f);
        Tone(b, 0f, 0.05f, 1800f, 1400f, 0.25f, Sine, 0.001f, 6f);
        Noise(b, 0f, 0.015f, 0.12f, 6000f, 6000f, 0.0005f, 8f);
        return Make("click", b, 0f);
    }

    // Bloco encaixando (Stack Tower): baque grave com ataque de ruído
    static AudioClip Place()
    {
        var b = Buf(0.3f);
        Tone(b, 0f, 0.25f, 180f, 90f, 0.6f, Sine, 0.002f, 3f);
        Noise(b, 0f, 0.06f, 0.25f, 2500f, 600f, 0.001f, 4f);
        return Make("place", b, 0.15f);
    }

    // Carrilhão cristalino: parciais em arpejo rápido, com brilho e cauda de reverb
    static AudioClip Chime(float[] notes, float vol, float step)
    {
        var b = Buf(0.9f);
        for (int i = 0; i < notes.Length; i++)
        {
            float t0 = i * step;
            Tone(b, t0, 0.6f, notes[i], notes[i], vol * 0.6f, Sine, 0.002f, 2.5f);
            Tone(b, t0, 0.35f, notes[i] * 2.01f, notes[i] * 2.01f, vol * 0.18f, Sine, 0.002f, 4f);   // brilho
            Tone(b, t0, 0.2f, notes[i] * 3.98f, notes[i] * 3.98f, vol * 0.06f, Sine, 0.001f, 6f);
        }
        Noise(b, 0f, 0.03f, 0.08f, 9000f, 9000f, 0.001f, 6f);   // "tilim" do impacto
        return Make("chime", b, 0.35f);
    }

    // Batida na borda: "thunk" metálico grave com ressonância curta
    static AudioClip Thunk()
    {
        var b = Buf(0.35f);
        Tone(b, 0f, 0.3f, 150f, 95f, 0.6f, Sine, 0.001f, 3.5f);
        Tone(b, 0f, 0.2f, 523f, 500f, 0.18f, Triangle, 0.001f, 5f);    // ressonância metálica
        Tone(b, 0f, 0.15f, 1187f, 1150f, 0.07f, Sine, 0.001f, 6f);
        Noise(b, 0f, 0.04f, 0.3f, 3000f, 800f, 0.0005f, 5f);
        return Make("thunk", b, 0.12f);
    }

    // Whoosh: ruído filtrado com o corte varrendo (de f0 para f1), sobe e desce suave
    static AudioClip Whoosh(float dur, float f0, float f1, float vol)
    {
        var b = Buf(dur + 0.15f);
        Noise(b, 0f, dur, vol, f0, f1, dur * 0.4f, 1.2f);
        Tone(b, 0f, dur, f0 * 0.12f, f1 * 0.12f, vol * 0.12f, Sine, dur * 0.3f, 2f);   // corpo
        return Make("whoosh", b, 0.1f);
    }

    // Explosão: estrondo grave descendo, ruído largo que vai fechando e estalos
    static AudioClip Explosion()
    {
        var b = Buf(1.6f);
        Tone(b, 0f, 1.2f, 110f, 32f, 0.75f, Sine, 0.002f, 2.2f);           // sub
        Tone(b, 0f, 0.5f, 220f, 60f, 0.25f, Triangle, 0.002f, 3f);
        Noise(b, 0f, 1.3f, 0.6f, 5000f, 300f, 0.002f, 1.8f);                // corpo da explosão
        var rng = new System.Random(7);
        for (int i = 0; i < 14; i++)                                         // estalos (detritos)
        {
            float t0 = 0.05f + (float)rng.NextDouble() * 0.7f;
            Noise(b, t0, 0.03f, 0.18f * (1f - t0), 7000f, 3000f, 0.0005f, 8f);
        }
        SoftClip(b, 1.4f);
        return Make("explosion", b, 0.3f);
    }

    // Fanfarra de recorde: arpejo maior com timbre suave (seno + triângulo) e reverb
    static AudioClip Fanfare()
    {
        var b = Buf(1.4f);
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < notes.Length; i++)
        {
            float t0 = i * 0.09f;
            float len = i == notes.Length - 1 ? 0.8f : 0.25f;
            Tone(b, t0, len, notes[i], notes[i], 0.3f, Sine, 0.004f, 2f);
            Tone(b, t0, len, notes[i], notes[i] * 1.003f, 0.12f, Triangle, 0.004f, 2.5f);
            Tone(b, t0, len * 0.6f, notes[i] * 2f, notes[i] * 2f, 0.06f, Sine, 0.004f, 4f);
        }
        return Make("record", b, 0.4f);
    }

    // ---------- Síntese ----------

    delegate float Osc(float phase);
    static float Sine(float p) => Mathf.Sin(p * 2f * Mathf.PI);
    static float Triangle(float p) => 4f * Mathf.Abs(p - Mathf.Floor(p + 0.5f)) - 1f;

    static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];

    // Tom com varredura de frequência e envelope ataque + decaimento exponencial
    static void Tone(float[] b, float start, float dur, float f0, float f1, float vol, Osc osc, float attack, float decay)
    {
        int s0 = (int)(start * Rate), len = (int)(dur * Rate);
        double phase = 0;
        for (int i = 0; i < len && s0 + i < b.Length; i++)
        {
            float k = (float)i / len;
            float t = (float)i / Rate;
            phase += Mathf.Lerp(f0, f1, k) / Rate;
            float env = Mathf.Min(1f, t / Mathf.Max(attack, 1e-4f)) * Mathf.Exp(-k * decay) * (1f - k * k);
            b[s0 + i] += osc((float)(phase % 1.0)) * env * vol;
        }
    }

    // Ruído branco passando por um filtro passa-baixa de 2 polos com corte variando de c0 para c1
    static void Noise(float[] b, float start, float dur, float vol, float c0, float c1, float attack, float decay)
    {
        int s0 = (int)(start * Rate), len = (int)(dur * Rate);
        var rng = new System.Random(s0 + len);
        float y1 = 0f, y2 = 0f;
        for (int i = 0; i < len && s0 + i < b.Length; i++)
        {
            float k = (float)i / len;
            float t = (float)i / Rate;
            float cut = Mathf.Lerp(c0, c1, k);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cut / Rate);
            float x = (float)rng.NextDouble() * 2f - 1f;
            y1 += a * (x - y1);
            y2 += a * (y1 - y2);
            float env = Mathf.Min(1f, t / Mathf.Max(attack, 1e-4f)) * Mathf.Exp(-k * decay) * (1f - k * k);
            b[s0 + i] += y2 * env * vol * 2f;
        }
    }

    // Saturação suave: segura picos e encorpa (usada na explosão)
    static void SoftClip(float[] b, float drive)
    {
        for (int i = 0; i < b.Length; i++) b[i] = (float)System.Math.Tanh(b[i] * drive) / drive * 1.1f;
    }

    // Reverb curto (filtros comb em paralelo) misturado ao som seco; normaliza o pico
    static AudioClip Make(string name, float[] dry, float wet)
    {
        var outBuf = (float[])dry.Clone();
        if (wet > 0f)
        {
            int[] delays = { 1116, 1188, 1277, 1356 };
            foreach (int d in delays)
            {
                var line = new float[d];
                int idx = 0;
                for (int i = 0; i < dry.Length; i++)
                {
                    float y = line[idx];
                    line[idx] = dry[i] + y * 0.72f;
                    idx = (idx + 1) % d;
                    outBuf[i] += y * wet * 0.25f;
                }
            }
        }
        float peak = 0f;
        foreach (var v in outBuf) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > 0.95f) for (int i = 0; i < outBuf.Length; i++) outBuf[i] *= 0.95f / peak;

        var clip = AudioClip.Create(name, outBuf.Length, 1, Rate, false);
        clip.SetData(outBuf, 0);
        return clip;
    }
}
