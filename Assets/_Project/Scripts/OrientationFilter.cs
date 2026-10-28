using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
// XRI 3.x only (delete these two lines on XRI 2.x):
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class OrientationFilter : MonoBehaviour, IXRSelectFilter
{
    public Transform target;          // the socket's attach transform
    public float maxAngle = 25f;

    public bool canProcess => isActiveAndEnabled;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return Quaternion.Angle(interactable.transform.rotation, target.rotation) <= maxAngle;
    }
}