using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// A held bow can't be taken by the other hand.
public class BowGrabInteractable : XRGrabInteractable
{
    public override bool IsSelectableBy(IXRSelectInteractor interactor)
        => (!isSelected || interactor.IsSelecting(this)) && base.IsSelectableBy(interactor);
}
