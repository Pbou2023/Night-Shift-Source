using UnityEngine;
using TMPro;

public class InteractPromptUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInteractor interactor;   // drag your PlayerInteractor here
    [SerializeField] private TextMeshProUGUI promptText;    // drag your TMP text here
    [SerializeField] private CanvasGroup canvasGroup;       // optional (nice fade). Can be null.

    [Header("Prompt")]
    [SerializeField] private string keyHint = "Press E";
    [SerializeField] private bool showActionText = true;    // "Press E — Take Wire" vs just "Press E to interact"
    [SerializeField] private bool showEvenIfCannotInteract = true;
    [SerializeField] private string cannotInteractPrefix = ""; // e.g. "Can't: " (leave empty if you just want dim text)

    [Header("Detection")]
    [Tooltip("Use a small SphereCast instead of a Raycast (more forgiving).")]
    [SerializeField] private bool useSphereCast = true;
    [SerializeField] private float sphereRadius = 0.18f;

    [Header("Visual")]
    [SerializeField] private float fadeSpeed = 12f;
    [SerializeField] private Color canColor = Color.white;
    [SerializeField] private Color cannotColor = new Color(1f, 1f, 1f, 0.55f);

    private PlayerInventory inv;

    void Awake()
    {
        if (!interactor) interactor = FindObjectOfType<PlayerInteractor>();
        if (!promptText) promptText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (interactor)
            inv = interactor.GetComponent<PlayerInventory>();

        SetVisible(false, true);
    }

    void Update()
    {
        if (!interactor || interactor.rayOrigin == null || promptText == null)
        {
            SetVisible(false);
            return;
        }

        // Copy the exact same settings as the interactor so behavior matches
        Transform origin = interactor.rayOrigin;
        float range = interactor.interactRange;
        LayerMask mask = interactor.interactMask;

        RaycastHit hit;
        bool hasHit = useSphereCast
            ? Physics.SphereCast(origin.position, sphereRadius, origin.forward, out hit, range, mask, QueryTriggerInteraction.Collide)
            : Physics.Raycast(origin.position, origin.forward, out hit, range, mask, QueryTriggerInteraction.Collide);

        if (!hasHit)
        {
            SetVisible(false);
            return;
        }

        // Find IInteractable on hit object or parents (same approach as your TryInteract)
        var mbs = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
        IInteractable ia = null;

        for (int i = 0; i < mbs.Length; i++)
        {
            if (mbs[i] is IInteractable interactable)
            {
                ia = interactable;
                break;
            }
        }

        if (ia == null)
        {
            SetVisible(false);
            return;
        }

        bool can = (inv != null) && ia.CanInteract(inv);
        if (!can && !showEvenIfCannotInteract)
        {
            SetVisible(false);
            return;
        }

        string action = ia.GetPrompt(); // e.g. "Take Wire", "Accept Order", "Deliver Order"
        string text;

        if (showActionText)
            text = $"{keyHint} To {action}";
        else
            text = $"{keyHint} to interact";

        if (!can && !string.IsNullOrEmpty(cannotInteractPrefix))
            text = cannotInteractPrefix + action;

        promptText.text = text;
        promptText.color = can ? canColor : cannotColor;

        SetVisible(true);
    }

    void SetVisible(bool visible, bool instant = false)
    {
        if (canvasGroup)
        {
            float target = visible ? 1f : 0f;
            if (instant) canvasGroup.alpha = target;
            else canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.unscaledDeltaTime * fadeSpeed);

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else
        {
            // No CanvasGroup? Just enable/disable
            if (promptText) promptText.enabled = visible;
        }
    }
}
