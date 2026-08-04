using Unity.Netcode;
using UnityEngine;

// Attach to each team's base zone, alongside a Collider2D (Is Trigger).
public class NetworkFlagBase : NetworkBehaviour
{
    public Team team;
    public NetworkFlagController homeFlag; // this team's own flag

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        TeamMember member = other.GetComponent<TeamMember>();
        if (member == null || member.team.Value != team) return;

        NetworkFlagController flag = member.carriedFlag;
        if (flag == null || flag.team == team) return; // must be carrying the enemy flag

        // classic rule: your own flag must be home to score
        if (homeFlag != null && !homeFlag.IsAtHome()) return;

        Vector3 scorePos = other.transform.position;
        flag.CaptureServer();
        NetworkGameManager.Instance.AddScoreServer(team);
        CapturePopClientRpc(scorePos, team);
    }

    [ClientRpc]
    void CapturePopClientRpc(Vector3 position, Team scoringTeam)
    {
        CameraFollow.Shake(0.35f, 0.3f);

        var go = new GameObject("CapturePop");
        go.transform.position = position;
        go.AddComponent<CapturePopEffect>();

        Color c = scoringTeam == Team.Red ? new Color(1f, 0.4f, 0.4f) : new Color(0.45f, 0.65f, 1f);
        string who = scoringTeam == Team.Red ? "RED" : "BLUE";

        FloatingText.Spawn(position, "+1", new Color(1f, 0.88f, 0.3f), 1.5f);
        GameFeedUI.Instance.Banner(who + " SCORES!", c, 1.2f);
        GameFeedUI.Instance.Toast(who + " captured the flag", c);
    }
}
