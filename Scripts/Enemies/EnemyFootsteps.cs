using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyFootsteps : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NavMeshAgent agent;
    [Tooltip("Optional: if assigned, footsteps get louder/faster in chase.")]
    [SerializeField] private BasicChaseEnemy enemy;

    [Header("Clips")]
    [SerializeField] private AudioClip[] stepClips;

    [Header("Audio")]
    [SerializeField] private AudioSource stepSource;
    [Range(0f, 1f)][SerializeField] private float volume = 0.9f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private Vector2 volumeJitter = new Vector2(0.9f, 1.0f);

    [Header("When to Step")]
    [Tooltip("Below this horizontal speed, no footsteps.")]
    [SerializeField] private float minMoveSpeed = 0.15f;

    [Tooltip("Distance traveled per step while walking (meters).")]
    [SerializeField] private float walkStepDistance = 1.3f;

    [Tooltip("Distance traveled per step while running/chasing (meters).")]
    [SerializeField] private float runStepDistance = 0.85f;

    [Tooltip("If agent speed is above this, we treat it as 'run'.")]
    [SerializeField] private float runSpeedThreshold = 2.8f;

    [Header("Chase Boost (optional)")]
    [SerializeField] private bool boostInChase = true;
    [SerializeField] private float chaseVolumeMultiplier = 1.15f;

    private Vector3 lastPos;
    private float distAccumulator;
    private bool inChase;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();

        EnsureAudioSource();

        lastPos = transform.position;
    }

    void OnEnable()
    {
        if (enemy)
        {
            enemy.OnChaseStarted += OnChaseStarted;
            enemy.OnChaseEnded += OnChaseEnded;
        }
    }

    void OnDisable()
    {
        if (enemy)
        {
            enemy.OnChaseStarted -= OnChaseStarted;
            enemy.OnChaseEnded -= OnChaseEnded;
        }
    }

    void Update()
    {
        if (stepClips == null || stepClips.Length == 0) return;
        if (!agent || !agent.enabled) return;

        // Horizontal movement only (ignore tiny y jitter)
        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - lastPos;
        delta.y = 0f;

        float moved = delta.magnitude;
        lastPos = currentPos;

        // Use agent velocity as a sanity check so we don't step while basically stationary
        Vector3 v = agent.velocity;
        v.y = 0f;
        float speed = v.magnitude;

        if (speed < minMoveSpeed || moved <= 0.0001f) return;

        distAccumulator += moved;

        bool isRun = speed >= runSpeedThreshold;
        float stepDist = isRun ? runStepDistance : walkStepDistance;

        // Optional: make chase feel more present
        if (boostInChase && inChase)
            stepDist *= 0.9f; // slightly faster cadence

        while (distAccumulator >= stepDist)
        {
            distAccumulator -= stepDist;
            PlayStep(isRun);
        }
    }

    void PlayStep(bool isRun)
    {
        if (!stepSource) return;

        AudioClip clip = stepClips[Random.Range(0, stepClips.Length)];
        if (!clip) return;

        stepSource.pitch = Random.Range(pitchRange.x, pitchRange.y);

        float vol = volume * Random.Range(volumeJitter.x, volumeJitter.y);

        if (boostInChase && inChase)
            vol *= chaseVolumeMultiplier;

        stepSource.PlayOneShot(clip, vol);
    }

    void OnChaseStarted() => inChase = true;
    void OnChaseEnded() => inChase = false;

    void EnsureAudioSource()
    {
        if (stepSource) return;

        stepSource = GetComponent<AudioSource>();
        if (!stepSource) stepSource = gameObject.AddComponent<AudioSource>();

        stepSource.playOnAwake = false;
        stepSource.loop = false;

        // IMPORTANT: footsteps should be positional
        stepSource.spatialBlend = 1f;
        stepSource.rolloffMode = AudioRolloffMode.Logarithmic;
        stepSource.dopplerLevel = 0f;

        // Tune these to taste
        stepSource.minDistance = 2f;
        stepSource.maxDistance = 18f;
    }
}
