using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hollow tube that rises out of the ground when the archer enters the shooting state, at a spot that
/// depends on handedness (two markers in the scene). The arrows are real physics objects: they are dropped
/// in from above, one after another, and settle against the tube walls with their nock ends poking out.
/// Squeeze grip OR trigger with the drawing hand near the exposed end of an arrow to pull it out
/// (by the shaft or by the nock). The arrow is held in the fingers, pointing the way the hand points.
/// Bring its nock to the string's nocking point (on a bow held in the other hand) and it snaps on.
/// Let go over the tube mouth and it falls back in; let go anywhere else and it drops.
/// An arrow lying on the ground can be picked up again the same way (anywhere along the shaft).
/// Any arrow left lying around (dropped, or knocked out of the tube) drops back into the tube after a few seconds,
/// and a few seconds after the last arrow has gone, every arrow does.
/// The tube is hidden outside the shooting state.
/// </summary>
[DefaultExecutionOrder(20)]   // after HandInput (10), so this frame's grip/trigger values are current
public class Quiver : MonoBehaviour
{
    public static Quiver Instance { get; private set; }

    enum Phase { Hidden, Waiting, Rising, Ready }

    [Header("Arrows")]
    [SerializeField] Arrow arrowPrefab;
    [SerializeField] int arrowCount = 3;
    [SerializeField] float arrowLength = 0.8128f;
    [SerializeField] float returnDelay = 5f;          // seconds after the last arrow is gone, then all of them drop back in
    [SerializeField] float looseReturnDelay = 5f;     // an arrow lying around outside the tube drops back in after this long

    [Header("Tube (built by the QuiverTube component on it)")]
    [SerializeField] Transform tube;
    [SerializeField] float tubeHeight = 0.6f;         // only used if the tube has no QuiverTube component
    [SerializeField] float appearDelay = 0.3f;        // wait for the teleport to land
    [SerializeField] float riseTime = 0.7f;           // seconds to come up out of the ground

    [Header("Where it appears (move these two markers in the scene)")]
    [SerializeField] Transform rightHandedSpot;
    [SerializeField] Transform leftHandedSpot;

    [Header("Picking up")]
    [SerializeField] float pickRadius = 0.12f;        // hand to the exposed part of an arrow (or any part of one on the ground)
    [SerializeField] float maxExposed = 0.35f;        // longest stretch of arrow considered grabbable above the rim
    [SerializeField] float returnRadius = 0.15f;      // let go this close to the tube mouth: arrow falls back in
    [SerializeField] Vector3 gripPointInHand = new Vector3(0f, 0f, -0.03f);   // controller space
    [SerializeField] float pressAt = 0.6f;
    [SerializeField] float releaseAt = 0.4f;

    public event Action RoundEnded;                   // all arrows shot; fires just before they drop back in

    public bool Holding => held != null;
    public int ArrowsLeft => arrows == null ? 0 : CountAvailable();
    public float RefillIn => refillTimer;             // seconds until the arrows come back, or -1

    Arrow[] arrows;
    QuiverTube shape;
    Collider[] playerColliders;
    readonly List<Arrow> pending = new List<Arrow>();   // waiting their turn to fall into the tube
    Arrow held;
    HandInput heldHand;
    Vector3 heldPos;
    Quaternion heldRot = Quaternion.identity;
    Phase phase = Phase.Hidden;
    float phaseTime, refillTimer = -1f;
    bool squeezing;
    Vector3 gripPrev, handVelocity;

    float Height => shape != null ? shape.Height : tubeHeight;
    float InnerRadius => shape != null ? shape.InnerRadius : 0.05f;
    float OuterRadius => shape != null ? shape.OuterRadius : 0.07f;
    Vector3 Mouth => transform.position + Vector3.up * Height;

