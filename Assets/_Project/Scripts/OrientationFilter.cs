using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class OrientationFilter : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [SerializeField] float maxAngle = 25f;
    XRSocketInteractor socket;

    public bool canProcess => isActiveAndEnabled;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectFilters.Add(this);
        socket.hoverFilters.Add(this);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => IsAligned(interactor, interactable);
    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => IsAligned(interactor, interactable);

    bool IsAligned(IXRInteractor interactor, IXRInteractable interactable)
    {
        var partAttach = interactable.GetAttachTransform(interactor);
        var socketAttach = socket.GetAttachTransform(interactable);
        return Quaternion.Angle(partAttach.rotation, socketAttach.rotation) <= maxAngle;
    }
}