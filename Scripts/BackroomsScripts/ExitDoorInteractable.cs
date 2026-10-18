using UnityEngine;
using UnityEngine.Events;

public class ExitDoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Lock Requirement")]
    [SerializeField] private CodeLock requiredLock;

    [Header("Prompt")]
    [SerializeField] private string lockedPrompt = "Exit (Locked)";
    [SerializeField] private string unlockedPrompt = "Exit";

    [Header("On Open")]
    public UnityEvent onOpen; // hook scene load / door anim / etc.

    public string GetPrompt()
    {
        if (requiredLock != null && !requiredLock.solved) return lockedPrompt;
        return unlockedPrompt;
    }

    // Important:
    // Returning false here makes your PlayerInteractor do NOTHING when locked.
    public bool CanInteract(PlayerInventory inv)
    {
        if (requiredLock == null) return true;
        return requiredLock.solved;
    }

    public void Interact(PlayerInventory inv)
    {
        // At this point, CanInteract was true -> lock solved
        onOpen?.Invoke();
    }
}
