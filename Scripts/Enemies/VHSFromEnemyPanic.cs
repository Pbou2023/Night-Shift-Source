using UnityEngine;
using UnityEngine.UI;

public class VhsMaterialDriver : MonoBehaviour
{
    [Header("UI Output")]
    [SerializeField] private RawImage rawImage;

    [Header("Enemy")]
    [SerializeField] private BasicChaseEnemy enemy;
    [SerializeField] private Transform player;

    [Header("Base VHS (always on)")]
    [Range(0f, 1f)] public float baseIntensity = 0.03f;

    [Header("Panic VHS (added during chase)")]
    [Range(0f, 1f)] public float panicMaxAdd = 0.18f;
    public float panicStartDistance = 14f;
    public float panicMaxDistance = 2.5f;

    [Header("Smoothing")]
    public float rampUpSpeed = 12f;
    public float fadeOutSpeed = 18f;

    [Header("Time Speed")]
    [Tooltip("How fast the VHS animates when NOT chasing (keep low).")]
    public float idleTimeSpeed = 0.15f;

    [Tooltip("How fast the VHS animates during chase (higher = more frantic).")]
    public float panicTimeSpeed = 1.0f;

    [Tooltip("How quickly time speed changes between idle/panic.")]
    public float timeSpeedLerp = 8f;

    static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    static readonly int TimeID = Shader.PropertyToID("_TimeX");

    Material runtimeMat;
    float panicCurrent;
    float timeSpeedCurrent;
    float timeAccum;

    void Awake()
    {
        if (!rawImage) rawImage = GetComponent<RawImage>();
        if (!player && Camera.main) player = Camera.main.transform;

        if (rawImage != null && rawImage.material != null)
            runtimeMat = rawImage.material = Instantiate(rawImage.material);
        else
            Debug.LogWarning("[VHS] RawImage or its Material missing.");

        timeSpeedCurrent = idleTimeSpeed;
        timeAccum = 0f;
    }

    void OnDisable()
    {
        if (runtimeMat != null)
        {
            runtimeMat.SetFloat(IntensityID, 0f);
            runtimeMat.SetFloat(TimeID, 0f);
        }
        panicCurrent = 0f;
        timeSpeedCurrent = idleTimeSpeed;
        timeAccum = 0f;
    }

    void Update()
    {
        if (runtimeMat == null) return;

        bool chasing = (enemy != null && enemy.CurrentState == BasicChaseEnemy.State.Chase);

        // Smoothly blend animation speed between idle and panic
        float targetTimeSpeed = chasing ? panicTimeSpeed : idleTimeSpeed;
        timeSpeedCurrent = Mathf.Lerp(timeSpeedCurrent, targetTimeSpeed, 1f - Mathf.Exp(-timeSpeedLerp * Time.unscaledDeltaTime));

        // Accumulate our own time (instead of raw Time.time)
        timeAccum += Time.unscaledDeltaTime * timeSpeedCurrent;
        runtimeMat.SetFloat(TimeID, timeAccum);

        // Panic intensity target
        float panicTarget = 0f;

        if (chasing && enemy != null && player != null)
        {
            float d = Vector3.Distance(enemy.transform.position, player.position);

            float t = Mathf.InverseLerp(panicMaxDistance, panicStartDistance, d);
            float panic01 = Mathf.Clamp01(1f - t);

            panicTarget = panic01 * panicMaxAdd;
            panicCurrent = Mathf.MoveTowards(panicCurrent, panicTarget, rampUpSpeed * Time.unscaledDeltaTime);
        }
        else
        {
            panicCurrent = Mathf.MoveTowards(panicCurrent, 0f, fadeOutSpeed * Time.unscaledDeltaTime);
        }

        float total = Mathf.Clamp01(baseIntensity + panicCurrent);
        runtimeMat.SetFloat(IntensityID, total);
    }
}
