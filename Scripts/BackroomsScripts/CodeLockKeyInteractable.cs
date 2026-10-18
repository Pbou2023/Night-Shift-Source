using UnityEngine;

public class CodeLockKeypadInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private CodeLock codeLock;
    [SerializeField] private CodeLockPopupUI popupUI;

    [Header("Prompt")]
    [SerializeField] private string promptLocked = "Enter Code";
    [SerializeField] private string promptSolved = "Unlocked";

    public string GetPrompt()
    {
        if (codeLock != null && codeLock.solved) return promptSolved;
        return promptLocked;
    }

    // You can still interact even if solved (up to you).
    // If you want it disabled after solved, return !codeLock.solved here.
    public bool CanInteract(PlayerInventory inv) => inv != null;

    public void Interact(PlayerInventory inv)
    {
        if (!popupUI)
        {
            Debug.LogWarning("[Keypad] No popupUI assigned.");
            return;
        }

        popupUI.Open(codeLock);
    }
}
