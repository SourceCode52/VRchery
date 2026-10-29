using UnityEngine;

public enum ArrowState
{
    InQuiver,   // resting inside the quiver tube (real physics: it fell in and leans on the walls)
    Held,       // in the drawing hand
    Nocked,     // on the string / shelf
    Loosed,     // shot
    Dropped     // let go away from the quiver and the bow
}

[RequireComponent(typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    [SerializeField] Transform tip;
    [SerializeField] float stickDepth = 0.06f;   // how far the tip sinks in
    [SerializeField] float minStickSpeed = 3f;   // slower than this and it just drops
    [SerializeField] float missLifetime = 20f;   // loose arrows are put away (or destroyed) after this

    [Header("Resting in the quiver")]
    [SerializeField] float tubeLinearDamping = 0.2f;
    [SerializeField] float tubeAngularDamping = 3f;   // settles instead of rattling

    public ArrowState State { get; private set; } = ArrowState.Loosed;   // an arrow placed in a scene on its own behaves like a loose one
    public Quiver Owner { get; set; }                                    // set by the Quiver that spawned it
    public bool Stuck { get; private set; }
    public float LooseTime { get; set; }                                 // seconds spent lying around after being dropped (the Quiver counts it)
    public Vector3 TipPosition => tip.position;

    /// <summary>A shot arrow has hit something for the first time: the arrow, whether it hit the target (or an arrow already in it), and where. Used for sound.</summary>
    public static event System.Action<Arrow, bool, Vector3> Landed;

    Rigidbody rb;
    Collider col;
    float flightLinearDamping, flightAngularDamping;
    Vector3 lastVelocity;
    bool flying;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        flightLinearDamping = rb.linearDamping;
        flightAngularDamping = rb.angularDamping;
    }

    /// <summary>InQuiver (physics, resting in the tube), Held or Nocked (carried: no physics, no collisions).</summary>
    public void SetState(ArrowState state)
    {
        State = state;
        CancelInvoke(nameof(Cleanup));
        flying = false;
        Stuck = false;

        if (state == ArrowState.Loosed || state == ArrowState.Dropped) return;

        bool carried = state == ArrowState.Held || state == ArrowState.Nocked;
        col.isTrigger = carried;                  // a carried arrow must not shove anything about

        if (carried)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }
        else   // InQuiver: ordinary physics
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.linearDamping = tubeLinearDamping;
            rb.angularDamping = tubeAngularDamping;
        }
    }

    /// <summary>Move the arrow instantly, whatever its state.</summary>
    public void Place(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        if (!rb.isKinematic) { rb.position = position; rb.rotation = rotation; }
    }

    void EnterFreeFlight()
    {
        transform.SetParent(null);
        col.isTrigger = false;
        rb.isKinematic = false;
        rb.linearDamping = flightLinearDamping;
        rb.angularDamping = flightAngularDamping;
        Stuck = false;
        CancelInvoke(nameof(Cleanup));
    }

    public void Launch(Vector3 velocity)
    {
        State = ArrowState.Loosed;
        EnterFreeFlight();
        rb.linearVelocity = velocity;
        rb.angularVelocity = Vector3.zero;
        flying = true;
        if (Owner == null) Invoke(nameof(Cleanup), missLifetime);   // a miss is put away after a while (with a quiver it stays until the ARROWS button brings it back)
    }

    /// <summary>Let go of the arrow: it falls.</summary>
    public void Drop(Vector3 velocity)
    {
        MarkLoose();
        EnterFreeFlight();
        rb.linearVelocity = velocity;
        flying = false;
        if (Owner == null) Invoke(nameof(Cleanup), missLifetime);   // with a quiver, the quiver brings it back
    }

    /// <summary>The arrow is lying around outside the quiver (dropped, or knocked out of the tube).</summary>
    public void MarkLoose()
    {
        State = ArrowState.Dropped;
        LooseTime = 0f;
    }

    void FixedUpdate()
    {
        if (!flying) return;
        lastVelocity = rb.linearVelocity;   // pre-collision velocity
        if (lastVelocity.sqrMagnitude > 0.1f)
            rb.MoveRotation(Quaternion.LookRotation(lastVelocity));   // nose follows the arc
    }

    void OnCollisionEnter(Collision c)
    {
        if (!flying) return;
        flying = false;

        bool onTarget = c.collider.GetComponentInParent<ArrowTarget>() != null;
        var other = c.collider.GetComponentInParent<Arrow>();
        if (other != null && other != this && other.Stuck) onTarget = true;   // landed on an arrow already in the target
        Landed?.Invoke(this, onTarget, c.GetContact(0).point);

        bool sticks = c.collider.GetComponentInParent<ArrowTarget>() != null
                   || c.collider.GetComponentInParent<ArrowStickSurface>() != null;
        if (sticks && lastVelocity.magnitude >= minStickSpeed)
            Stick(c);
        // otherwise physics takes over and Cleanup removes it later
    }

    void Stick(Collision c)
    {
        CancelInvoke(nameof(Cleanup));
        Stuck = true;
        Vector3 dir = lastVelocity.normalized;
        rb.isKinematic = true;
        transform.rotation = Quaternion.LookRotation(dir);
        Vector3 point = c.GetContact(0).point;
        var ray = new Ray(tip.position - dir * 0.5f, dir);   // where the line of flight meets the surface
        if (c.collider.Raycast(ray, out RaycastHit hit, 2f)) point = hit.point;
        transform.position += (point - tip.position) + dir * stickDepth;
    }

    void Cleanup()
    {
        if (Owner != null) Owner.Recycle(this);   // arrows belong to a quiver: they go back in
        else Destroy(gameObject);
    }

    [ContextMenu("Test Launch")]
    void TestLaunch() => Launch(transform.forward * 40f);
}
