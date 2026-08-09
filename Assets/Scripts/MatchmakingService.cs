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

// NOTE: do not add com.unity.services.multiplayer to this project. It re-declares Lobby,
// Allocation and JoinAllocation in the SAME namespaces as the Lobbies/Relay packages used
// here, so every reference becomes ambiguous (CS0433) and no using-alias can resolve it.
// The Multiplayer Center wizard offers to install it - decline.
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
    [Tooltip("Seconds to wait for a real opponent before starting the match with bots instead.")]
    public float soloStartDelay = 15f;
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

        // Every online failure below falls through to a local match rather than a dead end.
        // A player who taps PLAY wants to play; "could not join a lobby" is our problem, not
        // something they can act on.
        if (initTask.IsFaulted)
        {
            yield return StartOfflineMatch(initTask.Exception?.GetBaseException().Message ?? "service init failed");
            yield break;
        }

        SetState(MatchmakingState.Searching, "Searching for match...");
        searchStartedAt = Time.unscaledTime;

        Task<bool> matchTask = QuickJoinOrCreateAsync();
        while (!matchTask.IsCompleted) yield return null;

        if (matchTask.IsFaulted)
        {
            yield return StartOfflineMatch(matchTask.Exception?.GetBaseException().Message ?? "matchmaking failed");
            yield break;
        }

        if (!matchTask.Result)
        {
            yield return StartOfflineMatch("could not join or create a lobby");
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

            // Covers the case the loop above cannot: transport never comes up, or a join that
            // hangs without ever faulting. Without this the search spins forever.
            if (Time.unscaledTime - searchStartedAt >= soloStartDelay + 5f)
            {
                yield return StartOfflineMatch("timed out waiting for the network");
                yield break;
            }

            yield return null;
        }
    }

    // Local host, no Relay and no Lobby. Bots fill the arena on arrival because
    // NetworkGameManager.botFillDelay is 0, so the player drops straight into a live match.
    IEnumerator StartOfflineMatch(string reason)
    {
        Debug.LogWarning($"Matchmaking: falling back to a bot match ({reason}).");
        SetState(MatchmakingState.Searching, "No players found - starting with bots...");

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Fail("NetworkManager missing.");
            yield break;
        }

        if (nm.IsListening)
        {
            nm.Shutdown();
            while (nm.ShutdownInProgress) yield return null;
            yield return null;
        }

        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null) transport.SetConnectionData("127.0.0.1", 7777);

        nm.NetworkConfig.ConnectionData = PlayerDataService.BuildConnectionPayload();
        if (!nm.StartHost())
        {
            Fail("Could not start an offline match.");
            yield break;
        }

        isHost = true;
        SetState(MatchmakingState.MatchFound, "Match ready!");
        yield return new WaitForSecondsRealtime(0.8f);

        SetState(MatchmakingState.LoadingArena, "Loading arena...");
        LoadArenaForAll();
        searchRoutine = null;
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

        // A previous attempt can leave the NetworkManager listening - StartHost then refuses
        // with "Can't start while listening" and the failure surfaces as a lobby error, which
        // sends you hunting the wrong problem entirely.
        if (NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("Matchmaking: NetworkManager was still listening; shutting down before hosting.");
            NetworkManager.Singleton.Shutdown();
        }

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
