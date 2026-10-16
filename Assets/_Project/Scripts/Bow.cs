using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

[RequireComponent(typeof(BowGrabInteractable))]
public class Bow : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] Transform gripPoint;
    [SerializeField] Transform upperTip;
    [SerializeField] Transform lowerTip;
    [SerializeField] Transform nockRest;
    [SerializeField] LineRenderer stringLine;
    [SerializeField] Arrow arrowPrefab;
    [SerializeField] float arrowHalfLength = 0.35f;

    [Header("Grip input (Select Value of each hand)")]
    [SerializeField] XRInputValueReader<float> leftGrip;
    [SerializeField] XRInputValueReader<float> rightGrip;

    [Header("Draw")]
    [SerializeField] float maxDraw = 0.5f;
    [SerializeField] float minDraw = 0.08f;      // shorter pulls cancel instead of firing
    [SerializeField] float pickRadius = 0.15f;   // how close the hand must be to the string
    [SerializeField] float slipDistance = 0.3f;  // hand this far past full draw = string slips, cancel
    [SerializeField] float gripOn = 0.7f;
    [SerializeField] float gripOff = 0.3f;
    [SerializeField] float minSpeed = 5f;
    [SerializeField] float maxSpeed = 45f;

    BowGrabInteractable grab;
    Arrow nocked;
    bool drawing, wasGripping;
    float tension, nockOffset;
    Vector3 aim = Vector3.forward;

    void Awake()
    {
        grab = GetComponent<BowGrabInteractable>();
        if (arrowPrefab == null || arrowPrefab.GetComponent<Collider>() == null ||
            gripPoint == null || nockRest == null || upperTip == null ||
            lowerTip == null || stringLine == null)
        {
            Debug.LogError("Bow: a slot is empty, or the Arrow prefab's root has no Collider.", this);
            enabled = false;
            return;
        }
        nockOffset = (nockRest.localPosition - gripPoint.localPosition).magnitude;
    }

    void OnEnable()
    {
        leftGrip.EnableDirectActionIfModeUsed();
        rightGrip.EnableDirectActionIfModeUsed();
    }

    void OnDisable()
    {
        leftGrip.DisableDirectActionIfModeUsed();
        rightGrip.DisableDirectActionIfModeUsed();
    }

    void LateUpdate()
    {
        var hands = HandednessManager.Instance;
        Transform hand = hands.DrawHand;
        float grip = (hands.Current == Handedness.Right ? rightGrip : leftGrip).ReadValue();

        bool gripping = grip > gripOn;
        bool squeezed = gripping && !wasGripping;   // only a fresh squeeze starts a draw
        wasGripping = gripping;

        if (!drawing && squeezed && grab.isSelected && HeldByOtherHand(hand) &&
            Vector3.Distance(hand.position, nockRest.position) < pickRadius)
            BeginDraw();

        if (!drawing) { SetString(nockRest.localPosition); return; }

        if (!grab.isSelected || grip < gripOff) { EndDraw(fire: grab.isSelected); return; }

        UpdateDraw(hand);
    }

    bool HeldByOtherHand(Transform drawHand)
    {
        var holder = grab.firstInteractorSelecting as Component;
        return holder != null && !holder.transform.IsChildOf(drawHand);
    }

    void BeginDraw()
    {
        drawing = true;
        nocked = Instantiate(arrowPrefab);

        // the arrow must not collide with the bow or the player it leaves
        var arrowCol = nocked.GetComponent<Collider>();
        foreach (var c in GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(arrowCol, c);
        foreach (var c in HandednessManager.Instance.DrawHand.root.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(arrowCol, c);
    }

    void UpdateDraw(Transform hand)
    {
        Vector3 toBow = gripPoint.position - hand.position;
        float dist = toBow.magnitude;
        if (dist < 0.01f) return;

        aim = toBow / dist;                       // string hand -> bow hand
        float pull = dist - nockOffset;
        if (pull > maxDraw + slipDistance) { EndDraw(fire: false); return; }

        float draw = Mathf.Clamp(pull, 0f, maxDraw);
        tension = draw / maxDraw;

        Vector3 nockWorld = gripPoint.position - aim * (nockOffset + draw);
        SetString(transform.InverseTransformPoint(nockWorld));
        nocked.transform.SetPositionAndRotation(
            nockWorld + aim * arrowHalfLength, Quaternion.LookRotation(aim));
    }

    void EndDraw(bool fire)
    {
        drawing = false;
        if (fire && tension * maxDraw >= minDraw)
            nocked.Launch(aim * Mathf.Lerp(minSpeed, maxSpeed, tension));
        else
            Destroy(nocked.gameObject);
        nocked = null;
        tension = 0f;
        SetString(nockRest.localPosition);
    }

    void SetString(Vector3 mid)
    {
        stringLine.SetPosition(0, upperTip.localPosition);
        stringLine.SetPosition(1, mid);
        stringLine.SetPosition(2, lowerTip.localPosition);
    }
}