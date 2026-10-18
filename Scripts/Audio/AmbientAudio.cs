using System.Collections;
using UnityEngine;

public class AmbientAudio : MonoBehaviour
{
    public enum SurfaceType { Office, Backrooms }

    [Header("Surface (auto set by FootstepZone)")]
    public SurfaceType currentSurface = SurfaceType.Office;

    [Header("Music (long)")]
    public AudioClip[] officeLoops;
    public AudioClip[] backroomsLoops;

    [Header("One-shots (short)")]
    public AudioClip[] officeOneShots;
    public AudioClip[] backroomsOneShots;

    [Header("Music Volumes")]
    [Range(0f, 1f)] public float officeMusicVolume = 0.35f;
    [Range(0f, 1f)] public float backroomsMusicVolume = 0.35f;

    [Header("One-shot Volumes")]
    [Range(0f, 1f)] public float oneShotVolume = 0.55f;
    [Tooltip("How fast one-shot layers fade when swapping zones.")]
    public float oneShotLayerFadeDuration = 0.35f;

    [Header("Crossfade (music)")]
    public float fadeDuration = 1.0f;

    [Header("Random Start (only once at boot)")]
    public bool randomStartTime = true;
    public float avoidLastSeconds = 8f;

    [Header("One-shot Timing")]
    public Vector2 oneShotIntervalRange = new Vector2(6f, 14f);
    public Vector2 oneShotPitchRange = new Vector2(0.95f, 1.05f);

    AudioSource officeMusicSrc;
    AudioSource backroomsMusicSrc;

    AudioSource officeOneShotSrc;
    AudioSource backroomsOneShotSrc;

    float oneShotTimer;
    Coroutine musicFadeCo;
    Coroutine oneShotFadeCo;

    void Awake()
    {
        officeMusicSrc = gameObject.AddComponent<AudioSource>();
        backroomsMusicSrc = gameObject.AddComponent<AudioSource>();

        officeOneShotSrc = gameObject.AddComponent<AudioSource>();
        backroomsOneShotSrc = gameObject.AddComponent<AudioSource>();

        SetupMusicSource(officeMusicSrc);
        SetupMusicSource(backroomsMusicSrc);

        SetupOneShotSource(officeOneShotSrc);
        SetupOneShotSource(backroomsOneShotSrc);

        ResetOneShotTimer();
    }

    void Start()
    {
        // Start BOTH music tracks once, keep running
        StartZoneMusic(officeMusicSrc, officeLoops);
        StartZoneMusic(backroomsMusicSrc, backroomsLoops);

        ApplyMusicVolumesInstant(currentSurface);
        ApplyOneShotLayerVolumesInstant(currentSurface);
    }

    void Update()
    {
        oneShotTimer -= Time.deltaTime;
        if (oneShotTimer <= 0f)
        {
            PlayRandomOneShot(currentSurface);
            ResetOneShotTimer();
        }
    }

