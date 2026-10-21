using UnityEngine;

/// <summary>
/// The edges of the archer's vision darken as the string is drawn, so they can concentrate on the target.
/// It builds up with the draw length (Bow.AimFocus), is complete at the full draw length set on the bow, and fades
/// away slowly after the arrow is loosed or the draw is let go.
/// The dark oval is a full-screen quad parented to the main camera at runtime (nothing to add to the player).
/// </summary>
public class FocusVignette : MonoBehaviour
{
    [SerializeField] Material material;                 // uses the VRchery/FocusVignette shader
    [SerializeField] Color color = new Color(0f, 0f, 0f, 0.97f);   // alpha = how dark the edges get at full focus
    [SerializeField] float clearRadiusAtStart = 1.3f;  // the clear oval when it first appears (1 = edge of the view, 1.41 = corners)
    [SerializeField] float clearRadiusAtFull = 0.32f;   // the clear oval at full focus
    [SerializeField] float fadeDistanceAtFull = 0.3f;   // how softly it fades from clear to dark
    [SerializeField] float earlyEmphasis = 2f;          // >1: the oval closes in faster at the start, so it is noticed early in the draw
    [SerializeField] float buildSeconds = 0.25f;        // time to build from nothing to full: short, so it follows the draw closely
    [SerializeField] float fadeOutSeconds = 1.8f;       // time to fade from full to nothing after the shot
    [SerializeField] int layer = 5;                     // the quad's layer (UI): the target camera leaves it out

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int InnerId = Shader.PropertyToID("_Inner");
    static readonly int FeatherId = Shader.PropertyToID("_Feather");
    static readonly int StrengthId = Shader.PropertyToID("_Strength");

    Material instance;
    Renderer quad;
    Mesh mesh;
    float strength;

    /// <summary>Current vignette strength, 0..1.</summary>
    public float Strength => strength;

    void Update()
    {
        float target = 0f;
        foreach (var bow in Bow.All)
            if (bow.Drawing) target = Mathf.Max(target, bow.AimFocus);

        float seconds = target > strength ? buildSeconds : fadeOutSeconds;
        strength = Mathf.MoveTowards(strength, target, Time.deltaTime / Mathf.Max(0.05f, seconds));

        if (strength <= 0.001f)
        {
            if (quad != null) quad.enabled = false;
            return;
        }
        if (quad == null && !Build()) return;

        quad.enabled = true;
        instance.SetColor(ColorId, color);
        float eased = 1f - Mathf.Pow(1f - strength, Mathf.Max(1f, earlyEmphasis));
        instance.SetFloat(InnerId, Mathf.Lerp(clearRadiusAtStart, clearRadiusAtFull, eased));
        instance.SetFloat(FeatherId, Mathf.Lerp(0.35f, fadeDistanceAtFull, eased));
        instance.SetFloat(StrengthId, Mathf.Clamp01(eased * 1.5f));     // darkens a little ahead of closing in
    }

    bool Build()
    {
        var cam = Camera.main;
        if (cam == null || material == null) return false;

        instance = new Material(material);
        mesh = Instantiate(Resources.GetBuiltinResource<Mesh>("Quad.fbx"));
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);   // never culled: the shader ignores the transform

        var go = new GameObject("Focus Vignette (runtime)") { layer = layer };
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
