using Unity.Netcode;
using UnityEngine;

public enum Team { Red, Blue }
public enum FlagState { AtHome, Carried }

// Attach to the Player prefab. Server assigns the team on connect.
public class TeamMember : NetworkBehaviour
{
    public NetworkVariable<Team> team = new NetworkVariable<Team>(
        Team.Red, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> skinIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // True while standing in a bush. Server-owned; clients read it to decide who to draw.
    public NetworkVariable<bool> hiddenInBush = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Bushes overlap, so count occupancy rather than toggling a flag.
    int bushCount;

    public void ServerEnterBush()
    {
        if (!IsServer) return;
        bushCount++;
        hiddenInBush.Value = true;
    }

    public void ServerExitBush()
    {
        if (!IsServer) return;
        bushCount = Mathf.Max(0, bushCount - 1);
        if (bushCount == 0) hiddenInBush.Value = false;
    }

    public SpriteRenderer bodyRenderer;

    [Tooltip("Ground ring under the character, coloured by team.")]
    public SpriteRenderer teamRing;

    [Header("Per-team animation")]
    public RuntimeAnimatorController redController;
    public RuntimeAnimatorController blueController;

    // Server-only bookkeeping: which flag (if any) this player is carrying.
    [System.NonSerialized] public NetworkFlagController carriedFlag;

    Animator animator;
    static Sprite[] cachedSprites;

    public override void OnNetworkSpawn()
    {
        animator = GetComponent<Animator>();
        team.OnValueChanged += (oldVal, newVal) => ApplySkin();
        skinIndex.OnValueChanged += (oldVal, newVal) => ApplySkin();
        ApplySkin();
    }

    public void ApplySkin()
    {
        // Team identity lives on the ground ring, not a colour multiply. The character
        // art is fully coloured now, so tinting it would turn the sash and armour to mud.
        if (teamRing != null)
        {
            var c = GameUI.TeamColor(team.Value);
            c.a = 0.85f;
            teamRing.color = c;
        }

        // Swap the whole controller rather than tinting: the team colour is baked into
        // the sash and plume, so the art stays exactly as drawn.
        if (animator != null)
        {
            var wantedController = team.Value == Team.Red ? redController : blueController;
            if (wantedController != null && animator.runtimeAnimatorController != wantedController)
            {
                animator.runtimeAnimatorController = wantedController;
                animator.Rebind();
            }
        }

        if (bodyRenderer == null) return;

        // When an Animator is present it owns m_Sprite - assigning here would be
        // overwritten every frame, so leave the body alone and keep it untinted.
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            bodyRenderer.color = Color.white;
            return;
        }

        // Fallback for un-animated skins: pick from the shared sheet and tint by team.
        if (cachedSprites == null || cachedSprites.Length == 0)
            cachedSprites = Resources.LoadAll<Sprite>("ctf_spritesheet");

        string wanted = "ctf_char_" + Mathf.Clamp(skinIndex.Value, 0, 4);
        foreach (var s in cachedSprites)
        {
            if (s.name == wanted) { bodyRenderer.sprite = s; break; }
        }

        bodyRenderer.color = (team.Value == Team.Red)
            ? new Color(1f, 0.42f, 0.38f, 1f)
            : new Color(0.42f, 0.62f, 1f, 1f);
    }
}
