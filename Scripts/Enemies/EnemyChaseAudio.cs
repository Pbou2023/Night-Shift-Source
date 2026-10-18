using System.Collections;
using UnityEngine;

public class EnemyChaseAudio : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BasicChaseEnemy enemy;

    [Header("Clips")]
    [SerializeField] private AudioClip chaseStinger;
    [SerializeField] private AudioClip chaseLoop;

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [SerializeField] private float fadeIn = 0.25f;
    [SerializeField] private float fadeOut = 0.8f;
    [SerializeField] private float retriggerCooldown = 10f;

    [Header("Intensity From Panic (optional)")]
    [SerializeField] private bool driveLoopFromPanic = true;
    [SerializeField] private float loopMinVol = 0.35f;
    [SerializeField] private float loopMaxVol = 1.0f;
    [SerializeField] private float loopSmooth = 10f;

    AudioSource stingerSource;
    AudioSource loopSource;

    float targetLoopVol;
    float currentLoopVol;
    float lastStopTime = -999f;

    Coroutine fadeRoutine;

    void Awake()
    {
        // two sources so stinger doesn't cut the loop
        stingerSource = gameObject.AddComponent<AudioSource>();
        loopSource = gameObject.AddComponent<AudioSource>();

        stingerSource.playOnAwake = false;
        loopSource.playOnAwake = false;

        stingerSource.loop = false;
        loopSource.loop = true;

        stingerSource.spatialBlend = 0f;
        loopSource.spatialBlend = 0f;

        loopSource.volume = 0f;
        currentLoopVol = 0f;
        targetLoopVol = 0f;
    }

    void OnEnable()
    {
        if (!enemy) return;
        enemy.OnAggroStarted += HandleChaseStart; // reuse your start logic
        enemy.OnAggroEnded += HandleChaseEnd;     // reuse your end logic
        enemy.OnPanicChanged += HandlePanic;
    }

    void OnDisable()
    {
        if (!enemy) return;
        enemy.OnAggroStarted -= HandleChaseStart;
        enemy.OnAggroEnded -= HandleChaseEnd;
        enemy.OnPanicChanged -= HandlePanic;
    }


    void Update()
    {
        // smooth loop volume changes
        currentLoopVol = Mathf.Lerp(currentLoopVol, targetLoopVol, 1f - Mathf.Exp(-loopSmooth * Time.unscaledDeltaTime));
        loopSource.volume = currentLoopVol;
    }

    void HandleChaseStart()
    {
        // prevent spam if it toggles rapidly
        if (Time.unscaledTime - lastStopTime < retriggerCooldown)
        {
            StartLoopIfNeeded();
            targetLoopVol = (driveLoopFromPanic ? loopMinVol : 1f) * masterVolume;
            return;
        }

        if (chaseStinger)
            stingerSource.PlayOneShot(chaseStinger, masterVolume);

        StartLoopIfNeeded();

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo((driveLoopFromPanic ? loopMinVol : 1f) * masterVolume, fadeIn));
    }

    void HandleChaseEnd()
    {
        lastStopTime = Time.unscaledTime;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    void HandlePanic(float p)
    {
        if (!driveLoopFromPanic) return;
        if (!loopSource.isPlaying) return;

        float v = Mathf.Lerp(loopMinVol, loopMaxVol, Mathf.Clamp01(p)) * masterVolume;
        targetLoopVol = v;
    }

    void StartLoopIfNeeded()
    {
        if (!chaseLoop) return;

        loopSource.clip = chaseLoop;
        if (!loopSource.isPlaying)
        {
            loopSource.volume = 0f;
            currentLoopVol = 0f;
            targetLoopVol = 0f;
            loopSource.Play();
        }
    }

    IEnumerator FadeTo(float to, float duration)
    {
        float from = currentLoopVol;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = duration <= 0.001f ? 1f : (t / duration);
            currentLoopVol = Mathf.Lerp(from, to, a);
            targetLoopVol = currentLoopVol;
            yield return null;
        }

        currentLoopVol = to;
        targetLoopVol = to;
    }

    IEnumerator FadeOutAndStop()
    {
        float from = currentLoopVol;
        float t = 0f;

        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float a = fadeOut <= 0.001f ? 1f : (t / fadeOut);
            currentLoopVol = Mathf.Lerp(from, 0f, a);
            targetLoopVol = currentLoopVol;
            yield return null;
        }

        currentLoopVol = 0f;
        targetLoopVol = 0f;

        if (loopSource.isPlaying)
            loopSource.Stop();
    }
}
