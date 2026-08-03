using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Legacy LAN host/join overlay — hidden once Netcode is listening (Lobby flow handles connection).
public class NetworkConnectionUI : MonoBehaviour
{
    public string defaultIp = "127.0.0.1";
    public ushort port = 7777;

    string ipInput;
    bool menuVisible = true;

    void Start()
    {
        ipInput = defaultIp;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            menuVisible = false;
    }

    void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            menuVisible = false;
    }

    void OnGUI()
    {
        if (!menuVisible) return;

        GUILayout.BeginArea(new Rect(20, 20, 260, 160), GUI.skin.box);
        GUILayout.Label("Basic CTF - Multiplayer");

        GUILayout.Space(10);
        if (GUILayout.Button("Host"))
        {
            NetworkManager.Singleton.StartHost();
            menuVisible = false;
        }

        GUILayout.Space(10);
        GUILayout.Label("Server IP:");
        ipInput = GUILayout.TextField(ipInput);

        if (GUILayout.Button("Join"))
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(ipInput, port);
            NetworkManager.Singleton.StartClient();
            menuVisible = false;
        }
        GUILayout.EndArea();
    }
}
