using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class ReturnToStart : MonoBehaviour
{
    [SerializeField] float returnDelay = 1f;

    XRGrabInteractable grab;
    Rigidbody rb;
    Vector3 startPos;
    Quaternion startRot;
    Coroutine pending;
    bool snapped;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Picked up (or socketed): cancel any pending return
        if (pending != null) { StopCoroutine(pending); pending = null; }

        if (args.interactorObject is XRSocketInteractor)
            snapped = true;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        // Leaving a socket or being held by a socket never triggers a return
        if (snapped || args.interactorObject is XRSocketInteractor) return;

        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(ReturnAfterDelay());
    }

    IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);

        // The socket may have grabbed it during the delay, or the player picked it up again
        if (grab.isSelected || snapped) { pending = null; yield break; }

        rb.linearVelocity = Vector3.zero;      // Unity 6 name (was rb.velocity)
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(startPos, startRot);
        pending = null;
    }
}