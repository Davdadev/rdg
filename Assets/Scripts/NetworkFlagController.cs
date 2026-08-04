using Unity.Netcode;
using UnityEngine;

// Attach to each Flag prefab, alongside NetworkObject + NetworkTransform + a Collider2D (Is Trigger).
public class NetworkFlagController : NetworkBehaviour
{
    public Team team;

    public NetworkVariable<FlagState> state = new NetworkVariable<FlagState>(
        FlagState.AtHome, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    Vector3 homePosition;
    NetworkPlayerController carrier;

    public override void OnNetworkSpawn()
    {
        homePosition = transform.position;
    }

    void Update()
    {
        if (!IsServer) return;

        // Server keeps the flag glued to whoever is carrying it; NetworkTransform syncs it down.
        if (state.Value == FlagState.Carried && carrier != null)
            transform.position = carrier.transform.position;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (state.Value == FlagState.Carried) return;

        TeamMember member = other.GetComponent<TeamMember>();
        if (member == null || member.team.Value == team) return; // only the opposing team can pick it up

        carrier = other.GetComponent<NetworkPlayerController>();
        member.carriedFlag = this;
        state.Value = FlagState.Carried;

        FlagTakenClientRpc(team, transform.position);
    }

    [ClientRpc]
    void FlagTakenClientRpc(Team flagTeam, Vector3 pos)
    {
        Color c = flagTeam == Team.Red ? new Color(1f, 0.4f, 0.4f) : new Color(0.45f, 0.65f, 1f);
        string who = flagTeam == Team.Red ? "RED" : "BLUE";
        FloatingText.Spawn(pos, "FLAG TAKEN!", c, 1.1f);
        GameFeedUI.Instance.Banner(who + " FLAG TAKEN!", c, 0.9f);
        GameFeedUI.Instance.Toast(who + " flag picked up", c);
        CameraFollow.Shake(0.18f, 0.12f);
    }

    public void CaptureServer()
    {
        if (!IsServer) return;
        ReleaseCarrier();
        state.Value = FlagState.AtHome;
        transform.position = homePosition;
    }

    void ReleaseCarrier()
    {
        if (carrier != null)
        {
            TeamMember member = carrier.GetComponent<TeamMember>();
            if (member != null) member.carriedFlag = null;
        }
        carrier = null;
    }

    public bool IsAtHome() => state.Value == FlagState.AtHome;
}
