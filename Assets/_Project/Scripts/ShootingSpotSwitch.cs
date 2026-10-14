using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class ShootingSpotSwitch : MonoBehaviour
{
    [SerializeField] TeleportationProvider teleportationProvider;
    [SerializeField] Transform shootingSpot;

    public void EnterShootingState()
    {
        var request = new TeleportRequest
        {
            destinationPosition = shootingSpot.position,
            destinationRotation = shootingSpot.rotation,
            matchOrientation = MatchOrientation.TargetUpAndForward
        };
        teleportationProvider.QueueTeleportRequest(request);

        HandednessManager.Instance.SetTeleportAllowed(false);
    }
}
