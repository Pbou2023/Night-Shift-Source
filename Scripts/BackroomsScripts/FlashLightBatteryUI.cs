using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FlashlightBatteryUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("If empty, will try to find FlashlightController in the scene.")]
    public FlashlightController flashlight;

    [Header("UI")]
    public Image batteryIcon;

    [Tooltip("Optional: if this Image type is Filled, we set fillAmount to match charge.")]
    public Image fillImage;

    [Tooltip("TMP text ONLY (will not accept RectTransform).")]
    public TMP_Text percentText;

    [Header("Display")]
    public string percentFormat = "{0}%";
    public bool wholeNumbers = true;

    [Min(0f)] public float refreshInterval = 0.05f;
    [Min(0f)] public float fillSmoothSpeed = 8f;

    float nextRefreshTime;
    float smoothedFill = 1f;

    void Awake()
    {
        AutoFindIfNeeded();
        if (percentText == null) percentText = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        AutoFindIfNeeded();
        if (percentText == null) percentText = GetComponentInChildren<TMP_Text>(true);
        ForceRefresh();
    }

    void Update()
    {
        if (refreshInterval > 0f && Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    void AutoFindIfNeeded()
    {
        if (flashlight != null) return;

#if UNITY_2023_1_OR_NEWER
        flashlight = FindFirstObjectByType<FlashlightController>();
#else
        flashlight = FindObjectOfType<FlashlightController>(true);
#endif
    }

    public void ForceRefresh()
    {
        nextRefreshTime = 0f;
        Refresh();
    }

    void Refresh()
    {
        if (flashlight == null)
        {
            if (percentText) percentText.text = "--%";
            if (fillImage) fillImage.fillAmount = 0f;
            return;
        }

        float charge01 = Mathf.Clamp01(flashlight.Charge01);

        int pct = wholeNumbers
            ? Mathf.RoundToInt(charge01 * 100f)
            : (int)(charge01 * 100f);

        if (percentText)
            percentText.text = string.Format(percentFormat, pct);

        if (fillImage)
        {
            if (fillSmoothSpeed <= 0f)
            {
                fillImage.fillAmount = charge01;
            }
            else
            {
                smoothedFill = Mathf.Lerp(smoothedFill, charge01, Time.unscaledDeltaTime * fillSmoothSpeed);
                fillImage.fillAmount = smoothedFill;
            }
        }
    }
}
