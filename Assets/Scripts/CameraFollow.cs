using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public float smoothSpeed = 8f;

    [Header("Arena bounds (camera never shows past these)")]
    public float arenaHalfWidth = 25.5f;
    public float arenaHalfHeight = 10.5f;

    static CameraFollow instance;

    Transform target;
    Vector3 shakeOffset;
    Camera cam;

    void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (target == null)
        {
            FindLocalPlayer();
            return;
        }

        Vector3 desired = target.position + offset;

        // Keep the view inside the arena so the player never sees past the walls.
        if (cam != null && cam.orthographic)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            desired.x = (halfW >= arenaHalfWidth)
                ? 0f
                : Mathf.Clamp(desired.x, -arenaHalfWidth + halfW, arenaHalfWidth - halfW);

            desired.y = (halfH >= arenaHalfHeight)
                ? 0f
                : Mathf.Clamp(desired.y, -arenaHalfHeight + halfH, arenaHalfHeight - halfH);
        }

        transform.position = Vector3.Lerp(transform.position, desired + shakeOffset, smoothSpeed * Time.deltaTime);
    }

    void FindLocalPlayer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
        var localObj = NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject();
        if (localObj != null) target = localObj.transform;
    }

    public static void Shake(float duration, float magnitude)
    {
        if (instance != null)
            instance.StartCoroutine(instance.ShakeRoutine(duration, magnitude));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            shakeOffset = Random.insideUnitCircle * magnitude;
            elapsed += Time.deltaTime;
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }
}
