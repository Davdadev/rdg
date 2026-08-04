using System.Collections;
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

    [Header("Bot fill")]
    public GameObject playerPrefab;
    public float botFillDelay = 30f;
    public int botFillTargetPerTeam = 2;

    [Header("Tagging")]
    [Tooltip("How close opponents must be to tag. Generous on purpose - exact body contact felt unresponsive.")]
    public float tagRadius = 1.25f;
    [Tooltip("Seconds a player cannot be tagged after respawning.")]
    public float spawnProtection = 1.75f;
    [Tooltip("Midline x. A player is vulnerable only while past it, on the enemy half.")]
    public float territoryMidline = 0f;

    [Header("UI (optional)")]
    public Text redScoreText;
    public Text blueScoreText;
    public Text winText;
    public ResultsController resultsController;

    [Header("Match timer")]
    public float matchDurationSeconds = 120f;
    public Text timerText;

    public NetworkVariable<int> redScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> blueScore = new NetworkVariable<int>(0);

    // Server counts down; clients read it for the HUD.
    public NetworkVariable<float> timeRemaining = new NetworkVariable<float>(0f);

    bool warned30, warned10;

    int redCount;
    int blueCount;
    bool matchEnded;
    readonly Queue<int> pendingSkinQueue = new();

    void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        redScore.OnValueChanged += (_, __) => UpdateUI();
        blueScore.OnValueChanged += (_, __) => UpdateUI();
        timeRemaining.OnValueChanged += (_, v) => UpdateTimerUI(v);
        UpdateUI();
        UpdateTimerUI(timeRemaining.Value);

        if (IsServer) timeRemaining.Value = matchDurationSeconds;

        // Tight camera means flags are usually off screen - give players edge arrows.
        if (IsClient) OffscreenIndicator.Ensure();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.ConnectionApprovalCallback += ApproveConnection;
            StartCoroutine(BotFillRoutine());
        }
    }

    IEnumerator BotFillRoutine()
    {
        yield return new WaitForSeconds(botFillDelay);

        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            yield break; // a real second player already joined

        // Give each side a mixed squad instead of identical rushers.
        for (int i = 0; i < botFillTargetPerTeam; i++)
        {
            var role = RoleForIndex(i);
            SpawnBot(Team.Red, role);
            SpawnBot(Team.Blue, role);
        }
    }

    // Weighted toward offence: an even attacker/defender split stalemates, because
    // a lone attacker gets intercepted before it can complete a run.
    static readonly BotAIController.Role[] RoleRotation =
    {
        BotAIController.Role.Attacker,
        BotAIController.Role.Defender,
        BotAIController.Role.Attacker,
        BotAIController.Role.Interceptor,
    };

    static BotAIController.Role RoleForIndex(int i) => RoleRotation[i % RoleRotation.Length];

    void SpawnBot(Team team, BotAIController.Role role)
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("NetworkGameManager: playerPrefab not assigned, cannot spawn bots.");
            return;
        }

        var instance = Instantiate(playerPrefab);
        var netObj = instance.GetComponent<NetworkObject>();
        netObj.Spawn();

        var controller = instance.GetComponent<NetworkPlayerController>();
        if (controller != null) controller.isBot = true;

        var brain = instance.GetComponent<BotAIController>();
        if (brain != null) brain.SetRole(role);

        AssignTeamAndSpawn(instance, team, PlayerDataService.DefaultSkinIndex);
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
        int requestedSkin = pendingSkinQueue.Count > 0
            ? pendingSkinQueue.Dequeue()
            : PlayerDataService.DefaultSkinIndex;

        AssignTeamAndSpawn(client.PlayerObject.gameObject, assigned, requestedSkin);
    }

    // Shared by real client connections and server-spawned bots: picks a spawn point for the
    // given team, sets the TeamMember's networked team/skin, and positions the player object.
    void AssignTeamAndSpawn(GameObject playerObject, Team assigned, int skinIndex)
    {
        Transform[] points = assigned == Team.Red ? redSpawnPoints : blueSpawnPoints;
        int index = assigned == Team.Red ? redCount : blueCount;

        if (assigned == Team.Red) redCount++; else blueCount++;

        TeamMember member = playerObject.GetComponent<TeamMember>();
        if (member != null)
        {
            member.team.Value = assigned;
            member.skinIndex.Value = skinIndex;
        }

        if (points != null && points.Length > index && points[index] != null)
            playerObject.transform.position = points[index].position;
    }

    // Single authoritative tag pass. Runs once per frame on the server, so a pair is
    // only ever resolved once - unlike per-player collision callbacks, which fired
    // on both sides and processed every tag twice.
    void Update()
    {
        if (!IsServer || matchEnded) return;

        float t = timeRemaining.Value - Time.deltaTime;
        timeRemaining.Value = Mathf.Max(0f, t);

        if (!warned30 && t <= 30f) { warned30 = true; TimeWarningClientRpc(30); }
        if (!warned10 && t <= 10f) { warned10 = true; TimeWarningClientRpc(10); }

        if (t <= 0f) EndMatchOnTime();
    }

    [ClientRpc]
    void TimeWarningClientRpc(int seconds)
    {
        Color c = seconds <= 10 ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.8f, 0.3f);
        GameFeedUI.Instance.Banner(seconds + " SECONDS LEFT!", c, 1.0f);
        GameFeedUI.Instance.Toast(seconds + "s remaining", c);
    }

    // Time expired: highest score wins, equal scores are a genuine draw.
    void EndMatchOnTime()
    {
        if (matchEnded) return;
        if (redScore.Value > blueScore.Value) EndMatch(Team.Red);
        else if (blueScore.Value > redScore.Value) EndMatch(Team.Blue);
        else EndMatch(null);
    }

    void FixedUpdate()
    {
        if (!IsServer || matchEnded) return;

        var players = FindObjectsOfType<TeamMember>();
        float sqrRadius = tagRadius * tagRadius;

        for (int i = 0; i < players.Length; i++)
        {
            for (int j = i + 1; j < players.Length; j++)
            {
                var a = players[i];
                var b = players[j];
                if (a == null || b == null) continue;
                if (a.team.Value == b.team.Value) continue;

                if (((Vector2)a.transform.position - (Vector2)b.transform.position).sqrMagnitude > sqrRadius)
                    continue;

                ResolveTag(a, b);
            }
        }
    }

    // Classic CTF rule: you are safe on your own half and vulnerable on the enemy's.
    // That makes "who loses this encounter" unambiguous without any hit priority.
    bool InEnemyTerritory(TeamMember m)
    {
        float x = m.transform.position.x;
        return m.team.Value == Team.Red ? x > territoryMidline : x < territoryMidline;
    }

    float DepthIntoEnemyTerritory(TeamMember m)
    {
        float x = m.transform.position.x;
        return m.team.Value == Team.Red ? x - territoryMidline : territoryMidline - x;
    }

    void ResolveTag(TeamMember a, TeamMember b)
    {
        bool aVulnerable = InEnemyTerritory(a);
        bool bVulnerable = InEnemyTerritory(b);

        TeamMember victim, tagger;

        if (aVulnerable && bVulnerable)
        {
            // Both straddling the midline: whoever is deeper in enemy ground loses.
            if (DepthIntoEnemyTerritory(a) >= DepthIntoEnemyTerritory(b)) { victim = a; tagger = b; }
            else { victim = b; tagger = a; }
        }
        else if (aVulnerable) { victim = a; tagger = b; }
        else if (bVulnerable) { victim = b; tagger = a; }
        else return; // both safe at home - no tag

        var vc = victim.GetComponent<NetworkPlayerController>();
        if (vc != null) vc.ServerTag(tagger, spawnProtection);
    }

    public void AddScoreServer(Team team)
    {
        if (!IsServer || matchEnded) return;

        if (team == Team.Red) redScore.Value++;
        else blueScore.Value++;

        if (redScore.Value >= scoreToWin) EndMatch(Team.Red);
        else if (blueScore.Value >= scoreToWin) EndMatch(Team.Blue);
    }

    // null winner = draw (only reachable when the clock runs out level).
    void EndMatch(Team? winner)
    {
        matchEnded = true;
        // RPCs can't carry a nullable enum, so encode: -1 draw, 0 Red, 1 Blue.
        int code = !winner.HasValue ? -1 : (winner.Value == Team.Red ? 0 : 1);
        EndGameClientRpc(code, redScore.Value, blueScore.Value);
    }

    [ClientRpc]
    void EndGameClientRpc(int winnerCode, int red, int blue)
    {
        Team? winner = winnerCode < 0 ? (Team?)null : (winnerCode == 0 ? Team.Red : Team.Blue);

        Team? localTeam = null;
        var localPlayer = NetworkManager.Singleton?.SpawnManager?.GetLocalPlayerObject();
        if (localPlayer != null)
        {
            var member = localPlayer.GetComponent<TeamMember>();
            if (member != null) localTeam = member.team.Value;
        }

        bool isDraw = !winner.HasValue;
        bool localWon = !isDraw && localTeam.HasValue && localTeam.Value == winner.Value;
        int coinReward = isDraw ? 60 : (localWon ? 100 : 40);
        PlayerDataService.Coins += coinReward;

        GameSessionState.SetMatchResult(winner, red, blue, coinReward, localTeam);

        if (winText != null)
            winText.text = isDraw ? "Draw!" : $"{winner.Value} team wins!";

        StartCoroutine(WinSequence(localWon, winner, coinReward));
    }

    // Celebration first, results panel after - so the win reads as an event
    // rather than a menu appearing out of nowhere.
    IEnumerator WinSequence(bool localWon, Team? winner, int coinReward)
    {
        CameraFollow.Shake(0.5f, 0.35f);
        string headline = !winner.HasValue ? "DRAW" : (localWon ? "VICTORY!" : "DEFEAT");
        GameFeedUI.Instance.Celebrate(localWon, headline);

        yield return new WaitForSecondsRealtime(0.9f);
        GameFeedUI.Instance.Toast($"+{coinReward} coins", new Color(1f, 0.85f, 0.3f));

        var localPlayer = NetworkManager.Singleton?.SpawnManager?.GetLocalPlayerObject();
        if (localPlayer != null)
            FloatingText.Spawn(localPlayer.transform.position, $"+{coinReward}", new Color(1f, 0.88f, 0.3f), 1.4f);

        yield return new WaitForSecondsRealtime(2.4f);
        if (resultsController != null)
            resultsController.ShowFromSession();
    }

    void UpdateUI()
    {
        if (redScoreText != null) redScoreText.text = "Red: " + redScore.Value;
        if (blueScoreText != null) blueScoreText.text = "Blue: " + blueScore.Value;
    }

    void UpdateTimerUI(float remaining)
    {
        if (timerText == null) return;

        int total = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        timerText.text = $"{total / 60}:{total % 60:00}";

        // Turn red and pulse in the final ten seconds.
        if (total <= 10)
        {
            timerText.color = new Color(1f, 0.35f, 0.3f);
            float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.18f;
            timerText.transform.localScale = Vector3.one * pulse;
        }
        else
        {
            timerText.color = total <= 30 ? new Color(1f, 0.8f, 0.3f) : Color.white;
            timerText.transform.localScale = Vector3.one;
        }
    }
}
