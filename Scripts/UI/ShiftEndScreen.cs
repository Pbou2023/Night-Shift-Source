using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShiftEndScreen : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;               // ShiftEndPanel
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI statsText;

    [Header("Options")]
    public string restartSceneName = "";   // leave empty to reload active scene
    public bool unlockCursor = true;
    public bool pauseTime = true;

    [Header("Next Level")]
    [Tooltip("If set, loads this scene when pressing Next. If empty, loads next build index.")]
    public string nextSceneName = "";
    [Tooltip("If true, Next button only works when shift success=true.")]
    public bool nextOnlyOnSuccess = true;

    [Header("Disable while End Screen is up")]
    public MonoBehaviour[] disableOnShow; // drag PlayerLook, PlayerMotorBasic, PlayerInteractor, etc.

    bool lastSuccess;

    void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void Start()
    {
        if (ShiftManager.Instance != null)
            ShiftManager.Instance.OnShiftEnded += OnShiftEnded;
        else
            Debug.LogWarning("[ShiftEndScreen] No ShiftManager.Instance found.");
    }

    void OnDestroy()
    {
        if (ShiftManager.Instance != null)
            ShiftManager.Instance.OnShiftEnded -= OnShiftEnded;
    }

    void OnShiftEnded(bool success)
    {
        lastSuccess = success;

        Debug.Log($"[ShiftEndScreen] SHOW (success={success})");

        if (disableOnShow != null)
        {
            foreach (var b in disableOnShow)
                if (b != null) b.enabled = false;
        }

        if (panel != null)
            panel.SetActive(true);

        if (titleText != null)
            titleText.text = success ? "SHIFT COMPLETE" : "SHIFT FAILED";

        if (statsText != null && ShiftManager.Instance != null)
        {
            var s = ShiftManager.Instance;
            statsText.text =
                $"Quota: {s.OrdersCompleted}/{s.QuotaTarget}\n" +
                $"Fails: {s.OrdersFailed}\n";
        }

        if (unlockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (pauseTime)
            Time.timeScale = 0f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(restartSceneName))
            SceneManager.LoadScene(restartSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        if (nextOnlyOnSuccess && !lastSuccess)
            return;

        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("[ShiftEndScreen] No next scene in Build Settings.");
            return;
        }

        SceneManager.LoadScene(nextIndex);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }
}
