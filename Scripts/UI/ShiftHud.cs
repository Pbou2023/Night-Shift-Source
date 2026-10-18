using TMPro;
using UnityEngine;

public class ShiftHUD : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI hudText;
    public ShiftManager shift;

    bool hasEnded;
    bool lastSuccess;

    void Awake()
    {
        if (hudText == null) hudText = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        if (shift == null) shift = ShiftManager.Instance;

        if (shift != null)
        {
            shift.OnShiftStarted += OnShiftStarted;
            shift.OnShiftEnded += OnShiftEnded;
            shift.OnProgressChanged += Refresh;
        }

        Refresh();
    }

    void OnDisable()
    {
        if (shift != null)
        {
            shift.OnShiftStarted -= OnShiftStarted;
            shift.OnShiftEnded -= OnShiftEnded;
            shift.OnProgressChanged -= Refresh;
        }
    }

    void OnShiftStarted()
    {
        hasEnded = false;
        Refresh();
    }

    void OnShiftEnded(bool success)
    {
        hasEnded = true;
        lastSuccess = success;
        Refresh();
    }

    void Refresh()
    {
        if (hudText == null) return;
        if (shift == null) shift = ShiftManager.Instance;

        if (shift == null)
        {
            hudText.text = "ShiftHUD: No ShiftManager";
            return;
        }

        string status;
        if (shift.ShiftRunning) status = "RUNNING";
        else status = hasEnded ? (lastSuccess ? "SUCCESS ✅" : "FAILED ❌") : "IDLE";

        hudText.text =
            $"Night {shift.nightIndex}\n" +
            $"Orders Completed: {shift.OrdersCompleted}/{shift.QuotaTarget}\n" +
            $"Orders Missed: {shift.OrdersMissed}/{shift.OrdersMissedFailThreshold}";
    }
}
