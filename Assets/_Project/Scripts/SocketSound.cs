using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Plays a sound when a part clicks into this socket. Put it on each bow-assembly socket and choose the sound.</summary>
[RequireComponent(typeof(XRSocketInteractor))]
public class SocketSound : MonoBehaviour
{
    [SerializeField] RangeAudio.Cue cue = RangeAudio.Cue.MetalClick;

    XRSocketInteractor socket;

    void Awake() => socket = GetComponent<XRSocketInteractor>();
    void OnEnable() => socket.selectEntered.AddListener(OnSnapped);
    void OnDisable() => socket.selectEntered.RemoveListener(OnSnapped);

    void OnSnapped(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        RangeAudio.Play(cue, socket.GetAttachTransform(args.interactableObject).position);
    }
}
