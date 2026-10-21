using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stand-in bow. The string is flat until the drawing hand pulls it; how far it goes is up to the player,
/// limited only by the arrow: the tip can't be drawn back past the shelf.
/// An arrow is nocked by bringing it (from the Quiver) to the nocking point of a bow held in the other hand;
/// it then lies on the shelf (above the grip) and slides along a fixed line parallel to the bow's forward axis.
/// Drawing uses the TRIGGER of the drawing hand (index, middle, ring hook the string).
/// </summary>
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
    [SerializeField] float fullDraw = 0.7f;           // 28 inch: the draw the speed range is calibrated to
    [SerializeField] float triggerOn = 0.6f;
    [SerializeField] float triggerOff = 0.3f;
    [SerializeField] float minSpeed = 5f;
    [SerializeField] float maxSpeed = 45f;

    BowGrabInteractable grab;
    Arrow nocked;
    bool drawing, wasTriggerDown;
    Vector3 aim = Vector3.forward;
    float draw;

    public static event System.Action<Bow> Loosed;                   // an arrow has just been shot

    public bool IsHeld => grab != null && grab.isSelected;
    public bool HeldByOffHand => IsHeld && HeldByOtherHand(HandednessManager.Instance.DrawHand);   // the right way round: bow in the non-drawing hand
    public bool Drawing => drawing;
    public bool HasArrow => nocked != null;
    public float Draw => draw;                                        // metres the string is back from flat
    public float Tension => Mathf.Clamp01(draw / fullDraw);           // for audio / haptics

    Vector3 RestLocal => (upperTip.localPosition + lowerTip.localPosition) * 0.5f;   // flat string

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
        }
    }

    void LateUpdate()
    {
        var hands = HandednessManager.Instance;
        var range = RangeStateController.Instance;
        bool shooting = range == null || range.State == RangeState.Shooting;

        Transform hand = hands.DrawHand;
        HandInput handInput = hands.DrawHandInput;

        // someone else (the quiver refilling) took the arrow away
        if (nocked != null && nocked.State != ArrowState.Nocked) { if (drawing) EndDraw(false); nocked = null; }

        bool triggerDown = handInput.Trigger > (drawing ? triggerOff : triggerOn);   // hysteresis
        bool squeezed = triggerDown && !wasTriggerDown;
        wasTriggerDown = triggerDown;

        if (drawing)
        {
            if (!shooting || !grab.isSelected) EndDraw(false);          // state changed or bow dropped: cancel
            else if (!triggerDown) EndDraw(true);                       // let go of the string: shoot
            else UpdateDraw(hand, hands.Current);
        }

        if (!drawing)
        {
            SetString(RestLocal);
            bool usable = shooting && grab.isSelected && HeldByOtherHand(hand);

            if (nocked != null)
            {
                if (!usable) Unnock();                                  // bow put down: the arrow goes back
                else RestArrow(hands.Current);
            }

            bool arrowReady = nocked != null || Quiver.Instance == null;   // no quiver: arrows are unlimited
            bool carrying = Quiver.Instance != null && Quiver.Instance.Holding;
            if (usable && squeezed && arrowReady && !carrying &&
                Vector3.Distance(hand.position, transform.TransformPoint(RestLocal)) < pickRadius)
                BeginDraw(hand, hands.Current);
        }

        if (range != null) range.ExitBlocked = drawing;                 // can't leave the range mid-draw
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
        if (nocked == null)   // no quiver in the scene: make an arrow
        {
            nocked = Instantiate(arrowPrefab);
            nocked.SetState(ArrowState.Nocked);
            IgnoreMyColliders(nocked);
            var arrowCol = nocked.GetComponent<Collider>();
            foreach (var c in hand.root.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(arrowCol, c);
        }
        drawing = true;
        UpdateDraw(hand, side);
    }

    void UpdateDraw(Transform hand, Handedness side)
    {
        // The arrow lies on the shelf and slides back and forth along a fixed line through it,
        // parallel to the bow's forward axis, so its tip always points forward relative to the bow.
        Vector3 shelfLocal = ShelfLocal(side);
        Vector3 handLocal = transform.InverseTransformPoint(hand.position);

        // Hand wandered too far from the arrow's line: the string slips.
        float lateral = new Vector2(handLocal.x - shelfLocal.x, handLocal.y - shelfLocal.y).magnitude;
        if (lateral > maxLateral) { EndDraw(false); return; }

        // The nock follows the hand along the line. It can't go forward of the flat string,
        // and the arrow's tip can't be drawn back past the shelf.
        float restZ = RestLocal.z;
        float limitZ = shelfLocal.z - (arrowLength - shelfClearance);
        float z = Mathf.Min(handLocal.z, restZ);
        if (z < limitZ - slipDistance) { EndDraw(false); return; }
        z = Mathf.Max(z, limitZ);

        draw = restZ - z;
        Vector3 nockLocal = new Vector3(shelfLocal.x, shelfLocal.y, z);
        SetString(nockLocal);

        aim = transform.forward;                  // the arrow always points along the bow
        nocked.transform.SetPositionAndRotation(transform.TransformPoint(nockLocal), transform.rotation);   // arrow pivot is its nock end
    }

    void EndDraw(bool fire)
    {
        drawing = false;
        if (nocked != null)
        {
            if (fire && draw >= minDraw)
            {
                nocked.Launch(aim * Speed(draw));
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
        draw = 0f;
        SetString(RestLocal);
    }

    float Speed(float d) => Mathf.Min(Mathf.LerpUnclamped(minSpeed, maxSpeed, d / fullDraw), maxSpeed * 1.15f);

    void SetString(Vector3 nockPoint)
    {
        stringLine.SetPosition(0, upperTip.localPosition);
        stringLine.SetPosition(1, nockPoint);
        stringLine.SetPosition(2, lowerTip.localPosition);
    }
}
