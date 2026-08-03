using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    [SerializeField] bool enableSceneManagement = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        var nm = GetComponent<NetworkManager>();
        if (nm != null)
            nm.NetworkConfig.EnableSceneManagement = enableSceneManagement;

        if (GetComponent<MatchmakingService>() == null)
            gameObject.AddComponent<MatchmakingService>();
    }
}

public class NetworkManagerSceneGuard : MonoBehaviour
{
    void Awake()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.gameObject != gameObject)
            Destroy(gameObject);
    }
}
