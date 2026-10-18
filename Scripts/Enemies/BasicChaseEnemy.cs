using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
public class BasicChaseEnemy : MonoBehaviour
{
    public enum State { Roam, Chase, Search }

    [Header("Refs")]
    [SerializeField] private Transform player;
    private NavMeshAgent agent;

    private Vector3 spawnPos;
    private Quaternion spawnRot;

    [Header("Detection")]
    [SerializeField] private float viewDistance = 12f;
    [SerializeField, Range(1f, 179f)] private float viewAngle = 70f;
    [SerializeField] private LayerMask lineOfSightBlockers;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float targetHeight = 1.3f;

    [Header("Roam Coverage")]
    [SerializeField] private Collider roamBounds;
    [SerializeField] private Transform roamCenterOverride;
    [SerializeField] private float fallbackRoamRadius = 25f;
    [SerializeField] private float roamPointTolerance = 0.8f;
    [SerializeField] private float repathInterval = 0.25f;

    [Header("No-Roam Zone (NavMesh Area)")]
    [SerializeField] private string noRoamAreaName = "NoEnemyRoam";
    [SerializeField] private bool allowChaseIntoNoRoam = true;

    [Header("Movement")]
    [SerializeField] private float roamSpeed = 2.2f;
    [SerializeField] private float chaseSpeed = 3.8f;
    [SerializeField] private float catchDistance = 1.35f;

    [Header("Chase Memory / Give Up")]
    [SerializeField] private float chaseMemorySeconds = 3.0f;
    [SerializeField] private float giveUpDistance = 14f;
    [SerializeField] private float searchDuration = 3f;

    [Header("Roam Path Quality")]
    [SerializeField] private bool requireCompletePathForRoam = true;

    [Header("Stuck Detection & Recovery")]
    [SerializeField] private bool enableStuckDetection = true;
    [SerializeField] private float stuckCheckInterval = 0.3f;
    [SerializeField] private float stuckVelocityThreshold = 0.1f;
    [SerializeField] private float stuckTimeBeforeRecovery = 1.2f;
    [SerializeField] private float destinationOffNavMeshTolerance = 1.5f;
    [SerializeField] private int maxConsecutiveRecoveries = 4;

    [Header("Advanced Recovery")]
    [SerializeField] private bool useAggressiveUnstuck = true;
    [SerializeField] private float unstuckBackupDistance = 2f;
    [SerializeField] private float unstuckSideStepDistance = 1.5f;

    [Header("Spawn Behavior")]
    [SerializeField] private bool startInChase = false; // for flashlight enemy
    [SerializeField] private bool forceAlwaysChase = false; // ignore FOV forever


    [Header("Panic/VHS Effect")]
    [SerializeField] private float panicDecaySpeed = 2f;

    [Header("Kill / Game Over")]
    [SerializeField] private bool triggerDeathOnCatch = true;
    [SerializeField] private PlayerDeathManager deathManager;
    public UnityEvent onCaughtPlayer; // optional extra hooks

    private bool hasCaughtPlayer;


    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugDraw = false;
    [SerializeField] private bool debugDrawPath = true;
    [SerializeField] private bool debugDrawStuck = true;

    public State CurrentState => state;
    private State state;

    private float currentPanic = 0f;

    private Vector3 lastSeenPos;
    private bool hasLastSeen;
    private float lastSeenTime;

    private float repathTimer;
    private float searchTimer;

    public event Action OnAggroStarted; // Chase OR Search started (from Roam)
    public event Action OnAggroEnded;   // Back to Roam (from Chase/Search)

    private int roamAreaMask = NavMesh.AllAreas;
    private int chaseAreaMask = NavMesh.AllAreas;

    // ---- EVENTS ----
    public event Action OnChaseStarted;
    public event Action OnChaseEnded;
    public event Action<float> OnPanicChanged;
    public event Action OnPlayerCaught;

    private float lastPanicSent = -1f;

    // ---- STUCK DETECTION ----
    private float stuckTimer;
    private float stuckCheckTimer;
    private Vector3 lastPosition;
    private Vector3 lastDestination;
    private int consecutiveRecoveries;

