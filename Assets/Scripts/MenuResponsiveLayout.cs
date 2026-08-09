using UnityEngine;
using UnityEngine.UI;

// The menu was composed for 1920x1080. That composition cannot simply be scaled into
// portrait: TopBar is a fixed 1920 wide so it overflows a 1080-wide screen, and PLAY
// (560, bottom-right) plus the icon rail (400, bottom-left) need ~1072 of the 1080
// available before they touch. So instead of one elastic arrangement this keeps two
// presets and swaps between them, and points the CanvasScaler at whichever axis is
// actually the constrained one.
// Deliberately NOT [ExecuteAlways]: in edit mode it would capture whatever the Game view
// aspect happens to be, apply the portrait overrides, and then serialize those as the
// authored layout the next time the scene is saved.
public class MenuResponsiveLayout : MonoBehaviour
{
    [Tooltip("Below this width/height ratio the portrait preset is used.")]
    [SerializeField] float portraitAspectThreshold = 1f;

    [SerializeField] CanvasScaler scaler;

    // Landscape baseline is whatever the scene was authored with, captured once so
    // rotating back is exact rather than a second set of hand-typed numbers.
    struct Snapshot
    {
        public RectTransform rt;
        public Vector2 aMin, aMax, pivot, pos, size;
    }

    // Never null: OnEnable -> Capture -> Apply -> RestoreBaseline runs before anything has
    // proven the capture succeeded, and a null here throws every frame on the menu.
    Snapshot[] baseline = new Snapshot[0];
    RectTransform topBar, titleWrap, playWrap, iconRail;
    RectTransform shopCard, settingsCard;
    RectTransform[] stageGroup;

    bool captured;
    int lastW, lastH;

    void OnEnable()
    {
        Capture();
        if (!captured) return;   // layout not found - stay inert rather than half-applying
        Apply(true);
    }

    void Update()
    {
        if (Screen.width == lastW && Screen.height == lastH) return;
        lastW = Screen.width;
        lastH = Screen.height;
        Apply(false);
    }

    void Capture()
    {
        if (captured) return;

        var root = FindChild(transform, "MenuRoot");
        if (root == null) { enabled = false; return; }

        topBar    = FindChild(root, "TopBar")   as RectTransform;
        titleWrap = FindChild(root, "TitleWrap") as RectTransform;
        playWrap  = FindChild(root, "PlayWrap")  as RectTransform;
        iconRail  = FindChild(root, "IconRail")  as RectTransform;

        // These all sit centred with their own y offsets, so portrait moves them as a
        // group rather than repositioning each one independently.
        stageGroup = new[]
        {
            FindChild(root, "Backdrop")   as RectTransform,
            FindChild(root, "StageGlow")  as RectTransform,
            FindChild(root, "Podium")     as RectTransform,
            FindChild(root, "PodiumFill") as RectTransform,
            FindChild(root, "MenuCharacter") as RectTransform,
            FindChild(root, "NamePlate")  as RectTransform,
        };

        // The modal cards live beside MenuRoot, not inside it. Shop's card is authored
        // 1500 wide, which is wider than a 1080-wide portrait screen, so it has to be
        // reshaped rather than just scaled down with everything else.
        shopCard     = FindCardIn("ShopPanel");
        settingsCard = FindCardIn("SettingsPanel");

        var all = new System.Collections.Generic.List<RectTransform>
            { topBar, titleWrap, playWrap, iconRail, shopCard, settingsCard };
        all.AddRange(stageGroup);

        var list = new System.Collections.Generic.List<Snapshot>();
        foreach (var rt in all)
        {
            if (rt == null) continue;
            list.Add(new Snapshot
            {
                rt = rt, aMin = rt.anchorMin, aMax = rt.anchorMax,
                pivot = rt.pivot, pos = rt.anchoredPosition, size = rt.sizeDelta
            });
        }
        baseline = list.ToArray();

        if (scaler == null) scaler = GetComponentInParent<CanvasScaler>();
        captured = true;
    }

    void Apply(bool force)
    {
        if (!captured) return;

        bool portrait = (float)Screen.width / Mathf.Max(1, Screen.height) < portraitAspectThreshold;

        RestoreBaseline();

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Match the constrained axis: width in portrait, height in landscape.
            // A blended 0.5 match is what made portrait shrink everything and still clip.
            scaler.referenceResolution = portrait ? new Vector2(1080f, 1920f)
                                                  : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = portrait ? 0f : 1f;
        }

        if (portrait) ApplyPortrait();
    }

    void RestoreBaseline()
    {
        if (baseline == null) return;
        foreach (var s in baseline)
        {
            if (s.rt == null) continue;
            s.rt.anchorMin = s.aMin;
            s.rt.anchorMax = s.aMax;
            s.rt.pivot = s.pivot;
            s.rt.anchoredPosition = s.pos;
            s.rt.sizeDelta = s.size;
        }
    }

    void ApplyPortrait()
    {
        // Stretch rather than keeping the authored 1920 width, which would hang off both
        // edges of a portrait screen.
        if (topBar != null)
        {
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot     = new Vector2(0.5f, 1f);
            topBar.anchoredPosition = Vector2.zero;
            topBar.sizeDelta = new Vector2(0f, topBar.sizeDelta.y);
        }

        if (titleWrap != null)
            titleWrap.anchoredPosition = new Vector2(40f, -190f);

        // Character group rides higher so the lower third is free for the controls.
        foreach (var rt in stageGroup)
        {
            if (rt == null) continue;
            rt.anchoredPosition += new Vector2(0f, 300f);
        }

        // PLAY becomes a full-width bar on the bottom edge - the strongest target on the
        // screen and reachable one-handed, which the bottom-right corner is not.
        if (playWrap != null)
        {
            playWrap.anchorMin = new Vector2(0.5f, 0f);
            playWrap.anchorMax = new Vector2(0.5f, 0f);
            playWrap.pivot     = new Vector2(0.5f, 0f);
            playWrap.anchoredPosition = new Vector2(0f, 110f);
            playWrap.sizeDelta = new Vector2(840f, 220f);
        }

        // Rail moves above PLAY as a centred row instead of competing for the bottom edge.
        if (iconRail != null)
        {
            iconRail.anchorMin = new Vector2(0.5f, 0f);
            iconRail.anchorMax = new Vector2(0.5f, 0f);
            iconRail.pivot     = new Vector2(0.5f, 0f);
            iconRail.anchoredPosition = new Vector2(0f, 370f);
        }

        // Modals go tall-and-narrow. Trading width for height keeps the same content area
        // without spilling past the screen edge, where the dim scrim would swallow it.
        if (shopCard != null)     shopCard.sizeDelta     = new Vector2(960f, 1340f);
        if (settingsCard != null) settingsCard.sizeDelta = new Vector2(960f, 900f);
    }

    // Both modals contain a child literally named "Card", so scope the lookup to the
    // owning panel rather than grabbing whichever one appears first in the hierarchy.
    RectTransform FindCardIn(string panelName)
    {
        var panel = FindChild(transform, panelName);
        return panel == null ? null : FindChild(panel, "Card") as RectTransform;
    }

    // Traverses instead of GameObject.Find, which skips inactive objects.
    static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var deep = FindChild(c, name);
            if (deep != null) return deep;
        }
        return null;
    }
}
