using System;
using UnityEngine;

public class ShiftManager : MonoBehaviour
{
    public static ShiftManager Instance { get; private set; }

    [Header("Shift Settings")]
    public bool autoStart = true;
    public int nightIndex = 1;

    [Tooltip("How many orders the player must complete to win the night.")]
    public int quotaTarget = 5;

    [Tooltip("If true, the shift ends immediately when the quota is met.")]
    public bool endShiftWhenQuotaMet = true;

    [Header("Runtime (read-only)")]
    [SerializeField] int ordersCompleted;
    [SerializeField] int ordersMissed;
    [SerializeField] bool shiftRunning;

    // -------- Public API --------
    public int OrdersCompleted => ordersCompleted;

    // New preferred name
    public int OrdersMissed => ordersMissed;

    // Backwards compatibility (old name)
    public int OrdersFailed => ordersMissed;

    public int QuotaTarget => quotaTarget;
    public bool ShiftRunning => shiftRunning;

    // Backwards compatibility: other scripts may still show/use a timer.
    // We have no timer now, so return 0.
    public float TimeRemaining => 0f;

    /// <summary>
    /// Fail condition threshold: 40% of quotaTarget (rounded up).
    /// Example: quota 5 -> 2, quota 6 -> 3.
    /// </summary>
    public int OrdersMissedFailThreshold => Mathf.Max(1, Mathf.CeilToInt(quotaTarget * 0.40f));

    public event Action OnShiftStarted;
    public event Action<bool> OnShiftEnded; // success?
    public event Action OnProgressChanged;

    // NEW: for Night2 / scripted moments
    public event Action<int> OnOrdersCompletedChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (autoStart)
            StartShift(nightIndex);
    }

    void Update()
    {
        if (!shiftRunning) return;

        // SUCCESS: quota met
        if (endShiftWhenQuotaMet && ordersCompleted >= quotaTarget)
        {
            EndShift(success: true);
            return;
        }

        // FAIL: missed threshold reached
        if (ordersMissed >= OrdersMissedFailThreshold)
        {
            EndShift(success: false);
            return;
        }
    }

    public void StartShift(int night)
    {
        nightIndex = night;

        ordersCompleted = 0;
        ordersMissed = 0;
        shiftRunning = true;

        // Safety: clear any leftover order
        if (OrderManager.Instance != null)
            OrderManager.Instance.ClearOrder();

        OnShiftStarted?.Invoke();
        OnProgressChanged?.Invoke();
        OnOrdersCompletedChanged?.Invoke(ordersCompleted); // optional: initialize listeners

        Debug.Log($"[SHIFT] Night {nightIndex} started. Target={quotaTarget}. Fail at missed >= {OrdersMissedFailThreshold}");
    }

    public void RegisterOrderCompleted()
    {
        if (!shiftRunning) return;

        ordersCompleted++;
        OnOrdersCompletedChanged?.Invoke(ordersCompleted);
        OnProgressChanged?.Invoke();

        Debug.Log($"[SHIFT] Order completed ({ordersCompleted}/{quotaTarget})");

        if (endShiftWhenQuotaMet && ordersCompleted >= quotaTarget)
            EndShift(success: true);
    }

    // New preferred name
    public void RegisterOrderMissed()
    {
        if (!shiftRunning) return;

        ordersMissed++;
        OnProgressChanged?.Invoke();

        Debug.Log($"[SHIFT] Order MISSED (missed={ordersMissed}/{OrdersMissedFailThreshold})");

        if (ordersMissed >= OrdersMissedFailThreshold)
            EndShift(success: false);
    }

    // Backwards compatibility (old name used elsewhere)
    public void RegisterOrderFailed()
    {
        RegisterOrderMissed();
    }

    void EndShift(bool success)
    {
        if (!shiftRunning) return;
        shiftRunning = false;

        // Clear active order so UI & logic don’t hang
        if (OrderManager.Instance != null)
            OrderManager.Instance.ClearOrder();

        Debug.Log(success
            ? $"[SHIFT] Night complete ✅ (Completed {ordersCompleted}/{quotaTarget})"
            : $"[SHIFT] Night failed ❌ (Missed {ordersMissed}/{OrdersMissedFailThreshold})");

        OnShiftEnded?.Invoke(success);
        OnProgressChanged?.Invoke();
    }
}