    // ---- CHASE PROGRESS TRACKING ----
    private float lastDistanceToTarget = -1f;
    private float noProgressTimer;
    private const float NO_PROGRESS_TIMEOUT = 2.5f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!player && Camera.main) player = Camera.main.transform;

        spawnPos = transform.position;
        spawnRot = transform.rotation;

        BuildAreaMasks();

        if (!deathManager) deathManager = FindObjectOfType<PlayerDeathManager>(true);

        // FIX: Prevent vertical jittering
        if (agent)
        {
            // Ensure agent controls position, not animator or rigidbody
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.updateUpAxis = true; // This keeps agent on navmesh plane
        }

        // Disable animator root motion if present
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        // Remove or configure rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnValidate()
    {
        BuildAreaMasks();
    }

    void OnEnable()
    {
        lastPosition = transform.position;
        stuckTimer = 0f;
        stuckCheckTimer = 0f;
        consecutiveRecoveries = 0;
        lastDistanceToTarget = -1f;
        noProgressTimer = 0f;

        // Ensure agent is active
        if (agent)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        EnterRoam();

        if (startInChase)
        {
            ForceChaseNow();
        }

    }

    void Update()
    {
        if (!agent || !player) return;

        // Safety: ensure agent stays enabled and running
        if (!agent.enabled) agent.enabled = true;
        if (agent.isStopped) agent.isStopped = false;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Catch player
        // Catch player
        if (state == State.Chase && distToPlayer <= catchDistance)
        {
            if (!hasCaughtPlayer)
            {
                hasCaughtPlayer = true;

                SendPanic(1f);
                OnPlayerCaught?.Invoke();
                onCaughtPlayer?.Invoke();

                if (triggerDeathOnCatch)
                {
                    if (!deathManager) deathManager = PlayerDeathManager.Instance;
                    if (deathManager) deathManager.TriggerDeath(true);

                }

                if (debugLog) Debug.Log("[ENEMY] Player caught!");
            }

            // stop this enemy from doing anything else
            if (agent) agent.isStopped = true;
            enabled = false;
            return;
        }


        bool los = HasLineOfSight();

        // State updates
        switch (state)
        {
            case State.Roam:
                UpdateRoam();
                UpdatePanic(0f);
                break;

            case State.Chase:
                UpdateChase(los, distToPlayer);
                UpdatePanic(ComputePanic(distToPlayer));
                break;

            case State.Search:
                UpdateSearch();
                UpdatePanic(0f);
                break;
        }

        // Stuck detection (runs in all states)
        if (enableStuckDetection)
            UpdateStuckDetection(los, distToPlayer);

        // Visual debugging
        if (debugDrawPath && agent.hasPath)
        {
            DrawAgentPath();
        }

        if (debugDrawStuck)
        {
            DrawStuckDebug();
        }
    }

    void DrawAgentPath()
    {
        if (agent.path == null || agent.path.corners.Length < 2) return;

        Color pathColor = state == State.Chase ? Color.red :
                         state == State.Search ? Color.yellow : Color.green;

        for (int i = 0; i < agent.path.corners.Length - 1; i++)
        {
            Debug.DrawLine(agent.path.corners[i], agent.path.corners[i + 1], pathColor);
        }

        // Draw destination
        if (agent.hasPath && !float.IsInfinity(agent.remainingDistance))
        {
            Debug.DrawLine(transform.position, agent.destination, pathColor * 0.5f);
        }
    }

    void DrawStuckDebug()
    {
        // Draw velocity vectors
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, agent.velocity, Color.cyan);
        Debug.DrawRay(transform.position + Vector3.up * 0.8f, agent.desiredVelocity, Color.magenta);

        // Draw sphere at enemy position (bigger if stuck)
        float sphereSize = stuckTimer > 0.5f ? 1f : 0.3f;
        Color stuckColor = stuckTimer > stuckTimeBeforeRecovery * 0.5f ? Color.red : Color.white;

        Debug.DrawRay(transform.position, Vector3.up * sphereSize, stuckColor);
    }

    // -------------------- Panic --------------------

    float ComputePanic(float distToPlayer)
    {
        float t = 1f - Mathf.InverseLerp(catchDistance, viewDistance, distToPlayer);
        return Mathf.Clamp01(t);
    }

    void UpdatePanic(float targetPanic)
    {
        // Smoothly lerp current panic toward target
        currentPanic = Mathf.MoveTowards(currentPanic, targetPanic, panicDecaySpeed * Time.deltaTime);
        SendPanic(currentPanic);
    }

    void SendPanic(float p)
    {
        if (Mathf.Abs(p - lastPanicSent) < 0.01f) return;
        lastPanicSent = p;
        OnPanicChanged?.Invoke(p);
    }

    // -------------------- State Updates --------------------

    void UpdateRoam()
    {
        agent.speed = roamSpeed;

        if (forceAlwaysChase)
        {
            ForceChaseNow();
            return;
        }

        if (CanDetectPlayerWithFOV())
        {
            CacheSeen();
            EnterChase();
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer > 0f) return;
        repathTimer = repathInterval;

        // Check if we need a new destination
        bool needNewDest = !agent.hasPath ||
                           agent.pathPending ||
                           agent.pathStatus != NavMeshPathStatus.PathComplete ||
                           float.IsInfinity(agent.remainingDistance) ||
                           agent.remainingDistance <= roamPointTolerance;

        if (needNewDest)
        {
            if (TryPickRoamDestination(out Vector3 dest))
            {
                SetDestinationSafely(dest);
                if (debugLog) Debug.Log($"[ROAM] New destination: {dest}");
            }
        }
    }

    void UpdateChase(bool los, float distToPlayer)
    {
        agent.speed = chaseSpeed;

        if (los && distToPlayer <= viewDistance)
            CacheSeen();

        // Track progress toward target
        if (lastDistanceToTarget < 0f) lastDistanceToTarget = distToPlayer;

        float progressThreshold = 0.1f; // Must get 10cm closer to count as progress
        if (distToPlayer < lastDistanceToTarget - progressThreshold)
        {
            // Making progress, reset timer
            noProgressTimer = 0f;
            lastDistanceToTarget = distToPlayer;
        }
        else
        {
            // Not making progress
            noProgressTimer += Time.deltaTime;
        }

        // If no progress for too long AND no LOS, give up chase
        if (!los && noProgressTimer >= NO_PROGRESS_TIMEOUT)
        {
            if (debugLog) Debug.Log("[CHASE] No progress + no LOS, entering Search");
            EnterSearch();
            return;
        }

        // Repath periodically
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;

            if (los)
            {
                SetDestinationSafely(player.position);
            }
            else if (hasLastSeen)
            {
                SetDestinationSafely(lastSeenPos);
            }
        }

        // Memory-based give up
        bool memoryExpired = (Time.time - lastSeenTime) >= chaseMemorySeconds;
        if (!los && memoryExpired && distToPlayer >= giveUpDistance)
        {
            if (debugLog) Debug.Log("[CHASE] Memory expired + too far, entering Search");
            EnterSearch();
        }
    }

    void UpdateSearch()
    {
        agent.speed = roamSpeed;

        if (CanDetectPlayerWithFOV())
        {
            CacheSeen();
            EnterChase();
            return;
        }

        if (!hasLastSeen)
        {
            EnterRoam();
            return;
        }

        // Repath if needed
        if (!agent.hasPath ||
            agent.pathStatus != NavMeshPathStatus.PathComplete ||
            float.IsInfinity(agent.remainingDistance))
        {
            SetDestinationSafely(lastSeenPos);
        }

        // Check if reached search location
        if (agent.remainingDistance <= roamPointTolerance)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchDuration)
            {
                if (debugLog) Debug.Log("[SEARCH] Duration expired, returning to Roam");
                EnterRoam();
            }
        }
    }

    // -------------------- Stuck Detection & Recovery --------------------

    void UpdateStuckDetection(bool los, float distToPlayer)
    {
        stuckCheckTimer -= Time.deltaTime;
        if (stuckCheckTimer > 0f) return;
        stuckCheckTimer = stuckCheckInterval;

        // Don't check if we've arrived or have no path
        if (!agent.hasPath || agent.pathPending)
        {
            ResetStuckTimer();
            return;
        }

        if (float.IsInfinity(agent.remainingDistance))
        {
            if (debugLog) Debug.Log("[STUCK] Infinite remainingDistance detected");
            AttemptRecovery(los, distToPlayer, "Infinite distance");
            return;
        }

        // If we're close to destination, not stuck
        if (agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.5f))
        {
            ResetStuckTimer();
            return;
        }

        // Check actual velocity (not desiredVelocity, which can be wrong)
        float actualSpeed = agent.velocity.magnitude;
        bool isMoving = actualSpeed > stuckVelocityThreshold;
        bool wantsToMove = agent.desiredVelocity.sqrMagnitude > 0.01f;

        // Check position change as backup
        float positionDelta = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        bool stuckCondition = !isMoving && wantsToMove && positionDelta < 0.05f;

        if (stuckCondition)
        {
            stuckTimer += stuckCheckInterval;

            if (debugLog && stuckTimer > 0.5f)
            {
                Debug.Log($"[STUCK] Timer: {stuckTimer:F2}s | Vel: {actualSpeed:F3} | PosDelta: {positionDelta:F3} | Remaining: {agent.remainingDistance:F2}");
            }

            if (stuckTimer >= stuckTimeBeforeRecovery)
            {
                AttemptRecovery(los, distToPlayer, "Movement timeout");
            }
        }
        else
        {
            // Decay timer if making progress
            stuckTimer = Mathf.Max(0f, stuckTimer - stuckCheckInterval * 2f);
        }
    }

    void ResetStuckTimer()
    {
        if (stuckTimer > 0f && debugLog)
            Debug.Log("[STUCK] Timer reset");
        stuckTimer = 0f;
    }

    void AttemptRecovery(bool los, float distToPlayer, string reason)
    {
        consecutiveRecoveries++;

        if (debugLog)
            Debug.LogWarning($"[RECOVERY #{consecutiveRecoveries}] Reason: {reason} | State: {state} | Pos: {transform.position}");

        // Safety valve: hard reset if too many recoveries
        if (consecutiveRecoveries >= maxConsecutiveRecoveries)
        {
            if (debugLog) Debug.LogError("[RECOVERY] Max recoveries reached, hard reset to spawn");
            ResetToSpawn();
            consecutiveRecoveries = 0;
            ResetStuckTimer();
            return;
        }

        agent.ResetPath();

        // Try different recovery strategies
        bool recovered = false;

        // Strategy 1: Simple navmesh warp
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, agent.areaMask))
        {
            agent.Warp(hit.position);
            recovered = true;
            if (debugLog) Debug.Log($"[RECOVERY] Warped to nearby navmesh: {hit.position}");
        }

        // Strategy 2: Aggressive unstuck (backup/sidestep)
        if (useAggressiveUnstuck && !recovered)
        {
            recovered = TryAggressiveUnstuck();
        }

        // Strategy 3: Smart destination re-calculation
        Vector3 newDest = transform.position;

        if (state == State.Chase)
        {
            // Sample position near player rather than exact player position
            newDest = SampleSafeDestinationNear(player.position, 1f);
            if (debugLog) Debug.Log($"[RECOVERY] Chase - new dest near player: {newDest}");
        }
        else if (state == State.Search && hasLastSeen)
        {
            newDest = SampleSafeDestinationNear(lastSeenPos, 1.5f);
            if (debugLog) Debug.Log($"[RECOVERY] Search - new dest near last seen: {newDest}");
        }
        else // Roam
        {
            if (TryPickRoamDestination(out newDest))
            {
                if (debugLog) Debug.Log($"[RECOVERY] Roam - picked new destination: {newDest}");
            }
        }

        SetDestinationSafely(newDest);
        ResetStuckTimer();
    }

    bool TryAggressiveUnstuck()
    {
        // Try backing up
        Vector3 backupPos = transform.position - transform.forward * unstuckBackupDistance;
        if (NavMesh.SamplePosition(backupPos, out NavMeshHit backupHit, 2f, agent.areaMask))
        {
            agent.Warp(backupHit.position);
            if (debugLog) Debug.Log($"[RECOVERY] Aggressive backup to: {backupHit.position}");
            return true;
        }

        // Try sidestep left
        Vector3 leftPos = transform.position - transform.right * unstuckSideStepDistance;
        if (NavMesh.SamplePosition(leftPos, out NavMeshHit leftHit, 2f, agent.areaMask))
        {
            agent.Warp(leftHit.position);
            if (debugLog) Debug.Log($"[RECOVERY] Aggressive sidestep left to: {leftHit.position}");
            return true;
        }

        // Try sidestep right
        Vector3 rightPos = transform.position + transform.right * unstuckSideStepDistance;
        if (NavMesh.SamplePosition(rightPos, out NavMeshHit rightHit, 2f, agent.areaMask))
        {
            agent.Warp(rightHit.position);
            if (debugLog) Debug.Log($"[RECOVERY] Aggressive sidestep right to: {rightHit.position}");
            return true;
        }

        return false;
    }

    Vector3 SampleSafeDestinationNear(Vector3 target, float radius)
    {
        // Try to find navmesh point near target rather than exact target
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, radius, agent.areaMask))
        {
            return hit.position;
        }

        // Fallback: try random offset
        for (int i = 0; i < 8; i++)
        {
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * radius;
            randomOffset.y = 0f;
            Vector3 testPos = target + randomOffset;

            if (NavMesh.SamplePosition(testPos, out NavMeshHit offsetHit, radius, agent.areaMask))
            {
                return offsetHit.position;
            }
        }

        return target; // Last resort
    }

    // -------------------- Helpers --------------------

    void CacheSeen()
    {
        lastSeenPos = player.position;
        hasLastSeen = true;
        lastSeenTime = Time.time;
    }

    bool HasLineOfSight()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * targetHeight;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        if (dist > viewDistance) return false;

        dir /= dist;

        bool blocked = Physics.Raycast(origin, dir, out RaycastHit hit, dist, lineOfSightBlockers, QueryTriggerInteraction.Ignore);

        if (debugDraw)
            Debug.DrawLine(origin, blocked ? hit.point : target, blocked ? Color.red : Color.green);

        return !blocked;
    }

    bool CanDetectPlayerWithFOV()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * targetHeight;

        Vector3 toPlayer = target - origin;
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return false;

        Vector3 dir = toPlayer / dist;

        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > viewAngle * 0.5f) return false;

        if (Physics.Raycast(origin, dir, dist, lineOfSightBlockers, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    bool TryPickRoamDestination(out Vector3 dest)
    {
        if (roamBounds != null)
            return TryGetRandomNavPointInBounds(roamBounds.bounds, roamAreaMask, out dest);

        Vector3 center = roamCenterOverride ? roamCenterOverride.position : spawnPos;
        return TryGetRandomNavPointInRadius(center, fallbackRoamRadius, roamAreaMask, out dest);
    }

    bool TryGetRandomNavPointInBounds(Bounds b, int areaMask, out Vector3 result)
    {
        for (int i = 0; i < 40; i++)
        {
            Vector3 rand = new Vector3(
                UnityEngine.Random.Range(b.min.x, b.max.x),
                b.center.y,
                UnityEngine.Random.Range(b.min.z, b.max.z)
            );

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 3.0f, areaMask))
            {
                if (!requireCompletePathForRoam || HasCompletePathTo(hit.position))
                {
                    result = hit.position;
                    return true;
                }
            }
        }

        result = transform.position;
        return false;
    }

    bool TryGetRandomNavPointInRadius(Vector3 center, float radius, int areaMask, out Vector3 result)
    {
        for (int i = 0; i < 40; i++)
        {
            Vector3 rand = center + UnityEngine.Random.insideUnitSphere * radius;
            rand.y = center.y;

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 3.0f, areaMask))
            {
                if (!requireCompletePathForRoam || HasCompletePathTo(hit.position))
                {
                    result = hit.position;
                    return true;
                }
            }
        }

        result = transform.position;
        return false;
    }

    bool HasCompletePathTo(Vector3 destination)
    {
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path)) return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    void BuildAreaMasks()
    {
        int idx = NavMesh.GetAreaFromName(noRoamAreaName);

        roamAreaMask = NavMesh.AllAreas;
        chaseAreaMask = NavMesh.AllAreas;

        if (idx >= 0)
        {
            int noRoamBit = 1 << idx;
            roamAreaMask = NavMesh.AllAreas & ~noRoamBit;
            chaseAreaMask = allowChaseIntoNoRoam ? NavMesh.AllAreas : roamAreaMask;
        }
    }

    void SetDestinationSafely(Vector3 target)
    {
        // First check if target is even on navmesh
        if (!NavMesh.SamplePosition(target, out NavMeshHit targetHit, destinationOffNavMeshTolerance, agent.areaMask))
        {
            if (debugLog) Debug.LogWarning($"[PATH] Target off navmesh: {target}, using nearest point");
            target = targetHit.position;
        }

        NavMeshPath path = new NavMeshPath();
        bool canCalculate = agent.CalculatePath(target, path);

        if (!canCalculate || path.corners == null || path.corners.Length == 0)
        {
            if (debugLog) Debug.LogWarning($"[PATH] Cannot calculate path to {target}");
            agent.SetDestination(target);
            return;
        }

        if (path.status == NavMeshPathStatus.PathComplete)
        {
            agent.SetDestination(target);
            consecutiveRecoveries = 0; // Success resets recovery counter
            if (debugDraw)
            {
                for (int i = 0; i < path.corners.Length - 1; i++)
                    Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.cyan, 0.5f);
            }
            return;
        }

        // Partial path: go to farthest reachable corner
        int idx = path.corners.Length - 1;
        while (idx > 0 && Vector3.Distance(transform.position, path.corners[idx]) < 0.8f)
            idx--;

        Vector3 fallbackDest = path.corners[idx];
        agent.SetDestination(fallbackDest);

        if (debugLog)
            Debug.LogWarning($"[PATH] Partial path to {target}, using corner {idx}: {fallbackDest}");

        if (debugDraw)
        {
            for (int i = 0; i < path.corners.Length - 1; i++)
                Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.yellow, 0.5f);
        }
    }

    // -------------------- State Transitions --------------------

    void EnterRoam()
    {
        // If we were aggro (chase/search), stop music now
        if (state == State.Chase || state == State.Search)
            OnAggroEnded?.Invoke();

        // Keep old semantics if you still want "chase ended" specifically
        if (state == State.Chase) OnChaseEnded?.Invoke();

        state = State.Roam;
        searchTimer = 0f;
        repathTimer = 0f;
        lastDistanceToTarget = -1f;
        noProgressTimer = 0f;

        agent.areaMask = roamAreaMask;
        agent.ResetPath();

        if (TryPickRoamDestination(out Vector3 dest))
            SetDestinationSafely(dest);

        if (debugLog) Debug.Log("[STATE] Entered ROAM");
    }

    void EnterChase()
    {
        // Aggro starts ONLY when we come from Roam
        if (state == State.Roam)
            OnAggroStarted?.Invoke();

        if (state != State.Chase) OnChaseStarted?.Invoke();

        state = State.Chase;
        repathTimer = 0f;
        lastDistanceToTarget = -1f;
        noProgressTimer = 0f;

        agent.areaMask = chaseAreaMask;
        agent.ResetPath();

        SetDestinationSafely(player.position);

        if (debugLog) Debug.Log("[STATE] Entered CHASE");
    }

    void EnterSearch()
    {
        // IMPORTANT: Do NOT end aggro here.
        // We only end aggro when we return to ROAM.

        // Keep old semantics: chase ended (we left CHASE), but music stays.
        if (state == State.Chase) OnChaseEnded?.Invoke();

        state = State.Search;
        searchTimer = 0f;
        repathTimer = 0f;
        lastDistanceToTarget = -1f;
        noProgressTimer = 0f;

        agent.areaMask = roamAreaMask;
        agent.ResetPath();

        if (hasLastSeen)
            SetDestinationSafely(lastSeenPos);

        if (debugLog) Debug.Log("[STATE] Entered SEARCH");
    }


    public void ResetToSpawn()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!agent) return;

        hasLastSeen = false;
        searchTimer = 0f;
        repathTimer = 0f;
        lastDistanceToTarget = -1f;
        noProgressTimer = 0f;
        consecutiveRecoveries = 0;

        if (state == State.Chase) OnChaseEnded?.Invoke();

        agent.ResetPath();

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        else
            agent.Warp(spawnPos);

        transform.rotation = spawnRot;

        EnterRoam();

        if (debugLog) Debug.Log("[RESET] Returned to spawn");
    }

    public void ForceChaseNow()
    {
        if (!agent || !player) return;

        CacheSeen(); // sets lastSeenPos/time

        // Aggro starts ONLY when coming from roam
        if (state == State.Roam) OnAggroStarted?.Invoke();
        if (state != State.Chase) OnChaseStarted?.Invoke();

        state = State.Chase;
        repathTimer = 0f;
        lastDistanceToTarget = -1f;
        noProgressTimer = 0f;

        agent.areaMask = chaseAreaMask;
        agent.ResetPath();
        SetDestinationSafely(player.position);

        if (debugLog) Debug.Log("[STATE] Forced CHASE (spawn behavior)");
    }


}