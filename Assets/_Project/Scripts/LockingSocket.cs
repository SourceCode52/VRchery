using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit; // SelectEnterEventArgs

[RequireComponent(typeof(XRSocketInteractor))]
public class LockingSocket : MonoBehaviour
{
    public BowAssembly assembly;
    public Transform lockParent;      // e.g. the assembly root, so the part moves with the bow

    XRSocketInteractor socket;
    bool locked;

    void Awake() => socket = GetComponent<XRSocketInteractor>();
    void OnEnable() => socket.selectEntered.AddListener(OnSnapped);
    void OnDisable() => socket.selectEntered.RemoveListener(OnSnapped);

    void OnSnapped(SelectEnterEventArgs args)
    {
        if (locked) return;
        if (args.interactableObject is not XRGrabInteractable grab) return;
        locked = true;
        StartCoroutine(LockRoutine(grab));
    }

    IEnumerator LockRoutine(XRGrabInteractable grab)
    {
        yield return null;               // let XRI finish its select-enter work

        // Take the part out of XRI so nothing can grab it again
        socket.enabled = false;          // releases the part from the socket
        grab.enabled = false;            // hands can no longer interact with it

        yield return new WaitForFixedUpdate(); // XRI's exit handlers have now run

        // Freeze it (after XRI has finished restoring its saved Rigidbody state)
        var rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;   // `velocity` on Unity 2022 and earlier
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        if (lockParent != null)
            grab.transform.SetParent(lockParent, true); // keep world pose

        assembly.RegisterSnapped(this);
    }
}