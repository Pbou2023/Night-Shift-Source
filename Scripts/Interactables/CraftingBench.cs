using System;
using UnityEngine;

public class CraftingBench : MonoBehaviour, IInteractable
{
    [Serializable]
    public class Recipe
    {
        [Header("Inputs (C optional)")]
        public string inputA = "MetalScrap";
        public int amountA = 1;

        public string inputB = "Wire";
        public int amountB = 1;

        public string inputC = "";      // leave empty to disable
        public int amountC = 1;

        [Header("Output")]
        public string output = "Gadget";
        public int outputAmount = 1;
    }

    [Header("Recipes")]
    public Recipe[] recipes;

    [Header("Audio")]
    [Tooltip("AudioSource used for craft SFX. If empty, one will be created on this GameObject.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Played when at least 1 output item is successfully crafted.")]
    [SerializeField] private AudioClip[] craftClips;

    [Range(0f, 1f)]
    [SerializeField] private float craftVolume = 1f;

    [Tooltip("Random pitch per craft.")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);

    [Tooltip("Random volume multiplier per craft (final = craftVolume * randomRange).")]
    [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1.0f);

    public string GetPrompt() => "Craft";

    void Awake()
    {
        EnsureAudioSource();
    }

    public bool CanInteract(PlayerInventory inv)
    {
        if (inv == null || recipes == null) return false;

        for (int i = 0; i < recipes.Length; i++)
            if (CanCraft(inv, recipes[i]))
                return true;

        return false;
    }

    public void Interact(PlayerInventory inv)
    {
        if (inv == null || recipes == null || recipes.Length == 0) return;

        for (int i = 0; i < recipes.Length; i++)
        {
            var r = recipes[i];
            if (!CanCraft(inv, r)) continue;

            // Remove inputs (A, B required)
            if (!inv.TryRemove(r.inputA, Mathf.Max(1, r.amountA))) return;
            if (!inv.TryRemove(r.inputB, Mathf.Max(1, r.amountB))) return;

            // Remove optional C if specified
            if (!string.IsNullOrEmpty(r.inputC))
            {
                if (!inv.TryRemove(r.inputC, Mathf.Max(1, r.amountC)))
                    return;
            }

            // Add output(s)
            int outCount = Mathf.Max(1, r.outputAmount);
            int added = 0;

            for (int k = 0; k < outCount; k++)
            {
                if (inv.TryAdd(r.output)) added++;
                else break; // inventory full
            }

            if (added > 0)
                PlayCraftSfx();

            Debug.Log($"[CRAFT] Crafted {r.output} x{added}");
            return;
        }

        Debug.Log("[CRAFT] No valid recipe for current inventory.");
    }

    bool CanCraft(PlayerInventory inv, Recipe r)
    {
        if (string.IsNullOrEmpty(r.inputA) || string.IsNullOrEmpty(r.inputB) || string.IsNullOrEmpty(r.output))
            return false;

        int a = Mathf.Max(1, r.amountA);
        int b = Mathf.Max(1, r.amountB);

        if (!inv.Has(r.inputA, a)) return false;
        if (!inv.Has(r.inputB, b)) return false;

        if (!string.IsNullOrEmpty(r.inputC))
        {
            int c = Mathf.Max(1, r.amountC);
            if (!inv.Has(r.inputC, c)) return false;
        }

        return true;
    }

    void PlayCraftSfx()
    {
        if (!sfxSource) return;
        if (craftClips == null || craftClips.Length == 0) return;

        AudioClip clip = craftClips[UnityEngine.Random.Range(0, craftClips.Length)];
        if (!clip) return;

        sfxSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);

        float volMul = UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
        float finalVol = craftVolume * volMul;

        sfxSource.PlayOneShot(clip, finalVol);
    }

    void EnsureAudioSource()
    {
        if (sfxSource) return;

        sfxSource = GetComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();

        // sensible defaults for UI-ish craft sounds
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f; // set to 1f if you want 3D positional crafting audio
        sfxSource.volume = 1f;
    }
}
