using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stand-in bow. The string is flat until the drawing hand pulls it; how far it goes is up to the player,
/// limited only by the arrow: the tip can't be drawn back past the shelf.
/// An arrow is nocked by bringing it (from the Quiver) to the nocking point of a bow held in the other hand;
/// it then lies on the shelf (above the grip) and slides along a fixed line parallel to the bow's forward axis.
/// Drawing uses the TRIGGER of the drawing hand (index, middle, ring hook the string).
/// </summary>
/// <summary>Things that go wrong while shooting (see Bow.Failed).</summary>
public enum BowFailure
{
    StringSlap,       // the released string hit the bow arm (harms the archer)
    DryFire,          // the string was let go with no arrow on it (harms the archer)
    ArrowFellOffRest, // the bow was tilted/pointed down too far and the nocked arrow slid off
    NockPoppedOff,    // the string was pulled sideways and the nock came off it
    Overdraw,         // drawn past the arrow's length: the point passed the rest and the arrow fell off
    BowDropped        // the bow was let go with an arrow on it
}

[RequireComponent(typeof(BowGrabInteractable))]
public class Bow : MonoBehaviour
{
    static readonly List<Bow> all = new List<Bow>();
    public static IReadOnlyList<Bow> All => all;

    [Header("Parts")]
    [SerializeField] Transform gripPoint;
    [SerializeField] Transform arrowShelf;      // where the arrow shaft lies; set up for a right-handed shooter (mirrored for left)
    [SerializeField] Transform upperTip;
    [SerializeField] Transform lowerTip;
    [SerializeField] LineRenderer stringLine;
    [SerializeField] Arrow arrowPrefab;         // only used when there is no Quiver in the scene (infinite arrows)

    [Header("Arrow")]
    [SerializeField] float arrowLength = 0.8128f;     // 32 inch
    [SerializeField] float shelfClearance = 0.04f;    // arrow tip must stay this far in front of the shelf

    [Header("Nocking")]
    [SerializeField] float nockRadius = 0.10f;        // arrow's nock end this close to the nocking point
    [SerializeField] float nockAngle = 60f;           // and the arrow within this angle of the bow's forward

    [Header("Draw")]
    [SerializeField] float pickRadius = 0.15f;        // how close the hand must be to the string
    [SerializeField] float slipDistance = 0.3f;       // pulled this far past the arrow's limit: string slips, no shot
    [SerializeField] float maxLateral = 0.25f;        // hand further than this from the arrow's line: string slips, no shot
    [SerializeField] float minDraw = 0.08f;           // shorter pulls don't fire
    [SerializeField] float fullDraw = 0.55f;          // full draw (32 inch arrow, ~8 inch brace height): the draw the speed range is calibrated to
    [SerializeField] float triggerOn = 0.6f;
    [SerializeField] float triggerOff = 0.3f;
    [SerializeField] float minSpeed = 5f;
    [SerializeField] float maxSpeed = 45f;

    [Header("Focus (how far into the aiming state the archer is, from the draw length)")]
    [SerializeField] float focusStartLength = 0.33f;  // draw length in metres (archery measure, see DrawLength) where the focus begins to build (13 in)
    [SerializeField] float focusFullLength = 0.66f;   // and where it is complete: fully darkened from here on (26 in)

    [Header("Steady aim - the bow is held steadier the closer to full focus (smooths hand tremor)")]
    [SerializeField] float steadyRotationSmoothing = 3f;     // follow speed of the bow's rotation at full focus (lower = steadier)
    [SerializeField] float steadyPositionSmoothing = 10f;    // same for its position
    [SerializeField] float looseSmoothing = 30f;             // follow speed when not steadied (high = practically no smoothing)

    [Header("Limb flex (optional: the limb pivots; the string tips should be children of them)")]
    [SerializeField] Transform upperLimb;
    [SerializeField] Transform lowerLimb;
    [SerializeField] float maxFlexDegrees = 8f;

