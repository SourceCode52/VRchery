using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public enum RangeState { Building, Shooting }

public class RangeStateController : MonoBehaviour
{
    public static RangeStateController Instance { get; private set; }

    [SerializeField] TeleportationProvider teleportationProvider;
    [SerializeField] Transform shootingSpot;
    [SerializeField] Transform returnSpot;
    [SerializeField] float cooldown = 1f;

    [Header("Shooting stance - extra yaw added to the Shooting Spot's rotation, per shooting hand")]
    [SerializeField] float rightHandedYaw = 150f;
    [SerializeField] float leftHandedYaw = -150f;

    public RangeState State { get; private set; } = RangeState.Building;
    public bool ExitBlocked { get; set; }   // the bow will set this later (string drawn, arrow in flight)
    public event Action<RangeState> StateChanged;

    float nextAllowedTime;

    void Awake() => Instance = this;

    public void EnterShooting()
    {
        if (State == RangeState.Shooting || Time.time < nextAllowedTime) return;
        SwitchTo(RangeState.Shooting, shootingSpot);
    }

    public void ExitShooting()
    {
        if (State == RangeState.Building || ExitBlocked || Time.time < nextAllowedTime) return;
        SwitchTo(RangeState.Building, returnSpot);
    }

    void SwitchTo(RangeState next, Transform destination)
    {
        nextAllowedTime = Time.time + cooldown;
        State = next;

        Quaternion rotation = destination.rotation;
        if (next == RangeState.Shooting)
        {
            bool rightHanded = HandednessManager.Instance.Current == Handedness.Right;
            rotation *= Quaternion.Euler(0f, rightHanded ? rightHandedYaw : leftHandedYaw, 0f);
        }

        teleportationProvider.QueueTeleportRequest(new TeleportRequest
        {
            destinationPosition = destination.position,
            destinationRotation = rotation,
            matchOrientation = MatchOrientation.TargetUpAndForward
        });

        HandednessManager.Instance.SetTeleportAllowed(next == RangeState.Building);
        StateChanged?.Invoke(next);
    }
}
