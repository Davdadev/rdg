using Unity.Netcode;
using UnityEngine;

// Sits on each bush. Bushes are walk-through concealment, not cover - crates block
// movement, bushes hide you.
//
// Plain MonoBehaviour on purpose: a NetworkBehaviour here would need a NetworkObject
// on all eight bushes, and IsServer would silently be false without one. Gating on
// NetworkManager.Singleton.IsServer avoids that whole trap.
[RequireComponent(typeof(Collider2D))]
public class BushZone : MonoBehaviour
{
    static bool IsServer =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        var member = other.GetComponent<TeamMember>();
        if (member != null) member.ServerEnterBush();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer) return;
        var member = other.GetComponent<TeamMember>();
        if (member != null) member.ServerExitBush();
    }
}
