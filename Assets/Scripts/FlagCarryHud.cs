using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Shows the "you are carrying the flag" banner for the local player.
//
// Who is carrying a flag is server-only state (TeamMember.carriedFlag is not
// networked), but the flag object itself is synced to the carrier's position -
// so on the client we infer it from proximity. Purely cosmetic; nothing
// authoritative depends on this.
public class FlagCarryHud : MonoBehaviour
{
    [SerializeField] GameObject banner;
    [SerializeField] Text bannerText;
    [SerializeField] float carryDistance = 1.2f;

    Transform localPlayer;
    float findTimer;

    void Update()
    {
        if (banner == null) return;

        if (localPlayer == null)
        {
            findTimer -= Time.deltaTime;
            if (findTimer <= 0f)
            {
                findTimer = 0.5f;
                var obj = NetworkManager.Singleton?.SpawnManager?.GetLocalPlayerObject();
                if (obj != null) localPlayer = obj.transform;
            }
            if (banner.activeSelf) banner.SetActive(false);
            return;
        }

        NetworkFlagController carried = null;
        foreach (var flag in FindObjectsByType<NetworkFlagController>(FindObjectsSortMode.None))
        {
            if (flag == null || flag.state.Value != FlagState.Carried) continue;
            if (Vector2.Distance(flag.transform.position, localPlayer.position) <= carryDistance)
            {
                carried = flag;
                break;
            }
        }

        bool show = carried != null;
        if (banner.activeSelf != show) banner.SetActive(show);

        if (show && bannerText != null)
        {
            bannerText.text = carried.team == Team.Red
                ? "CARRYING THE RED FLAG — GET HOME"
                : "CARRYING THE BLUE FLAG — GET HOME";
            bannerText.color = carried.team == Team.Red ? GameUI.Red : GameUI.Blue;
        }
    }
}