    void Awake()
    {
        Instance = this;
        if (arrowPrefab == null || arrowPrefab.GetComponent<Collider>() == null || tube == null ||
            rightHandedSpot == null || leftHandedSpot == null || arrowCount < 1)
        {
            Debug.LogError("Quiver: set the Arrow prefab (its root needs a Collider), the Tube, both spot markers and an arrow count.", this);
            enabled = false;
            return;
        }

        shape = tube.GetComponent<QuiverTube>();
        tube.gameObject.SetActive(false);
        arrows = new Arrow[arrowCount];
        for (int i = 0; i < arrowCount; i++)
        {
            var a = Instantiate(arrowPrefab);
            a.name = "Arrow " + (i + 1);
            a.Owner = this;
            a.gameObject.SetActive(false);
            arrows[i] = a;
        }
    }

    void Start()
    {
        if (!enabled) return;
        playerColliders = HandednessManager.Instance.GetComponentsInChildren<Collider>(true);

        var range = RangeStateController.Instance;
        if (range != null)
        {
            range.StateChanged += OnStateChanged;
            if (range.State == RangeState.Shooting) OnStateChanged(RangeState.Shooting);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (RangeStateController.Instance != null) RangeStateController.Instance.StateChanged -= OnStateChanged;
    }

    void OnStateChanged(RangeState state)
    {
        ClearHeld();
        refillTimer = -1f;
        pending.Clear();
        foreach (var a in arrows) Park(a);          // hidden and idle until the tube is up
        tube.gameObject.SetActive(false);
        phase = Phase.Hidden;
        if (state != RangeState.Shooting) return;

        var spot = HandednessManager.Instance.Current == Handedness.Right ? rightHandedSpot : leftHandedSpot;
        transform.SetPositionAndRotation(spot.position, Quaternion.Euler(0f, spot.eulerAngles.y, 0f));
        tube.localPosition = Vector3.down * Height;   // buried
        tube.localRotation = Quaternion.identity;
        phase = Phase.Waiting;
        phaseTime = Time.time + appearDelay;
    }

    void LateUpdate()
    {
        var range = RangeStateController.Instance;
        if (range == null || range.State != RangeState.Shooting) return;

        switch (phase)
        {
            case Phase.Waiting:
                if (Time.time >= phaseTime)
                {
                    tube.gameObject.SetActive(true);
                    phase = Phase.Rising;
                    phaseTime = Time.time;
                }
                break;

            case Phase.Rising:
                float p = riseTime <= 0f ? 1f : Mathf.Clamp01((Time.time - phaseTime) / riseTime);
                tube.localPosition = Vector3.down * (Height * (1f - Mathf.SmoothStep(0f, 1f, p)));
                if (p >= 1f)
                {
                    tube.localPosition = Vector3.zero;
                    phase = Phase.Ready;
                    foreach (var a in arrows) Enqueue(a);
                }
                break;

            case Phase.Ready:
                FeedPending();
                HandleHand();
                WatchArrows();
                HandleRound();
                break;
        }
    }

    // ---------------------------------------------------------------- the drawing hand
    void HandleHand()
    {
        var hands = HandednessManager.Instance;
        HandInput hi = hands.DrawHandInput;
        Transform hand = hands.DrawHand;

        float s = Mathf.Max(hi.Grip, hi.Trigger);                 // grip OR trigger
        bool down = squeezing ? s > releaseAt : s > pressAt;      // hysteresis
        bool pressedNow = down && !squeezing;
        bool releasedNow = !down && squeezing;
        squeezing = down;

        Vector3 grip = hand.TransformPoint(gripPointInHand);
        handVelocity = Vector3.ClampMagnitude((grip - gripPrev) / Mathf.Max(Time.deltaTime, 0.0001f), 6f);
        gripPrev = grip;

        if (held == null)
        {
            if (pressedNow && !AnyHeldOrNocked()) TryPick(hi, hand, grip);
            return;
        }

        if (releasedNow || heldHand != hi) { Release(); return; }

        held.transform.SetPositionAndRotation(hand.TransformPoint(heldPos), hand.rotation * heldRot);

        foreach (var bow in Bow.All)
        {
            if (!bow.TryNock(held, hand)) continue;
            ClearHeld();
            hi.Haptic(0.5f, 0.08f);
            return;
        }
    }

    void TryPick(HandInput hi, Transform hand, Vector3 grip)
    {
        Arrow best = null;
        float bestDist = pickRadius;
        float mouthY = Mouth.y;
        foreach (var a in arrows)
        {
            if (!a.gameObject.activeSelf) continue;
            Vector3 nockEnd = a.transform.position;
            float d;
            if (a.State == ArrowState.InQuiver)                      // in the tube: only the part sticking out
            {
                if (nockEnd.y < mouthY) continue;
                Vector3 dir = a.transform.forward;
                float exposed = dir.y < -0.05f ? Mathf.Min((nockEnd.y - mouthY) / -dir.y, maxExposed) : maxExposed;
                d = DistanceToSegment(grip, nockEnd, nockEnd + dir * exposed);
            }
            else if (a.State == ArrowState.Dropped)                  // lying around: anywhere along the shaft
                d = DistanceToSegment(grip, nockEnd, a.TipPosition);
            else continue;
            if (d < bestDist) { best = a; bestDist = d; }
        }
        if (best == null) return;

        // how far down the shaft from the nock it was grabbed
        float reach = best.State == ArrowState.Dropped ? arrowLength : maxExposed;
        float t = Mathf.Clamp(Vector3.Dot(grip - best.transform.position, best.transform.forward), 0f, reach);

        best.SetState(ArrowState.Held);

        // it comes out held in the fingers: tip along the hand's forward, the grabbed point in the grip
        Quaternion rot = hand.rotation;
        Vector3 pos = grip - rot * Vector3.forward * t;
        best.transform.SetPositionAndRotation(pos, rot);

        heldPos = hand.InverseTransformPoint(pos);
        heldRot = Quaternion.Inverse(hand.rotation) * rot;
        held = best;
        heldHand = hi;
        hi.HoldingArrow = true;
        hi.Haptic(0.3f, 0.05f);
    }

    void Release()
    {
        var a = held;
        var hi = heldHand;
        ClearHeld();

        float near = Mathf.Min(Vector3.Distance(a.TipPosition, Mouth), Vector3.Distance(a.transform.position, Mouth));
        if (near < returnRadius)
        {
            DropIn(a, Array.IndexOf(arrows, a));
            if (hi != null) hi.Haptic(0.2f, 0.04f);
        }
        else a.Drop(handVelocity);
    }

    void ClearHeld()
    {
        if (heldHand != null) heldHand.HoldingArrow = false;
        held = null;
        heldHand = null;
    }

    // ---------------------------------------------------------------- watching the arrows
    void WatchArrows()
    {
        Vector3 axis = transform.position;
        foreach (var a in arrows)
        {
            if (!a.gameObject.activeSelf) continue;

            if (a.State == ArrowState.InQuiver)
            {
                // knocked out of the tube (tip outside the wall), or fell through the world: it is loose now
                Vector3 tip = a.TipPosition;
                float outside = new Vector2(tip.x - axis.x, tip.z - axis.z).magnitude;
                if (outside > OuterRadius + 0.03f || tip.y < axis.y - 1f) a.MarkLoose();
            }
            else if (a.State == ArrowState.Dropped)
            {
                a.LooseTime += Time.deltaTime;
                if (a.LooseTime >= looseReturnDelay) Enqueue(a);   // nobody picked it up: it goes back in the tube
            }
        }
    }

    // ---------------------------------------------------------------- the round
    void HandleRound()
    {
        if (refillTimer < 0f)
        {
            if (CountAvailable() == 0) refillTimer = returnDelay;   // nothing left to shoot
            return;
        }

        refillTimer -= Time.deltaTime;
        if (refillTimer <= 0f)
        {
            Debug.Log("Quiver: round over - the arrows are dropping back into the tube.");
            RoundEnded?.Invoke();
            ResetAll();
        }
    }

    /// <summary>Every arrow that is not already in the tube drops back in.</summary>
    public void ResetAll()
    {
        if (arrows == null) return;
        ClearHeld();
        foreach (var a in arrows)
            if (a.State != ArrowState.InQuiver || !a.gameObject.activeSelf) Enqueue(a);
        refillTimer = -1f;
    }

    /// <summary>Put one arrow back into the tube (used by the bow and the arrow's own clean-up).</summary>
    public void Recycle(Arrow a)
    {
        if (Array.IndexOf(arrows, a) < 0) { Destroy(a.gameObject); return; }
        if (a == held) ClearHeld();
        Enqueue(a);
    }

    // Arrows disappear, then fall into the tube together.
    void Enqueue(Arrow a)
    {
        if (pending.Contains(a)) return;
        Park(a);
        pending.Add(a);
    }

    // every waiting arrow falls in at the same moment (each into its own corner of the tube)
    void FeedPending()
    {
        while (pending.Count > 0)
        {
            var a = pending[0];
            pending.RemoveAt(0);
            DropIn(a, Array.IndexOf(arrows, a));
        }
    }

    // Each arrow has its own corner of the tube (120 degrees apart), starts almost upright just over the mouth,
    // leaning slightly so it settles against the wall, and simply falls in.
    void DropIn(Arrow a, int slot)
    {
        if (slot < 0) slot = 0;
        pending.Remove(a);
        a.gameObject.SetActive(true);
        IgnoreOthers(a);

        float angle = (90f + slot * 120f) * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        float lean = 1.5f * Mathf.Deg2Rad;
        Vector3 dir = (Vector3.down * Mathf.Cos(lean) - radial * Mathf.Sin(lean)).normalized;   // tip down, nock end leaning outward
        Vector3 tipPos = Mouth + radial * Mathf.Min(0.022f, InnerRadius * 0.5f) + Vector3.up * 0.05f;

        a.SetState(ArrowState.InQuiver);
        a.Place(tipPos - dir * arrowLength, Quaternion.LookRotation(dir, Vector3.forward));   // arrow pivot is its nock end
    }

    void Park(Arrow a)
    {
        a.SetState(ArrowState.InQuiver);
        a.gameObject.SetActive(false);
    }

    // arrows never collide with the player, or with a bow (swinging it must not knock arrows about)
    void IgnoreOthers(Arrow a)
    {
        var col = a.GetComponent<Collider>();
        foreach (var c in playerColliders)
            if (c != null) Physics.IgnoreCollision(col, c);
        foreach (var bow in Bow.All)
            foreach (var c in bow.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(col, c);
    }

    // ---------------------------------------------------------------- helpers
    // arrows that can still be shot: in the tube (or about to fall into it), in the hand, or on the string
    int CountAvailable()
    {
        int n = 0;
        foreach (var a in arrows)
            if (a.State == ArrowState.InQuiver || a.State == ArrowState.Held || a.State == ArrowState.Nocked) n++;
        return n;
    }

    bool AnyHeldOrNocked()
    {
        foreach (var a in arrows)
            if (a.State == ArrowState.Held || a.State == ArrowState.Nocked) return true;
        return false;
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector3.Distance(p, a + ab * t);
    }

    void OnDrawGizmos()
    {
        DrawSpot(rightHandedSpot, new Color(0.2f, 0.8f, 0.3f));
        DrawSpot(leftHandedSpot, new Color(0.9f, 0.6f, 0.1f));
    }

    void DrawSpot(Transform spot, Color color)
    {
        if (spot == null) return;
        var s = tube != null ? tube.GetComponent<QuiverTube>() : null;
        float h = s != null ? s.Height : tubeHeight;
        float r = s != null ? s.OuterRadius : 0.07f;
        Gizmos.color = color;
        const int n = 20;
        Vector3 c = spot.position;
        for (int i = 0; i < n; i++)
        {
            float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
            Vector3 p0 = c + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
            Vector3 p1 = c + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p0 + Vector3.up * h, p1 + Vector3.up * h);
            if (i % 5 == 0) Gizmos.DrawLine(p0, p0 + Vector3.up * h);
        }
    }
}
