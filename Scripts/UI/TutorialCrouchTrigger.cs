using UnityEngine;

public class TutorialTipTrigger : MonoBehaviour
{
    [Header("Tip")]
    [SerializeField] private string tipKey = "crouch_tip";
    [TextArea][SerializeField] private string message = "Press Ctrl To Crouch";
    [SerializeField] private float seconds = 4f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableAfterTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        TutorialTipUI.ShowOnce(TutorialTipUI.Channel.Crouch, tipKey, message, seconds);

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }
}
