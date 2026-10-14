using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public enum Handedness { Right, Left }

public class HandednessManager : MonoBehaviour
{
    public static HandednessManager Instance { get; private set; }

    [SerializeField] Handedness startingHandedness = Handedness.Right;
    [SerializeField] ControllerInputActionManager leftController;
    [SerializeField] ControllerInputActionManager rightController;
    [SerializeField] GameObject leftTeleportInteractor;   // only ever switched off here
    [SerializeField] GameObject rightTeleportInteractor;

    public Handedness Current { get; private set; }      // the shooting (drawing) hand
    public bool TeleportAllowed { get; private set; } = true;
    public event Action<Handedness> Changed;

    void Awake() => Instance = this;
    void Start() => Set(startingHandedness);

    public void Set(Handedness h)
    {
        Current = h;
        Apply();
        Changed?.Invoke(h);
    }

    public void SetTeleportAllowed(bool allowed)
    {
        TeleportAllowed = allowed;
        Apply();
    }

    void Apply()
    {
        // No continuous movement on either hand.
        leftController.smoothMotionEnabled = false;
        rightController.smoothMotionEnabled = false;

        // Teleport belongs to the offhand: the hand that is NOT drawing.
        SetTeleport(leftController, leftTeleportInteractor, TeleportAllowed && Current == Handedness.Right);
        SetTeleport(rightController, rightTeleportInteractor, TeleportAllowed && Current == Handedness.Left);
    }

    static void SetTeleport(ControllerInputActionManager controller, GameObject interactor, bool on)
    {
        controller.enabled = on;
        if (!on) interactor.SetActive(false);   // also cancels an aim in progress
    }
}