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

    public SpriteRenderer bodyRenderer;

    // Server-only bookkeeping: which flag (if any) this player is carrying.
    [System.NonSerialized] public NetworkFlagController carriedFlag;

    private static Sprite[] cachedSprites;

    public override void OnNetworkSpawn()
    {
        team.OnValueChanged += (oldVal, newVal) => ApplySkin();
        skinIndex.OnValueChanged += (oldVal, newVal) => ApplySkin();
        ApplySkin();
    }

    public void ApplySkin()
    {
        if (bodyRenderer == null) return;

        if (cachedSprites == null || cachedSprites.Length == 0)
        {
            cachedSprites = Resources.LoadAll<Sprite>("ctf_spritesheet");
        }

        int index = Mathf.Clamp(skinIndex.Value, 0, 4);
        string spriteName = "ctf_char_" + index;

        Sprite skinSprite = null;
        if (cachedSprites != null)
        {
            foreach (var s in cachedSprites)
            {
                if (s.name == spriteName)
                {
                    skinSprite = s;
                    break;
                }
            }
        }

        if (skinSprite != null)
        {
            bodyRenderer.sprite = skinSprite;
        }

        // Apply distinct team colors to the characters (they have neutral-white suits which tint beautifully!)
        bodyRenderer.color = (team.Value == Team.Red) 
            ? new Color(0.95f, 0.35f, 0.35f, 1f) // soft vibrant red
            : new Color(0.35f, 0.55f, 0.95f, 1f); // soft vibrant blue
    }
}
