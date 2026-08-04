using System.Collections;
using UnityEngine;

// Self-contained flag-capture pop: a small expanding, fading burst spawned client-side.
public class CapturePopEffect : MonoBehaviour
{
    static Sprite cachedDot;

    void Start()
    {
        if (cachedDot == null)
        {
            var tex = Texture2D.whiteTexture;
            cachedDot = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        }

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = cachedDot;
        sr.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        sr.sortingOrder = 50;

        StartCoroutine(PopRoutine(sr));
    }

    IEnumerator PopRoutine(SpriteRenderer sr)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.2f;
        Vector3 endScale = Vector3.one * 3f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            var c = sr.color;
            c.a = Mathf.Lerp(0.9f, 0f, t);
            sr.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
