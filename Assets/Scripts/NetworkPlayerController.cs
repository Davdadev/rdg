using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Attach to the Player prefab alongside NetworkObject + NetworkTransform (2D-friendly settings)
// + Rigidbody2D (set to Dynamic, Gravity Scale 0, Freeze Rotation Z) + a Collider2D.
public class NetworkPlayerController : NetworkBehaviour
{
    public float moveSpeed = 7f;
    public SpriteRenderer bodyRenderer;

    [Header("Dash ability")]
    public float dashSpeedMultiplier = 2.8f;
    public float dashDuration = 0.26f;
    public float dashCooldown = 6f;

    [System.NonSerialized] public bool isBot;

    private Rigidbody2D rb;
    private Vector2 latestInput;
    private Vector2 serverInput;

    // Dash state is server-authoritative; clients only mirror it for UI/VFX.
    float dashEndsAt;
    float dashReadyAt;
    Vector2 dashDirection;

    public bool IsDashing => Time.time < dashEndsAt;
    public bool DashReady => Time.time >= dashReadyAt;
    // 0 = just used, 1 = ready. Drives the cooldown ring on the HUD.
    public float DashCooldownNormalised =>
        dashCooldown <= 0f ? 1f : Mathf.Clamp01(1f - (dashReadyAt - Time.time) / dashCooldown);

    private Animator animator;
    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int TaggedParam = Animator.StringToHash("Tagged");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void LateUpdate()
    {
        // Every client drives its own animator from the replicated velocity, so remote
        // players animate too rather than sliding along in a T-pose.
        if (animator == null || rb == null) return;
        animator.SetFloat(SpeedParam, rb.linearVelocity.magnitude);
    }

    public override void OnNetworkSpawn()
    {
        // Non-owners and the server don't need local input polling.
        enabled = IsOwner || IsServer;
    }

    // Server-only: BotAIController drives movement through here instead of the client input path.
    public void SetBotInput(Vector2 dir)
    {
        if (!IsServer) return;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        serverInput = dir;

        if (Mathf.Abs(dir.x) > 0.01f && bodyRenderer != null)
            bodyRenderer.flipX = dir.x < 0f;
    }

