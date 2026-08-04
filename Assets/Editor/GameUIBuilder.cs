using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;

// Editor-side factory for the game's UI components. Screens are assembled from
// these so spacing, outlines and colours stay identical everywhere.
public static class GameUIBuilder
{
    static Font font;
    public static Font Font => font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    static Sprite Load(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/" + n + ".png");
    public static Sprite RoundFill => Load("ui_round_fill");
    public static Sprite RoundOutline => Load("ui_round_outline");
    public static Sprite PillFill => Load("ui_pill_fill");
    public static Sprite PillOutline => Load("ui_pill_outline");
    public static Sprite CircleFill => Load("ui_circle_fill");
    public static Sprite CircleOutline => Load("ui_circle_outline");

    public static GameObject New(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var c in comps) go.AddComponent(c);
        return go;
    }

    public static RectTransform Stretch(GameObject go, float l = 0, float b = 0, float r = 0, float t = 0)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        return rt;
    }

    public static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static Text Label(string name, Transform parent, string text, int size, Color color,
                             TextAnchor anchor = TextAnchor.MiddleCenter, bool outline = true)
    {
        var go = New(name, parent, typeof(CanvasRenderer));
        var t = go.AddComponent<Text>();
        t.font = Font; t.text = text; t.fontSize = size; t.color = color;
        t.alignment = anchor; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (outline)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(3f, -3f);
        }
        return t;
    }

    /// <summary>Panel: dark fill with a heavy outline ring.</summary>
    public static GameObject Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                                   Color? fill = null)
    {
        var root = New(name, parent);
        Place(root, anchor, pos, size);

        var f = New("Fill", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(f);
        var fi = f.GetComponent<UIImage>();
        fi.sprite = RoundFill; fi.type = UIImage.Type.Sliced;
        fi.color = fill ?? GameUI.Panel;

        var o = New("Outline", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(o);
        var oi = o.GetComponent<UIImage>();
        oi.sprite = RoundOutline; oi.type = UIImage.Type.Sliced;
        oi.color = GameUI.Outline; oi.raycastTarget = false;

        return root;
    }

    /// <summary>
    /// Chunky game button: solid darker bottom edge rather than a soft shadow.
    /// Returns the root; the Button component lives on it.
    /// </summary>
    public static Button Btn(string name, Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size,
                             Color fill, int fontSize = GameUI.ButtonSize, float edge = GameUI.ButtonEdge,
                             Color? textColor = null)
    {
        var root = New(name, parent, typeof(Button), typeof(MenuButtonEffects));
        Place(root, anchor, pos, size);

        // bottom edge sits behind and below
        var e = New("Edge", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        var ert = Stretch(e);
        ert.offsetMin = new Vector2(0f, -edge);
        var ei = e.GetComponent<UIImage>();
        ei.sprite = RoundFill; ei.type = UIImage.Type.Sliced;
        ei.color = GameUI.Darken(fill, 0.38f); ei.raycastTarget = false;

        var f = New("Fill", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(f);
        var fi = f.GetComponent<UIImage>();
        fi.sprite = RoundFill; fi.type = UIImage.Type.Sliced;
        fi.color = fill;

        var o = New("Outline", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        var ort = Stretch(o);
        ort.offsetMin = new Vector2(0f, -edge);
        var oi = o.GetComponent<UIImage>();
        oi.sprite = RoundOutline; oi.type = UIImage.Type.Sliced;
        oi.color = GameUI.Outline; oi.raycastTarget = false;

        var lbl = Label("Text", root.transform, text, fontSize, textColor ?? Color.white);
        Stretch(lbl.gameObject);
        lbl.raycastTarget = false;

        var btn = root.GetComponent<Button>();
        btn.targetGraphic = fi;
        return btn;
    }

    /// <summary>Pill-shaped dark well, used for currency and status.</summary>
    public static GameObject Chip(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                                  out Text text, string content = "0", Color? textColor = null)
    {
        var root = New(name, parent);
        Place(root, anchor, pos, size);

        var f = New("Fill", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(f);
        var fi = f.GetComponent<UIImage>();
        fi.sprite = PillFill; fi.type = UIImage.Type.Sliced;
        fi.color = new Color(0f, 0f, 0f, 0.45f);

        var o = New("Outline", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(o);
        var oi = o.GetComponent<UIImage>();
        oi.sprite = PillOutline; oi.type = UIImage.Type.Sliced;
        oi.color = GameUI.Outline; oi.raycastTarget = false;

        text = Label("Text", root.transform, content, GameUI.BodySize + 4, textColor ?? GameUI.Cream);
        var trt = Stretch(text.gameObject, 58f, 0f, 22f, 0f);
        text.alignment = TextAnchor.MiddleLeft;

        return root;
    }

    /// <summary>Round coin token for currency chips.</summary>
    public static GameObject Coin(string name, Transform parent, Vector2 anchoredPos, float size)
    {
        var root = New(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        var f = New("Fill", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(f);
        var fi = f.GetComponent<UIImage>();
        fi.sprite = CircleFill; fi.color = GameUI.Coin; fi.raycastTarget = false;

        var o = New("Outline", root.transform, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(o);
        var oi = o.GetComponent<UIImage>();
        oi.sprite = CircleOutline; oi.color = GameUI.Outline; oi.raycastTarget = false;

        return root;
    }

    /// <summary>Full-screen scrim for modal overlays.</summary>
    public static GameObject Scrim(string name, Transform parent, float alpha = 0.55f)
    {
        var go = New(name, parent, typeof(CanvasRenderer), typeof(UIImage));
        Stretch(go);
        go.GetComponent<UIImage>().color = new Color(0f, 0f, 0f, alpha);
        return go;
    }
}
