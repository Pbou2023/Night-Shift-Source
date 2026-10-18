using UnityEngine;
using UnityEngine.AI;

public class MonsterAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;

    [Header("Animator Param")]
    [SerializeField] private string speedParam = "Speed";

    [Header("Tuning")]
    [Tooltip("Velocity magnitude that should count as 'full run' (Speed=1).")]
    [SerializeField] private float runSpeed = 4.5f;

    [Tooltip("Velocity magnitude that should count as 'walk' midpoint (Speed~0.5).")]
    [SerializeField] private float walkSpeed = 1.8f;

    [Tooltip("Smoothing for animator SetFloat")]
    [SerializeField] private float dampTime = 0.12f;

    int speedHash;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!agent) agent = GetComponentInChildren<NavMeshAgent>(true);

        speedHash = Animator.StringToHash(speedParam);

        // Important if you're moving using NavMeshAgent
        if (animator) animator.applyRootMotion = false;
    }

    void Update()
    {
        if (!animator) return;

        float v = 0f;

        if (agent)
        {
            // More stable than transform delta and matches nav movement
            v = agent.velocity.magnitude;
        }
        else
        {
            // Fallback if you're not using NavMeshAgent
            v = 0f;
        }

        // Map speed to 0..1 for the blend tree:
        // 0 = idle, ~0.5 = walk, 1 = run
        float normalized;
        if (v <= 0.01f) normalized = 0f;
        else if (v <= walkSpeed) normalized = Mathf.InverseLerp(0f, walkSpeed, v) * 0.5f;        // 0..0.5
        else normalized = 0.5f + Mathf.InverseLerp(walkSpeed, runSpeed, v) * 0.5f;               // 0.5..1

        animator.SetFloat(speedHash, normalized, dampTime, Time.deltaTime);
    }
}
