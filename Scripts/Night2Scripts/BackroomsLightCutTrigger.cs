using UnityEngine;

public class BackroomsLightCutTriggerDestroy : MonoBehaviour
{
    [Header("Lights to remove")]
    [Tooltip("Drag the Directional Light objects here.")]
    [SerializeField] private Light[] lightsToRemove;

    [Tooltip("If true, destroys the entire light GameObject. If false, destroys only the Light component.")]
    [SerializeField] private bool destroyWholeGameObject = true;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneShot = true;

    bool fired;

    void OnTriggerEnter(Collider other)
    {
        if (fired && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        fired = true;

        for (int i = 0; i < lightsToRemove.Length; i++)
        {
            var l = lightsToRemove[i];
            if (!l) continue;

            if (destroyWholeGameObject)
                Destroy(l.gameObject);
            else
                Destroy(l);
        }

        if (oneShot) gameObject.SetActive(false);
    }
}
