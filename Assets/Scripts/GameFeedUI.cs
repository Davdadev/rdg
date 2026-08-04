using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Screen-space match feedback: centre banners, a stacked event ticker, and the
// end-of-match celebration. Builds its own canvas on first use so there is
// nothing to wire up in the scene (and nothing to silently come unwired).
public class GameFeedUI : MonoBehaviour
{
    static GameFeedUI instance;
    static Font font;

    Canvas canvas;
    RectTransform root;
    Text bannerText;
    Coroutine bannerRoutine;
    readonly List<RectTransform> toasts = new();

    public static GameFeedUI Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("GameFeedUI");
                instance = go.AddComponent<GameFeedUI>();
                instance.Build();
            }
            return instance;
        }
    }

    void Build()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the gameplay HUD
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        root = canvas.GetComponent<RectTransform>();

        // Centre banner
        var b = NewText("Banner", root, 84, TextAnchor.MiddleCenter);
        var brt = b.rectTransform;
        brt.anchorMin = new Vector2(0.5f, 0.62f);
        brt.anchorMax = new Vector2(0.5f, 0.62f);
        brt.sizeDelta = new Vector2(1600f, 160f);
        brt.anchoredPosition = Vector2.zero;
        bannerText = b;
        bannerText.gameObject.SetActive(false);
    }

    Text NewText(string name, Transform parent, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = Color.white;
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(3f, -3f);
        return t;
    }

    // ---------- centre banner ----------

    public void Banner(string message, Color color, float hold = 1.1f)
    {
        bannerText.text = message;
        bannerText.color = color;
        bannerText.gameObject.SetActive(true);
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(hold));
    }

    IEnumerator BannerRoutine(float hold)
    {
        var rt = bannerText.rectTransform;
        // Punch in
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.22f;
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.12f, Mathf.Sin(k * Mathf.PI * 0.5f));
            SetAlpha(bannerText, Mathf.Clamp01(k * 2f));
            yield return null;
        }
        // Settle
        t = 0f;
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t / 0.12f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(hold);
        // Fade out
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(bannerText, 1f - t / 0.3f);
            yield return null;
        }
        bannerText.gameObject.SetActive(false);
        bannerRoutine = null;
    }

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color; c.a = a; g.color = c;
    }

    // ---------- event ticker ----------

    public void Toast(string message, Color color)
    {
        var t = NewText("Toast", root, 40, TextAnchor.MiddleRight);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 56f);
        rt.anchoredPosition = new Vector2(-40f, 120f);
        t.text = message;
        t.color = color;

        toasts.Insert(0, rt);
        RelayoutToasts();
        StartCoroutine(ToastRoutine(t));
    }

    void RelayoutToasts()
    {
        for (int i = 0; i < toasts.Count; i++)
        {
            if (toasts[i] == null) continue;
            toasts[i].anchoredPosition = new Vector2(-40f, 120f - i * 58f);
        }
    }

    IEnumerator ToastRoutine(Text t)
    {
        var rt = t.rectTransform;
        // slide in from the right
        float e = 0f;
        Vector2 target = rt.anchoredPosition;
        Vector2 from = target + new Vector2(320f, 0f);
        while (e < 0.22f)
        {
            e += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.Lerp(from, rt.anchoredPosition, e / 0.22f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(2.2f);
        e = 0f;
        while (e < 0.35f)
        {
            e += Time.unscaledDeltaTime;
            SetAlpha(t, 1f - e / 0.35f);
            yield return null;
        }
        toasts.Remove(rt);
        RelayoutToasts();
        Destroy(t.gameObject);
    }

    // ---------- win celebration ----------

    public void Celebrate(bool localWon, string headline)
    {
        Banner(headline, localWon ? new Color(1f, 0.9f, 0.35f) : new Color(1f, 0.45f, 0.4f), 2.6f);
        if (localWon) StartCoroutine(ConfettiRoutine());
        StartCoroutine(FlashRoutine(localWon));
    }

    IEnumerator FlashRoutine(bool win)
    {
        var go = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(root, false);
        go.transform.SetAsFirstSibling();
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        Color baseCol = win ? new Color(1f, 0.95f, 0.6f) : new Color(0.2f, 0.05f, 0.05f);
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            var c = baseCol; c.a = Mathf.Lerp(0.55f, 0f, t / 0.5f);
            img.color = c;
            yield return null;
        }
        Destroy(go);
    }

    IEnumerator ConfettiRoutine()
    {
        Color[] palette = {
            new Color(1f, 0.85f, 0.25f), new Color(1f, 0.4f, 0.4f),
            new Color(0.4f, 0.8f, 1f),   new Color(0.5f, 1f, 0.5f),
            new Color(1f, 0.6f, 0.9f)
        };

        for (int burst = 0; burst < 3; burst++)
        {
            for (int i = 0; i < 26; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(root, false);
                var img = go.AddComponent<Image>();
                img.color = palette[Random.Range(0, palette.Length)];
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(Random.Range(14f, 30f), Random.Range(22f, 40f));
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(Random.Range(-900f, 900f), Random.Range(40f, 260f));
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                StartCoroutine(ConfettiPiece(rt, img));
            }
            yield return new WaitForSecondsRealtime(0.35f);
        }
    }

    IEnumerator ConfettiPiece(RectTransform rt, Image img)
    {
        float vy = Random.Range(-40f, -140f);
        float vx = Random.Range(-140f, 140f);
        float spin = Random.Range(-320f, 320f);
        float life = 0f, total = Random.Range(1.8f, 3.0f);

        while (life < total && rt != null)
        {
            float dt = Time.unscaledDeltaTime;
            life += dt;
            vy -= 420f * dt;                       // gravity
            rt.anchoredPosition += new Vector2(vx, vy) * dt;
            rt.localRotation *= Quaternion.Euler(0f, 0f, spin * dt);
            if (life > total * 0.6f)
                SetAlpha(img, 1f - (life - total * 0.6f) / (total * 0.4f));
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }
}
