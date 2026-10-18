using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class FlashlightController : MonoBehaviour
{
    [Header("Lights")]
    [SerializeField] private Light spot;
    [SerializeField] private Light fill;

    [Header("Toggle")]
    [SerializeField] private bool startOn = true;
    private bool isOn;

    [Header("Battery")]
    [SerializeField] private float maxChargeSeconds = 90f;
    [SerializeField] private float drainPerSecond = 1f;
    [SerializeField] private bool autoTurnOffWhenEmpty = true;

    [Range(0f, 1f)]
    [SerializeField] private float lowBatteryThreshold = 0.15f;

    [Header("Low Battery Flicker")]
    [SerializeField] private bool flickerWhenLow = true;
    [SerializeField] private float flickerSpeed = 18f;
    [SerializeField] private float flickerChance = 0.35f;

    [Header("Handheld Wobble")]
    [SerializeField] private bool wobbleEnabled = true;
    [SerializeField] private float wobbleRotAmount = 0.35f;
    [SerializeField] private float wobblePosAmount = 0.01f;
    [SerializeField] private float wobbleSpeed = 1.6f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip toggleOnClip;
    [SerializeField] private AudioClip toggleOffClip;
    [SerializeField] private AudioClip batteryDeadClip;

    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1.0f);
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [SerializeField] private bool playSfxOnStart = false;

    [Header("Lose Condition (Option A)")]
    [SerializeField] private bool loseWhenBatteryEmpty = true;
    [SerializeField] private float deathDelaySeconds = 2.5f;

    [Tooltip("Use for UI/SFX/disable controls etc. DO NOT load scenes from here.")]
    public UnityEvent onBatteryDepleted;

    [Header("Game Over Fade")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private string gameOverScene = "GameOver";

    [Header("Game Over UI")]
    [SerializeField] private PlayerDeathManager deathManager;

    private bool batteryDepletedFired;
    private Coroutine gameOverRoutine;

    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;

    private float currentChargeSeconds;

    public float CurrentChargeSeconds => currentChargeSeconds;
    public float MaxChargeSeconds => maxChargeSeconds;
    public float Charge01 => (maxChargeSeconds <= 0f) ? 0f : Mathf.Clamp01(currentChargeSeconds / maxChargeSeconds);
    public bool IsOn => isOn;

    void Awake()
    {
        baseLocalPos = transform.localPosition;
        baseLocalRot = transform.localRotation;

        EnsureAudioSource();

        // If not assigned, try find one (including inactive)
        if (!screenFader)
            screenFader = FindObjectOfType<ScreenFader>(true);

        currentChargeSeconds = Mathf.Max(0f, maxChargeSeconds);
        isOn = startOn;

        // If starting ON but empty, force OFF
        if (isOn && currentChargeSeconds <= 0f)
            isOn = false;

        if (!deathManager)
            deathManager = FindObjectOfType<PlayerDeathManager>(true);

        ApplyState(playSfxOnStart, flickerOff: false);
    }

    void Update()
    {
        // If we're already dying, don't let gameplay keep toggling stuff
        if (!batteryDepletedFired)
        {
            // Toggle with F
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (!isOn)
                {
                    if (currentChargeSeconds > 0f)
                    {
                        isOn = true;
                        ApplyState(playSfx: true, flickerOff: false);
                    }
                    else
                    {
                        PlayOneShot(batteryDeadClip);
                    }
                }
                else
                {
                    isOn = false;
                    ApplyState(playSfx: true, flickerOff: false);
                }
            }
        }

        // Drain battery while ON
        if (isOn && drainPerSecond > 0f)
        {
            currentChargeSeconds -= drainPerSecond * Time.deltaTime;

            if (currentChargeSeconds <= 0f)
            {
                currentChargeSeconds = 0f;

                if (autoTurnOffWhenEmpty)
                {
                    isOn = false;
                    ApplyState(playSfx: false, flickerOff: false);
                    PlayOneShot(batteryDeadClip);
                }

                if (loseWhenBatteryEmpty)
                {
                    TriggerGameOverOnce();
                }
            }
        }

        // Compute flicker state this frame (doesn't permanently break light state)
        bool flickerOffNow = false;

        if (isOn && flickerWhenLow && currentChargeSeconds > 0f && Charge01 <= lowBatteryThreshold)
        {
            float t = Time.time * flickerSpeed;
            float noise = Mathf.PerlinNoise(t, 0.123f);
            flickerOffNow = noise < flickerChance;
        }

        // Apply light enabled state every frame (base state + flicker)
        ApplyLightEnabledState(flickerOffNow);
    }

    private void TriggerGameOverOnce()
    {
        if (batteryDepletedFired) return;
        batteryDepletedFired = true;

        // fire event once (UI/SFX hooks)
        onBatteryDepleted?.Invoke();

        // start one clean sequence
        if (gameOverRoutine != null) StopCoroutine(gameOverRoutine);
        gameOverRoutine = StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        if (deathDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(deathDelaySeconds);

        // Use same UI, but skip blood overlay
        if (!deathManager) deathManager = PlayerDeathManager.Instance;
        if (deathManager) deathManager.TriggerDeath(false);
    }


    void LateUpdate()
    {
        if (!wobbleEnabled || !isOn)
        {
            transform.localPosition = baseLocalPos;
            transform.localRotation = baseLocalRot;
            return;
        }

        float t = Time.time * wobbleSpeed;

        float nx = Mathf.PerlinNoise(t, 0.1f) - 0.5f;
        float ny = Mathf.PerlinNoise(0.2f, t) - 0.5f;
        float nz = Mathf.PerlinNoise(t * 0.7f, t * 0.3f) - 0.5f;

        var wobbleRot = Quaternion.Euler(
            ny * wobbleRotAmount,
            nx * wobbleRotAmount,
            nz * (wobbleRotAmount * 0.35f)
        );

        var wobblePos = new Vector3(
            nx * wobblePosAmount,
            ny * wobblePosAmount,
            0f
        );

        transform.localRotation = baseLocalRot * wobbleRot;
        transform.localPosition = baseLocalPos + wobblePos;
    }

    private void ApplyState(bool playSfx, bool flickerOff)
    {
        ApplyLightEnabledState(flickerOff);

        if (playSfx)
            PlayToggleSfx(isOn);
    }

    private void ApplyLightEnabledState(bool flickerOff)
    {
        bool enabled = isOn && !flickerOff;

        if (spot) spot.enabled = enabled;
        if (fill) fill.enabled = enabled;
    }

    private void PlayToggleSfx(bool turningOn)
    {
        if (!sfxSource) return;

        AudioClip clip = turningOn ? toggleOnClip : toggleOffClip;
        if (!clip) return;

        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        float volMul = Random.Range(volumeRange.x, volumeRange.y);
        sfxSource.PlayOneShot(clip, sfxVolume * volMul);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (!sfxSource || !clip) return;
        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        float volMul = Random.Range(volumeRange.x, volumeRange.y);
        sfxSource.PlayOneShot(clip, sfxVolume * volMul);
    }

    private void EnsureAudioSource()
    {
        if (sfxSource) return;

        sfxSource = GetComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 1f;
    }

    // -------- Battery Pickup API --------

    /// <summary>Adds charge (seconds). Returns true if anything was added.</summary>
    public bool AddChargeSeconds(float amount)
    {
        if (amount <= 0f) return false;
        if (maxChargeSeconds <= 0f) return false;

        float before = currentChargeSeconds;
        currentChargeSeconds = Mathf.Clamp(currentChargeSeconds + amount, 0f, maxChargeSeconds);
        return currentChargeSeconds > before;
    }

    /// <summary>Refills battery to full. Returns true if anything changed.</summary>
    public bool RefillToFull()
    {
        if (maxChargeSeconds <= 0f) return false;
        if (currentChargeSeconds >= maxChargeSeconds) return false;

        currentChargeSeconds = maxChargeSeconds;
        return true;
    }
}
