using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PanicPostFXDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BasicChaseEnemy enemy;
    [SerializeField] private Volume volume;

    [Header("Smoothing")]
    [SerializeField] private float smooth = 12f;

    [Header("Optional FOV Pulse")]
    [SerializeField] private bool enableFovPulse = false; // turn OFF by default
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float fovIncrease = 6f;
    [SerializeField] private float pulseSpeed = 6f;
    [SerializeField] private float pulseAmount = 1.2f;

    [Header("Effect Strength (at panic = 1)")]
    [SerializeField] private float vignetteMax = 0.55f;
    [SerializeField] private float chromaticMax = 0.65f;
    [SerializeField] private float grainMax = 0.55f;
    [SerializeField] private float lensDistortMax = -0.35f;

    float targetPanic;
    float panic;
    float baseFov;

    Vignette vignette;
    ChromaticAberration chroma;
    FilmGrain grain;
    LensDistortion lens;

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (targetCamera) baseFov = targetCamera.fieldOfView;

        if (volume && volume.profile)
        {
            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out chroma);
            volume.profile.TryGet(out grain);
            volume.profile.TryGet(out lens);
        }

        if (volume) volume.weight = 0f;
    }

    void OnEnable()
    {
        if (!enemy) return;
        enemy.OnPanicChanged += OnPanic;
        enemy.OnChaseEnded += HandleChaseEnd;
    }

    void OnDisable()
    {
        if (!enemy) return;
        enemy.OnPanicChanged -= OnPanic;
        enemy.OnChaseEnded -= HandleChaseEnd;
    }

    void OnPanic(float p) => targetPanic = Mathf.Clamp01(p);

    void HandleChaseEnd()
    {
        // ensures we go back to 0 even if last panic event doesn't fire for some reason
        targetPanic = 0f;
    }

    void Update()
    {
        panic = Mathf.Lerp(panic, targetPanic, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));

        if (volume) volume.weight = panic;

        // Drive specific overrides if they exist in the profile
        if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, vignetteMax, panic);
        if (chroma != null) chroma.intensity.value = Mathf.Lerp(0f, chromaticMax, panic);
        if (grain != null) grain.intensity.value = Mathf.Lerp(0f, grainMax, panic);
        if (lens != null) lens.intensity.value = Mathf.Lerp(0f, lensDistortMax, panic);

        if (targetCamera && enableFovPulse)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            targetCamera.fieldOfView = baseFov + (panic * fovIncrease) + (panic * pulse);
        }
        else if (targetCamera && !enableFovPulse)
        {
            targetCamera.fieldOfView = baseFov;
        }
    }
}
