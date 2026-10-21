using UnityEngine;

/// <summary>
/// A target sight for a recurve bow, built the way it is for a right-handed archer: a mounting plate on the riser's
/// side that faces the archer's eye (the side away from the arrow), an extension bar pointing at the target, and at
/// its end a sliding block carrying a ring (aperture) with a pin dot in the middle.
///
/// Everything is laid out from the settings below, in metres in the bow's own space, so a sight is set up for a
/// particular archer in the Inspector, outside the simulation (the sight updates as you change a value). For a
/// left-handed archer it mirrors across the bow along with the arrow shelf.
///
/// Bow space: +y up the riser, +z toward the target, -z toward the archer (the string side). For a right-handed
/// archer the arrow lies on the -x side of the riser (the archer's left) and the eye is on the +x side (the archer's right).
/// </summary>
[ExecuteAlways]
public class BowSight : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] Transform arrowShelf;          // where the arrow lies (right-handed layout); the sight is positioned from this line

    [Header("Mount and extension (metres)")]
    [Tooltip("Height of the mounting plate up the riser from the grip pivot.")]
    [Range(0f, 0.2f)] [SerializeField] float mountHeight = 0.095f;
    [Tooltip("How far the extension bar reaches toward the target, beyond the mounting plate. Longer = more accurate, heavier out front.")]
    [Range(0.02f, 0.4f)] [SerializeField] float extensionLength = 0.12f;
    [Tooltip("Half the riser's width, so the plate sits flush on its side.")]
    [Range(0.005f, 0.05f)] [SerializeField] float riserHalfWidth = 0.015f;

    [Header("Aim point: set per archer (metres)")]
    [Tooltip("Elevation: how far above the arrow the ring's centre sits. Raise it if the arrows land high, lower it if they land low.")]
    [Range(-0.05f, 0.2f)] [SerializeField] float elevation = 0.10f;
    [Tooltip("Windage: how far sideways of the arrow the ring's centre sits, positive = toward the archer's eye side (right for a right-handed archer). Moves arrows left/right.")]
    [Range(-0.08f, 0.1f)] [SerializeField] float windage = 0f;

    [Header("Ring and pin (metres)")]
    [Range(0.01f, 0.05f)] [SerializeField] float ringDiameter = 0.025f;
    [Range(0.0005f, 0.004f)] [SerializeField] float ringThickness = 0.0015f;
    [Range(0.0005f, 0.006f)] [SerializeField] float pinDiameter = 0.002f;

    [Header("Parts (built in the scene)")]
    [SerializeField] Transform mountPlate;
    [SerializeField] Transform bar;
    [SerializeField] Transform block;
    [SerializeField] Transform windageRail;
    [SerializeField] Transform elevationColumn;
    [SerializeField] LineRenderer ring;
    [SerializeField] Transform pin;

    [Header("Editor")]
    [SerializeField] bool drawAimLine = true;

    const float PlateThickness = 0.006f;
    const float PlateHeight = 0.05f;
    const float PlateDepth = 0.04f;
    const float RodThickness = 0.007f;
    const int RingSegments = 40;

    Handedness shownSide = Handedness.Right;
    bool subscribed;

    /// <summary>Elevation in metres above the arrow.</summary>
    public float Elevation { get => elevation; set { elevation = value; Apply(); } }
    /// <summary>Windage in metres, positive = toward the eye side.</summary>
    public float Windage { get => windage; set { windage = value; Apply(); } }

    void OnEnable() { Apply(); TrySubscribe(); }
    void OnDisable() { Unsubscribe(); }
    void OnValidate() { Apply(); }

    void Update()
    {
        if (!Application.isPlaying) return;
        TrySubscribe();
    }

    void TrySubscribe()
    {
        if (subscribed || !Application.isPlaying || HandednessManager.Instance == null) return;
        HandednessManager.Instance.Changed += OnHandedness;
        subscribed = true;
        OnHandedness(HandednessManager.Instance.Current);
    }

    void Unsubscribe()
    {
        if (!subscribed) return;
        if (HandednessManager.Instance != null) HandednessManager.Instance.Changed -= OnHandedness;
        subscribed = false;
    }

    void OnHandedness(Handedness h)
    {
        shownSide = h;
        Apply();
    }

    [ContextMenu("Reset to defaults")]
    void ResetToDefaults()
    {
        mountHeight = 0.095f; extensionLength = 0.12f; riserHalfWidth = 0.015f;
        elevation = 0.10f; windage = 0f;
        ringDiameter = 0.025f; ringThickness = 0.0015f; pinDiameter = 0.002f;
        Apply();
    }

    /// <summary>Lays every part out from the settings.</summary>
    [ContextMenu("Apply")]
    public void Apply()
    {
        if (arrowShelf == null || mountPlate == null || bar == null || block == null ||
            windageRail == null || elevationColumn == null || ring == null || pin == null) return;

        // +1 = the layout as set up on the bow (right-handed); -1 = mirrored for left-handed
        float mirror = shownSide == Handedness.Right ? 1f : -1f;
        Vector3 shelf = arrowShelf.localPosition;
        float eyeSide = shelf.x < 0f ? 1f : -1f;           // the eye is on the opposite side of the riser to the arrow
        float dir = eyeSide * mirror;                      // bow-space x direction of the eye side

        // the mounting plate sits flush on the riser's eye-side face
        float plateX = dir * (riserHalfWidth + PlateThickness * 0.5f);
        float plateZ = 0f;
        Place(mountPlate, new Vector3(plateX, mountHeight, plateZ), new Vector3(PlateThickness, PlateHeight, PlateDepth));

        // the extension bar leaves the front of the plate toward the target (+z)
        float barStartZ = plateZ + PlateDepth * 0.5f;
        float barEndZ = barStartZ + extensionLength;
        Place(bar, new Vector3(plateX, mountHeight, (barStartZ + barEndZ) * 0.5f), new Vector3(RodThickness, RodThickness, extensionLength));

        // the sight block at the end of the bar
        float blockZ = barEndZ + RodThickness * 0.5f;
        Place(block, new Vector3(plateX, mountHeight, blockZ), new Vector3(0.016f, 0.016f, 0.016f));

        // where the ring's centre sits: relative to the arrow
        float aimX = shelf.x * mirror + dir * windage;
        float aimY = shelf.y + elevation;

        // windage rail (sideways from the block) and elevation column (up to the ring)
        float railLength = Mathf.Max(0.001f, Mathf.Abs(aimX - plateX));
        Place(windageRail, new Vector3((aimX + plateX) * 0.5f, mountHeight, blockZ), new Vector3(railLength, RodThickness, RodThickness));
        float colLength = Mathf.Max(0.001f, Mathf.Abs(aimY - mountHeight));
        Place(elevationColumn, new Vector3(aimX, (aimY + mountHeight) * 0.5f, blockZ), new Vector3(RodThickness, colLength, RodThickness));

        // ring and pin
        ring.transform.localPosition = new Vector3(aimX, aimY, blockZ);
        ring.transform.localRotation = Quaternion.identity;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.alignment = LineAlignment.View;
        ring.widthMultiplier = ringThickness;
        ring.numCornerVertices = 2;
        if (ring.positionCount != RingSegments) ring.positionCount = RingSegments;
        float r = ringDiameter * 0.5f;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
        Place(pin, new Vector3(aimX, aimY, blockZ), Vector3.one * pinDiameter);
    }

    void Place(Transform t, Vector3 localPosition, Vector3 localScale)
    {
        t.localPosition = localPosition;
        t.localRotation = Quaternion.identity;
        t.localScale = localScale;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawAimLine || ring == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Vector3 from = ring.transform.position;
        Gizmos.DrawLine(from, from + transform.TransformDirection(Vector3.forward) * 2f);   // toward the target
        Gizmos.DrawLine(from, from + transform.TransformDirection(Vector3.back) * 0.8f);    // back toward the archer's eye
    }
}
