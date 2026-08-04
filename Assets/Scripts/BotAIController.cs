using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-side bot brain. Inert unless NetworkPlayerController.isBot is true.
//
// Steering-based rather than pathfinding: a desired "seek" vector is blended with
// repulsion from cover, separation from teammates, and evasion from threats. That
// keeps bots off the crates without needing a navmesh, and makes them read as
// reactive rather than rail-following.
public class BotAIController : NetworkBehaviour
{
    public enum Role { Attacker, Defender, Interceptor }

    [Header("Tuning")]
    public float thinkInterval = 0.12f;
    public float obstacleAvoidRadius = 2.6f;
    public float teammateSeparation = 2.0f;
    public float threatFleeRadius = 5.5f;
    public float defendLeash = 7f;
    [Tooltip("Defenders only chase intruders within this distance of their own base.")]
    public float chaseRange = 11f;

    [Header("Weights")]
    public float seekWeight = 1.0f;
    public float avoidWeight = 1.9f;
    public float separateWeight = 0.7f;
    public float fleeWeight = 1.6f;

    public Role role { get; private set; }

    NetworkPlayerController controller;
    TeamMember me;

    float thinkTimer;
    float personality;      // per-bot jitter so they don't move identically
    Vector2 wander;
    float wanderTimer;
    Vector2 smoothedInput;

    static Transform[] coverCache;
    static float coverCacheTime = -1f;

    public override void OnNetworkSpawn()
    {
        controller = GetComponent<NetworkPlayerController>();
        me = GetComponent<TeamMember>();
        enabled = IsServer;

        personality = Random.Range(0.75f, 1.25f);
        thinkTimer = Random.Range(0f, thinkInterval);
    }

    // NetworkGameManager assigns this when spawning the squad so the team is balanced.
    public void SetRole(Role r) => role = r;

    void FixedUpdate()
    {
        if (!IsServer || controller == null || !controller.isBot) return;

        thinkTimer -= Time.fixedDeltaTime;
        if (thinkTimer > 0f)
        {
            controller.SetBotInput(smoothedInput);
            return;
        }
        thinkTimer = thinkInterval;

        Vector2 pos = transform.position;
        Vector2 desired = Vector2.zero;

        Vector2 seekDir = Vector2.zero;
        Vector2? goal = ChooseGoal();
        if (goal.HasValue)
        {
            Vector2 toGoal = goal.Value - pos;
            if (toGoal.sqrMagnitude > 0.01f)
            {
                seekDir = toGoal.normalized;
                desired += seekDir * seekWeight;
            }
        }
        else
        {
            seekDir = Wander();
            desired += seekDir * 0.5f;
        }

        // Avoidance must be computed against the seek direction, otherwise a bot
        // walking straight at a crate gets pinned in a perfect force stalemate.
        desired += AvoidCover(pos, seekDir) * avoidWeight;
        desired += SeparateFromTeammates(pos) * separateWeight;

        desired += UnstickImpulse(pos, goal.HasValue);

        // You can only be tagged on the enemy half, so evade whenever you're over
        // there - not just while carrying. Dodge sideways rather than straight back,
        // otherwise fleeing cancels out the push forward.
        bool vulnerable = InEnemyTerritory();
        if (vulnerable)
        {
            Vector2 flee = FleeThreats(pos);
            if (flee.sqrMagnitude > 0.0001f && seekDir.sqrMagnitude > 0.0001f)
            {
                Vector2 lateral = new Vector2(-seekDir.y, seekDir.x);
                if (Vector2.Dot(lateral, flee) < 0f) lateral = -lateral;
                flee = Vector2.Lerp(flee.normalized, lateral, 0.65f);
            }
            // Carrying is worth protecting; an empty-handed attacker should still press on.
            float w = me.carriedFlag != null ? fleeWeight : fleeWeight * 0.55f;
            desired += flee * w;
            desired += seekDir * 0.5f;
        }

        if (desired.sqrMagnitude > 0.0001f)
            desired = desired.normalized * personality;

        // Smooth so bots accelerate rather than snapping direction every tick.
        smoothedInput = Vector2.Lerp(smoothedInput, desired, 0.45f);
        controller.SetBotInput(smoothedInput);

        ConsiderDash(pos, smoothedInput, vulnerable);
    }

