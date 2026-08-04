using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// With the camera zoomed in for a close action feel, the flags and the enemy carrier
// are usually off screen. This draws edge arrows pointing at them, plus an on-screen
// marker over whoever is carrying a flag. Self-bootstrapping, client-side only.
public class OffscreenIndicator : MonoBehaviour
{
    class Tracked
    {
        public Transform target;
        public Color color;
        public string label;
        public RectTransform arrow;
        public Text arrowLabel;
        public RectTransform marker;
    }

    static OffscreenIndicator instance;
    static Font font;
    static Sprite triangle;

    Canvas canvas;
    RectTransform root;
    readonly List<Tracked> tracked = new();
    float rescanTimer;

    public static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("OffscreenIndicators");
        instance = go.AddComponent<OffscreenIndicator>();
        instance.Build();
    }

    void Build()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (triangle == null) triangle = BuildTriangleSprite();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = canvas.GetComponent<RectTransform>();
    }

    static Sprite BuildTriangleSprite()
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                // Upward pointing triangle
                float fx = Mathf.Abs(x - S * 0.5f) / (S * 0.5f);
                float fy = y / (float)S;
                bool inside = fx <= fy;
                tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null || root == null) return;

        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0f) { rescanTimer = 1f; Rescan(); }

        foreach (var t in tracked)
        {
            if (t.target == null) { HideEntry(t); continue; }

            Vector3 vp = cam.WorldToViewportPoint(t.target.position);
            bool onScreen = vp.z > 0f && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.94f;

            if (onScreen)
            {
                if (t.arrow != null) t.arrow.gameObject.SetActive(false);
                ShowMarker(t, vp);
            }
            else
            {
                if (t.marker != null) t.marker.gameObject.SetActive(false);
                ShowArrow(t, vp);
            }
        }
    }

    void HideEntry(Tracked t)
    {
        if (t.arrow != null) t.arrow.gameObject.SetActive(false);
        if (t.marker != null) t.marker.gameObject.SetActive(false);
    }

    void ShowMarker(Tracked t, Vector3 vp)
    {
        if (t.marker == null) t.marker = CreateMarker(t);
        t.marker.gameObject.SetActive(true);
        var size = root.rect.size;
        t.marker.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y + 60f);
        // gentle bob so it draws the eye
        t.marker.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 6f) * 0.12f);
    }

    void ShowArrow(Tracked t, Vector3 vp)
    {
        if (t.arrow == null) CreateArrow(t);
        t.arrow.gameObject.SetActive(true);

        if (vp.z < 0f) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }

        Vector2 size = root.rect.size;
        Vector2 centred = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (centred.sqrMagnitude < 0.0001f) centred = Vector2.up * 0.01f;

        // Push out to the screen edge, keeping a margin for the arrow itself.
        float mx = 0.5f - 0.05f, my = 0.5f - 0.07f;
        float scale = Mathf.Min(mx / Mathf.Max(Mathf.Abs(centred.x), 0.0001f),
                                my / Mathf.Max(Mathf.Abs(centred.y), 0.0001f));
        Vector2 edge = centred * scale;

        t.arrow.anchoredPosition = new Vector2(edge.x * size.x, edge.y * size.y);
        float angle = Mathf.Atan2(centred.y, centred.x) * Mathf.Rad2Deg - 90f; // sprite points up
        t.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (t.arrowLabel != null)
        {
            t.arrowLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            t.arrowLabel.text = t.label;
        }
    }

    void CreateArrow(Tracked t)
    {
        var go = new GameObject("Arrow_" + t.label, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(root, false);
        var img = go.AddComponent<Image>();
        img.sprite = triangle;
        img.color = t.color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(64f, 64f);
        t.arrow = rt;

        var lbl = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        lbl.transform.SetParent(go.transform, false);
        var text = lbl.AddComponent<Text>();
        text.font = font;
        text.fontSize = 26;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var o = lbl.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);
        var lrt = text.rectTransform;
        lrt.sizeDelta = new Vector2(200f, 40f);
        lrt.anchoredPosition = new Vector2(0f, -46f);
        t.arrowLabel = text;
    }

    RectTransform CreateMarker(Tracked t)
    {
        var go = new GameObject("Marker_" + t.label, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(root, false);
        var img = go.AddComponent<Image>();
        img.sprite = triangle;
        img.color = t.color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(44f, 44f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 180f); // point down at the target
        return rt;
    }

    // Rebuild the tracked list: both flags always, plus any active flag carrier.
    void Rescan()
    {
        foreach (var t in tracked) { if (t.arrow) Destroy(t.arrow.gameObject); if (t.marker) Destroy(t.marker.gameObject); }
        tracked.Clear();

        foreach (var flag in FindObjectsOfType<NetworkFlagController>())
        {
            if (flag == null) continue;
            tracked.Add(new Tracked
            {
                target = flag.transform,
                color = flag.team == Team.Red ? new Color(1f, 0.35f, 0.35f) : new Color(0.4f, 0.6f, 1f),
                label = flag.team == Team.Red ? "RED FLAG" : "BLUE FLAG"
            });
        }
    }
}
