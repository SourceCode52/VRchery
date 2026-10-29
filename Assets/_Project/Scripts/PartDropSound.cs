using UnityEngine;

/// <summary>Plays the metal drop sound when a loose part hits something hard enough (dropped, knocked off the table).</summary>
[RequireComponent(typeof(Rigidbody))]
public class PartDropSound : MonoBehaviour
{
    [Tooltip("Impacts slower than this (metres per second) are silent.")]
    [SerializeField] float minImpactSpeed = 0.8f;

    [Tooltip("An impact at this speed plays at full volume.")]
    [SerializeField] float fullVolumeSpeed = 4f;

    [Tooltip("Seconds after the part appears during which it stays silent (it may be settling onto its table).")]
    [SerializeField] float quietAtStart = 2f;

    [SerializeField] float cooldown = 0.15f;

    Rigidbody body;
    float enabledAt, lastPlayed = -10f;

    void Awake() => body = GetComponent<Rigidbody>();
    void OnEnable() => enabledAt = Time.time;

    void OnCollisionEnter(Collision collision)
    {
        if (body.isKinematic) return;   // locked into the bow, or carried
        if (Time.time - enabledAt < quietAtStart || Time.time - lastPlayed < cooldown) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;

        lastPlayed = Time.time;
        float volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(minImpactSpeed, fullVolumeSpeed, speed));
        RangeAudio.Play(RangeAudio.Cue.PartDrop, collision.GetContact(0).point, volume);
    }
}
