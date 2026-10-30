using UnityEngine;

/// <summary>
/// Curls the finger bones procedurally. Reads HandInput on the controller.
/// Grab mode:       trigger or grip closes the whole hand, and it stays closed while something is held.
/// StringHook mode: trigger curls index, middle and ring around the string; the pinky tucks into the hand; thumb stays open.
/// </summary>
public class HandPoser : MonoBehaviour
{
    [SerializeField] HandInput input;

    [Header("Bones (joints 1-3 of each finger)")]
    [SerializeField] Transform[] indexBones;
    [SerializeField] Transform[] middleBones;
    [SerializeField] Transform[] ringBones;
    [SerializeField] Transform[] pinkyBones;
    [SerializeField] Transform[] thumbBones;

    [Header("Rotation at full curl (tune in Play mode)")]
    [SerializeField] Vector3 fingerCurl = new Vector3(80, 0, 0);
    [SerializeField] Vector3 thumbCurl = new Vector3(0, 0, 40);
    [SerializeField] float smoothing = 15f;

    [Header("Poses")]
    [SerializeField, Range(0f, 1f)] float holdCurl = 0.85f;    // fingers stay this closed while holding an object
    [SerializeField, Range(0f, 1f)] float hookCurl = 0.7f;     // string hook: index/middle/ring at full trigger
    [SerializeField, Range(0f, 1f)] float pinkyCurl = 0.85f;     // string hook: the pinky curls into the palm (not around the string) at full trigger

    Quaternion[] indexRest, middleRest, ringRest, pinkyRest, thumbRest;
    float index, middle, ring, pinky, thumb;

    void Awake()
    {
        indexRest = Capture(indexBones);
        middleRest = Capture(middleBones);
        ringRest = Capture(ringBones);
        pinkyRest = Capture(pinkyBones);
        thumbRest = Capture(thumbBones);
    }

    void LateUpdate()
    {
        float tIndex, tMiddle, tRing, tPinky, tThumb;

        if (input.Mode == HandMode.StringHook && !input.HoldingArrow)
        {
            float squeeze = input.Squeeze;       // grip or trigger
            float hook = squeeze * hookCurl;
            tIndex = tMiddle = tRing = hook;
            tPinky = squeeze * pinkyCurl;
            tThumb = 0f;
        }
        else
        {
            float grab = Mathf.Max(input.Grip, input.Trigger);
            if (input.Holding || input.HoldingArrow) grab = Mathf.Max(grab, holdCurl);
            tIndex = tMiddle = tRing = tPinky = tThumb = grab;
        }

        float step = smoothing * Time.deltaTime;
        index = Mathf.MoveTowards(index, tIndex, step);
        middle = Mathf.MoveTowards(middle, tMiddle, step);
        ring = Mathf.MoveTowards(ring, tRing, step);
        pinky = Mathf.MoveTowards(pinky, tPinky, step);
        thumb = Mathf.MoveTowards(thumb, tThumb, step);

        Apply(indexBones, indexRest, fingerCurl, index);
        Apply(middleBones, middleRest, fingerCurl, middle);
        Apply(ringBones, ringRest, fingerCurl, ring);
        Apply(pinkyBones, pinkyRest, fingerCurl, pinky);
        Apply(thumbBones, thumbRest, thumbCurl, thumb);
    }

    static Quaternion[] Capture(Transform[] bones)
    {
        if (bones == null) return new Quaternion[0];
        var rest = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            rest[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
        return rest;
    }

    static void Apply(Transform[] bones, Quaternion[] rest, Vector3 curl, float t)
    {
        if (bones == null) return;
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null) bones[i].localRotation = rest[i] * Quaternion.Euler(curl * t);
    }
}
