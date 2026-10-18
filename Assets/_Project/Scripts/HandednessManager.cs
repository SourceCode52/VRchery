using System;
using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Locomotion actions (same references the controller managers use)")]
    [SerializeField] InputActionReference leftMove;
    [SerializeField] InputActionReference rightMove;
    [SerializeField] InputActionReference leftTurn;
    [SerializeField] InputActionReference rightTurn;
    [SerializeField] InputActionReference leftSnapTurn;
    [SerializeField] InputActionReference rightSnapTurn;

    public Handedness Current { get; private set; }      // the shooting (drawing) hand
    public bool TeleportAllowed { get; private set; } = true;
    public event Action<Handedness> Changed;

    public Transform DrawHand => (Current == Handedness.Right ? rightController : leftController).transform;
    public HandInput DrawHandInput => Current == Handedness.Right ? rightInput : leftInput;
    public HandInput OffHandInput => Current == Handedness.Right ? leftInput : rightInput;

    HandInput leftInput, rightInput;

    void Awake()
    {
        Instance = this;
        leftInput = leftController.GetComponent<HandInput>();
        rightInput = rightController.GetComponent<HandInput>();
    }

    void Start() => Set(startingHandedness);

    // Every frame, so nothing else (e.g. a controller manager re-enabling) can hand a locomotion action back.
    void LateUpdate() => ApplyLocomotionActions();

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
        ApplyLocomotionActions();
    }

    // The dominant (drawing) hand never moves or turns the player. The offhand can snap-turn,
    // but only while building - turning is off in the shooting state (the player is placed facing the target).
    void ApplyLocomotionActions()
    {
        SetAction(leftMove, false);
        SetAction(rightMove, false);
        SetAction(leftTurn, false);
        SetAction(rightTurn, false);

        bool turnAllowed = TeleportAllowed;   // true only while building
        SetAction(leftSnapTurn, turnAllowed && Current == Handedness.Right);
        SetAction(rightSnapTurn, turnAllowed && Current == Handedness.Left);
    }

    static void SetAction(InputActionReference reference, bool on)
    {
        var action = reference != null ? reference.action : null;
        if (action == null) return;
        if (on && !action.enabled) action.Enable();
        else if (!on && action.enabled) action.Disable();
    }

    static void SetTeleport(ControllerInputActionManager controller, GameObject interactor, bool on)
    {
        controller.enabled = on;
        if (!on) interactor.SetActive(false);   // also cancels an aim in progress
    }
}
