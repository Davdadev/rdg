using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;

    [Header("Setup")]
    public int maxPerTeam = 5;
    public Transform[] redSpawnPoints;
    public Transform[] blueSpawnPoints;
    public int scoreToWin = 3;

    [Header("UI (optional)")]
    public Text redScoreText;
    public Text blueScoreText;
    public Text winText;
    public ResultsController resultsController;

    public NetworkVariable<int> redScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> blueScore = new NetworkVariable<int>(0);

    int redCount;
    int blueCount;
    bool matchEnded;
    readonly Queue<int> pendingSkinQueue = new();

    void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        redScore.OnValueChanged += (_, __) => UpdateUI();
        blueScore.OnValueChanged += (_, __) => UpdateUI();
        UpdateUI();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.ConnectionApprovalCallback += ApproveConnection;
        }
    }

    void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool roomAvailable = redCount < maxPerTeam || blueCount < maxPerTeam;
        response.Approved = roomAvailable;
        response.CreatePlayerObject = roomAvailable;

        int requestedSkin = PlayerDataService.ParseSkinFromPayload(request.Payload);
        if (!PlayerDataService.OwnsSkin(requestedSkin))
            requestedSkin = PlayerDataService.DefaultSkinIndex;

        pendingSkinQueue.Enqueue(requestedSkin);
    }

    void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        if (client.PlayerObject == null) return;

        Team assigned = redCount <= blueCount ? Team.Red : Team.Blue;
        Transform[] points = assigned == Team.Red ? redSpawnPoints : blueSpawnPoints;
        int index = assigned == Team.Red ? redCount : blueCount;

        if (assigned == Team.Red) redCount++; else blueCount++;

        int requestedSkin = pendingSkinQueue.Count > 0
            ? pendingSkinQueue.Dequeue()
            : PlayerDataService.DefaultSkinIndex;

        TeamMember member = client.PlayerObject.GetComponent<TeamMember>();
        if (member != null)
        {
            member.team.Value = assigned;
            member.skinIndex.Value = requestedSkin;
        }

        if (points != null && points.Length > index && points[index] != null)
            client.PlayerObject.transform.position = points[index].position;
    }

    public void AddScoreServer(Team team)
    {
        if (!IsServer || matchEnded) return;

        if (team == Team.Red) redScore.Value++;
        else blueScore.Value++;

        if (redScore.Value >= scoreToWin) EndMatch(Team.Red);
        else if (blueScore.Value >= scoreToWin) EndMatch(Team.Blue);
    }

    void EndMatch(Team winner)
    {
        matchEnded = true;
        EndGameClientRpc(winner, redScore.Value, blueScore.Value);
    }

    [ClientRpc]
    void EndGameClientRpc(Team winner, int red, int blue)
    {
        Team? localTeam = null;
        var localPlayer = NetworkManager.Singleton?.SpawnManager?.GetLocalPlayerObject();
        if (localPlayer != null)
        {
            var member = localPlayer.GetComponent<TeamMember>();
            if (member != null) localTeam = member.team.Value;
        }

        bool localWon = localTeam.HasValue && localTeam.Value == winner;
        int coinReward = localWon ? 100 : 40;
        PlayerDataService.Coins += coinReward;

        GameSessionState.SetMatchResult(winner, red, blue, coinReward, localTeam);

        if (winText != null) winText.text = $"{winner} team wins!";
        if (resultsController != null)
            resultsController.ShowFromSession();
    }

    void UpdateUI()
    {
        if (redScoreText != null) redScoreText.text = "Red: " + redScore.Value;
        if (blueScoreText != null) blueScoreText.text = "Blue: " + blueScore.Value;
    }
}
