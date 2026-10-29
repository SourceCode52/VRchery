using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// All the sound effects of the range, in one place. Put one of these in the scene, give each sound its clip and
/// volume in the list, and everything is played from here:
///   - the building sounds are played by <see cref="SocketSound"/> (on each socket), <see cref="PartDropSound"/> (on each
///     loose part) and BowHandoff (the finished bow);
///   - everything to do with shooting is picked up here by watching the bow, the arrows and the teleport.
/// Other scripts can play any sound with <c>RangeAudio.Play(RangeAudio.Cue.X, position)</c>.
/// </summary>
public class RangeAudio : MonoBehaviour
{
    public enum Cue
    {
        LimbClick,        // a limb clicks into the riser
        MetalClick,       // any other part clicks in (riser into the stand, sight, stabiliser)
        StringMove,       // the string is attached; the bow is picked up or put down
        Screw,            // screwing in the stabiliser (not played yet: screwing is not built)
        BowDraw,          // the string is pulled back
        BowRelease,       // the string is let go (the loud snap)
        StringDetension,  // the string is eased back without a shot
        ArrowWhoosh,      // the arrow leaves the bow
        ArrowHit,         // the arrow strikes the target
        Miss,             // the arrow missed the target (failure chord)
        Teleport,         // the player teleports (dirt crunch)
        BowBuilt,         // the bow has been fully assembled
        PartDrop          // a loose part is dropped or knocked on the table or floor
    }

    [Serializable]
    public class Entry
    {
        public Cue cue;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Random pitch change (plus or minus), so repeats do not sound identical.")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.04f;
        [Tooltip("On: heard from where it happens (3D). Off: the same in both ears, for feedback sounds.")]
        public bool positional = true;
    }

    public static RangeAudio Instance { get; private set; }

    [SerializeField] Entry[] sounds;
    [SerializeField] int voices = 12;
    [SerializeField] float minDistance = 1.5f;
    [SerializeField] float maxDistance = 30f;

    [Header("Shooting")]
    [SerializeField] TeleportationProvider teleportationProvider;   // found automatically if empty
    [SerializeField] float whooshDelay = 0.03f;                     // the whoosh follows the snap of the string a moment later
    [SerializeField] float drawMinSpeed = 0.25f;                    // metres per second the string must move to creak again while still drawing
    [SerializeField] float minShotDraw = 0.08f;                     // same as the bow's minimum draw: shorter pulls do not fire

    [Header("Debug")]
    [SerializeField] bool logPlays;

    /// <summary>The last sound played and when (for testing).</summary>
    public Cue LastCue { get; private set; }
    public float LastCueTime { get; private set; } = -1f;
    public int PlayCount { get; private set; }

    readonly Dictionary<Cue, Entry> byCue = new Dictionary<Cue, Entry>();
    AudioSource[] pool;
    int nextVoice;

    class BowState
    {
        public bool held, drawing;
        public float lastDraw;
        public float nextCreak;
        public int endFrame = -1;
    }
    readonly Dictionary<Bow, BowState> bows = new Dictionary<Bow, BowState>();
    int loosedFrame = -10;

    void Awake()
    {
        Instance = this;
        foreach (var e in sounds)
            if (e != null && e.clip != null) byCue[e.cue] = e;

        pool = new AudioSource[Mathf.Max(1, voices)];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("Voice " + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            pool[i] = s;
        }
    }

    void OnEnable()
    {
        Bow.Loosed += OnLoosed;
        Arrow.Landed += OnArrowLanded;
        if (teleportationProvider == null) teleportationProvider = FindFirstObjectByType<TeleportationProvider>();
        if (teleportationProvider != null) teleportationProvider.locomotionEnded += OnTeleported;
    }

