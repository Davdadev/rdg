using UnityEngine;

// Tight action-game framing: the camera holds a fixed close zoom so characters read large
// on screen, rather than fitting the whole arena (which made everything tiny and flat).
// It only widens if a device is so narrow that the fixed zoom would show less than
// minVisibleWidth, so gameplay stays fair across aspect ratios.
[RequireComponent(typeof(Camera))]
public class CameraAspectFit : MonoBehaviour
{
    [Tooltip("Half-height of the view in world units. Smaller = more zoomed in.")]
    public float zoomHalfHeight = 5.5f;

    [Tooltip("Never show less horizontal play space than this (world units).")]
    public float minVisibleWidth = 17f;

    Camera _cam;
    float _lastAspect;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        Apply();
    }

    void Update()
    {
        if (!Mathf.Approximately(_cam.aspect, _lastAspect))
            Apply();
    }

    void Apply()
    {
        _lastAspect = _cam.aspect;
        // Widen only if a narrow screen would otherwise cut horizontal play space too much.
        float sizeForMinWidth = (minVisibleWidth * 0.5f) / Mathf.Max(_cam.aspect, 0.01f);
        _cam.orthographicSize = Mathf.Max(zoomHalfHeight, sizeForMinWidth);
    }
}
