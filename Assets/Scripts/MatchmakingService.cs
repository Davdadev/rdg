using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchmakingService : MonoBehaviour
{
    public static MatchmakingService Instance { get; private set; }

    public enum MatchmakingState
    {
        Idle,
        Initializing,
        Searching,
        MatchFound,
        LoadingArena,
        Failed,
        Cancelled
    }

    [Header("Lobby")]
    public int maxPlayers = 10;
    public int minPlayersToStart = 2;
    public float soloStartDelay = 8f;
    public string arenaSceneName = "fsfs";

    public MatchmakingState State { get; private set; } = MatchmakingState.Idle;
    public string StatusMessage { get; private set; } = "";

    public event Action<MatchmakingState> OnStateChanged;
    public event Action<string> OnError;

    Lobby activeLobby;
    Coroutine searchRoutine;
    bool isHost;
    float searchStartedAt;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginQuickMatch()
    {
        if (searchRoutine != null)
            StopCoroutine(searchRoutine);

        searchRoutine = StartCoroutine(QuickMatchRoutine());
    }

    public void CancelMatchmaking()
    {
        if (searchRoutine != null)
        {
            StopCoroutine(searchRoutine);
            searchRoutine = null;
        }

        StartCoroutine(CancelRoutine());
    }

    IEnumerator QuickMatchRoutine()
    {
        SetState(MatchmakingState.Initializing, "Connecting to services...");
        Task initTask = InitializeServicesAsync();
        while (!initTask.IsCompleted) yield return null;

        if (initTask.IsFaulted)
        {
            Fail(initTask.Exception?.GetBaseException().Message ?? "Service init failed.");
            yield break;
        }

        SetState(MatchmakingState.Searching, "Searching for match...");
        searchStartedAt = Time.unscaledTime;

        Task<bool> matchTask = QuickJoinOrCreateAsync();
        while (!matchTask.IsCompleted) yield return null;

        if (matchTask.IsFaulted)
        {
            Fail(matchTask.Exception?.GetBaseException().Message ?? "Matchmaking failed.");
            yield break;
        }

        if (!matchTask.Result)
        {
            Fail("Could not join or create a lobby.");
            yield break;
        }

        yield return WaitForMatchReady();

        if (State == MatchmakingState.Cancelled || State == MatchmakingState.Failed)
            yield break;

        SetState(MatchmakingState.MatchFound, "Match found!");
        yield return new WaitForSecondsRealtime(1.2f);

        SetState(MatchmakingState.LoadingArena, "Loading arena...");
        LoadArenaForAll();
        searchRoutine = null;
    }

    IEnumerator WaitForMatchReady()
    {
        while (State != MatchmakingState.Cancelled && State != MatchmakingState.Failed)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                if (isHost)
                {
                    int connected = nm.ConnectedClientsIds.Count;
                    if (connected >= minPlayersToStart)
                        yield break;

                    if (connected >= 1 && Time.unscaledTime - searchStartedAt >= soloStartDelay)
                        yield break;
                }
                else if (nm.IsConnectedClient)
                {
                    yield break;
                }
            }

            yield return null;
        }
    }

    void LoadArenaForAll()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Fail("NetworkManager missing.");
            return;
        }

        if (nm.IsHost)
        {
            nm.SceneManager.LoadScene(arenaSceneName, LoadSceneMode.Single);
        }
    }

    IEnumerator CancelRoutine()
    {
        SetState(MatchmakingState.Cancelled, "Cancelled.");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (activeLobby != null && activeLobby.HostId == AuthenticationService.Instance.PlayerId)
        {
            Task deleteTask = LobbyService.Instance.DeleteLobbyAsync(activeLobby.Id);
            while (!deleteTask.IsCompleted) yield return null;
        }
        else if (activeLobby != null)
        {
            Task leaveTask = LobbyService.Instance.RemovePlayerAsync(activeLobby.Id, AuthenticationService.Instance.PlayerId);
            while (!leaveTask.IsCompleted) yield return null;
        }

        activeLobby = null;
        isHost = false;
        SetState(MatchmakingState.Idle, "");
    }

    async Task InitializeServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    async Task<bool> QuickJoinOrCreateAsync()
    {
        try
        {
            var lobbies = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 5,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            });

            if (lobbies.Results.Count > 0)
                return await JoinExistingLobbyAsync(lobbies.Results[0]);

            return await CreateLobbyAsHostAsync();
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogWarning($"Quick join failed ({ex.Reason}), creating lobby: {ex.Message}");
            return await CreateLobbyAsHostAsync();
        }
    }

    async Task<bool> JoinExistingLobbyAsync(Lobby lobby)
    {
        activeLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);
        string relayJoinCode = activeLobby.Data["RelayJoinCode"].Value;

        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
        ConfigureTransportAsClient(joinAllocation);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = PlayerDataService.BuildConnectionPayload();
        if (!NetworkManager.Singleton.StartClient())
            return false;

        isHost = false;
        return true;
    }

    async Task<bool> CreateLobbyAsHostAsync()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
        string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        ConfigureTransportAsHost(allocation);

        activeLobby = await LobbyService.Instance.CreateLobbyAsync(
            $"{PlayerDataService.PlayerName}'s Match",
            maxPlayers,
            new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            });

        NetworkManager.Singleton.NetworkConfig.ConnectionData = PlayerDataService.BuildConnectionPayload();
        if (!NetworkManager.Singleton.StartHost())
            return false;

        isHost = true;
        return true;
    }

    void ConfigureTransportAsHost(Allocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetHostRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData);
    }

    void ConfigureTransportAsClient(JoinAllocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetClientRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData);
    }

    void SetState(MatchmakingState state, string message)
    {
        State = state;
        StatusMessage = message;
        OnStateChanged?.Invoke(state);
    }

    void Fail(string message)
    {
        Debug.LogError($"Matchmaking: {message}");
        OnError?.Invoke(message);
        SetState(MatchmakingState.Failed, message);
    }
}
