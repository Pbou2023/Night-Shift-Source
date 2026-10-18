using System.Collections;
using UnityEngine;

public class AmbienceManager : MonoBehaviour
{
    public static AmbienceManager Instance { get; private set; }

    public enum AmbienceType { Office, Backrooms }

    [Header("Clips")]
    [SerializeField] private AudioClip officeLoop;
    [SerializeField] private AudioClip backroomsLoop;

    [Header("Mix")]
    [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;
    [SerializeField] private float fadeSeconds = 1.0f;

    AudioSource a;
    AudioSource b;
    AudioSource current;

    Coroutine fadeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // two sources so we can crossfade cleanly
        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        Setup(a);
        Setup(b);

        current = a;
    }

    void Setup(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f; // ambience = 2D
        s.volume = 0f;
        s.dopplerLevel = 0f;
    }

    public void SetAmbience(AmbienceType type)
    {
        AudioClip target = (type == AmbienceType.Office) ? officeLoop : backroomsLoop;
        if (!target) return;

        // Already playing this clip?
        if (current.isPlaying && current.clip == target) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossFadeTo(target));
    }

    IEnumerator CrossFadeTo(AudioClip nextClip)
    {
        AudioSource from = current;
        AudioSource to = (current == a) ? b : a;

        to.clip = nextClip;
        to.volume = 0f;
        to.Play();

        float t = 0f;
        float fromStart = from.volume;
        float toTarget = masterVolume;

        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeSeconds <= 0.001f ? 1f : (t / fadeSeconds);

            from.volume = Mathf.Lerp(fromStart, 0f, k);
            to.volume = Mathf.Lerp(0f, toTarget, k);

            yield return null;
        }

        from.volume = 0f;
        from.Stop();

        to.volume = masterVolume;
        current = to;
        fadeRoutine = null;
    }
}
