using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ScrewInteractable : XRBaseInteractable
{
    [SerializeField] Vector3 localAxis = Vector3.forward;
    [SerializeField] float turnsRequired = 4f;
    [SerializeField] float travelPerTurn = 0.003f;   // metres along axis per turn
    [SerializeField] UnityEvent onFullyScrewed;

    float totalDegrees;
    float lastAngle;
    bool done;
    Vector3 startLocalPos;

    protected override void Awake()
    {
        base.Awake();
        startLocalPos = transform.localPosition;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        lastAngle = HandAngle();
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !isSelected || done) return;

        float angle = HandAngle();
        float delta = Mathf.DeltaAngle(lastAngle, angle);
        lastAngle = angle;

        totalDegrees = Mathf.Clamp(totalDegrees + delta, 0f, turnsRequired * 360f); // tighten only
        ApplyPose();

        if (totalDegrees >= turnsRequired * 360f)
        {
            done = true;
            onFullyScrewed.Invoke();
            interactionManager.CancelInteractableSelection((IXRSelectInteractable)this);
        }
    }

    float HandAngle()
    {
        var hand = interactorsSelecting[0].GetAttachTransform(this);
        Vector3 axis = transform.parent.TransformDirection(localAxis).normalized;
        Vector3 toHand = Vector3.ProjectOnPlane(hand.position - transform.position, axis);
        Vector3 reference = Vector3.ProjectOnPlane(transform.parent.right, axis);
        return Vector3.SignedAngle(reference, toHand, axis);
    }

    void ApplyPose()
    {
        transform.localRotation = Quaternion.AngleAxis(totalDegrees, localAxis);
        transform.localPosition = startLocalPos + localAxis.normalized * (totalDegrees / 360f * travelPerTurn);
    }
}
