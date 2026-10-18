using UnityEngine;

public class TrashCanInteractable : MonoBehaviour, IInteractable
{
    public string GetPrompt() => "Trash last item";

    public bool CanInteract(PlayerInventory inv)
        => inv != null && inv.HasAny();

    public void Interact(PlayerInventory inv)
    {
        if (inv == null) return;

        if (inv.TryRemoveLast(out string removed))
            Debug.Log($"[TRASH] Deleted: {removed}");
        else
            Debug.Log("[TRASH] Inventory empty.");
    }
}
