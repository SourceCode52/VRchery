using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
// XRI 3.x only (delete these lines on XRI 2.x):
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

/// <summary>
/// Locks a part into this socket once it has snapped in.
///
/// The socket keeps holding the part exactly as it did before the lock, so the way the
/// components fit together is never changed. The lock's only job is to stop anything
/// except this socket from selecting (grabbing) the part again.
/// </summary>
[RequireComponent(typeof(XRSocketInteractor))]
public class LockingSocket : MonoBehaviour, IXRSelectFilter
{
    public BowAssembly assembly;

    [Tooltip("Optional. If set, the locked part is re-parented here (world pose is kept).")]
    public Transform lockParent;

    [Header("Settling")]
    [Tooltip("How close (metres) the part must be to the socket's attach point before it is locked.")]
    [SerializeField] float settlePosition = 0.003f;

    [Tooltip("How close (degrees) the part must be to the socket's attach rotation before it is locked.")]
    [SerializeField] float settleAngle = 1.5f;

    [Tooltip("Lock anyway after this many seconds, even if the part has not fully settled.")]
    [SerializeField] float settleTimeout = 2f;

    XRSocketInteractor socket;
    XRGrabInteractable lockedPart;
    bool locking;

    public bool IsLocked => lockedPart != null;

    void Awake() => socket = GetComponent<XRSocketInteractor>();
    void OnEnable() => socket.selectEntered.AddListener(OnSnapped);

    void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnSnapped);
        Unlock();
    }

    // --- IXRSelectFilter: applied to the locked part only. Only this socket may hold it. ---
    public bool canProcess => isActiveAndEnabled && lockedPart != null;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return ReferenceEquals(interactor, socket);
    }

    void OnSnapped(SelectEnterEventArgs args)
    {
        if (locking || lockedPart != null) return;
        var grab = args.interactableObject as XRGrabInteractable;
        if (grab == null) return;
        locking = true;
        StartCoroutine(LockRoutine(grab));
    }

    IEnumerator LockRoutine(XRGrabInteractable grab)
    {
        // Let the socket finish pulling the part into place exactly as it normally would.
        float elapsed = 0f;
        while (elapsed < settleTimeout)
        {
            if (!socket.IsSelecting(grab)) { locking = false; yield break; } // pulled away first: no lock
            if (AttachError(grab, out float dist, out float angle) && dist <= settlePosition && angle <= settleAngle)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (!socket.IsSelecting(grab)) { locking = false; yield break; }

        // Put it exactly on the attach pose so the fit is perfect.
        MatchAttachPose(grab);

        // Hold it rigidly: the socket keeps selecting it and moves it with the socket's attach point,
        // but no physics acts on it any more.
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        var rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Nothing but this socket may select it from now on, so it can't be pulled out.
        grab.selectFilters.Add(this);
        lockedPart = grab;
        locking = false;

        if (lockParent != null)
            grab.transform.SetParent(lockParent, true);

        if (assembly != null) assembly.RegisterSnapped(this);
    }

    void Unlock()
    {
        if (lockedPart != null)
            lockedPart.selectFilters.Remove(this);
        lockedPart = null;
        locking = false;
    }

    // Distance / angle between the part's attach pose and this socket's attach pose.
    bool AttachError(XRGrabInteractable grab, out float dist, out float angle)
    {
        Transform sa = socket.GetAttachTransform(grab);
        Transform pa = grab.GetAttachTransform(socket);
        dist = 0f; angle = 0f;
        if (sa == null || pa == null) return false;
        dist = Vector3.Distance(sa.position, pa.position);
        angle = Quaternion.Angle(sa.rotation, pa.rotation);
        return true;
    }

    // Moves the part so its attach transform sits exactly on the socket's attach transform.
    void MatchAttachPose(XRGrabInteractable grab)
    {
        Transform sa = socket.GetAttachTransform(grab);
        Transform pa = grab.GetAttachTransform(socket);
        if (sa == null || pa == null) return;

        Transform root = grab.transform;
        Vector3 localPos = root.InverseTransformPoint(pa.position);
        Quaternion localRot = Quaternion.Inverse(root.rotation) * pa.rotation;

        Quaternion rootRot = sa.rotation * Quaternion.Inverse(localRot);
        Vector3 rootPos = sa.position - rootRot * localPos;
        root.SetPositionAndRotation(rootPos, rootRot);
    }
}
