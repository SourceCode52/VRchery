using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Shows the name of a bow part when it is picked up: a name tag with a line to the part, facing the player, that
/// stays for a few seconds and then goes away. Put it on each loose part and type its name.
/// Picking the part up again shows the tag again; clicking it into the bow (or hiding the part) removes the tag.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class PartLabel : MonoBehaviour
{
    [Tooltip("The name shown on the tag.")]
    [SerializeField] string labelText = "Part";

    [Tooltip("The tag itself (Prefabs/PartLabel).")]
    [SerializeField] PartLabelView labelPrefab;

    [Tooltip("How long the tag stays up after the part is picked up.")]
    [SerializeField] float showSeconds = 3f;

    [SerializeField] float popInTime = 0.12f;
    [SerializeField] float fadeOutTime = 0.25f;

    [Tooltip("How far (metres) the ring floats off the part's surface, towards the player, so it is not buried in it.")]
    [SerializeField] float ringLift = 0.012f;

    XRGrabInteractable grab;
    PartLabelView view;
    Collider[] colliders;
    Renderer[] renderers;
    float shownAt = -1f;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable() => grab.selectEntered.AddListener(OnSelected);

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnSelected);
        Hide();
    }

    void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }

    void OnSelected(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) Hide();   // clicked into the bow: the tag has done its job
        else Show();
    }

    void Show()
    {
        if (labelPrefab == null) return;
        if (view == null)
        {
            view = Instantiate(labelPrefab);          // not a child: it must not inherit the part's turning and scale
            view.name = "Label - " + labelText;
        }
        view.SetText(labelText);
        view.gameObject.SetActive(true);
        shownAt = Time.time;
        Place();
    }

    void Hide()
    {
        shownAt = -1f;
        if (view != null) view.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (shownAt < 0f || view == null) return;

        float age = Time.time - shownAt;
        if (age >= showSeconds) { Hide(); return; }

        Place();

        // pop in, hold, shrink away at the end
        float inK = popInTime <= 0f ? 1f : Mathf.Clamp01(age / popInTime);
        float outK = fadeOutTime <= 0f ? 1f : Mathf.Clamp01((showSeconds - age) / fadeOutTime);
        view.transform.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Min(inK, outK));
    }

    // the ring sits on the part's surface facing the player; the tag faces the player
    void Place()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 point = Centre();
        Vector3 toCam = cam.transform.position - point;
        float dist = toCam.magnitude;
        Vector3 dir = dist > 0.001f ? toCam / dist : -cam.transform.forward;

        // the surface point of the part nearest the player, along the line from the player to the part's middle
        var ray = new Ray(cam.transform.position, -dir);
        float best = float.MaxValue;
        foreach (var c in colliders)
        {
            if (c == null || !c.enabled) continue;
            if (c.Raycast(ray, out RaycastHit hit, dist + 2f) && hit.distance < best)
            {
                best = hit.distance;
                point = hit.point;
            }
        }

        view.transform.SetPositionAndRotation(
            point + dir * ringLift,
            Quaternion.LookRotation(-dir, Vector3.up));    // the tag is read looking along its +z
    }

    Vector3 Centre()
    {
        bool any = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled || r is LineRenderer || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b.center;
    }
}
