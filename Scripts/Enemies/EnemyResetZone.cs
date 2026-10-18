using UnityEngine;

public class EnemyResetZone : MonoBehaviour
{
    [Tooltip("If set, only objects on this layer can trigger the reset. Leave as Everything to allow any.")]
    [SerializeField] private LayerMask enemyLayerMask = ~0;

    private void OnTriggerEnter(Collider other)
    {
        // Layer filter
        if ((enemyLayerMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        // Try on the collider object or its parents
        var enemy = other.GetComponentInParent<BasicChaseEnemy>();
        if (enemy != null)
        {
            enemy.ResetToSpawn();
            return;
        }

        // Optional: also support other enemy scripts if you rename later
        // (You can delete this if you want it strict)
        var agent = other.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            // If you somehow only have an agent and no AI script, at least unstick it
            agent.ResetPath();
        }
    }
}
