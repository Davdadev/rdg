using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Entrance sequence and ambient motion for the main menu. Runs on unscaled time so
// it still plays if anything has touched Time.timeScale.
public class MenuMotion : MonoBehaviour
{
    [Header("Entrance")]
    public RectTransform character;
    public RectTransform namePlate;
    public RectTransform title;
    public RectTransform topBar;
    public RectTransform playButton;
    public RectTransform iconRail;

    [Header("Ambient")]
    public RectTransform stageGlow;
    public float playPulse = 0.035f;
    public float playPulseSpeed = 2.2f;

    Vector3 playBaseScale = Vector3.one;
    Vector3 glowBaseScale = Vector3.one;

    void Start()
    {
        if (playButton != null) playBaseScale = playButton.localScale;
        if (stageGlow != null) glowBaseScale = stageGlow.localScale;
        StartCoroutine(Entrance());
    }

    IEnumerator Entrance()
    {
        // Character lands first - it's the subject, everything else frames it.
        yield return Pop(character, 0.55f, 0.40f);
        StartCoroutine(SlideIn(namePlate, new Vector2(0f, -60f), 0.28f));
        StartCoroutine(SlideIn(title,     new Vector2(-180f, 0f), 0.30f));
        yield return new WaitForSecondsRealtime(0.06f);
        StartCoroutine(SlideIn(topBar,    new Vector2(0f, 120f), 0.28f));
        yield return new WaitForSecondsRealtime(0.06f);
        StartCoroutine(SlideIn(iconRail,  new Vector2(-200f, 0f), 0.30f));
        yield return new WaitForSecondsRealtime(0.06f);
        yield return Pop(playButton, 0.7f, 0.34f);
    }

    IEnumerator Pop(RectTransform rt, float from, float dur)
    {
        if (rt == null) yield break;
        Vector3 target = rt == playButton ? playBaseScale : rt.localScale;
        float e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / dur);
            // overshoot then settle
            float s = Mathf.LerpUnclamped(from, 1f, 1f - Mathf.Pow(1f - k, 3f));
            s += Mathf.Sin(k * Mathf.PI) * 0.10f;
            rt.localScale = target * s;
            yield return null;
        }
        rt.localScale = target;
    }

    IEnumerator SlideIn(RectTransform rt, Vector2 offset, float dur)
    {
        if (rt == null) yield break;
        Vector2 target = rt.anchoredPosition;
        Vector2 start = target + offset;
        float e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / dur);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, target, 1f - Mathf.Pow(1f - k, 3f));
            yield return null;
        }
        rt.anchoredPosition = target;
    }

    void Update()
    {
        float t = Time.unscaledTime;

        // Slow breathe on the primary action so the eye returns to it.
        if (playButton != null)
            playButton.localScale = playBaseScale * (1f + Mathf.Sin(t * playPulseSpeed) * playPulse);

        // Stage light drifts, which keeps the centre from feeling like a still image.
        if (stageGlow != null)
            stageGlow.localScale = glowBaseScale * (1f + Mathf.Sin(t * 0.7f) * 0.05f);
    }
}
