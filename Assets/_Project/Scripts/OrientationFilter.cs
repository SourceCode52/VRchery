using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
// XRI 3.x only (delete these two lines on XRI 2.x):
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Select filter for a socket: only lets a part snap in if it is close to the correct orientation.
///
/// What is compared is the part's attach transform (the point XRI actually lines up with the
/// socket) against the socket's attach transform, so "correct orientation" means exactly the pose
/// the part will end up in once it snaps. A part held backwards or upside down is rejected until
/// it is turned to within <see cref="maxAngle"/> degrees of that pose.
/// </summary>
public class OrientationFilter : MonoBehaviour, IXRSelectFilter
{
    [Tooltip("The socket's attach transform (the correct orientation).")]
    public Transform target;

    [Tooltip("Largest angle, in degrees, a part may be away from the correct orientation and still snap in. " +
             "0 = must be exact, 180 = any orientation is accepted.")]
    [Range(0f, 180f)]
    public float maxAngle = 45f;

    public bool canProcess => isActiveAndEnabled;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        Transform correct = target != null ? target : interactor.GetAttachTransform(interactable);
        Transform part = interactable.GetAttachTransform(interactor);
        if (correct == null || part == null) return true; // nothing to compare against: don't block

        return Quaternion.Angle(part.rotation, correct.rotation) <= maxAngle;
    }
}
