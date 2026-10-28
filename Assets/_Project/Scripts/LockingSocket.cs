using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
// XRI 3.x only (delete these two lines on XRI 2.x):
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSocketInteractor))]
public class LockingSocket : MonoBehaviour
{
    public BowAssembly assembly;
    public Transform lockParent;

    XRSocketInteractor socket;
    bool locked;

    void Awake() => socket = GetComponent<XRSocketInteractor>();
    void OnEnable() => socket.selectEntered.AddListener(OnSnapped);
    void OnDisable() => socket.selectEntered.RemoveListener(OnSnapped);

    void OnSnapped(SelectEnterEventArgs args)
    {
        if (locked) return;
        var grab = args.interactableObject as XRGrabInteractable;
        if (grab == null) return;
        locked = true;
        StartCoroutine(LockRoutine(grab));
    }

    IEnumerator LockRoutine(XRGrabInteractable grab)
    {
        yield return null;
        socket.enabled = false;
        grab.enabled = false;
        yield return new WaitForFixedUpdate();

        var rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (lockParent != null)
            grab.transform.SetParent(lockParent, true);

        if (assembly != null) assembly.RegisterSnapped(this);
    }
}