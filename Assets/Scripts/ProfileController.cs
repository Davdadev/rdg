using UnityEngine;
using UnityEngine.UI;

// Player profile screen. Built entirely in code so it needs no scene wiring and
// cannot drift from a serialized layout (same approach as TutorialController).
// Opened by MainMenuController.OnPlayerPressed(); lets the player see and edit
// their name, view their avatar, and check their currency.
public class ProfileController : MonoBehaviour
{
    GameObject root;
    InputField nameInput;
    Image avatarImage;
    Text coinsText;
    Text gemsText;
    Font font;

    public void Show()
    {
        if (root == null) Build();
        Refresh();
        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    void Refresh()
    {
        if (nameInput != null) nameInput.SetTextWithoutNotify(PlayerDataService.PlayerName);
        if (coinsText != null) coinsText.text = PlayerDataService.Coins.ToString();
        if (gemsText != null) gemsText.text = PlayerDataService.Gems.ToString();
        if (avatarImage != null)
        {
            var sprite = SkinCatalog.GetPreviewSprite(PlayerDataService.EquippedSkinIndex);
            avatarImage.sprite = sprite;
            avatarImage.enabled = sprite != null;
        }
    }

    // Persist as the player types so the name is saved even if they tap Play directly.
    void OnNameChanged(string value)
    {
        PlayerDataService.PlayerName = PlayerDataService.SanitizeName(value);
    }

    // ---------------- code-built UI ----------------

    void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Overlay canvas on top of everything else in the menu.
        root = new GameObject("ProfilePanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        root.transform.SetParent(transform, false);

        // Dark scrim that also eats clicks so the menu behind can't be pressed.
        var scrim = NewImage("Scrim", root.transform, new Color(0f, 0f, 0f, 0.6f));
        Stretch(scrim.rectTransform);
        AddButton(scrim.gameObject, Hide); // tap outside to close

        // Card
        var card = NewImage("Card", root.transform, new Color(0.09f, 0.14f, 0.24f, 1f));
        Center(card.rectTransform, new Vector2(760f, 900f));

        var outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.82f, 0.28f, 1f); // gold rim
        outline.effectDistance = new Vector2(4f, -4f);

        MakeLabel("Title", card.transform, "PROFILE", 54, new Color(1f, 0.85f, 0.3f),
            new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 80f));

        // Avatar disc
        var avatarBg = NewImage("AvatarBg", card.transform, new Color(0.16f, 0.22f, 0.36f, 1f));
        Place(avatarBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(260f, 260f));
        var avatarGo = NewImage("Avatar", avatarBg.transform, Color.white);
        Place(avatarGo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
        avatarGo.preserveAspect = true;
        avatarImage = avatarGo;

        // Name field label + input
        MakeLabel("NameHdr", card.transform, "NAME", 30, new Color(0.75f, 0.82f, 0.95f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(600f, 40f));

        var inputBg = NewImage("NameField", card.transform, new Color(0.04f, 0.07f, 0.14f, 1f));
        Place(inputBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -545f), new Vector2(560f, 90f));

        var textGo = MakeLabel("Text", inputBg.transform, "", 40, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 80f));
        textGo.alignment = TextAnchor.MiddleCenter;
        textGo.supportRichText = false;

        var placeholder = MakeLabel("Placeholder", inputBg.transform, "Enter name...", 40,
            new Color(1f, 1f, 1f, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 80f));
        placeholder.alignment = TextAnchor.MiddleCenter;
        placeholder.fontStyle = FontStyle.Italic;

        nameInput = inputBg.gameObject.AddComponent<InputField>();
        nameInput.textComponent = textGo;
        nameInput.placeholder = placeholder;
        nameInput.characterLimit = 16;
        nameInput.lineType = InputField.LineType.SingleLine;
        nameInput.onValueChanged.AddListener(OnNameChanged);

        // Currency chips
        MakeChip("Coins", card.transform, new Vector2(-150f, -690f), new Color(1f, 0.82f, 0.28f, 1f),
            out coinsText);
        MakeChip("Gems", card.transform, new Vector2(150f, -690f), new Color(0.45f, 0.75f, 1f, 1f),
            out gemsText);

        // Close button
        var close = NewImage("Close", card.transform, new Color(0.85f, 0.28f, 0.32f, 1f));
        Place(close.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(360f, 96f));
        var closeOutline = close.gameObject.AddComponent<Outline>();
        closeOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        closeOutline.effectDistance = new Vector2(3f, -3f);
        var closeLbl = MakeLabel("Text", close.transform, "CLOSE", 40, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 90f));
        closeLbl.raycastTarget = false;
        AddButton(close.gameObject, Hide);
    }

    // ---------------- small UI helpers ----------------

    Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    Text MakeLabel(string name, Transform parent, string content, int size, Color color,
                   Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.text = content; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Place(t.rectTransform, anchor, pos, sizeDelta);
        return t;
    }

    void MakeChip(string name, Transform parent, Vector2 pos, Color accent, out Text valueText)
    {
        var chip = NewImage(name, parent, new Color(0f, 0f, 0f, 0.4f));
        Place(chip.rectTransform, new Vector2(0.5f, 1f), pos, new Vector2(260f, 84f));

        var dot = NewImage("Dot", chip.transform, accent);
        Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(50f, 50f));
        dot.raycastTarget = false;

        valueText = MakeLabel("Value", chip.transform, "0", 38, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(28f, 0f), new Vector2(160f, 70f));
        valueText.alignment = TextAnchor.MiddleLeft;
        valueText.raycastTarget = false;
    }

    static void AddButton(GameObject go, UnityEngine.Events.UnityAction onClick)
    {
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
}
