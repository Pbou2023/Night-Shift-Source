using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeathManager : MonoBehaviour
{
    public static PlayerDeathManager Instance { get; private set; }

    [Header("UI (CanvasGroups)")]
    [SerializeField] private CanvasGroup bloodOverlayGroup;   // full-screen image (blood)
    [SerializeField] private CanvasGroup gameOverMenuGroup;   // panel with restart/quit buttons

    [Header("Timings")]
    [SerializeField] private float bloodFadeInDuration = 0.2f;
    [SerializeField] private float menuShowDelay = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip deathClip;
    [Range(0f, 1f)][SerializeField] private float deathVolume = 1f;

    [Header("Behavior")]
    [SerializeField] private bool disablePlayerControllers = true;
    [SerializeField] private bool pauseTimeAfterUI = true;
    [SerializeField] private bool unlockCursor = true;

    private bool dead;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Optional: keep across scenes if you want, otherwise remove this line.
        // DontDestroyOnLoad(gameObject);

        SetGroup(bloodOverlayGroup, 0f, false);
        SetGroup(gameOverMenuGroup, 0f, false);
    }

   

    private IEnumerator DeathSequence(bool showBlood)
    {
        // sound
        if (sfxSource && deathClip)
            sfxSource.PlayOneShot(deathClip, deathVolume);

        // blood overlay (optional)
        if (showBlood && bloodOverlayGroup)
        {
            SetGroup(bloodOverlayGroup, 0f, true);
            yield return FadeCanvasGroup(bloodOverlayGroup, 1f, bloodFadeInDuration);

            // blood should not block menu clicks
            bloodOverlayGroup.blocksRaycasts = false;
            bloodOverlayGroup.interactable = false;
        }
        else
        {
            // ensure blood is hidden if we skip it
            if (bloodOverlayGroup)
            {
                bloodOverlayGroup.alpha = 0f;
                bloodOverlayGroup.blocksRaycasts = false;
                bloodOverlayGroup.interactable = false;
            }
        }

        if (menuShowDelay > 0f)
            yield return new WaitForSecondsRealtime(menuShowDelay);

        // show menu
        if (gameOverMenuGroup)
            SetGroup(gameOverMenuGroup, 1f, true);

        // cursor + pause
        if (unlockCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (pauseTimeAfterUI)
            Time.timeScale = 0f;
    }


    private IEnumerator FadeCanvasGroup(CanvasGroup g, float target, float duration)
    {
        if (!g) yield break;

        float start = g.alpha;
        if (duration <= 0f)
        {
            g.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);
            g.alpha = Mathf.Lerp(start, target, a);
            yield return null;
        }

        g.alpha = target;
    }

    private void SetGroup(CanvasGroup g, float a, bool on)
    {
        if (!g) return;
        g.alpha = a;
        g.interactable = on;
        g.blocksRaycasts = on;
        g.gameObject.SetActive(true);
    }

    // ---- Buttons (hook these from UI buttons) ----

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    public void LoadSceneByName(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
    public void TriggerDeath(bool showBlood)
    {
        if (dead) return;
        dead = true;

        StartCoroutine(DeathSequence(showBlood));
    }


}
