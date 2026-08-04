using Unity.Netcode;
using UnityEngine;

// Decides, per client, whether this player is drawn while hidden in a bush.
//
// Rules:
//   - you always see yourself, faded, so you know the hiding is working
//   - you always see your own team, faded
//   - enemies in a bush are invisible to you
//   - EXCEPT a flag carrier, who is always revealed - otherwise sitting in a bush
//     with the flag is an unbeatable strategy
public class BushConcealment : MonoBehaviour
{
    [SerializeField] float hiddenAlpha = 0.45f;
    [SerializeField] float fadeSpeed = 8f;

    TeamMember me;
    SpriteRenderer body;
    SpriteRenderer[] extras;   // shadow, team ring
    float[] extraBaseAlpha;    // their authored alpha, so fading scales instead of overwriting
    float currentAlpha = 1f;

    void Awake()
    {
        me = GetComponent<TeamMember>();
        body = me != null ? me.bodyRenderer : GetComponent<SpriteRenderer>();

        var all = GetComponentsInChildren<SpriteRenderer>(true);
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var sr in all) if (sr != body) list.Add(sr);
        extras = list.ToArray();

        extraBaseAlpha = new float[extras.Length];
        for (int i = 0; i < extras.Length; i++) extraBaseAlpha[i] = extras[i].color.a;
    }

    void LateUpdate()
    {
        if (me == null || body == null) return;

        float target = 1f;

        if (me.hiddenInBush.Value)
        {
            bool carrying = IsCarryingFlag();
            if (carrying)
            {
                target = 1f;                       // carriers can't hide
            }
            else if (IsFriendlyToLocalPlayer())
            {
                target = hiddenAlpha;              // self and team: faded but visible
            }
            else
            {
                target = 0f;                       // enemies: gone
            }
        }

        currentAlpha = Mathf.MoveTowards(currentAlpha, target, fadeSpeed * Time.deltaTime);
        Apply(currentAlpha);
    }

    // carriedFlag is server-only state, so infer from flag proximity on clients.
    bool IsCarryingFlag()
    {
        foreach (var flag in FindObjectsByType<NetworkFlagController>(FindObjectsSortMode.None))
        {
            if (flag == null || flag.state.Value != FlagState.Carried) continue;
            if (Vector2.Distance(flag.transform.position, transform.position) <= 1.2f) return true;
        }
        return false;
    }

    bool IsFriendlyToLocalPlayer()
    {
        var localObj = NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null
            ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject()
            : null;

        // No local player (spectator or host with no body) - show everything.
        if (localObj == null) return true;
        if (localObj.gameObject == gameObject) return true;

        var localTeam = localObj.GetComponent<TeamMember>();
        return localTeam != null && localTeam.team.Value == me.team.Value;
    }

    void Apply(float a)
    {
        var c = body.color; c.a = a; body.color = c;

        for (int i = 0; i < extras.Length; i++)
        {
            var sr = extras[i];
            if (sr == null) continue;
            var ec = sr.color;
            ec.a = extraBaseAlpha[i] * a;   // scale the authored alpha
            sr.color = ec;
        }
    }
}
