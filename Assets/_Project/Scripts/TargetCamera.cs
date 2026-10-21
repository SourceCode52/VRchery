using UnityEngine;

/// <summary>
/// A camera that looks at the target from close up and shows its picture on the screen on the table, so the archer
/// can see where the arrows landed without crossing the range.
/// It only renders while the archer is shooting, and at a limited frame rate, to keep the headset's GPU free.
/// </summary>
public class TargetCamera : MonoBehaviour
{
    [SerializeField] Camera feedCamera;            // renders into a RenderTexture (set as its Target Texture)
    [SerializeField] Renderer screen;              // the display surface; its material shows the RenderTexture
    [SerializeField] float framesPerSecond = 30f;
    [SerializeField] bool onlyWhileShooting = true;
    [SerializeField] Color screenOffColor = new Color(0.03f, 0.045f, 0.06f, 1f);

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock block;
    bool live;
    float nextFrameTime;

    void Awake()
    {
        if (feedCamera == null) { Debug.LogError("TargetCamera: no camera assigned.", this); enabled = false; return; }
        feedCamera.enabled = false;                // rendered a frame at a time from Update
        block = new MaterialPropertyBlock();
        ShowScreen(false);
    }

    void OnDisable()
    {
        if (feedCamera != null) feedCamera.enabled = false;
    }

    void Update()
    {
        var range = RangeStateController.Instance;
        bool wanted = !onlyWhileShooting || range == null || range.State == RangeState.Shooting;

        if (wanted != live)
        {
            live = wanted;
            ShowScreen(live);
            nextFrameTime = 0f;
        }

        if (!live) { feedCamera.enabled = false; return; }

        // the camera renders in the frame after it is switched on, so: on for one frame, then off until the next is due
        if (feedCamera.enabled) feedCamera.enabled = false;
        else if (Time.unscaledTime >= nextFrameTime)
        {
            feedCamera.enabled = true;
            nextFrameTime = Time.unscaledTime + 1f / Mathf.Max(1f, framesPerSecond);
        }
    }

    void ShowScreen(bool on)
    {
        if (screen == null) return;
        screen.GetPropertyBlock(block);
        block.SetColor(BaseColor, on ? Color.white : screenOffColor);
        screen.SetPropertyBlock(block);
    }
}
