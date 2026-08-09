using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

// First-run coaching for the arena. Each step waits for the player to actually perform the
// action rather than tapping through, because the things that confuse new players here are
// the territory rule and the return leg - neither of which a text wall conveys.
//
// The overlay is built in code rather than authored into the scene so it cannot drift out
// of sync with the HUD it points at, and so a missing control degrades to "skip the step"
// instead of a broken prefab reference.
public class TutorialController : MonoBehaviour
{
    const string DoneKey = "tutorial_completed_v1";

    enum Step { Move, Dash, GrabFlag, Score, Done }

    [SerializeField] float carryDistance = 1.2f;

    Step step = Step.Move;
    Transform localPlayer;
    NetworkPlayerController localController;

    CanvasGroup group;
    Text instruction;
    RectTransform highlight;

    int scoreBaseline = -1;
    float moveHeldFor;

    public static void ResetProgress() => PlayerPrefs.DeleteKey(DoneKey);

    void Start()
    {
        if (PlayerPrefs.GetInt(DoneKey, 0) == 1) { Destroy(gameObject); return; }

        var canvas = FindHudCanvas();
        if (canvas == null) { enabled = false; return; }

        Build(canvas);
        ShowStep();
    }

    void Update()
    {
        if (group == null) return;

        if (localPlayer == null)
        {
            var obj = NetworkManager.Singleton?.SpawnManager?.GetLocalPlayerObject();
            if (obj == null) return;
            localPlayer = obj.transform;
            localController = obj.GetComponent<NetworkPlayerController>();
        }

        switch (step)
        {
            case Step.Move:     CheckMove();     break;
            case Step.Dash:     CheckDash();     break;
            case Step.GrabFlag: CheckGrabFlag(); break;
            case Step.Score:    CheckScore();    break;
        }
    }

    void CheckMove()
    {
        var joy = VirtualJoystick.Instance;
        if (joy == null) { Advance(); return; }   // desktop build - nothing to point at

        if (joy.InputDirection.sqrMagnitude > 0.12f)
            moveHeldFor += Time.unscaledDeltaTime;

        if (moveHeldFor > 0.6f) Advance();
    }

    void CheckDash()
    {
        if (localController == null) { Advance(); return; }
        if (localController.IsDashing) Advance();
    }

    void CheckGrabFlag()
    {
        if (CarriedFlagNearLocalPlayer() != null) Advance();
    }

    void CheckScore()
    {
        var gm = NetworkGameManager.Instance;
        if (gm == null) { Finish(); return; }

        int total = gm.redScore.Value + gm.blueScore.Value;
        if (scoreBaseline < 0) scoreBaseline = total;
        if (total > scoreBaseline) Finish();
    }

    // Same approach FlagCarryHud uses: the carrier reference is server-side, but a carried
    // flag is synced onto the carrier's position, so proximity is the reliable client tell.
    NetworkFlagController CarriedFlagNearLocalPlayer()
    {
        if (localPlayer == null) return null;

        foreach (var flag in FindObjectsByType<NetworkFlagController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (flag == null || flag.state.Value != FlagState.Carried) continue;
            if (Vector2.Distance(flag.transform.position, localPlayer.position) <= carryDistance)
                return flag;
        }
        return null;
    }

    void Advance()
    {
        step = (Step)((int)step + 1);
        moveHeldFor = 0f;
        if (step == Step.Done) { Finish(); return; }
        ShowStep();
    }

    void ShowStep()
    {
        switch (step)
        {
            case Step.Move:
                Say("Drag to move", "Joystick_Bg", "Joystick_Container");
                break;
            case Step.Dash:
                Say("Tap to dash - it has a cooldown, so save it", "DashButton");
                break;
            case Step.GrabFlag:
                Say("Take the enemy flag.\nYou are safe on your half - on theirs you can be tagged");
                break;
            case Step.Score:
                Say("Now run it home. You only score if your own flag is still at base");
                break;
        }
    }

