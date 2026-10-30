using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public enum HandMode
{
    Grab,        // trigger OR grip closes the whole hand and grabs
    StringHook   // drawing hand while shooting: trigger hooks the string with index/middle/ring
}

/// <summary>
/// One per controller (put it on the Left/Right Controller object).
/// Reads grip + trigger once, decides what the hand is doing, and feeds the Near-Far Interactor's
/// select input so that EITHER trigger or grip grabs, an object is held only while the button is held
/// (let go and it drops), the drawing hand does not grab anything while shooting, and the hand holding
/// the bow cannot let go of it while shooting.
/// </summary>
[DefaultExecutionOrder(10)]   // after XRInteractionManager, so queued input is never overwritten before it is read
public class HandInput : MonoBehaviour
{
    [SerializeField] Handedness side;
    [SerializeField] XRInputValueReader<float> gripInput = new XRInputValueReader<float>("Grip");
    [SerializeField] XRInputValueReader<float> triggerInput = new XRInputValueReader<float>("Trigger");
    [SerializeField] XRBaseInputInteractor grabInteractor;   // this hand's Near-Far Interactor

    [Header("Grabbing")]
    [SerializeField] bool toggleHold = false;                // off: hold the button to hold the object, let go to drop it. On: press once to pick up, press again to let go
    [SerializeField] float pressAt = 0.6f;
    [SerializeField] float releaseAt = 0.4f;

    public Handedness Side => side;
    public float Grip { get; private set; }
    public float Trigger { get; private set; }
    /// <summary>How hard the hand is squeezing: the grip (side button) or the trigger, whichever is further down.</summary>
    public float Squeeze => Mathf.Max(Grip, Trigger);
    public HandMode Mode { get; private set; }
    public bool Holding => grabInteractor != null && grabInteractor.hasSelection;
    public bool HoldingArrow { get; set; }   // set by the Quiver while this hand carries an arrow

    bool pressed;

    void Awake()
    {
        // We drive the select button ourselves (max of grip and trigger), so the interactor
        // reads a manual value instead of the grip action.
        grabInteractor.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
        grabInteractor.selectActionTrigger = toggleHold
            ? XRBaseInputInteractor.InputTriggerType.Toggle
            : XRBaseInputInteractor.InputTriggerType.StateChange;
    }

    void OnEnable()
    {
        gripInput?.EnableDirectActionIfModeUsed();
        triggerInput?.EnableDirectActionIfModeUsed();
    }

    void OnDisable()
    {
        gripInput?.DisableDirectActionIfModeUsed();
        triggerInput?.DisableDirectActionIfModeUsed();
    }

    void Update()
    {
        Grip = gripInput.ReadValue();
        Trigger = triggerInput.ReadValue();

        var newMode = IsDrawHandWhileShooting() ? HandMode.StringHook : HandMode.Grab;
        if (newMode == HandMode.StringHook && Mode != HandMode.StringHook && Holding)
            grabInteractor.interactionManager.CancelInteractorSelection((IXRSelectInteractor)grabInteractor);   // drawing hand can't keep holding something
        Mode = newMode;

        float grab = Mode == HandMode.StringHook ? 0f : Mathf.Max(Grip, Trigger);
        bool was = pressed;
        if (HoldsBowWhileShooting())
        {
            pressed = true;      // the bow hand cannot let go of the bow on the shooting line: its buttons do nothing
            grab = 1f;
        }
        else if (grab > pressAt) pressed = true;
        else if (grab < releaseAt) pressed = false;

        grabInteractor.selectInput.QueueManualState(pressed, grab, pressed && !was, !pressed && was);
    }

    // The hand that is not drawing, while shooting, with something (the bow) in it.
    bool HoldsBowWhileShooting()
    {
        var range = RangeStateController.Instance;
        var hands = HandednessManager.Instance;
        return range != null && hands != null
            && range.State == RangeState.Shooting
            && hands.Current != side
            && Holding;
    }

    bool IsDrawHandWhileShooting()
    {
        var range = RangeStateController.Instance;
        var hands = HandednessManager.Instance;
        return range != null && hands != null
            && range.State == RangeState.Shooting
            && hands.Current == side;
    }

    public void Haptic(float amplitude, float duration)
    {
        if (grabInteractor != null) grabInteractor.SendHapticImpulse(amplitude, duration);
    }
}
