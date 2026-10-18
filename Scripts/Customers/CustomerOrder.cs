using System;
using UnityEngine;

public class CustomerOrder : MonoBehaviour, IInteractable
{
    [Serializable]
    public struct OrderLine
    {
        public string itemId;
        public int amount;
    }

    [Header("Order (Multi-line)")]
    public OrderLine[] requiredItems;

    [Header("Fallback (if requiredItems is empty)")]
    public string requestedItemId = "Cube";
    public int requestedAmount = 1;

    [Header("Rewards / Patience")]
    public int scoreReward = 10;
    public float patienceSeconds = 20f;

    [Header("Audio")]
    [Tooltip("AudioSource used for customer/order UI SFX. If empty, one will be created on this GameObject.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Played when player accepts the order.")]
    [SerializeField] private AudioClip[] acceptClips;

    [Tooltip("Played when player successfully delivers the order.")]
    [SerializeField] private AudioClip[] completeClips;

    [Tooltip("Played when order fails (timer/manager failure).")]
    [SerializeField] private AudioClip[] failClips;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1.0f);

    bool orderAccepted = false;

    void Awake()
    {
        EnsureAudioSource();
    }

    public OrderLine[] GetOrderLines()
    {
        // If user didn’t fill requiredItems, fallback to the old single-item fields
        if (requiredItems == null || requiredItems.Length == 0)
        {
            requiredItems = new OrderLine[]
            {
                new OrderLine { itemId = requestedItemId, amount = Mathf.Max(1, requestedAmount) }
            };
        }
        return requiredItems;
    }

    public void SetOrder(OrderLine[] lines, float patience, int reward)
    {
        requiredItems = lines;
        patienceSeconds = patience;
        scoreReward = reward;
    }

    public string GetPrompt()
    {
        var om = OrderManager.Instance;

        if (!orderAccepted) return "Accept Order";

        if (om == null) return "Deliver Order";

        if (!om.IsActiveCustomer(this))
            return "Deliver Order (Wrong Customer)";

        return "Deliver Order";
    }


    public bool CanInteract(PlayerInventory inv)
    {

        var om = OrderManager.Instance;
        if (om == null) return false;

        if (!orderAccepted)
            return !om.HasActiveOrder;

        if (!om.IsActiveCustomer(this)) return false;

        // Must have ALL required items
        var lines = GetOrderLines();
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i].itemId)) return false;
            if (!inv.Has(lines[i].itemId, Mathf.Max(1, lines[i].amount)))
                return false;
        }
        return true;
    }

    public void Interact(PlayerInventory inv)
    {
        var om = OrderManager.Instance;
        if (om == null) return;

        // Accept
        if (!orderAccepted)
        {
            if (om.TryAcceptOrder(this, patienceSeconds))
            {
                orderAccepted = true;
                PlaySfx(acceptClips);
                Debug.Log("[CUSTOMER] Order accepted.");
            }
            return;
        }

        // Complete
        if (!om.IsActiveCustomer(this)) return;

        var lines = GetOrderLines();

        // Remove all items (we pre-checked in CanInteract)
        for (int i = 0; i < lines.Length; i++)
        {
            if (!inv.TryRemove(lines[i].itemId, Mathf.Max(1, lines[i].amount)))
                return;
        }

        // SFX before leaving/destroying
        PlaySfx(completeClips);

        ScoreManager.Add(scoreReward);
        ShiftManager.Instance?.RegisterOrderCompleted();

        om.ClearOrder();

        var mover = GetComponent<CustomerMover>();
        if (mover != null) mover.Leave();
        else Destroy(gameObject);
    }

    public void OnOrderFailed()
    {
        // SFX before leaving/destroying
        PlaySfx(failClips);

        ShiftManager.Instance?.RegisterOrderFailed();

        var mover = GetComponent<CustomerMover>();
        if (mover != null) mover.Leave();
        else Destroy(gameObject);
    }

    void PlaySfx(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (!clip) return;

        float pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        float volMul = UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
        float finalVol = sfxVolume * volMul;

        // If we have a source, use it (better control, can be 2D/3D as you set).
        if (sfxSource != null)
        {
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, finalVol);
            return;
        }

        // Fallback: plays even if this object gets destroyed right after.
        // Note: PlayClipAtPoint does NOT support pitch, so we sacrifice pitch in fallback.
        AudioSource.PlayClipAtPoint(clip, transform.position, finalVol);
    }

    void EnsureAudioSource()
    {
        if (sfxSource) return;

        sfxSource = GetComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f; // 2D by default (set to 1f if you want it positional)
        sfxSource.volume = 1f;
    }
}
