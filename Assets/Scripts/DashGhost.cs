using UnityEngine;

// Afterimage left behind by a dash. Fades and removes itself.
public class DashGhost : MonoBehaviour
{
    SpriteRenderer sr;
    float life;
    const float Duration = 0.28f;
    float startAlpha;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) startAlpha = sr.color.a;
    }

    void Update()
    {
        life += Time.deltaTime;
        if (sr != null)
        {
            var c = sr.color;
            c.a = Mathf.Lerp(startAlpha, 0f, life / Duration);
            sr.color = c;
        }
        if (life >= Duration) Destroy(gameObject);
    }
}
