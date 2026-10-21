using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum GuideStep { Welcome, PickUpBow, PickUpArrow, NockArrow, Draw, Release, Loosed, RoundComplete }

/// <summary>One screen of the board. Give it an image (16:9, e.g. 1920x1080) to replace the text layout.</summary>
[Serializable]
public class BoardSlide
{
    public GuideStep step;
    public string label;                 // small line above the title, e.g. "STEP 1 OF 5"
    public string title;
    [TextArea(2, 6)] public string body;   // {draw} and {bow} become "right"/"left" for the archer's drawing / bow hand
    public Texture2D image;
}

/// <summary>
/// The instruction board down the range. It shows the next step of shooting and how to do it, working out the step
/// from what the archer is doing (bow held, arrow in hand, string drawn, ...).
/// There is ONE board: it stands at one of two markers depending on handedness (move the markers in the scene).
/// Its screen faces the board's local +Z, so the markers should point at the shooting line.
/// </summary>
public class InstructionBoard : MonoBehaviour
{
    [Header("Where it stands (move these two markers in the scene)")]
    [SerializeField] Transform rightHandedSpot;
    [SerializeField] Transform leftHandedSpot;

    [Header("Screen")]
    [SerializeField] Renderer screen;
    [SerializeField] TMP_Text labelText;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] Color panelColor = new Color(0.055f, 0.075f, 0.11f, 1f);

    [Header("Text size in dmm (1 dmm = 1 mm seen from 1 m), so it follows the archer's distance from the board")]
    [SerializeField] Transform viewer;                 // where the archer stands (the Shooting Spot)
    [SerializeField] float labelDmm = 28f;
    [SerializeField] float titleMaxDmm = 52f;
    [SerializeField] float titleMinDmm = 36f;
    [SerializeField] float bodyMaxDmm = 40f;
    [SerializeField] float bodyMinDmm = 28f;           // never smaller than this: the course slides recommend 20 dmm at least

    [Header("Slides")]
    [SerializeField] List<BoardSlide> slides = new List<BoardSlide>();
    [SerializeField] float loosedShowTime = 2.5f;      // "arrow away" stays up this long after a shot

    [Header("Editor preview (right-click the component > Preview Slide)")]
    [SerializeField] GuideStep previewStep = GuideStep.Welcome;

    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock block;
    GuideStep shownStep = (GuideStep)(-1);
    Handedness shownHand = (Handedness)(-1);
    float loosedUntil;

    public GuideStep CurrentStep { get; private set; }

    /// <summary>Distance (m) from the archer's spot to the board, measured along the ground.</summary>
    public float ViewDistance
    {
        get
        {
            if (viewer == null) return 3f;
            Vector3 d = transform.position - viewer.position;
            d.y = 0f;
            return Mathf.Max(0.5f, d.magnitude);
        }
    }

    void OnEnable()
    {
        Bow.Loosed += OnLoosed;
        var hands = HandednessManager.Instance;
        if (hands != null) { hands.Changed += OnHandednessChanged; PlaceFor(hands.Current); }
    }

    void OnDisable()
    {
        Bow.Loosed -= OnLoosed;
        var hands = HandednessManager.Instance;
        if (hands != null) hands.Changed -= OnHandednessChanged;
    }

    void Start()
    {
        // HandednessManager may have woken after us
        var hands = HandednessManager.Instance;
        if (hands != null) { hands.Changed -= OnHandednessChanged; hands.Changed += OnHandednessChanged; PlaceFor(hands.Current); }
        else ApplyTextSizes();
    }

    void OnHandednessChanged(Handedness h) => PlaceFor(h);

    void OnLoosed(Bow bow) => loosedUntil = Time.time + loosedShowTime;

    void PlaceFor(Handedness h)
    {
        Transform spot = h == Handedness.Right ? rightHandedSpot : leftHandedSpot;
        if (spot != null) transform.SetPositionAndRotation(spot.position, spot.rotation);
        ApplyTextSizes();
    }

    // font sizes from angular sizes: TextMeshPro world text is 0.1 m per font-size unit, so size = dmm * 0.001 * distance * 10
    [ContextMenu("Apply Text Sizes")]
    void ApplyTextSizes()
    {
        float k = ViewDistance * 0.01f;
        SetSize(labelText, labelDmm, labelDmm, k);
        SetSize(titleText, titleMinDmm, titleMaxDmm, k);
        SetSize(bodyText, bodyMinDmm, bodyMaxDmm, k);
    }

    static void SetSize(TMP_Text text, float minDmm, float maxDmm, float k)
    {
        if (text == null) return;
        text.enableAutoSizing = true;
        text.fontSizeMin = minDmm * k;
        text.fontSizeMax = maxDmm * k;
        text.fontSize = maxDmm * k;
    }

    void Update()
    {
        var hands = HandednessManager.Instance;
        Handedness hand = hands != null ? hands.Current : Handedness.Right;
        CurrentStep = Evaluate();
        if (CurrentStep != shownStep || hand != shownHand)
        {
            shownStep = CurrentStep;
            shownHand = hand;
            Show(CurrentStep, hand);
        }
    }

    /// <summary>What should the archer be doing right now?</summary>
    GuideStep Evaluate()
    {
        var range = RangeStateController.Instance;
        if (range != null && range.State != RangeState.Shooting) return GuideStep.Welcome;
        if (Time.time < loosedUntil) return GuideStep.Loosed;

        Bow bow = null;
        foreach (var b in Bow.All) if (b.HeldByOffHand) { bow = b; break; }
        if (bow == null) return GuideStep.PickUpBow;
        if (bow.Drawing) return GuideStep.Release;
        if (bow.HasArrow) return GuideStep.Draw;

        var quiver = Quiver.Instance;
        if (quiver != null)
        {
            if (quiver.Holding) return GuideStep.NockArrow;
            if (quiver.ArrowsLeft == 0) return GuideStep.RoundComplete;
        }
        return GuideStep.PickUpArrow;
    }

    // ---------- showing a slide ----------

    BoardSlide Find(GuideStep step)
    {
        foreach (var s in slides) if (s.step == step) return s;
        return null;
    }

    static string Fill(string text, Handedness drawHand)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string draw = drawHand == Handedness.Right ? "right" : "left";
        string bow = drawHand == Handedness.Right ? "left" : "right";
        return text.Replace("{draw}", draw).Replace("{bow}", bow);
    }

    void Show(GuideStep step, Handedness hand)
    {
        if (screen == null) return;
        var slide = Find(step);
        bool picture = slide != null && slide.image != null;

        if (block == null) block = new MaterialPropertyBlock();
        screen.GetPropertyBlock(block);
        block.SetTexture(BaseMap, picture ? slide.image : Texture2D.whiteTexture);
        block.SetColor(BaseColor, picture ? Color.white : panelColor);
        screen.SetPropertyBlock(block);

        bool text = !picture;
        if (labelText != null) { labelText.gameObject.SetActive(text); if (slide != null) labelText.text = Fill(slide.label, hand); }
        if (titleText != null) { titleText.gameObject.SetActive(text); if (slide != null) titleText.text = Fill(slide.title, hand); }
        if (bodyText != null) { bodyText.gameObject.SetActive(text); if (slide != null) bodyText.text = Fill(slide.body, hand); }
    }

    [ContextMenu("Preview Slide")]
    void PreviewSlide() { ApplyTextSizes(); Show(previewStep, Handedness.Right); }

    [ContextMenu("Load Default Slides")]
    public void LoadDefaultSlides()
    {
        slides = new List<BoardSlide>
        {
            new BoardSlide { step = GuideStep.Welcome, label = "WELCOME", title = "Welcome to the range",
                body = "Choose your shooting hand, then press START to walk to the shooting line." },
            new BoardSlide { step = GuideStep.PickUpBow, label = "STEP 1 OF 5", title = "Take hold of the bow",
                body = "Grab the bow with your {bow} hand. Your {draw} hand will draw the string." },
            new BoardSlide { step = GuideStep.PickUpArrow, label = "STEP 2 OF 5", title = "Take an arrow",
                body = "With your {draw} hand, squeeze grip or trigger on an arrow sticking out of the tube and pull it free." },
            new BoardSlide { step = GuideStep.NockArrow, label = "STEP 3 OF 5", title = "Nock the arrow",
                body = "Bring the back of the arrow to the string, on the arrow rest just above the grip. It clicks on." },
            new BoardSlide { step = GuideStep.Draw, label = "STEP 4 OF 5", title = "Draw the string",
                body = "Put your {draw} hand on the string, squeeze and hold the trigger, then pull straight back." },
            new BoardSlide { step = GuideStep.Release, label = "STEP 5 OF 5", title = "Aim and release",
                body = "Hold steady, line up the target, then let go of the trigger to shoot." },
            new BoardSlide { step = GuideStep.Loosed, label = "ARROW AWAY", title = "Nice shot",
                body = "Watch it fly. Take the next arrow when you are ready." },
            new BoardSlide { step = GuideStep.RoundComplete, label = "ROUND COMPLETE", title = "That is all your arrows",
                body = "They return to the tube in a moment. Shoot another round, or press EXIT to leave the line." },
        };
    }
}
