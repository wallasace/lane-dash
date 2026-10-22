using UnityEngine;

// Bloom da skin espacial: tudo o que é bem claro (neon, jatos, lava, cristais, estrelas) "vaza" luz.
// Leve para celular: começa em meia resolução e desfoca em cascata (dual Kawase), com
// compressão suave dos brilhos e vinheta na composição final. Shader: Resources/LDShaders/LD_Bloom.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class LDBloom : MonoBehaviour
{
    [Range(0f, 3f)] public float threshold = 1.2f;
    [Range(0f, 1f)] public float knee = 0.5f;
    [Range(0f, 4f)] public float intensity = 0.8f;
    [Range(0.5f, 2f)] public float exposure = 1.0f;
    [Range(0f, 2f)] public float saturation = 1.05f;
    [Range(0f, 1.5f)] public float vignette = 0.75f;
    public Color tint = Color.white;

    const int MaxLevels = 4;   // poucos níveis: brilho "apertado" em volta das luzes, sem névoa na tela toda
    readonly RenderTexture[] levels = new RenderTexture[MaxLevels];
    Material mat;

    void OnEnable()
    {
        var shader = Resources.Load<Shader>("LDShaders/LD_Bloom");
        if (shader && shader.isSupported) mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        GetComponent<Camera>().allowHDR = true;
    }

    void OnDisable()
    {
        if (mat) DestroyImmediate(mat);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!mat) { Graphics.Blit(src, dst); return; }

        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
            ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        mat.SetFloat("_Threshold", threshold);
        mat.SetFloat("_Knee", Mathf.Max(knee, 0.01f));
        mat.SetFloat("_Intensity", intensity);
        mat.SetFloat("_Exposure", exposure);
        mat.SetFloat("_Saturation", saturation);
        mat.SetFloat("_Vignette", vignette);
        mat.SetColor("_Tint", tint);

        int w = src.width / 2, h = src.height / 2;
        levels[0] = RenderTexture.GetTemporary(w, h, 0, format);
        Graphics.Blit(src, levels[0], mat, 0);

        int count = 1;
        for (; count < MaxLevels; count++)
        {
            w /= 2; h /= 2;
            if (w < 4 || h < 4) break;
            levels[count] = RenderTexture.GetTemporary(w, h, 0, format);
            Graphics.Blit(levels[count - 1], levels[count], mat, 1);
        }
        // Amplia de volta somando cada nível ao de cima (o passe 2 usa Blend One One)
        for (int i = count - 1; i > 0; i--)
            Graphics.Blit(levels[i], levels[i - 1], mat, 2);

        mat.SetTexture("_BloomTex", levels[0]);
        Graphics.Blit(src, dst, mat, 3);

        for (int i = 0; i < count; i++)
        {
            RenderTexture.ReleaseTemporary(levels[i]);
            levels[i] = null;
        }
    }
}
