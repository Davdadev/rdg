using UnityEngine;

// World-space popup that rises and fades. Used for "+1", "TAGGED!", "FLAG!" etc.
// Spawned client-side only - purely cosmetic, never authoritative.
public class FloatingText : MonoBehaviour
{
    static Font font;

    public static void Spawn(Vector3 worldPos, string message, Color color, float size = 1f)
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var go = new GameObject("FloatingText");
        go.transform.position = worldPos + new Vector3(0f, 0.8f, 0f);

        MakeMesh(go, message, color, size, 201);

        // Offset dark copy behind it - TextMesh has no outline, and without this the
        // popups get lost against the busy grass.
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(go.transform, false);
        shadow.transform.localPosition = new Vector3(0.06f, -0.06f, 0.01f);
        MakeMesh(shadow, message, new Color(0f, 0f, 0f, 0.75f), size, 200);

        go.AddComponent<FloatingText>();
    }

    static TextMesh MakeMesh(GameObject go, string message, Color color, float size, int order)
    {
        var tm = go.AddComponent<TextMesh>();
        tm.text = message;
        tm.font = font;
        tm.fontSize = 90;
        tm.characterSize = 0.085f * size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = order;
        return tm;
    }

    TextMesh[] meshes;
    float[] baseAlpha;
    float life;
    const float Duration = 1.05f;
    Vector3 drift;

    void Awake()
    {
        meshes = GetComponentsInChildren<TextMesh>();
        baseAlpha = new float[meshes.Length];
        for (int i = 0; i < meshes.Length; i++) baseAlpha[i] = meshes[i].color.a;
        drift = new Vector3(Random.Range(-0.35f, 0.35f), 0f, 0f);
    }

    void Update()
    {
        life += Time.deltaTime;
        float t = life / Duration;

        // Pop up fast, then ease and fade out.
        transform.position += (Vector3.up * 1.9f + drift) * Time.deltaTime * (1f - t * 0.6f);

        float pop = t < 0.18f ? Mathf.Lerp(0.5f, 1.18f, t / 0.18f)
                              : Mathf.Lerp(1.18f, 1f, Mathf.Clamp01((t - 0.18f) / 0.2f));
        transform.localScale = Vector3.one * pop;

        float fade = t < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.65f) / 0.35f);
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] == null) continue;
            var c = meshes[i].color;
            c.a = baseAlpha[i] * fade;
            meshes[i].color = c;
        }

        if (life >= Duration) Destroy(gameObject);
    }
}
