using UnityEngine;
using UnityEngine.AI;

public class FlashlightEnemyManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraOrigin;                 // for optional behind-player spawning
    [SerializeField] private FlashlightController flashlight;
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Around Player")]
    [SerializeField] private float minRadius = 6f;
    [SerializeField] private float maxRadius = 12f;
    [SerializeField] private int spawnTries = 20;

    [Tooltip("Try to avoid spawning directly in front of the player.")]
    [SerializeField] private bool avoidFront = true;
    [SerializeField] private float frontAvoidAngle = 70f; // degrees

    [Header("Spawn Timing")]
    [SerializeField] private float baseSpawnInterval = 18f;
    [SerializeField] private float minSpawnInterval = 6f;
    [SerializeField] private float offTimeToMinInterval = 12f;
    [SerializeField] private float intervalJitter = 2f;

    [Header("Rules")]
    [SerializeField] private bool singleActiveEnemy = true;

    private float offTime;
    private float nextSpawnTime;
    private GameObject currentEnemy;

    void Awake()
    {
        if (!player && Camera.main) player = Camera.main.transform;
        if (!cameraOrigin && Camera.main) cameraOrigin = Camera.main.transform;
        if (!flashlight) flashlight = FindObjectOfType<FlashlightController>(true);

        ScheduleNextSpawn(0.5f);
    }

    void Update()
    {
        if (!player || !flashlight) return;

        if (flashlight.IsOn) offTime = 0f;
        else offTime += Time.deltaTime;

        if (Time.time >= nextSpawnTime)
        {
            if (!singleActiveEnemy || currentEnemy == null)
                SpawnNearPlayer();

            ScheduleNextSpawn();
        }
    }

    void SpawnNearPlayer()
    {
        if (!enemyPrefab) return;

        if (!TryFindSpawnPos(out Vector3 pos))
        {
            Debug.LogWarning("[FlashlightEnemyManager] Failed to find spawn pos near player.");
            return;
        }

        currentEnemy = Instantiate(enemyPrefab, pos, Quaternion.identity);

        // If it uses your BasicChaseEnemy, force chase behavior:
        var chase = currentEnemy.GetComponent<BasicChaseEnemy>();
        if (chase != null)
        {
            // Make sure its player ref is correct
            // (Your BasicChaseEnemy uses Camera.main by default, but we set it cleanly anyway)
            var playerField = currentEnemy.GetComponentInChildren<Transform>(); // not needed; just here as placeholder
            // Instead: expose player in inspector OR keep Camera.main logic.
            // Easiest: set startInChase/forceAlwaysChase on the prefab.
            chase.ForceChaseNow();
        }

        // If it uses the flashlight dispel script, make sure it has refs:
        var dispel = currentEnemy.GetComponent<FlashlightReactiveEnemy>();
        if (dispel != null)
        {
            // set these in the prefab OR use an Init() function if you added one
            // (If you're using my earlier Init version, call it here.)
            // dispel.Init(flashlight, cameraOrigin, this);
        }
    }

    bool TryFindSpawnPos(out Vector3 result)
    {
        Vector3 p = player.position;

        for (int i = 0; i < spawnTries; i++)
        {
            float r = Random.Range(minRadius, maxRadius);
            Vector2 circle = Random.insideUnitCircle.normalized * r;
            Vector3 candidate = p + new Vector3(circle.x, 0f, circle.y);

            // optional: avoid front
            if (avoidFront && cameraOrigin != null)
            {
                Vector3 dir = (candidate - p);
                dir.y = 0f;
                float angle = Vector3.Angle(cameraOrigin.forward, dir);
                if (angle < frontAvoidAngle * 0.5f)
                    continue;
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = p;
        return false;
    }
    public void NotifyEnemyCleared(GameObject enemy)
    {
        if (enemy == currentEnemy)
            currentEnemy = null;
    }

    void ScheduleNextSpawn(float overrideDelay = -1f)
    {
        float delay;

        if (overrideDelay >= 0f)
        {
            delay = overrideDelay;
        }
        else
        {
            float t = (offTimeToMinInterval <= 0f) ? 1f : Mathf.Clamp01(offTime / offTimeToMinInterval);
            float interval = Mathf.Lerp(baseSpawnInterval, minSpawnInterval, t);
            interval += Random.Range(-intervalJitter, intervalJitter);
            delay = Mathf.Max(0.5f, interval);
        }

        nextSpawnTime = Time.time + delay;
    }
}
