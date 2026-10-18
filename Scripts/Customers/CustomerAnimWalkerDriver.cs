using UnityEngine;

public class CustomerWalkAnimDriver : MonoBehaviour
{
    [Header("Animator (leave empty to auto-find)")]
    [SerializeField] private Animator animator;

    [Header("Parameter name in Animator Controller")]
    [SerializeField] private string moveSpeedParam = "MoveSpeed";

    [Header("Tuning")]
    [SerializeField] private float speedToCountAsWalking = 0.05f;
    [SerializeField] private float dampTime = 0.1f;

    private int moveSpeedHash;
    private Vector3 lastPos;

    void Awake()
    {
        moveSpeedHash = Animator.StringToHash(moveSpeedParam);
        lastPos = transform.position;
    }

    void Start()
    {
        // After all Awake() are done, try find it safely
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);
    }

    public void BindAnimator(Animator a)
    {
        animator = a;
        if (animator != null)
        {
            // Forces animator to pick up controller/state properly
            animator.Rebind();
            animator.Update(0f);
        }
    }

    void Update()
    {
        if (!animator)
        {
            // Fallback in case spawn happens later for some reason
            animator = GetComponentInChildren<Animator>(true);
            if (!animator) return;
        }

        float speed = (transform.position - lastPos).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
        lastPos = transform.position;

        float drivingValue = (speed >= speedToCountAsWalking) ? speed : 0f;

        animator.SetFloat(moveSpeedHash, drivingValue, dampTime, Time.deltaTime);
    }
}
