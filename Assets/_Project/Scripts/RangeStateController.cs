using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public enum RangeState { Building, Shooting }

public class RangeStateController : MonoBehaviour
{
    public static RangeStateController Instance { get; private set; }

    [SerializeField] TeleportationProvider teleportationProvider;
    [SerializeField] Transform shootingSpot;
    [SerializeField] Transform returnSpot;
    [SerializeField] float cooldown = 1f;

    [Header("Going to the shooting line needs the bow in the bow hand")]
    [Tooltip("The label on the START button. It shows the hint below for a moment when START is pressed without the bow.")]
    [SerializeField] TMP_Text startLabel;
    [SerializeField] string needBowText = "HOLD BOW";
    [SerializeField] float needBowSeconds = 2.5f;

    [Header("Shooting stance - extra yaw added to the Shooting Spot's rotation, per shooting hand")]
    [SerializeField] float rightHandedYaw = 140f;
    [SerializeField] float leftHandedYaw = -140f;

    public RangeState State { get; private set; } = RangeState.Building;
    public bool ExitBlocked { get; set; }   // the bow will set this later (string drawn, arrow in flight)
    public event Action<RangeState> StateChanged;

    float nextAllowedTime;
    string startLabelNormal;
    Coroutine hint;

    /// <summary>True while a bow is held in the hand that does not draw (the right way round for shooting).</summary>
    public bool HoldingBow
    {
        get
        {
            foreach (var bow in Bow.All) if (bow.HeldByOffHand) return true;
            return false;
        }
    }

    void Awake()
    {
        Instance = this;
        if (startLabel != null) startLabelNormal = startLabel.text;
        // The stock teleport turns the player so the camera's forward vector, flattened onto the floor, matches the
        // destination. When the player is looking down at a button that flattened vector is unreliable, so the
        // heading they arrive with depended on how they happened to be standing/looking. Use a steadier heading.
        if (teleportationProvider != null) teleportationProvider.forwardTransformation = new SteadyHeadingAlignment();
    }

    public void EnterShooting()
    {
        if (State == RangeState.Shooting || Time.time < nextAllowedTime) return;
        if (!HoldingBow) { NeedBow(); return; }       // no bow in the bow hand: stay where you are
        SwitchTo(RangeState.Shooting, shootingSpot);
    }

    // START was pressed without the bow: the failure chord and a hint on the button
    void NeedBow()
    {
        RangeAudio.Play(RangeAudio.Cue.Miss, startLabel != null ? startLabel.transform.position : transform.position);
        if (startLabel == null) return;
        if (hint != null) StopCoroutine(hint);
        hint = StartCoroutine(ShowHint());
    }

    IEnumerator ShowHint()
    {
        startLabel.text = needBowText;
        yield return new WaitForSeconds(needBowSeconds);
        startLabel.text = startLabelNormal;
        hint = null;
    }

    public void ExitShooting()
    {
        if (State == RangeState.Building || ExitBlocked || Time.time < nextAllowedTime) return;
        SwitchTo(RangeState.Building, returnSpot);
    }

    void SwitchTo(RangeState next, Transform destination)
    {
        nextAllowedTime = Time.time + cooldown;
        State = next;

        Quaternion rotation = destination.rotation;
        if (next == RangeState.Shooting)
        {
            bool rightHanded = HandednessManager.Instance.Current == Handedness.Right;
            rotation *= Quaternion.Euler(0f, rightHanded ? rightHandedYaw : leftHandedYaw, 0f);
        }

        teleportationProvider.QueueTeleportRequest(new TeleportRequest
        {
            destinationPosition = destination.position,
            destinationRotation = rotation,
            matchOrientation = MatchOrientation.TargetUpAndForward
        });

        HandednessManager.Instance.SetTeleportAllowed(next == RangeState.Building);
        StateChanged?.Invoke(next);
    }
}

/// <summary>
/// Turns the player so their HEAD HEADING (the direction they are facing, ignoring nodding and tilting) matches the
/// target direction. Looking forward the camera's forward vector gives the heading; looking steeply down or up that
/// vector flattens to almost nothing and is thrown off by small head tilts, so the camera's up vector (which points
/// along the heading when looking down, and against it when looking up) takes over smoothly.
/// </summary>
public class SteadyHeadingAlignment : XRCameraForwardXZAlignment
{
    public override void Apply(XRMovableBody body)
    {
        var xrOrigin = body.xrOrigin;
        var up = body.originTransform.up;
        Vector3 heading = Heading(xrOrigin.Camera.transform, up);
        Vector3 target = Vector3.ProjectOnPlane(targetDirection, up);
        if (heading.sqrMagnitude < 1e-6f || target.sqrMagnitude < 1e-6f) return;
        float angle = Vector3.SignedAngle(heading, target.normalized, up);
        xrOrigin.RotateAroundCameraPosition(up, angle);
    }

    public static Vector3 Heading(Transform head, Vector3 up)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(head.forward, up);
        Vector3 upFlat = Vector3.ProjectOnPlane(head.up, up);
        float pitch = Vector3.Dot(head.forward, up);               // +1 looking straight up, -1 straight down
        if (pitch > 0f) upFlat = -upFlat;                          // looking up: the head's up vector points backwards
        float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.9f, Mathf.Abs(pitch)));   // ~30 degrees .. ~64 degrees
        Vector3 f = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.zero;
        Vector3 u = upFlat.sqrMagnitude > 1e-6f ? upFlat.normalized : Vector3.zero;
        Vector3 h = f * (1f - w) + u * w;
        return h.sqrMagnitude > 1e-6f ? h.normalized : (f.sqrMagnitude > 0f ? f : u);
    }
}