    // Takes candidates because the joystick's visual base and its input host are separate
    // objects, and which one exists is a scene detail this should not be coupled to.
    void Say(string text, params string[] highlightCandidates)
    {
        if (instruction != null) instruction.text = text;
        if (highlight == null) return;

        RectTransform target = null;
        foreach (var name in highlightCandidates)
        {
            var t = FindDeep(instruction.canvas.transform, name);
            target = t as RectTransform;
            if (target != null) break;
        }

        highlight.gameObject.SetActive(target != null);
        if (target == null) return;

        highlight.position = target.position;
        var size = target.rect.size * 2.1f;
        highlight.sizeDelta = new Vector2(Mathf.Max(size.x, 260f), Mathf.Max(size.y, 260f));
    }

    void Finish()
    {
        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.Save();
        step = Step.Done;
        // Hide via CanvasGroup, then destroy on the next frame - never SetActive(false) on
        // our own GameObject, which would stop Update() before the cleanup runs.
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
        Destroy(gameObject, 0.1f);
    }

    Canvas FindHudCanvas()
    {
        Canvas best = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (best == null || c.sortingOrder >= best.sortingOrder) best = c;
        }
        return best;
    }

    void Build(Canvas canvas)
    {
        var rootGo = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(CanvasGroup));
        var rootRt = (RectTransform)rootGo.transform;
        rootRt.SetParent(canvas.transform, false);
        Stretch(rootRt);
        rootRt.SetAsLastSibling();

        group = rootGo.GetComponent<CanvasGroup>();
        // Never block raycasts: gameplay controls must stay live, since every step requires
        // the player to use them. A coaching overlay that eats input soft-locks first launch.
        group.blocksRaycasts = false;
        group.interactable = false;

        var scrim = NewImage("Scrim", rootRt);
        Stretch((RectTransform)scrim.transform);
        scrim.color = new Color(0f, 0f, 0f, 0.5f);
        scrim.raycastTarget = false;

        var glow = Resources.Load<Sprite>("UI/ui_glow");
        var hl = NewImage("Highlight", rootRt);
        hl.sprite = glow;
        hl.color = new Color(1f, 0.85f, 0.3f, glow != null ? 0.55f : 0.22f);
        hl.raycastTarget = false;
        highlight = (RectTransform)hl.transform;
        highlight.anchorMin = highlight.anchorMax = new Vector2(0.5f, 0.5f);
        highlight.gameObject.SetActive(false);

        // Sits clear of the score chips along the top edge, and on its own plate: the arena
        // floor is mid-tone and white text on it alone is unreadable at a glance.
        var plate = NewImage("InstructionPlate", rootRt);
        var plateRt = (RectTransform)plate.transform;
        plateRt.anchorMin = plateRt.anchorMax = new Vector2(0.5f, 1f);
        plateRt.pivot = new Vector2(0.5f, 1f);
        plateRt.anchoredPosition = new Vector2(0f, -230f);
        // Sized for the longest step (the territory-rule line), which wraps to three rows.
        plateRt.sizeDelta = new Vector2(1180f, 220f);
        plate.color = new Color(0f, 0f, 0f, 0.72f);
        plate.raycastTarget = false;

        var textGo = new GameObject("Instruction", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var textRt = (RectTransform)textGo.transform;
        textRt.SetParent(plateRt, false);
        Stretch(textRt);
        textRt.offsetMin = new Vector2(32f, 20f);
        textRt.offsetMax = new Vector2(-32f, -20f);

        instruction = textGo.GetComponent<Text>();
        instruction.font = ResolveFont(canvas);
        instruction.fontSize = 56;
        instruction.alignment = TextAnchor.MiddleCenter;
        instruction.color = Color.white;
        instruction.raycastTarget = false;
        instruction.horizontalOverflow = HorizontalWrapMode.Wrap;
        instruction.verticalOverflow = VerticalWrapMode.Overflow;
    }

    static Image NewImage(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Borrow whatever font the HUD already uses so the coaching text matches; the built-in
    // Arial that older Unity versions shipped is gone in Unity 6.
    static Font ResolveFont(Canvas canvas)
    {
        foreach (var t in canvas.GetComponentsInChildren<Text>(true))
            if (t.font != null) return t.font;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static Transform FindDeep(Transform p, string name)
    {
        if (p.name == name) return p;
        for (int i = 0; i < p.childCount; i++)
        {
            var r = FindDeep(p.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
