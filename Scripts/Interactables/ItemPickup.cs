using UnityEngine;

public class ItemPickup : MonoBehaviour, IInteractable
{
    public string itemId = "Cube";

    public string GetPrompt() => $"Pick up {itemId}";
    public bool CanInteract(PlayerInventory inv) => true;

    public void Interact(PlayerInventory inv)
    {
        if (inv.TryAdd(itemId))
            Destroy(gameObject);
        else
            Debug.Log("[Pickup] Inventory full.");
    }
}
