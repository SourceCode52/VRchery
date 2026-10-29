using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Puts a loose part back where it started if it gets knocked away.
///
/// Whenever the part is away from its starting pose and nobody is holding it (no hand, no socket), a timer runs.
/// If it is still away and still not held after <see cref="returnDelay"/> seconds, it is teleported back to the
/// pose it had when the scene started, so the player can pick it up again from a comfortable position.
/// The timer restarts whenever the part is picked up, so a part is never taken from the player's hand.
/// A part that is socketed into the bow counts as held, so it is never returned.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class ReturnToStart : MonoBehaviour
{
    [Tooltip("Seconds the part must stay knocked away and un-held before it returns to where it started.")]
    [SerializeField] float returnDelay = 3f;

    [Header("What counts as 'knocked away'")]
    [Tooltip("Metres from the starting position beyond which the part is considered knocked away.")]
    [SerializeField] float movedDistance = 0.03f;

    [Tooltip("Degrees from the starting rotation beyond which the part is considered knocked away (e.g. tipped over).")]
    [SerializeField] float movedAngle = 20f;

    [Tooltip("The part may start a little above its table and drop onto it. For this many seconds after the scene starts " +
             "the 'start' pose follows the part until it has settled, so a part resting on the table is not counted as knocked away.")]
    [SerializeField] float settleTime = 2f;

    XRGrabInteractable grab;
    Rigidbody rb;
    Vector3 startPos;
    Quaternion startRot;
    float awayTime;
    float settleTimer;
    bool homeFixed;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void OnEnable()
    {
        awayTime = 0f;
        settleTimer = 0f;
        homeFixed = false;
    }

    void Update()
    {
        // While the part is still dropping onto its table, its resting place is its starting place.
        if (!homeFixed)
        {
            if (grab.isSelected)
            {
                homeFixed = true;   // picked up early: keep the last pose it rested in
            }
            else
            {
                startPos = transform.position;
                startRot = transform.rotation;
                settleTimer += Time.deltaTime;
                if (settleTimer >= settleTime) homeFixed = true;
                return;
            }
        }

        // Held by a hand or locked in a socket: never return, and start the wait again once it is let go.
        if (grab.isSelected || !IsAwayFromStart())
        {
            awayTime = 0f;
            return;
        }

        awayTime += Time.deltaTime;
        if (awayTime >= returnDelay)
            ReturnNow();
    }

    bool IsAwayFromStart()
    {
        return (transform.position - startPos).sqrMagnitude > movedDistance * movedDistance
               || Quaternion.Angle(transform.rotation, startRot) > movedAngle;
    }

    /// <summary>Teleports the part back to its starting pose straight away.</summary>
    public void ReturnNow()
    {
        awayTime = 0f;

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;      // Unity 6 name (was rb.velocity)
            rb.angularVelocity = Vector3.zero;
        }
        rb.position = startPos;
        rb.rotation = startRot;
        transform.SetPositionAndRotation(startPos, startRot);
    }
}
