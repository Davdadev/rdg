using UnityEngine;

// Keeps the whole arena visible on any screen aspect ratio,
// instead of using a fixed orthographic size that can crop the sides on some iPhones.
[RequireComponent(typeof(Camera))]
public class CameraAspectFit : MonoBehaviour
{
    public float arenaHalfWidth = 26f;
    public float arenaHalfHeight = 11f;

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
        {
            Apply();
        }
    }

    void Apply()
    {
        _lastAspect = _cam.aspect;
        _cam.orthographicSize = Mathf.Max(arenaHalfHeight, arenaHalfWidth / _cam.aspect);
    }
}