    [Header("Failure states")]
    [SerializeField] bool logFailures = true;              // write each failure (and each string release) to the Console
    [SerializeField] float nockPopLateral = 0.20f;         // string pulled this far sideways/up/down from the arrow's line: the nock pops off the string
    [SerializeField] float overdrawGrace = 0.03f;          // drawn this far past the arrow's limit: the arrow falls off the rest
    [SerializeField] float maxRestCant = 50f;              // degrees of cant (bow rolled sideways) a nocked arrow stays on the rest at
    [SerializeField] float maxRestPitchDown = 60f;         // degrees the bow can point downwards before a nocked arrow slides off
    [SerializeField] float restFallDelay = 0.2f;           // seconds beyond those limits before the arrow actually falls
    [SerializeField] float dryFireHarmDraw = 0.2f;         // letting go of a bare string drawn at least this far (metres) hurts
    [Header("String slap (the bow arm is estimated: the hand, back along the forearm towards the shoulder)")]
    [SerializeField] float shoulderDrop = 0.24f;           // shoulder this far below the eyes
    [SerializeField] float shoulderReach = 0.10f;          // and this far towards the bow hand
    [SerializeField, Range(0f, 1f)] float forearmFollowsBow = 0.5f;   // 1 = forearm runs straight back along the bow, 0 = straight towards the shoulder
    [SerializeField] float armLength = 0.40f;              // hand to elbow
    [SerializeField] float armRadius = 0.03f;              // half the thickness of the forearm
    [SerializeField] float elbowOutward = 0.06f;           // how far the elbow is assumed to be turned out, away from the string (a trained archer does this); 0 = a beginner's straight arm
    [SerializeField] float stringRadius = 0.005f;

    BowGrabInteractable grab;
    Arrow nocked;
    bool drawing, wasTriggerDown;
    enum DrawEnd { Shot, Slipped, Cancelled }   // let go of the string / it slipped off the fingers / the draw was called off
    Vector3 lastPos, velocity;
    float badRestTime, restHalfSpan;
    Vector3 aim = Vector3.forward;
    float draw;
    Vector3 restLocal;                       // the flat string's centre, measured before any limb flexes
    float anchoredTime, steady;
    Quaternion upperRestRot, lowerRestRot;
    bool grabSettingsSaved, savedSmoothPos, savedSmoothRot;
    float savedPosAmount, savedRotAmount;

    public static event System.Action<Bow> Loosed;                   // an arrow has just been shot
    /// <summary>Something went wrong: the failure and how much it hurts the archer (0 = not at all .. 1 = badly). The red screen tint listens to this.</summary>
    public static event System.Action<Bow, BowFailure, float> Failed;

