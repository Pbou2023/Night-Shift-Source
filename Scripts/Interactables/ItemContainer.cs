using UnityEngine;

public class ItemContainer : MonoBehaviour, IInteractable
{
    public string itemId = "Cube";
    [Min(1)] public int amountPerInteract = 1;
    public float interactCooldown = 0.1f;

    float nextUseTime;

    [Header("Audio")]
    [Tooltip("AudioSource used for container pickup SFX. If empty, one will be created on this GameObject.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Played when at least 1 item is successfully taken.")]
    [SerializeField] private AudioClip[] pickupClips;

    [Tooltip("Optional: played when inventory is full (given == 0).")]
    [SerializeField] private AudioClip[] fullInventoryClips;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1.0f);

    void Awake()
    {
        EnsureAudioSource();
    }

    public string GetPrompt() => $"Take {itemId}";
    public bool CanInteract(PlayerInventory inv) => inv != null; // we’ll handle “full” in Interact

    public void Interact(PlayerInventory inv)
    {
        if (inv == null) return;
        if (Time.time < nextUseTime) return;
        nextUseTime = Time.time + interactCooldown;

        int given = 0;
        for (int i = 0; i < amountPerInteract; i++)
        {
            if (inv.TryAdd(itemId)) given++;
            else break; // inventory full
        }

        if (given == 0)
        {
            PlaySfx(fullInventoryClips);
            Debug.Log("[Container] Inventory full.");
        }
        else
        {
            PlaySfx(pickupClips);
            Debug.Log($"[Container] Took {given}x {itemId}");
        }
    }

    void PlaySfx(AudioClip[] clips)
    {
        if (!sfxSource) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (!clip) return;

        sfxSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);

        float volMul = UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
        float finalVol = sfxVolume * volMul;

        sfxSource.PlayOneShot(clip, finalVol);
    }

    void EnsureAudioSource()
    {
        if (sfxSource) return;

        sfxSource = GetComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f; // 2D by default; set to 1 for positional
        sfxSource.volume = 1f;
    }
}
