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

        flag.CaptureServer();
        NetworkGameManager.Instance.AddScoreServer(team);
    }
}
