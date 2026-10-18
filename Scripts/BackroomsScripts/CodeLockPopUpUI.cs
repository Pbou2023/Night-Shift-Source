using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CodeLockPopupUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text statusText;

    [Header("Disable While Open")]
    [Tooltip("Only accepts PlayerInteractor now.")]
    [SerializeField] private PlayerInteractor interactorToDisable;

    [Tooltip("Optional extra scripts to disable (motor/look/etc).")]
    [SerializeField] private MonoBehaviour[] extraDisable;

    [Header("Behavior")]
    [SerializeField] private bool lockCursorWhenOpen = true;
    [SerializeField] private bool pauseTimeWhenOpen = false;

    [Tooltip("If true, this panel GameObject is disabled when closed (recommended).")]
    [SerializeField] private bool disableGameObjectWhenClosed = true;

    private CodeLock activeLock;
    private bool isOpen;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        if (!inputField) inputField = GetComponentInChildren<TMP_InputField>(true);

        HideImmediate();

        if (disableGameObjectWhenClosed)
            gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;

        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            Submit();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Open(CodeLock lockToUse)
    {
        activeLock = lockToUse;

        if (disableGameObjectWhenClosed && !gameObject.activeSelf)
            gameObject.SetActive(true);

        isOpen = true;

        if (pauseTimeWhenOpen) Time.timeScale = 0f;

        if (lockCursorWhenOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        SetVisible(true);
        SetDisabled(true);

        if (statusText) statusText.text = "";

        if (inputField)
        {
            inputField.readOnly = false;
            inputField.interactable = true;

            inputField.text = "";
            inputField.ActivateInputField();
            inputField.Select();
        }

        if (EventSystem.current && inputField)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);
    }

    public void Submit()
    {
        if (!isOpen) return;

        if (activeLock == null)
        {
            if (statusText) statusText.text = "NO LOCK";
            return;
        }

        string code = inputField ? inputField.text.Trim() : "";
        bool ok = activeLock.TrySubmit(code);

        if (ok)
        {
            if (statusText) statusText.text = "UNLOCKED";
            Close();
        }
        else
        {
            if (statusText) statusText.text = "WRONG CODE";
            if (inputField)
            {
                inputField.text = "";
                inputField.ActivateInputField();
                inputField.Select();
            }
        }
    }

    public void Close()
    {
        isOpen = false;

        if (pauseTimeWhenOpen) Time.timeScale = 1f;

        if (lockCursorWhenOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        SetVisible(false);
        SetDisabled(false);

        activeLock = null;

        if (disableGameObjectWhenClosed)
            gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        if (!group) return;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void SetVisible(bool visible)
    {
        if (!group) return;
        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    private void SetDisabled(bool disable)
    {
        if (interactorToDisable) interactorToDisable.enabled = !disable;

        if (extraDisable != null)
        {
            for (int i = 0; i < extraDisable.Length; i++)
            {
                if (extraDisable[i])
                    extraDisable[i].enabled = !disable;
            }
        }
    }
}
