using UnityEngine;
using UnityEngine.UI;

// Plays a sprite sequence on a UI Image.
//
// The Animator clips bind to SpriteRenderer.m_Sprite, which a Canvas Image can't use.
// Rather than mixing a world-space SpriteRenderer into an overlay canvas, this just
// cycles the frames directly - same result, one render path.
[RequireComponent(typeof(Image))]
public class UISpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 24f;
    public bool loop = true;

    [Header("Idle bob")]
    [Tooltip("Extra vertical drift layered on top of the animation, in canvas units.")]
    public float bobAmplitude = 8f;
    public float bobSpeed = 1.6f;

    Image image;
    RectTransform rect;
    Vector2 basePos;
    float t;

    void Awake()
    {
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        basePos = rect.anchoredPosition;
        if (frames != null && frames.Length > 0) image.sprite = frames[0];
    }

    void OnEnable()
    {
        t = 0f;
        if (rect != null) rect.anchoredPosition = basePos;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        t += Time.unscaledDeltaTime;

        int i = Mathf.FloorToInt(t * fps);
        if (loop) i %= frames.Length;
        else i = Mathf.Min(i, frames.Length - 1);
        image.sprite = frames[i];

        if (bobAmplitude > 0f)
            rect.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(t * bobSpeed) * bobAmplitude);
    }
}
