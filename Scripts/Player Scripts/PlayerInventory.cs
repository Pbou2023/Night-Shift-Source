using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public struct Slot
    {
        public string itemId;
        public bool IsEmpty => string.IsNullOrEmpty(itemId);
    }

    [Header("Inventory")]
    [SerializeField] private int capacity = 3;
    [SerializeField] private Slot[] slots;

    public event Action OnChanged;

    public int Capacity => capacity;
    public Slot[] Slots => slots;

    void Awake()
    {
        if (slots == null || slots.Length != capacity)
            slots = new Slot[capacity];
    }

    public int CountOf(string itemId)
    {
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
            if (!slots[i].IsEmpty && slots[i].itemId == itemId)
                count++;
        return count;
    }

    public bool Has(string itemId, int amount = 1)
        => CountOf(itemId) >= amount;

    public bool TryAdd(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].itemId = itemId;
                Debug.Log($"[INV] + {itemId} -> slot {i}");
                OnChanged?.Invoke();
                return true;
            }
        }

        Debug.Log($"[INV] FULL! Could not add {itemId}");
        return false;
    }

    public bool TryRemove(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
        if (!Has(itemId, amount)) return false;

        int remaining = amount;

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty && slots[i].itemId == itemId)
            {
                slots[i].itemId = ""; // clear slot
                remaining--;
            }
        }

        Debug.Log($"[INV] - {amount} {itemId}");
        OnChanged?.Invoke();
        return true;
    }

    public bool HasAny()
    {
        for (int i = 0; i < slots.Length; i++)
            if (!slots[i].IsEmpty)
                return true;
        return false;
    }

    public bool TryRemoveLast(out string removedItemId)
    {
        removedItemId = "";

        // 3 -> 1 (last slot to first slot)
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            if (!slots[i].IsEmpty)
            {
                removedItemId = slots[i].itemId;
                slots[i].itemId = "";

                Debug.Log($"[INV] TRASHED {removedItemId} from slot {i}");
                OnChanged?.Invoke();
                return true;
            }
        }

        return false;
    }


}
