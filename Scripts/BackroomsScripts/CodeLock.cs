using UnityEngine;
using UnityEngine.Events;

public class CodeLock : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent onSolved;

    [Header("Audio")]
    [Tooltip("If empty, an AudioSource will be created on this GameObject.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Played when the code is correct (first time only).")]
    [SerializeField] private AudioClip solvedClip;

    [Tooltip("Played when the code is wrong.")]
    [SerializeField] private AudioClip wrongClip;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1.0f);

    [Tooltip("0 = 2D UI sound, 1 = 3D positional sound.")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 0f;

    [Header("Debug")]
    public bool solved;

    void Awake()
    {
        EnsureAudioSource();
    }

    public bool TrySubmit(string code)
    {
        if (solved) return true;

        var mgr = CodePuzzleManager.Instance;
        if (mgr == null) return false;

        if (mgr.IsCorrect(code))
        {
            solved = true;
            Debug.Log("[CodeLock] SOLVED.");

            PlaySfx(solvedClip);
            onSolved?.Invoke();
            return true;
        }

        Debug.Log("[CodeLock] Wrong code.");
        PlaySfx(wrongClip);
        return false;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!sfxSource || !clip) return;

        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);

        float volMul = Random.Range(volumeRange.x, volumeRange.y);
        float finalVol = sfxVolume * volMul;

        sfxSource.PlayOneShot(clip, finalVol);
    }

    private void EnsureAudioSource()
    {
        if (sfxSource) return;

        sfxSource = GetComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = spatialBlend;
        sfxSource.volume = 1f;
    }
}
