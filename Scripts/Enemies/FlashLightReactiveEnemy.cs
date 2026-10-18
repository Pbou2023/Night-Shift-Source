using UnityEngine;
using UnityEngine.Events;

public class FlashlightReactiveEnemy : MonoBehaviour
{
    [Header("Illumination Requirements")]
    [Tooltip("How long the flashlight must be on-target to dispel the enemy.")]
    [SerializeField] private float exposureToDispel = 1.25f;

    [Tooltip("If beam leaves target, exposure drains at this rate (sec/sec). 0 = resets instantly.")]
    [SerializeField] private float exposureDecay = 1.5f;

    [Header("Beam Check")]
    [SerializeField] private float maxDistance = 12f;
    [SerializeField, Range(1f, 179f)] private float coneAngle = 25f;

    [Tooltip("Blockers for line-of-sight check (walls, props).")]
    [SerializeField] private LayerMask blockers = ~0;

    [Tooltip("Optional: aim at collider center if present.")]
    [SerializeField] private Collider targetCollider;

    [Header("Events")]
    public UnityEvent onDispelled;

    private FlashlightController flashlight;
    private Transform beamOrigin;
    private FlashlightEnemyManager manager;

    private float exposure;

    public void Init(FlashlightController fl, Transform beamOrigin, FlashlightEnemyManager mgr)
    {
        flashlight = fl;
        this.beamOrigin = beamOrigin;
        manager = mgr;

        if (!targetCollider) targetCollider = GetComponentInChildren<Collider>();
    }

    void Update()
    {
        if (flashlight == null || beamOrigin == null) return;

        bool lit = IsIlluminated();

        if (lit)
        {
            exposure += Time.deltaTime;
            if (exposure >= exposureToDispel)
            {
                Dispel();
                return;
            }
        }
        else
        {
            if (exposureDecay <= 0f) exposure = 0f;
            else exposure = Mathf.Max(0f, exposure - exposureDecay * Time.deltaTime);
        }
    }

    bool IsIlluminated()
    {
        if (!flashlight.IsOn) return false;

        Vector3 targetPos = GetTargetPoint();
        Vector3 origin = beamOrigin.position;

        Vector3 toTarget = targetPos - origin;
        float dist = toTarget.magnitude;
        if (dist > maxDistance) return false;

        Vector3 dir = toTarget / Mathf.Max(0.0001f, dist);

        float angle = Vector3.Angle(beamOrigin.forward, dir);
        if (angle > coneAngle) return false;

        // Line of sight
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, blockers, QueryTriggerInteraction.Ignore))
        {
            // If we hit something before reaching enemy -> blocked
            return false;
        }

        return true;
    }

    Vector3 GetTargetPoint()
    {
        if (targetCollider) return targetCollider.bounds.center;
        return transform.position;
    }

    void Dispel()
    {
        onDispelled?.Invoke();
        manager?.NotifyEnemyCleared(gameObject);
        Destroy(gameObject);

       
    }
}
