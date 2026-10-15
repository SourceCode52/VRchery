using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class HandPoser : MonoBehaviour
{
    [SerializeField] XRInputValueReader<float> gripInput = new XRInputValueReader<float>("Grip");
    [SerializeField] XRInputValueReader<float> triggerInput = new XRInputValueReader<float>("Trigger");

    [Header("Bones (joints 1-3 of each finger)")]
    [SerializeField] Transform[] gripFingerBones;  // middle, ring, pinky
    [SerializeField] Transform[] thumbBones;
    [SerializeField] Transform[] indexBones;

    [Header("Rotation at full squeeze (tune in Play mode)")]
    [SerializeField] Vector3 fingerCurl = new Vector3(80, 0, 0);
    [SerializeField] Vector3 thumbCurl = new Vector3(0, 0, 40);
    [SerializeField] float smoothing = 15f;

    Quaternion[] gripRest, thumbRest, indexRest;
    float grip, trigger;

    void Awake()
    {
        gripRest = Capture(gripFingerBones);
        thumbRest = Capture(thumbBones);
        indexRest = Capture(indexBones);
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

    void LateUpdate()
    {
        float step = smoothing * Time.deltaTime;
        grip = Mathf.MoveTowards(grip, gripInput.ReadValue(), step);
        trigger = Mathf.MoveTowards(trigger, triggerInput.ReadValue(), step);

        Apply(gripFingerBones, gripRest, fingerCurl, grip);
        Apply(thumbBones, thumbRest, thumbCurl, grip);
        Apply(indexBones, indexRest, fingerCurl, trigger);
    }

    static Quaternion[] Capture(Transform[] bones)
    {
        var rest = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localRotation;
        return rest;
    }

    static void Apply(Transform[] bones, Quaternion[] rest, Vector3 curl, float t)
    {
        for (int i = 0; i < bones.Length; i++)
            bones[i].localRotation = rest[i] * Quaternion.Euler(curl * t);
    }
}
