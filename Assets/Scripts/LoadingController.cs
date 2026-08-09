using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class LoadingController : MonoBehaviour
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private Text tipText;

    private static readonly string[] Tips = new string[]
    {
        "Tip: Grab the enemy flag and get it back to your base to score!",
        "Tip: Watch your surroundings, obstacles can block line of sight.",
        "Tip: Visit the Shop to unlock new skins and upgrades.",
        "Tip: Dash to escape a chaser or close the gap on a flag carrier.",
        "Tip: Tag an enemy carrier to send your flag straight home.",
        "Tip: Hide in bushes to break line of sight and ambush.",
    };

    // MainMenu and Lobby load almost instantly, so at 0.35s the screen was a flash the eye
    // read as a glitch rather than a transition. Long enough to register, short enough not
    // to feel like padding.
    [SerializeField] private float minimumDisplaySeconds = 0.85f;

    // Code-built pieces (used when the scene didn't wire serialized refs).
    Image barFill;
    RectTransform spinner;
    Text percentText;
    Text builtTip;

    void Start()
    {
        // Always build the polished blue/gold loading UI on a top-most opaque canvas.
        // It fully covers whatever the LoadingScreen scene authored, so the transition
        // looks consistent with the rest of the UI regardless of the old scene contents.
        BuildUI();

        builtTip.text = Tips[Random.Range(0, Tips.Length)];

        string target = string.IsNullOrEmpty(SceneLoader.TargetScene) ? SceneLoader.MainMenu : SceneLoader.TargetScene;
        StartCoroutine(LoadAsync(target));
    }

    void Update()
    {
        if (spinner != null)
            spinner.Rotate(0f, 0f, -220f * Time.unscaledDeltaTime);
    }

    IEnumerator LoadAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float startedAt = Time.unscaledTime;
        float shown = 0f;

        // These scenes finish loading in a frame or two, so the raw op.progress snaps to
        // full and the bar never reads as progress. Drive the bar off elapsed time as well
        // as real progress and take whichever is lower, so it always sweeps.
        while (true)
        {
            float elapsed = Time.unscaledTime - startedAt;
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float timed = minimumDisplaySeconds > 0f
                ? Mathf.Clamp01(elapsed / minimumDisplaySeconds)
                : 1f;

            shown = Mathf.Max(shown, Mathf.Min(real, timed));
            if (progressBar != null) progressBar.value = shown;
            if (barFill != null) barFill.fillAmount = shown;
            if (percentText != null) percentText.text = Mathf.RoundToInt(shown * 100f) + "%";

            if (shown >= 1f && op.progress >= 0.9f) break;
            yield return null;
        }

        op.allowSceneActivation = true;
    }

    // ---------------- code-built loading screen ----------------

    void BuildUI()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("LoadingCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.transform.SetParent(transform, false);

        // Full-screen deep-navy background.
        var bg = NewImage("BG", canvasGo.transform, GameUI.Ground);
        Stretch(bg.rectTransform);

        // Title / wordmark.
        var title = NewText("Title", canvasGo.transform, "CAPTURE THE FLAG", 84,
            GameUI.Crate, font);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(1000f, 130f));
        AddOutline(title.gameObject, GameUI.Outline, 4f);

        // Spinning ring above the bar as an activity cue.
        var ring = NewImage("Spinner", canvasGo.transform, GameUI.Crate);
        ring.sprite = GameUI.CircleOutline;
        ring.type = Image.Type.Simple;
        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(140f, 140f));
        // partial arc look: a second dimmed full ring behind it
        var ringBg = NewImage("SpinnerBg", canvasGo.transform, GameUI.Alpha(GameUI.PanelHi, 0.5f));
        ringBg.sprite = GameUI.CircleOutline;
        Place(ringBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(140f, 140f));
        ringBg.transform.SetSiblingIndex(ring.transform.GetSiblingIndex()); // behind
        spinner = ring.rectTransform;

        // Progress bar track.
        var track = NewImage("BarTrack", canvasGo.transform, GameUI.Outline);
        track.sprite = GameUI.PillFill; track.type = Image.Type.Sliced;
        Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(760f, 54f));

        var inset = NewImage("BarInset", track.transform, GameUI.Alpha(Color.black, 0.35f));
        inset.sprite = GameUI.PillFill; inset.type = Image.Type.Sliced; inset.raycastTarget = false;
        Stretch(inset.rectTransform, 6, 6, 6, 6);

        // Fill (image fill so it sweeps left→right cleanly with sliced pill).
        var fill = NewImage("BarFill", track.transform, GameUI.Crate);
        fill.sprite = GameUI.PillFill; fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0; fill.fillAmount = 0f; fill.raycastTarget = false;
        Stretch(fill.rectTransform, 6, 6, 6, 6);
        barFill = fill;

        var outline = NewImage("BarOutline", track.transform, GameUI.Outline);
        outline.sprite = GameUI.PillOutline; outline.type = Image.Type.Sliced; outline.raycastTarget = false;
        Stretch(outline.rectTransform);

        // Percent readout on the bar.
        percentText = NewText("Percent", track.transform, "0%", 28, Color.white, font);
        Stretch(percentText.rectTransform);
        AddOutline(percentText.gameObject, GameUI.Outline, 2f);

        // Tip line under the bar.
        var tip = NewText("Tip", canvasGo.transform, "", 30, GameUI.CreamDim, font);
        tip.fontStyle = FontStyle.Normal;
        Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(900f, 120f));
        tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        builtTip = tip;

        // The scene serialized 0.35s, which the eye reads as a glitchy flash. Guarantee a
        // readable minimum sweep regardless of the authored value.
        if (minimumDisplaySeconds < 0.85f) minimumDisplaySeconds = 0.85f;
    }

    // ---------------- helpers ----------------

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text NewText(string name, Transform parent, string content, int size, Color color, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.text = content; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void AddOutline(GameObject go, Color color, float dist)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(dist, -dist);
    }

    static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
}
