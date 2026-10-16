using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    [SerializeField] Transform tip;
    [SerializeField] float stickDepth = 0.06f;   // how far the tip sinks in
    [SerializeField] float minStickSpeed = 3f;   // slower than this and it just drops
    [SerializeField] float missLifetime = 20f;   // clean up arrows that miss

    Rigidbody rb;
    Vector3 lastVelocity;
    bool flying;

    void Awake() => rb = GetComponent<Rigidbody>();

    public void Launch(Vector3 velocity)
    {
        transform.SetParent(null);
        rb.isKinematic = false;
        rb.linearVelocity = velocity;
        flying = true;
        Invoke(nameof(Cleanup), missLifetime);
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

        bool hitTarget = c.collider.GetComponentInParent<ArrowTarget>() != null;
        if (hitTarget && lastVelocity.magnitude >= minStickSpeed)
            Stick(c);
        // otherwise physics takes over and Cleanup removes it later
    }

    void Stick(Collision c)
    {
        CancelInvoke(nameof(Cleanup));
        Vector3 dir = lastVelocity.normalized;
        rb.isKinematic = true;
        transform.rotation = Quaternion.LookRotation(dir);
        transform.position += (c.GetContact(0).point - tip.position) + dir * stickDepth;
    }

    void Cleanup() => Destroy(gameObject);

    [ContextMenu("Test Launch")]
    void TestLaunch() => Launch(transform.forward * 40f);
}
