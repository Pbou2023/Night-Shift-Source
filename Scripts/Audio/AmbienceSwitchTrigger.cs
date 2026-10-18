using UnityEngine;

public class AmbienceSwitchTrigger : MonoBehaviour
{
    [SerializeField] private AmbienceManager.AmbienceType setTo = AmbienceManager.AmbienceType.Backrooms;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneShot = true;

    bool fired;

    void OnTriggerEnter(Collider other)
    {
        if (fired && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        fired = true;
        if (AmbienceManager.Instance != null)
            AmbienceManager.Instance.SetAmbience(setTo);
    }
}