    public bool IsHeld => grab != null && grab.isSelected;
    public bool HeldByOffHand => IsHeld && HeldByOtherHand(HandednessManager.Instance.DrawHand);   // the right way round: bow in the non-drawing hand
    public bool Drawing => drawing;
    /// <summary>0..1: the build-up of focus while the string is drawn (0 when not drawing). Drives the vignette and the steadiness.</summary>
    public float AimFocus => drawing ? Mathf.InverseLerp(focusStartLength, focusFullLength, DrawLength) : 0f;
    /// <summary>Drawn far enough for full focus.</summary>
    public bool Anchored => drawing && DrawLength >= focusFullLength;
    /// <summary>Archery draw length in metres (AMO): from the nock back to the grip, plus 0.0445 m. Only meaningful while drawing.</summary>
    public float DrawLength => arrowShelf.localPosition.z - restLocal.z + draw + 0.0445f;
    /// <summary>How long (seconds) the string has been held at full draw.</summary>
    public float AnchoredTime => anchoredTime;
    /// <summary>How far the bow is rolled sideways about its aiming axis, in degrees (0 = upright, positive = the top leans left as the archer sees it).</summary>
    public float Cant
    {
        get
        {
            Vector3 f = transform.forward;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, f);
            if (up.sqrMagnitude < 0.01f) return 0f;     // pointing (almost) straight up or down: no sideways
            return Vector3.SignedAngle(up, Vector3.ProjectOnPlane(transform.up, f), f);
        }
    }
    public bool HasArrow => nocked != null;
    public float Draw => draw;                                        // metres the string is back from flat
    public float Tension => Mathf.Clamp01(draw / fullDraw);           // for audio / haptics

    Vector3 RestLocal => restLocal;   // flat string

    Vector3 ShelfLocal(Handedness side)
    {
        Vector3 s = arrowShelf.localPosition;
        if (side == Handedness.Left) s.x = -s.x;
        return s;
    }

    // where the arrow's nock end sits at rest: on the shelf line, on the flat string
    Vector3 NockRestLocal(Handedness side)
    {
        Vector3 s = ShelfLocal(side);
        return new Vector3(s.x, s.y, RestLocal.z);
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Awake()
    {
        grab = GetComponent<BowGrabInteractable>();
        if (gripPoint == null || arrowShelf == null || upperTip == null || lowerTip == null || stringLine == null)
        {
            Debug.LogError("Bow: a slot is empty.", this);
            enabled = false;
            return;
        }
        restLocal = transform.InverseTransformPoint((upperTip.position + lowerTip.position) * 0.5f);
        restHalfSpan = Mathf.Abs(transform.InverseTransformPoint(upperTip.position).y - transform.InverseTransformPoint(lowerTip.position).y) * 0.5f;
        lastPos = transform.position;
        if (upperLimb != null) upperRestRot = upperLimb.localRotation;
        if (lowerLimb != null) lowerRestRot = lowerLimb.localRotation;
    }

    void LateUpdate()
    {
        var hands = HandednessManager.Instance;
        var range = RangeStateController.Instance;
        bool shooting = range == null || range.State == RangeState.Shooting;

        Transform hand = hands.DrawHand;
        HandInput handInput = hands.DrawHandInput;

        velocity = Vector3.ClampMagnitude((transform.position - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f), 6f);
        lastPos = transform.position;

        // someone else (the quiver refilling) took the arrow away: a draw in progress carries on with a bare string
        if (nocked != null && nocked.State != ArrowState.Nocked) nocked = null;

        bool triggerDown = handInput.Trigger > (drawing ? triggerOff : triggerOn);   // hysteresis
        bool squeezed = triggerDown && !wasTriggerDown;
        wasTriggerDown = triggerDown;

        if (drawing)
        {
            if (!shooting || !grab.isSelected) EndDraw(DrawEnd.Cancelled);   // state changed or bow dropped: cancel
            else if (!triggerDown) EndDraw(DrawEnd.Shot);                    // let go of the string: shoot (or dry fire)
            else UpdateDraw(hand, hands.Current);
        }

        if (!drawing)
        {
            SetString(RestLocal);
            bool usable = shooting && grab.isSelected && HeldByOtherHand(hand);

            if (nocked != null)
            {
                if (!usable)
                {
                    if (!shooting) Unnock();                            // the round is over: the arrow goes back
                    else DropNocked(grab.isSelected ? BowFailure.ArrowFellOffRest : BowFailure.BowDropped);   // bow let go: the arrow falls with it
                }
                else
                {
                    RestArrow(hands.Current);
                    SetString(NockRestLocal(hands.Current));            // the string runs through the back of the arrow
                    CheckRestHold();
                }
            }
            else badRestTime = 0f;

            bool carrying = Quiver.Instance != null && Quiver.Instance.Holding;
            if (usable && squeezed && !carrying &&
                Vector3.Distance(hand.position, transform.TransformPoint(RestLocal)) < pickRadius)
                BeginDraw(hand, hands.Current);
        }

        UpdateFocus();
        if (range != null) range.ExitBlocked = drawing;                 // can't leave the range mid-draw
    }

    // ---------------------------------------------------------------- focus, steadiness, limb flex
    void UpdateFocus()
    {
        float focus = AimFocus;
        anchoredTime = Anchored ? anchoredTime + Time.deltaTime : 0f;

        // limbs bend with the draw
        if (upperLimb != null || lowerLimb != null)
        {
            float flex = maxFlexDegrees * Mathf.SmoothStep(0f, 1f, Tension);
            if (upperLimb != null) upperLimb.localRotation = upperRestRot * Quaternion.Euler(-flex, 0f, 0f);   // tips come back towards the archer
            if (lowerLimb != null) lowerLimb.localRotation = lowerRestRot * Quaternion.Euler(flex, 0f, 0f);
        }

        // the bow follows the hand more slowly the more focused the archer is: tremor is filtered out
        steady = Mathf.MoveTowards(steady, focus, (focus > steady ? 6f : 4f) * Time.deltaTime);
        if (steady > 0.001f)
        {
            if (!grabSettingsSaved)
            {
                grabSettingsSaved = true;
                savedSmoothPos = grab.smoothPosition; savedSmoothRot = grab.smoothRotation;
                savedPosAmount = grab.smoothPositionAmount; savedRotAmount = grab.smoothRotationAmount;
            }
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.smoothPositionAmount = Mathf.Lerp(looseSmoothing, steadyPositionSmoothing, steady);
            grab.smoothRotationAmount = Mathf.Lerp(looseSmoothing, steadyRotationSmoothing, steady);
        }
        else if (grabSettingsSaved)
        {
            grab.smoothPosition = savedSmoothPos; grab.smoothRotation = savedSmoothRot;
            grab.smoothPositionAmount = savedPosAmount; grab.smoothRotationAmount = savedRotAmount;
            grabSettingsSaved = false;
        }
    }

    bool HeldByOtherHand(Transform drawHand)
    {
        var holder = grab.firstInteractorSelecting as Component;
        return holder != null && !holder.transform.IsChildOf(drawHand);
    }

    /// <summary>The quiver offers an arrow it is carrying. True if it snapped onto the string.</summary>
    public bool TryNock(Arrow arrow, Transform drawHand)
    {
        if (!isActiveAndEnabled || nocked != null || drawing) return false;
        var range = RangeStateController.Instance;
        if (range != null && range.State != RangeState.Shooting) return false;
        if (!grab.isSelected || !HeldByOtherHand(drawHand)) return false;

        Vector3 nockPoint = transform.TransformPoint(NockRestLocal(HandednessManager.Instance.Current));
        if (Vector3.Distance(arrow.transform.position, nockPoint) > nockRadius) return false;
        if (Vector3.Angle(arrow.transform.forward, transform.forward) > nockAngle) return false;

        nocked = arrow;
        arrow.SetState(ArrowState.Nocked);
        IgnoreMyColliders(arrow);
        RestArrow(HandednessManager.Instance.Current);
        return true;
    }

    void IgnoreMyColliders(Arrow arrow)
    {
        var arrowCol = arrow.GetComponent<Collider>();
        foreach (var c in GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(arrowCol, c);
    }

    void RestArrow(Handedness side)
    {
        aim = transform.forward;
        nocked.transform.SetPositionAndRotation(transform.TransformPoint(NockRestLocal(side)), transform.rotation);   // arrow pivot is its nock end
    }

    void Unnock()
    {
        var a = nocked;
        nocked = null;
        if (a == null) return;
        if (a.Owner != null) a.Owner.Recycle(a);
        else Destroy(a.gameObject);
    }

    void BeginDraw(Transform hand, Handedness side)
    {
        if (nocked == null && Quiver.Instance == null && arrowPrefab != null)   // no quiver in the scene: arrows are unlimited
        {
            nocked = Instantiate(arrowPrefab);
            nocked.SetState(ArrowState.Nocked);
            IgnoreMyColliders(nocked);
            var arrowCol = nocked.GetComponent<Collider>();
            foreach (var c in hand.root.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(arrowCol, c);
        }
        drawing = true;
        badRestTime = 0f;
        UpdateDraw(hand, side);
    }

    void UpdateDraw(Transform hand, Handedness side)
    {
        // The arrow lies on the shelf and slides back and forth along a fixed line through it,
        // parallel to the bow's forward axis, so its tip always points forward relative to the bow.
        Vector3 shelfLocal = ShelfLocal(side);
        Vector3 handLocal = transform.InverseTransformPoint(hand.position);

        // Hand wandered too far from the arrow's line: the string slips off the fingers.
        float lateral = new Vector2(handLocal.x - shelfLocal.x, handLocal.y - shelfLocal.y).magnitude;
        if (lateral > maxLateral) { EndDraw(DrawEnd.Slipped); return; }
        // Not quite as far, and the string is pulled sideways off the nock: the arrow falls and the draw goes on with a bare string.
        if (nocked != null && lateral > nockPopLateral && draw > 0.05f) DropNocked(BowFailure.NockPoppedOff);

        // The nock follows the hand along the line. It can't go forward of the flat string,
        // and the arrow's tip can't be drawn back past the shelf.
        float restZ = RestLocal.z;
        float limitZ = shelfLocal.z - (arrowLength - shelfClearance);
        float z = Mathf.Min(handLocal.z, restZ);
        if (z < limitZ - slipDistance) { EndDraw(DrawEnd.Slipped); return; }
        if (nocked != null)
        {
            // the arrow stops the string; drawn clearly past that, its point is behind the rest and it falls off
            if (z < limitZ - overdrawGrace) DropNocked(BowFailure.Overdraw);
            else z = Mathf.Max(z, limitZ);
        }

        draw = restZ - z;
        Vector3 nockLocal = new Vector3(shelfLocal.x, shelfLocal.y, z);
        SetString(nockLocal);

        aim = transform.forward;                  // the arrow always points along the bow
        if (nocked != null)
            nocked.transform.SetPositionAndRotation(transform.TransformPoint(nockLocal), transform.rotation);   // arrow pivot is its nock end
    }

    void EndDraw(DrawEnd how)
    {
        drawing = false;
        float released = draw;
        bool snapped = how != DrawEnd.Cancelled && released >= minDraw;   // the string is let go while drawn: it snaps forward

        if (nocked != null)
        {
            if (how == DrawEnd.Shot && released >= minDraw)
            {
                nocked.Launch(aim * Speed(released));
                nocked = null;
                Loosed?.Invoke(this);
            }
            else if (nocked.Owner == null)
            {
                Destroy(nocked.gameObject);        // unlimited-arrows fallback: nothing to keep
                nocked = null;
            }
            // otherwise the arrow stays nocked: it goes back to rest on the shelf
        }
        else if (snapped && released >= dryFireHarmDraw)
            Report(BowFailure.DryFire, Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(released / fullDraw)));

        float clearance = float.PositiveInfinity;
        if (snapped && StringHitsArm(released, out clearance))
            Report(BowFailure.StringSlap, Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(released / fullDraw)));
        else if (snapped && logFailures)
            Debug.Log("[Bow] string released at " + released.ToString("0.00") + " m: bow arm clear by " +
                      (float.IsInfinity(clearance) ? "- (not behind the string)" : (clearance * 100f).ToString("0.0") + " cm"), this);

        draw = 0f;
        SetString(RestLocal);
    }

    // The arrow leaves the bow and falls (with the bow's own motion).
    void DropNocked(BowFailure why)
    {
        var a = nocked;
        nocked = null;
        if (a == null) return;
        a.Drop(velocity);
        Report(why, 0f);
    }

    // A nocked arrow only stays on the rest while the bow is not rolled over or pointing too far down.
    void CheckRestHold()
    {
        bool slipping = Mathf.Abs(Cant) > maxRestCant || transform.forward.y < -Mathf.Sin(maxRestPitchDown * Mathf.Deg2Rad);
        badRestTime = slipping ? badRestTime + Time.deltaTime : 0f;
        if (badRestTime >= restFallDelay) DropNocked(BowFailure.ArrowFellOffRest);
    }

    void Report(BowFailure what, float harm)
    {
        if (logFailures) Debug.Log("[Bow] " + what + (harm > 0f ? " (harm " + harm.ToString("0.00") + ")" : ""), this);
        Failed?.Invoke(this, what, harm);
    }

    // The bow hand holds the bow; its forearm is estimated from the hand back towards the shoulder (below and a little
    // ahead of the eyes), blended with the bow's own direction. The released string sweeps a triangle in the bow's
    // vertical plane (the limb tips and the drawn nock); the arm is hit if any of it is inside that triangle.
    // clearance = how far (metres) the arm missed by; negative when it was hit; infinity when it was nowhere near.
    bool StringHitsArm(float drawDistance, out float clearance)
    {
        clearance = float.PositiveInfinity;
        var cam = Camera.main;
        var hands = HandednessManager.Instance;
        if (cam == null || hands == null || drawDistance < 0.01f) return false;

        Vector3 head = cam.transform.position;
        Vector3 hand = hands.OffHandInput.transform.position;
        Vector3 towardHand = Vector3.ProjectOnPlane(hand - head, Vector3.up).normalized;
        Vector3 shoulder = head + Vector3.down * shoulderDrop + towardHand * shoulderReach;
        Vector3 dir = (-transform.forward * forearmFollowsBow + (shoulder - hand).normalized * (1f - forearmFollowsBow)).normalized;

        float reach = armRadius + stringRadius;
        Vector3 outward = transform.right * (hands.Current == Handedness.Right ? -1f : 1f);   // the bow arm is on the archer's left for a right-hander
        float restZ = RestLocal.z;
        const int samples = 12;
        for (int i = 0; i <= samples; i++)
        {
            float along = (float)i / samples;
            Vector3 p = transform.InverseTransformPoint(hand + dir * (armLength * along) + outward * (elbowOutward * along));
            float k = (restZ - p.z) / drawDistance;               // 0 at the limb tips' plane .. 1 at the drawn nock
            if (k < 0f || k > 1f) continue;
            float sideways = Mathf.Abs(p.x - RestLocal.x);
            float vertical = Mathf.Abs(p.y - RestLocal.y) - restHalfSpan * (1f - k);
            clearance = Mathf.Min(clearance, Mathf.Max(sideways, vertical) - reach);
        }
        return clearance < 0f;
    }

    float Speed(float d) => Mathf.Min(Mathf.LerpUnclamped(minSpeed, maxSpeed, d / fullDraw), maxSpeed * 1.15f);

    // nockPoint is in the bow's space; the tips are read live because the limbs may be flexing
    void SetString(Vector3 nockPoint)
    {
        Transform line = stringLine.transform;
        stringLine.SetPosition(0, line.InverseTransformPoint(upperTip.position));
        stringLine.SetPosition(1, line.InverseTransformPoint(transform.TransformPoint(nockPoint)));
        stringLine.SetPosition(2, line.InverseTransformPoint(lowerTip.position));
    }
}