    void Update()
    {
        if (IsOwner && !isBot)
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

            // Space on desktop; the HUD dash button calls RequestDash() on mobile.
            if (Input.GetKeyDown(KeyCode.Space)) RequestDash();
        }
    }

    /// <summary>Called by the local player (HUD button or key). Safe to spam.</summary>
    public void RequestDash()
    {
        if (!IsOwner || isBot) return;
        if (!DashReady) return;
        // Predict the cooldown locally so the UI reacts instantly; the server is
        // still the authority on whether the dash actually happens.
        dashReadyAt = Time.time + dashCooldown;
        RequestDashServerRpc(latestInput);
    }

    [ServerRpc]
    void RequestDashServerRpc(Vector2 dir)
    {
        ServerStartDash(dir);
    }

    // Server-only. Also used by BotAIController so bots share the exact same rules.
    public bool ServerStartDash(Vector2 dir)
    {
        if (!IsServer) return false;
        if (Time.time < dashReadyAt) return false;
        if (dir.sqrMagnitude < 0.01f) dir = serverInput;
        if (dir.sqrMagnitude < 0.01f) return false; // standing still - nothing to dash into

        dashDirection = dir.normalized;
        dashEndsAt = Time.time + dashDuration;
        dashReadyAt = Time.time + dashCooldown;

        DashClientRpc(dashDirection);
        return true;
    }

    [ClientRpc]
    void DashClientRpc(Vector2 dir)
    {
        // Mirror cooldown on all clients so remote players' state is consistent.
        dashEndsAt = Time.time + dashDuration;
        dashReadyAt = Time.time + dashCooldown;
        dashDirection = dir;

        StartCoroutine(DashTrailRoutine());
        if (IsOwner) CameraFollow.Shake(0.12f, 0.09f);
    }

    IEnumerator DashTrailRoutine()
    {
        if (bodyRenderer == null) yield break;

        // Quick stretch + afterimages sell the burst of speed.
        Vector3 baseScale = transform.localScale;
        float t = 0f;
        float nextGhost = 0f;

        while (t < dashDuration)
        {
            t += Time.deltaTime;
            if (t >= nextGhost)
            {
                nextGhost += dashDuration / 4f;
                SpawnGhost();
            }
            float k = 1f - (t / dashDuration);
            transform.localScale = new Vector3(baseScale.x * (1f + 0.18f * k), baseScale.y * (1f - 0.12f * k), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    void SpawnGhost()
    {
        var go = new GameObject("DashGhost");
        go.transform.position = transform.position;
        go.transform.localScale = transform.localScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = bodyRenderer.sprite;
        sr.flipX = bodyRenderer.flipX;
        sr.sortingOrder = bodyRenderer.sortingOrder - 1;
        var c = bodyRenderer.color; c.a = 0.45f;
        sr.color = c;
        go.AddComponent<DashGhost>();
    }

    void FixedUpdate()
    {
        if (IsServer)
        {
            // Server moves the authoritative physics Rigidbody2D
            if (IsDashing)
                rb.linearVelocity = dashDirection * moveSpeed * dashSpeedMultiplier;
            else
                rb.linearVelocity = serverInput * moveSpeed;
        }
    }

    [ServerRpc]
    void SubmitInputServerRpc(Vector2 dir)
    {
        serverInput = dir;
    }

    // ---------------- tagging ----------------
    // Detection lives in NetworkGameManager so it runs exactly once per pair.
    // Doing it here via OnCollisionEnter2D fired on both players and processed
    // every tag twice (double flag return, double respawn, double popup).

    [System.NonSerialized] public float invulnerableUntil;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    // Server-only. Returns true if the tag actually landed.
    public bool ServerTag(TeamMember tagger, float spawnProtection)
    {
        if (!IsServer || IsInvulnerable) return false;

        var me = GetComponent<TeamMember>();
        if (me == null) return false;

        bool wasCarrying = me.carriedFlag != null;
        Team droppedFlagTeam = wasCarrying ? me.carriedFlag.team : default;

        if (wasCarrying)
            me.carriedFlag.CaptureServer(); // returns the flag home

        Transform[] spawnPoints = (me.team.Value == Team.Red)
            ? NetworkGameManager.Instance.redSpawnPoints
            : NetworkGameManager.Instance.blueSpawnPoints;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (p != null) transform.position = p.position;
        }

        // Stop dead so a respawned player doesn't slide off on old velocity.
        serverInput = Vector2.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        invulnerableUntil = Time.time + spawnProtection;

        TaggedClientRpc(wasCarrying, droppedFlagTeam, spawnProtection);
        if (tagger != null)
        {
            var tc = tagger.GetComponent<NetworkPlayerController>();
            if (tc != null) tc.TaggerFeedbackClientRpc(wasCarrying);
        }
        return true;
    }

    [ClientRpc]
    void TaggedClientRpc(bool wasCarrying, Team droppedFlagTeam, float protection)
    {
        if (animator != null) animator.SetTrigger(TaggedParam);

        StartCoroutine(FlashRoutine());
        StartCoroutine(BlinkRoutine(protection));

        FloatingText.Spawn(transform.position, "TAGGED!", new Color(1f, 0.5f, 0.25f), 1.2f);

        // Only shake the screen for the player it actually happened to.
        if (IsOwner) CameraFollow.Shake(0.28f, 0.22f);

        if (wasCarrying)
        {
            Color c = droppedFlagTeam == Team.Red ? new Color(1f, 0.4f, 0.4f) : new Color(0.45f, 0.65f, 1f);
            string who = droppedFlagTeam == Team.Red ? "RED" : "BLUE";
            GameFeedUI.Instance.Banner(who + " FLAG RETURNED!", c, 0.9f);
            GameFeedUI.Instance.Toast("Carrier tagged - flag returned", c);
        }
        else
        {
            GameFeedUI.Instance.Toast("Player tagged", new Color(1f, 0.7f, 0.35f));
        }
    }

    [ClientRpc]
    public void TaggerFeedbackClientRpc(bool stoppedACarrier)
    {
        FloatingText.Spawn(transform.position,
            stoppedACarrier ? "SAVED!" : "TAG!",
            stoppedACarrier ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.9f, 0.4f),
            stoppedACarrier ? 1.4f : 1.1f);

        if (IsOwner) CameraFollow.Shake(0.16f, 0.12f);
    }

    IEnumerator FlashRoutine()
    {
        if (bodyRenderer == null) yield break;
        Color original = bodyRenderer.color;
        bodyRenderer.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        bodyRenderer.color = original;
    }

    // Visual tell that a player can't be tagged yet.
    IEnumerator BlinkRoutine(float duration)
    {
        if (bodyRenderer == null) yield break;
        Color original = bodyRenderer.color;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var c = original;
            c.a = Mathf.Lerp(0.35f, 1f, Mathf.PingPong(t * 6f, 1f));
            bodyRenderer.color = c;
            yield return null;
        }
        bodyRenderer.color = original;
    }
}
