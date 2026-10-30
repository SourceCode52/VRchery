using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>One part of the bow, where it ends up, and how it fits onto the riser.</summary>
[Serializable]
public class BuildPart
{
    [Tooltip("What the screen calls it, e.g. \"Upper limb\".")]
    public string name = "Part";

    [Tooltip("The loose part the archer picks up.")]
    public XRGrabInteractable part;

    [Tooltip("The LockingSocket it ends up in.")]
    public LockingSocket socket;

    [Tooltip("Heading shown while the archer is holding this part. Leave empty for \"Fit the <name>\".")]
    public string fitTitle;

    [TextArea(2, 5)]
    [Tooltip("How this part fits onto the riser. Shown while the archer is holding it.")]
    public string fitText;
}

/// <summary>
/// The build-instructions screen beside the stand. It first tells the archer to place the riser on the stand platform.
/// After that the order does not matter, so it simply asks for another part; as soon as the archer picks one up it shows
/// how that part fits onto the riser, and when it is in it asks for the next one, until the bow is built.
///
/// It reads what is going on (which part is in a hand, which sockets have locked) so nothing has to be wired up per step.
/// The text lives in the Inspector. In the text, {list} becomes the names of the parts still to fit.
/// </summary>
public class BuildGuideScreen : MonoBehaviour
{
    [Header("Step 1: the riser goes on the stand first")]
    [SerializeField] BuildPart riser = new BuildPart { name = "Riser" };
    [SerializeField] string riserLabel = "STEP 1";
    [SerializeField] string riserTitle = "Place the riser on the stand";
    [TextArea(2, 5)]
    [SerializeField] string riserBody = "Pick up the riser and hold it over the stand platform in the right position. Let go and it locks into place.";

    [Header("Then these parts, in any order")]
    [SerializeField] List<BuildPart> parts = new List<BuildPart>();

    [Header("Between parts")]
    [SerializeField] string grabTitle = "Grab another part";
    [TextArea(2, 5)]
    [SerializeField] string grabBody = "Pick up any part from the tables. The order does not matter.\nStill to fit: {list}";

    [Header("When everything is on")]
    [SerializeField] string doneLabel = "BOW BUILT";
    [SerializeField] string doneTitle = "Your bow is built";
    [TextArea(2, 5)]
    [SerializeField] string doneBody = "Pick it up, then press START on the table by the shooting line.";

    [Header("Screen")]
    [SerializeField] TMP_Text labelText;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text bodyText;

    [Tooltip("Text sizes, in TextMeshPro font sizes (1 unit is about 0.1 m of text height). The text shrinks to fit.")]
    [SerializeField] Vector2 titleSize = new Vector2(0.42f, 0.72f);
    [SerializeField] Vector2 bodySize = new Vector2(0.3f, 0.5f);
    [SerializeField] float labelSize = 0.34f;

    [Tooltip("Seconds the screen keeps showing a part's fit after it is let go, so it does not flicker if the hand slips.")]
    [SerializeField] float holdGrace = 1f;

    bool[] latched;            // each of the parts, riser first, once its socket has locked (locks are permanent)
    bool built;                // the bow is complete (the last part locks and the parts are hidden in the same frame)
    int shownHeld = -1;
    float heldAt;
    string shownKey;

    int Count => parts.Count + 1;
    BuildPart Get(int i) => i == 0 ? riser : parts[i - 1];

    void Awake() => ApplySizes();

    void OnEnable()
    {
        var assembly = riser.socket != null ? riser.socket.assembly : null;
        if (assembly != null) assembly.onComplete.AddListener(OnBuilt);
    }

    void OnDisable()
    {
        var assembly = riser.socket != null ? riser.socket.assembly : null;
        if (assembly != null) assembly.onComplete.RemoveListener(OnBuilt);
    }

    void OnBuilt() => built = true;

    void Update()
    {
        if (latched == null || latched.Length != Count) latched = new bool[Count];
        for (int i = 0; i < Count; i++)
        {
            var p = Get(i);
            if (p.socket != null && p.socket.IsLocked) latched[i] = true;
        }

        if (built)
        {
            Show("done", doneLabel, doneTitle, doneBody);
            return;
        }

        // 1. the riser, until it is on the stand
        if (!Placed(0))
        {
            Show("riser", riserLabel, riserTitle, riserBody);
            return;
        }

        // 2. which loose part, if any, is in a hand
        int heldNow = -1;
        for (int i = 1; i < Count; i++)
            if (!Placed(i) && Held(Get(i))) { heldNow = i; break; }

        if (heldNow >= 0) { shownHeld = heldNow; heldAt = Time.time; }
        else if (shownHeld >= 0 && (Placed(shownHeld) || Time.time - heldAt > holdGrace)) shownHeld = -1;

        int left = 0;
        for (int i = 1; i < Count; i++) if (!Placed(i)) left++;

        if (left == 0 && heldNow < 0)
        {
            Show("done", doneLabel, doneTitle, doneBody);
            return;
        }

        int placed = Count - left;   // how many are on the bow already (the riser counts)

        if (shownHeld >= 0)
        {
            var p = Get(shownHeld);
            string title = string.IsNullOrEmpty(p.fitTitle) ? "Fit the " + p.name.ToLowerInvariant() : p.fitTitle;
            Show("fit" + shownHeld, "PART " + (placed + 1) + " OF " + Count, title, p.fitText);
            return;
        }

        Show("grab" + left + ":" + placed, left == 1 ? "1 PART LEFT" : left + " PARTS LEFT", grabTitle, grabBody);
    }

    // On the bow: locked for good, or sitting in its socket and about to lock.
    bool Placed(int i)
    {
        if (latched[i]) return true;
        var p = Get(i);
        if (p.socket == null || p.part == null) return false;
        var s = p.socket.GetComponent<XRSocketInteractor>();
        return s != null && s.IsSelecting(p.part);
    }

    static bool Held(BuildPart p)
    {
        if (p.part == null) return false;
        foreach (var i in p.part.interactorsSelecting)
            if (!(i is XRSocketInteractor)) return true;
        return false;
    }

    string StillToFit()
    {
        var sb = new StringBuilder();
        for (int i = 1; i < Count; i++)
        {
            if (Placed(i)) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(Get(i).name);
        }
        return sb.ToString();
    }

    void Show(string key, string label, string title, string body)
    {
        if (key == shownKey) return;
        shownKey = key;
        if (labelText != null) labelText.text = label;
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = (body ?? "").Replace("{list}", StillToFit());
    }

    [ContextMenu("Apply Text Sizes")]
    void ApplySizes()
    {
        SetSize(labelText, labelSize, labelSize);
        SetSize(titleText, titleSize.x, titleSize.y);
        SetSize(bodyText, bodySize.x, bodySize.y);
    }

    static void SetSize(TMP_Text text, float min, float max)
    {
        if (text == null) return;
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.fontSize = max;
    }
}
