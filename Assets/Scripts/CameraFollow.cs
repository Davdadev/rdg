using Unity.Netcode;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public float smoothSpeed = 8f;

    Transform target;

    void Update()
    {
        if (target == null)
        {
            FindLocalPlayer();
            return;
        }

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }

    void FindLocalPlayer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
        var localObj = NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject();
        if (localObj != null) target = localObj.transform;
    }
}
