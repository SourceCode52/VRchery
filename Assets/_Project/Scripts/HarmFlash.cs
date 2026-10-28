using UnityEngine;

/// <summary>
/// A quick red tinge over the view when the archer is hurt (string slap, dry fire): it fades in almost at once and
/// back out within about half a second. Listens to Bow.Failed; any failure with harm above zero triggers it, and the
/// harm (0..1) sets how strong it gets. Uses the same screen-space quad as the focus vignette (made at runtime on the
/// main camera), drawn on top of it.
/// </summary>
public class HarmFlash : MonoBehaviour
{
    [SerializeField] Material material;                         // uses the VRchery/FocusVignette shader (the same material is fine)
    [SerializeField] Color color = new Color(0.85f, 0.02f, 0.02f, 0.6f);   // alpha = how strong the tinge is at full harm
    [SerializeField] float fadeInSeconds = 0.07f;
    [SerializeField] float fadeOutSeconds = 0.5f;
    [SerializeField] float innerRadius = -0.5f;                 // negative: the centre of the view is tinted a little too
    [SerializeField] float feather = 2f;                        // how gradually the tint grows towards the edges
    [SerializeField] int layer = 5;                             // the quad's layer (UI): the target camera leaves it out

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int InnerId = Shader.PropertyToID("_Inner");
    static readonly int FeatherId = Shader.PropertyToID("_Feather");
    static readonly int StrengthId = Shader.PropertyToID("_Strength");

    Material instance;
    Renderer quad;
    Mesh mesh;
    float peak, time = float.MaxValue;

    /// <summary>Current tint strength, 0..1 (before the colour's alpha).</summary>
    public float Level => time >= fadeInSeconds + fadeOutSeconds ? 0f : peak * Envelope(time);

    void OnEnable() => Bow.Failed += OnFailed;
    void OnDisable() => Bow.Failed -= OnFailed;

    void OnFailed(Bow bow, BowFailure kind, float harm)
    {
        if (harm <= 0f) return;
        Flash(harm);
    }

    /// <summary>Starts a flash (a stronger one replaces a weaker one still showing).</summary>
    public void Flash(float harm)
    {
        harm = Mathf.Clamp01(harm);
        if (harm < Level) return;
        peak = harm;
        time = 0f;
    }

    float Envelope(float t)
    {
        if (t < fadeInSeconds) return t / Mathf.Max(0.001f, fadeInSeconds);
        return 1f - Mathf.SmoothStep(0f, 1f, (t - fadeInSeconds) / Mathf.Max(0.001f, fadeOutSeconds));
    }

    void LateUpdate()
    {
        time += Time.deltaTime;
        float level = Level;
        if (level <= 0.001f)
        {
            if (quad != null) quad.enabled = false;
            return;
        }
        if (quad == null && !Build()) return;

        quad.enabled = true;
        instance.SetColor(ColorId, color);
        instance.SetFloat(InnerId, innerRadius);
        instance.SetFloat(FeatherId, feather);
        instance.SetFloat(StrengthId, level);
    }

    bool Build()
    {
        var cam = Camera.main;
        if (cam == null || material == null) return false;

        instance = new Material(material) { renderQueue = 4001 };   // after the focus vignette
        mesh = Instantiate(Resources.GetBuiltinResource<Mesh>("Quad.fbx"));
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);   // never culled: the shader ignores the transform

        var go = new GameObject("Harm Flash (runtime)") { layer = layer };
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = instance;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        quad = mr;
        return true;
    }

    void OnDestroy()
    {
        if (quad != null) Destroy(quad.gameObject);
        if (instance != null) Destroy(instance);
        if (mesh != null) Destroy(mesh);
    }
}
