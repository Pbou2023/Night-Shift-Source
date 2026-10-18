using TMPro;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;
    public TextMeshProUGUI[] slotTexts; // size 3

    void OnEnable()
    {
        if (inventory != null)
            inventory.OnChanged += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.OnChanged -= Refresh;
    }

    void Refresh()
    {
        if (inventory == null || slotTexts == null) return;

        var slots = inventory.Slots;

        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null) continue;

            if (i >= slots.Length || slots[i].IsEmpty)
                slotTexts[i].text = $"[{i + 1}] -";
            else
                slotTexts[i].text = $"[{i + 1}] {slots[i].itemId}";
        }
    }
}
