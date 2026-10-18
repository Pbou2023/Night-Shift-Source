using UnityEngine;

public class BatteryPickup : MonoBehaviour, IInteractable
{
    [Header("Refill")]
    [Tooltip("How many seconds of charge to add. (Max is clamped by flashlight)")]
    public float addChargeSeconds = 35f;

    [Tooltip("If true, ignores addChargeSeconds and refills to full.")]
    public bool refillToFull = false;

    public float interactCooldown = 0.1f;
    private float nextUseTime;

    [Header("Prompt")]
    public string prompt = "Take Battery";

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip[] pickupClips;
    [SerializeField] private AudioClip[] fullBatteryClips;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1.0f);

    void Awake() => EnsureAudioSource();

    public string GetPrompt() => prompt;
    public bool CanInteract(PlayerInventory inv) => inv != null;

    public void Interact(PlayerInventory inv)
    {
        if (inv == null) return;
        if (Time.time < nextUseTime) return;
        nextUseTime = Time.time + interactCooldown;

        // PlayerInventory should be on the player root, so this is a clean way to find the flashlight.
        FlashlightController fl = inv.GetComponentInChildren<FlashlightController>(true);
        if (fl == null)
        {
            Debug.LogWarning("[BatteryPickup] No FlashlightController found on player.");
            return;
        }

        bool changed = refillToFull ? fl.RefillToFull() : fl.AddChargeSeconds(addChargeSeconds);

        if (!changed)
        {
            PlaySfx(fullBatteryClips);
            Debug.Log("[BatteryPickup] Battery already full.");
            return;
        }

        PlaySfx(pickupClips);
        Destroy(gameObject);
    }

    void PlaySfx(AudioClip[] clips)
    {
        if (!sfxSource) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (!clip) return;

        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        float volMul = Random.Range(volumeRange.x, volumeRange.y);
        sfxSource.PlayOneShot(clip, sfxVolume * volMul);
    }

    void EnsureAudioSource()
    {
        if (sfxSource) return;

        sfxSource = GetComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 1f;
    }
}
