using UnityEngine;

public class CustomerVisualRandomizer : MonoBehaviour
{
    [Header("Where the model gets spawned")]
    [SerializeField] private Transform visualRoot;

    [Header("Candidate model prefabs (rigged humanoids preferred)")]
    [SerializeField] private GameObject[] modelPrefabs;

    [Header("Animator defaults (optional but recommended)")]
    [SerializeField] private RuntimeAnimatorController defaultController; // your Customer_Anim.controller

    [Header("Optional")]
    [SerializeField] private bool randomizeOnEnable = true;
    [SerializeField] private bool destroyPrevious = true;

    private GameObject currentInstance;
    LockHipsTranslation hipsLocker;
    public Animator CurrentAnimator { get; private set; }

    void Awake()
    {
        if (!visualRoot) visualRoot = transform; // fallback
        hipsLocker = GetComponent<LockHipsTranslation>(); // NEW
        
        if (randomizeOnEnable) PickAndSpawn();
    }

    void OnEnable()
    {
        if (randomizeOnEnable && currentInstance == null)
            PickAndSpawn();
    }

    public void PickAndSpawn()
    {
        if (modelPrefabs == null || modelPrefabs.Length == 0) return;

        if (destroyPrevious && currentInstance)
            Destroy(currentInstance);

        var prefab = modelPrefabs[Random.Range(0, modelPrefabs.Length)];
        if (!prefab) return;

        currentInstance = Instantiate(prefab, visualRoot);
        currentInstance.transform.localPosition = Vector3.zero;
        currentInstance.transform.localRotation = Quaternion.identity;
        currentInstance.transform.localScale = Vector3.one;

        // Find animator on spawned model
        CurrentAnimator = currentInstance.GetComponentInChildren<Animator>(true);

        // If it exists but has no controller, force one
        if (CurrentAnimator != null && CurrentAnimator.runtimeAnimatorController == null && defaultController != null)
        {
            CurrentAnimator.runtimeAnimatorController = defaultController;
        }

        // Tell the driver (if present) to rebind to this animator
        var driver = GetComponentInParent<CustomerWalkAnimDriver>();
        if (driver != null)
            driver.BindAnimator(CurrentAnimator);

        if (hipsLocker) hipsLocker.Rebind();
    }
}
