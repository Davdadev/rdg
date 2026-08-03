using Unity.Netcode;
using UnityEngine;

// Attach to the Player prefab alongside NetworkObject + NetworkTransform (2D-friendly settings)
// + Rigidbody2D (set to Dynamic, Gravity Scale 0, Freeze Rotation Z) + a Collider2D.
public class NetworkPlayerController : NetworkBehaviour
{
    public float moveSpeed = 7f;
    public SpriteRenderer bodyRenderer;

    private Rigidbody2D rb;
    private Vector2 latestInput;
    private Vector2 serverInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        // Non-owners and the server don't need local input polling.
        enabled = IsOwner || IsServer;
    }

    void Update()
    {
        if (IsOwner)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 dir = new Vector2(h, v);

            // Integrate Mobile Virtual Joystick input
            if (VirtualJoystick.Instance != null && VirtualJoystick.Instance.InputDirection.sqrMagnitude > 0.01f)
            {
                dir = VirtualJoystick.Instance.InputDirection;
            }

            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (dir != latestInput)
            {
                latestInput = dir;
                SubmitInputServerRpc(dir);
            }

            if (Mathf.Abs(dir.x) > 0.01f && bodyRenderer != null)
                bodyRenderer.flipX = dir.x < 0f;
        }
    }

    void FixedUpdate()
    {
        if (IsServer)
        {
            // Server moves the authoritative physics Rigidbody2D
            rb.linearVelocity = serverInput * moveSpeed;
        }
    }

    [ServerRpc]
    void SubmitInputServerRpc(Vector2 dir)
    {
        serverInput = dir;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;

        TeamMember myTeam = GetComponent<TeamMember>();
        TeamMember otherTeam = collision.gameObject.GetComponent<TeamMember>();

        if (myTeam != null && otherTeam != null && myTeam.team.Value != otherTeam.team.Value)
        {
            // Opposing players collided! Let's check if either is carrying the flag.
            HandleTag(myTeam, otherTeam);
        }
    }

    private void HandleTag(TeamMember playerA, TeamMember playerB)
    {
        // If player A is carrying the flag, they get tagged and reset
        if (playerA.carriedFlag != null)
        {
            ResetPlayerAfterTag(playerA);
        }
        // If player B is carrying the flag, they get tagged and reset
        if (playerB.carriedFlag != null)
        {
            ResetPlayerAfterTag(playerB);
        }
    }

    private void ResetPlayerAfterTag(TeamMember player)
    {
        if (player.carriedFlag != null)
        {
            // Drop and return flag to its base
            player.carriedFlag.CaptureServer();
        }

        // Teleport back to spawn point
        Transform[] spawnPoints = (player.team.Value == Team.Red) 
            ? NetworkGameManager.Instance.redSpawnPoints 
            : NetworkGameManager.Instance.blueSpawnPoints;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIdx = Random.Range(0, spawnPoints.Length);
            if (spawnPoints[spawnIdx] != null)
            {
                player.transform.position = spawnPoints[spawnIdx].position;
            }
        }
    }
}
