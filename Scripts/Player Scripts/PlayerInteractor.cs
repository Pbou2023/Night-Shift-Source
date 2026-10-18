using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset actionsAsset;
    public string actionMapName = "Player";
    public string interactActionName = "Interact";

    [Header("Raycast")]
    public Transform rayOrigin;              // Camera transform
    public float interactRange = 2.5f;
    public LayerMask interactMask = ~0;

    [Header("Debug")]
    public bool debugLogs = true;
    public bool debugRay = true;

    InputActionMap map;
    InputAction interactAction;
    PlayerInventory inv;

    void Awake()
    {
        inv = GetComponent<PlayerInventory>();

        map = actionsAsset.FindActionMap(actionMapName, true);
        interactAction = map.FindAction(interactActionName, true);
    }

    void OnEnable() => map.Enable();
    void OnDisable() => map.Disable();

    void Update()
    {
        if (debugRay && rayOrigin != null)
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * interactRange, Color.yellow);

        if (interactAction.WasPressedThisFrame())
            TryInteract();
    }

    void TryInteract()
    {
        if (rayOrigin == null)
        {
            if (debugLogs) Debug.LogWarning("[Interactor] No rayOrigin assigned.");
            return;
        }

        if (!Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit,
            interactRange, interactMask, QueryTriggerInteraction.Collide))
        {
            if (debugLogs) Debug.Log("[Interactor] Interact pressed, but hit NOTHING.");
            return;
        }

        // Find ANY MonoBehaviour on this object or its parents that implements IInteractable
        var mbs = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
        foreach (var mb in mbs)
        {
            if (mb is IInteractable ia)
            {
                if (debugLogs) Debug.Log($"[Interactor] Hit {hit.collider.name} -> using {mb.GetType().Name}");

                if (!ia.CanInteract(inv))
                {
                    if (debugLogs) Debug.Log($"[Interactor] Can't interact: {ia.GetPrompt()}");
                    return;
                }

                ia.Interact(inv);
                return;
            }
        }

        if (debugLogs) Debug.Log($"[Interactor] Hit {hit.collider.name} but found NO IInteractable on it/parents.");
    }
}