    void SetupMusicSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f; // 2D
        s.volume = 0f;
    }

    void SetupOneShotSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f; // 2D
        s.volume = 1f;       // layer volume (we fade this)
    }

    void StartZoneMusic(AudioSource src, AudioClip[] clips)
    {
        if (src == null) return;
        if (clips == null || clips.Length == 0) return;

        var clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        src.clip = clip;

        if (randomStartTime)
        {
            float maxStart = Mathf.Max(0f, clip.length - avoidLastSeconds);
            src.time = Random.Range(0f, maxStart);
        }

        src.Play();
    }

    void ResetOneShotTimer()
    {
        oneShotTimer = Random.Range(oneShotIntervalRange.x, oneShotIntervalRange.y);
    }

    void PlayRandomOneShot(SurfaceType surface)
    {
        AudioClip[] ones = (surface == SurfaceType.Backrooms) ? backroomsOneShots : officeOneShots;
        AudioSource src = (surface == SurfaceType.Backrooms) ? backroomsOneShotSrc : officeOneShotSrc;

        if (ones == null || ones.Length == 0 || src == null) return;

        var clip = ones[Random.Range(0, ones.Length)];
        if (clip == null) return;

        src.pitch = Random.Range(oneShotPitchRange.x, oneShotPitchRange.y);
        src.PlayOneShot(clip, oneShotVolume);
    }

    void ApplyMusicVolumesInstant(SurfaceType s)
    {
        if (officeMusicSrc) officeMusicSrc.volume = (s == SurfaceType.Office) ? officeMusicVolume : 0f;
        if (backroomsMusicSrc) backroomsMusicSrc.volume = (s == SurfaceType.Backrooms) ? backroomsMusicVolume : 0f;
    }

    void ApplyOneShotLayerVolumesInstant(SurfaceType s)
    {
        // This is the important bit: backrooms one-shots fade out when in office (and vice versa).
        if (officeOneShotSrc) officeOneShotSrc.volume = (s == SurfaceType.Office) ? 1f : 0f;
        if (backroomsOneShotSrc) backroomsOneShotSrc.volume = (s == SurfaceType.Backrooms) ? 1f : 0f;
    }

    public void SetSurface(SurfaceType next)
    {
        if (next == currentSurface) return;

        currentSurface = next;

        // Music crossfade (volumes only, tracks keep playing)
        if (musicFadeCo != null) StopCoroutine(musicFadeCo);
        musicFadeCo = StartCoroutine(CrossfadeMusicTo(next));

        // One-shot layer fade (this kills lingering backrooms one-shots when entering office)
        if (oneShotFadeCo != null) StopCoroutine(oneShotFadeCo);
        oneShotFadeCo = StartCoroutine(FadeOneShotLayersTo(next));
    }

    IEnumerator CrossfadeMusicTo(SurfaceType s)
    {
        float t = 0f;

        float officeStart = officeMusicSrc ? officeMusicSrc.volume : 0f;
        float backStart = backroomsMusicSrc ? backroomsMusicSrc.volume : 0f;

        float officeTarget = (s == SurfaceType.Office) ? officeMusicVolume : 0f;
        float backTarget = (s == SurfaceType.Backrooms) ? backroomsMusicVolume : 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = fadeDuration <= 0.0001f ? 1f : (t / fadeDuration);

            if (officeMusicSrc) officeMusicSrc.volume = Mathf.Lerp(officeStart, officeTarget, a);
            if (backroomsMusicSrc) backroomsMusicSrc.volume = Mathf.Lerp(backStart, backTarget, a);

            yield return null;
        }

        if (officeMusicSrc) officeMusicSrc.volume = officeTarget;
        if (backroomsMusicSrc) backroomsMusicSrc.volume = backTarget;
    }

    IEnumerator FadeOneShotLayersTo(SurfaceType s)
    {
        float t = 0f;

        float officeStart = officeOneShotSrc ? officeOneShotSrc.volume : 0f;
        float backStart = backroomsOneShotSrc ? backroomsOneShotSrc.volume : 0f;

        float officeTarget = (s == SurfaceType.Office) ? 1f : 0f;
        float backTarget = (s == SurfaceType.Backrooms) ? 1f : 0f;

        float dur = Mathf.Max(0.01f, oneShotLayerFadeDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float a = t / dur;

            if (officeOneShotSrc) officeOneShotSrc.volume = Mathf.Lerp(officeStart, officeTarget, a);
            if (backroomsOneShotSrc) backroomsOneShotSrc.volume = Mathf.Lerp(backStart, backTarget, a);

            yield return null;
        }

        if (officeOneShotSrc) officeOneShotSrc.volume = officeTarget;
        if (backroomsOneShotSrc) backroomsOneShotSrc.volume = backTarget;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<FootstepZone>(out var zone))
        {
            SetSurface(zone.surface == FootstepZone.SurfaceType.Backrooms
                ? SurfaceType.Backrooms
                : SurfaceType.Office);
        }
    }
}