    void OnDisable()
    {
        Bow.Loosed -= OnLoosed;
        Arrow.Landed -= OnArrowLanded;
        if (teleportationProvider != null) teleportationProvider.locomotionEnded -= OnTeleported;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- playing
    /// <summary>Plays a sound at a place in the world. Does nothing if there is no RangeAudio in the scene or the cue has no clip.</summary>
    public static void Play(Cue cue, Vector3 position, float volumeScale = 1f, float delay = 0f)
    {
        if (Instance != null) Instance.PlayHere(cue, position, volumeScale, delay);
    }

    void PlayHere(Cue cue, Vector3 position, float volumeScale, float delay)
    {
        if (!byCue.TryGetValue(cue, out Entry e)) return;

        var s = pool[nextVoice];
        nextVoice = (nextVoice + 1) % pool.Length;
        s.Stop();
        s.transform.position = position;
        s.spatialBlend = e.positional ? 1f : 0f;
        s.clip = e.clip;
        s.volume = Mathf.Clamp01(e.volume * volumeScale);
        s.pitch = 1f + UnityEngine.Random.Range(-e.pitchVariation, e.pitchVariation);
        if (delay > 0f) s.PlayDelayed(delay); else s.Play();

        LastCue = cue; LastCueTime = Time.time; PlayCount++;
        if (logPlays) Debug.Log("[Audio] " + cue + " (" + e.clip.name + ")", this);
    }

    // ---------------------------------------------------------------- the bow
    void LateUpdate()
    {
        foreach (var bow in Bow.All)
        {
            if (bow == null) continue;
            if (!bows.TryGetValue(bow, out BowState st))
            {
                st = new BowState { held = bow.IsHeld, drawing = bow.Drawing };   // first sight of a bow: no sound for how it already is
                bows[bow] = st;
            }

            // picked up / put down
            bool held = bow.IsHeld;
            if (held != st.held)
            {
                st.held = held;
                Play(Cue.StringMove, bow.transform.position);
            }

            // pulling back
            bool drawing = bow.Drawing;
            Vector3 stringPos = bow.transform.position;
            if (drawing && !st.drawing)
            {
                Play(Cue.BowDraw, stringPos);
                st.nextCreak = Time.time + 1f;
                st.endFrame = -1;
            }
            else if (drawing)
            {
                float speed = (bow.Draw - st.lastDraw) / Mathf.Max(Time.deltaTime, 0.0001f);   // still pulling (not just holding): creak again
                if (speed > drawMinSpeed && Time.time >= st.nextCreak)
                {
                    Play(Cue.BowDraw, stringPos, Mathf.Clamp01(speed / 1.5f));
                    st.nextCreak = Time.time + 1f;
                }
            }
            else if (st.drawing)
            {
                st.endFrame = Time.frameCount;   // the draw just ended: decide what it was a frame later, when the bow has reported it
            }
            if (drawing) st.lastDraw = bow.Draw;
            st.drawing = drawing;

            if (st.endFrame >= 0 && Time.frameCount > st.endFrame)
            {
                st.endFrame = -1;
                ResolveDrawEnd(bow, st);
            }
        }
    }

    void ResolveDrawEnd(Bow bow, BowState st)
    {
        if (Time.frameCount - loosedFrame <= 2) return;                      // a shot: OnLoosed has already played it
        if (bow.HasArrow || st.lastDraw < minShotDraw)
            Play(Cue.StringDetension, bow.transform.position);               // string eased back, arrow still on it (or a tiny pull)
        else
            Play(Cue.BowRelease, bow.transform.position);                    // string let go with no arrow: it snaps
    }

    void OnLoosed(Bow bow)
    {
        loosedFrame = Time.frameCount;
        Vector3 p = bow.transform.position;
        Play(Cue.BowRelease, p);
        Play(Cue.ArrowWhoosh, p, 1f, whooshDelay);
    }

    // ---------------------------------------------------------------- arrows and the player
    void OnArrowLanded(Arrow arrow, bool onTarget, Vector3 point)
    {
        Play(onTarget ? Cue.ArrowHit : Cue.Miss, point);
    }

    void OnTeleported(UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider provider)
    {
        var cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : transform.position;
        Play(Cue.Teleport, at);
    }
}
