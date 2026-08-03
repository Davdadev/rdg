using UnityEngine;

// Keeps a RectTransform's edges inside the device's safe area
// (avoids the iPhone notch/Dynamic Island and the home indicator bar).
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform _rect;
    Rect _lastSafeArea;
    Vector2Int _lastScreenSize;
    ScreenOrientation _lastOrientation;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        if (_lastSafeArea != Screen.safeArea ||
            _lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height ||
            _lastOrientation != Screen.orientation)
        {
            Apply();
        }
    }

    void Apply()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _lastOrientation = Screen.orientation;

        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
    }
}