    // Bots use the same dash the player has, on the same cooldown - they just pick
    // their moments: breaking away with the flag, or closing on a nearby target.
    void ConsiderDash(Vector2 pos, Vector2 heading, bool vulnerable)
    {
        if (controller.DashReady == false) return;
        if (heading.sqrMagnitude < 0.05f) return;

        float nearestEnemy = float.MaxValue;
        foreach (var p in AllPlayers())
        {
            if (p == null || p.team.Value == me.team.Value) continue;
            float d = ((Vector2)p.transform.position - pos).magnitude;
            if (d < nearestEnemy) nearestEnemy = d;
        }

        bool shouldDash = false;

        if (me.carriedFlag != null && nearestEnemy < 6f)
            shouldDash = true;                                    // break away with the flag
        else if (role == Role.Defender && nearestEnemy < 5f && !vulnerable)
            shouldDash = true;                                    // pounce on an intruder
        else if (role == Role.Interceptor && nearestEnemy < 5.5f)
            shouldDash = true;                                    // close the gap on a carrier
        else if (vulnerable && nearestEnemy < 3f)
            shouldDash = true;                                    // escape a defender

        // Small random hesitation so a whole squad doesn't dash in unison.
        if (shouldDash && Random.value < 0.55f)
            controller.ServerStartDash(heading);
    }

    // ---------------- goal selection ----------------

    Vector2? ChooseGoal()
    {
        Team myTeam = me.team.Value;

        // Carrying the enemy flag always overrides role: run it home.
        if (me.carriedFlag != null)
        {
            var home = FindBase(myTeam);
            return home != null ? (Vector2?)home.transform.position : null;
        }

        var myFlag = FindFlag(myTeam);
        var enemyFlag = FindFlag(Opposite(myTeam));

        bool myFlagStolen = myFlag != null && !myFlag.IsAtHome();
        TeamMember enemyCarrier = FindEnemyCarrier(myTeam);

        switch (role)
        {
            case Role.Defender:
                // Own flag taken -> hunt the thief. Otherwise hold near home.
                if (myFlagStolen && enemyCarrier != null)
                    return enemyCarrier.transform.position;

                var ownBase = FindBase(myTeam);

                // Intruders on our half are taggable while we're safe, but only chase
                // ones actually threatening the base. Hunting across the whole half
                // shut down offence entirely and stalemated matches.
                if (ownBase != null)
                {
                    var intruder = FindIntruderNearBase(myTeam, ownBase.transform.position, chaseRange);
                    if (intruder != null)
                        return intruder.transform.position;
                }

                if (ownBase == null) return null;
                Vector2 basePos = ownBase.transform.position;
                // Loiter around the base rather than standing exactly on it.
                if (((Vector2)transform.position - basePos).magnitude > defendLeash)
                    return basePos;
                return basePos + Wander() * 3f;

            case Role.Interceptor:
                // Hunt any enemy carrier; otherwise contest the enemy flag.
                if (enemyCarrier != null)
                    return enemyCarrier.transform.position;
                if (myFlagStolen && myFlag != null)
                    return myFlag.transform.position;
                return enemyFlag != null ? (Vector2?)enemyFlag.transform.position : null;

            default: // Attacker
                // If our flag is home and theirs is loose, go get it.
                if (enemyFlag != null)
                    return enemyFlag.transform.position;
                return null;
        }
    }

    // ---------------- steering behaviours ----------------

    Vector2 AvoidCover(Vector2 pos, Vector2 seekDir)
    {
        var cover = GetCover();
        Vector2 push = Vector2.zero;
        foreach (var t in cover)
        {
            if (t == null) continue;
            Vector2 delta = pos - (Vector2)t.position;
            float d = delta.magnitude;
            if (d > obstacleAvoidRadius || d < 0.0001f) continue;

            float strength = 1f - (d / obstacleAvoidRadius);
            Vector2 away = delta / d;

            // Steer *around* the obstacle: pick whichever tangent keeps the most
            // forward progress toward the goal. Tangent-dominant so bots slide past
            // rather than shoving straight back and stalling.
            Vector2 tanA = new Vector2(-away.y, away.x);
            Vector2 tanB = -tanA;
            Vector2 tangent = Vector2.Dot(tanA, seekDir) >= Vector2.Dot(tanB, seekDir) ? tanA : tanB;

            push += (away * 0.35f + tangent * 1.15f) * strength;
        }
        return push;
    }

    // Safety net: if a bot has a goal but has stopped making progress (wedged into
    // a corner or between two crates), commit to a random escape heading briefly.
    Vector2 lastStuckCheckPos;
    float stuckTimer;
    float escapeTimer;
    Vector2 escapeDir;

    Vector2 UnstickImpulse(Vector2 pos, bool hasGoal)
    {
        if (escapeTimer > 0f)
        {
            escapeTimer -= thinkInterval;
            return escapeDir * 1.6f;
        }

        stuckTimer += thinkInterval;
        if (stuckTimer < 0.75f) return Vector2.zero;

        float moved = Vector2.Distance(pos, lastStuckCheckPos);
        lastStuckCheckPos = pos;
        stuckTimer = 0f;

        if (hasGoal && moved < 0.35f)
        {
            escapeDir = Random.insideUnitCircle.normalized;
            escapeTimer = 0.5f;
            return escapeDir * 1.6f;
        }
        return Vector2.zero;
    }

