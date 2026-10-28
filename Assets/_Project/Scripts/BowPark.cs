using UnityEngine;

/// <summary>
/// Keeps a bow parked at a marker (for example on the side of the table) until someone first picks it up, so it
/// does not lie on the floor (or fall over) while it waits. The bow stays a normal physics object, so once it is
/// picked up and let go it behaves as usual. Move the marker in the scene to change where the bow is parked.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BowPark : MonoBehaviour
{
    [SerializeField] Transform mount;           // where the bow waits: its position and rotation are the bow's
    [SerializeField] BowGrabInteractable grab;  // used to tell when it has been picked up (found on this object if empty)

    Rigidbody body;
    bool parked = true;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (grab == null) grab = GetComponent<BowGrabInteractable>();
    }

    void Start() => Park();

    /// <summary>Puts the bow back on its mount and holds it there until it is next picked up.</summary>
    [ContextMenu("Park now")]
    public void Park()
    {
        if (mount == null) return;
        parked = true;
        Hold();
    }

    void FixedUpdate()
    {
        if (!parked) return;
        if (grab != null && grab.isSelected) { parked = false; return; }
        Hold();
    }

    void Hold()
    {
        if (mount == null) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = mount.position;
        body.rotation = mount.rotation;
        transform.SetPositionAndRotation(mount.position, mount.rotation);
    }
}
