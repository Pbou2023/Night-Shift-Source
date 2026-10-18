public interface IInteractable
{
    string GetPrompt();                 // optional: “Pick up Cube”
    bool CanInteract(PlayerInventory inv);
    void Interact(PlayerInventory inv);
}