    Vector2 SeparateFromTeammates(Vector2 pos)
    {
        Vector2 push = Vector2.zero;
        foreach (var other in AllPlayers())
        {
            if (other == null || other == me) continue;
            if (other.team.Value != me.team.Value) continue;
            Vector2 delta = pos - (Vector2)other.transform.position;
            float d = delta.magnitude;
            if (d > teammateSeparation || d < 0.0001f) continue;
            push += (delta / d) * (1f - d / teammateSeparation);
        }
        return push;
    }

    Vector2 FleeThreats(Vector2 pos)
    {
        Vector2 push = Vector2.zero;
        foreach (var other in AllPlayers())
        {
            if (other == null || other.team.Value == me.team.Value) continue;
            Vector2 delta = pos - (Vector2)other.transform.position;
            float d = delta.magnitude;
            if (d > threatFleeRadius || d < 0.0001f) continue;
            push += (delta / d) * (1f - d / threatFleeRadius);
        }
        return push;
    }

    Vector2 Wander()
    {
        wanderTimer -= Time.fixedDeltaTime;
        if (wanderTimer <= 0f)
        {
            wanderTimer = Random.Range(0.8f, 2.0f);
            wander = Random.insideUnitCircle.normalized;
        }
        return wander;
    }

    // ---------------- lookups ----------------

    static Team Opposite(Team t) => t == Team.Red ? Team.Blue : Team.Red;

    static Transform[] GetCover()
    {
        // Cover is static for the match, but re-resolve occasionally so a scene
        // reload doesn't leave us holding destroyed transforms.
        if (coverCache != null && Time.time - coverCacheTime < 5f) return coverCache;

        var root = GameObject.Find("Cover");
        if (root == null) { coverCache = new Transform[0]; }
        else
        {
            var list = new List<Transform>();
            // Bushes are walk-through concealment now, so only crates are obstacles.
            foreach (Transform c in root.transform)
                if (!c.name.StartsWith("Bush")) list.Add(c);
            coverCache = list.ToArray();
        }
        coverCacheTime = Time.time;
        return coverCache;
    }

    static TeamMember[] playerCache;
    static float playerCacheTime = -1f;
    static TeamMember[] AllPlayers()
    {
        if (playerCache != null && Time.time - playerCacheTime < 0.5f) return playerCache;
        playerCache = FindObjectsOfType<TeamMember>();
        playerCacheTime = Time.time;
        return playerCache;
    }

    static NetworkFlagController[] flagCache;
    static NetworkFlagController FindFlag(Team t)
    {
        if (flagCache == null || flagCache.Length == 0)
            flagCache = FindObjectsOfType<NetworkFlagController>();
        foreach (var f in flagCache)
            if (f != null && f.team == t) return f;
        return null;
    }

    static NetworkFlagBase[] baseCache;
    static NetworkFlagBase FindBase(Team t)
    {
        if (baseCache == null || baseCache.Length == 0)
            baseCache = FindObjectsOfType<NetworkFlagBase>();
        foreach (var b in baseCache)
            if (b != null && b.team == t) return b;
        return null;
    }

    TeamMember FindEnemyCarrier(Team myTeam)
    {
        foreach (var p in AllPlayers())
            if (p != null && p.team.Value != myTeam && p.carriedFlag != null)
                return p;
        return null;
    }

    // Bots respect concealment: a hidden enemy can't be targeted, same as for a
    // human player. Carriers stay visible, so they can still be chased down.
    static bool IsConcealedFrom(TeamMember target, Team observerTeam)
    {
        if (target == null) return true;
        if (target.team.Value == observerTeam) return false;
        return target.hiddenInBush.Value && target.carriedFlag == null;
    }

    static float Midline =>
        NetworkGameManager.Instance != null ? NetworkGameManager.Instance.territoryMidline : 0f;

    // True when this bot is standing on the enemy half, i.e. it can be tagged.
    bool InEnemyTerritory()
    {
        float x = transform.position.x;
        return me.team.Value == Team.Red ? x > Midline : x < Midline;
    }

    // Nearest enemy on our half that is close enough to our base to be a real threat.
    TeamMember FindIntruderNearBase(Team myTeam, Vector2 basePos, float range)
    {
        TeamMember best = null;
        float bestSqr = float.MaxValue;
        float mid = Midline;
        float rangeSqr = range * range;

        foreach (var p in AllPlayers())
        {
            if (p == null || p.team.Value == myTeam) continue;
            if (IsConcealedFrom(p, myTeam)) continue;   // can't hunt what you can't see

            Vector2 ppos = p.transform.position;
            bool onOurHalf = myTeam == Team.Red ? ppos.x < mid : ppos.x > mid;
            if (!onOurHalf) continue;

            float toBaseSqr = (ppos - basePos).sqrMagnitude;
            if (toBaseSqr > rangeSqr) continue;

            if (toBaseSqr < bestSqr) { bestSqr = toBaseSqr; best = p; }
        }
        return best;
    }
}
