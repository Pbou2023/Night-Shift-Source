using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class BackroomsDoorTrigger : MonoBehaviour
{
    [Header("Gate (optional)")]
    [SerializeField] private int requireOrdersCompletedAtLeast = 0;

    [Header("Scene")]
    [SerializeField] private string nextSceneName = "BackroomsLevel";
    [SerializeField] private int nextSceneBuildIndex = -1; // if >= 0, uses this instead of name

    [Header("Fall / Clip Effect")]
    [SerializeField] private bool doDropEffect = true;
    [SerializeField] private float dropDuration = 0.75f;
    [SerializeField] private float dropSpeed = 10f; // units per second down

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip enterClip;
    [Range(0f, 1f)][SerializeField] private float volume = 1f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneShot = true;

    [Header("Disable these on trigger (optional)")]
    [SerializeField] private MonoBehaviour[] disableOnTrigger;

    private bool triggered;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        // Order gate
        if (ShiftManager.Instance != null && ShiftManager.Instance.OrdersCompleted < requireOrdersCompletedAtLeast)
        {
            Debug.Log($"[BackroomsDoorTrigger] Blocked. Need orders >= {requireOrdersCompletedAtLeast}");
            return;
        }

        triggered = true;

        // IMPORTANT: if anything paused time earlier (end screen etc.)
        Time.timeScale = 1f;

        // Find the player root (in case the collider is on a child)
        GameObject playerRoot = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        playerRoot = playerRoot.transform.root.gameObject;

        StartCoroutine(DoTransition(playerRoot));
    }

    IEnumerator DoTransition(GameObject playerRoot)
    {
        Debug.Log($"[BackroomsDoorTrigger] Triggered by '{playerRoot.name}'. Drop={doDropEffect}");

        // play SFX
        if (enterClip)
        {
            if (!sfxSource) sfxSource = GetComponent<AudioSource>();
            if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.PlayOneShot(enterClip, volume);
        }

        // Disable control scripts if provided
        if (disableOnTrigger != null)
        {
            foreach (var mb in disableOnTrigger)
                if (mb) mb.enabled = false;
        }

        // Stop physics fighting us
        Rigidbody rb = playerRoot.GetComponentInChildren<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; // we will move manually
        }

        // Disable all colliders so we can "clip"
        Collider[] cols = playerRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        // Drop effect (manual so movement scripts/grounding can't cancel it)
        if (doDropEffect)
        {
            Vector3 start = playerRoot.transform.position;
            float t = 0f;

            while (t < dropDuration)
            {
                t += Time.unscaledDeltaTime;
                playerRoot.transform.position = start + Vector3.down * (dropSpeed * t);
                yield return null;
            }
        }

        // Load next scene
        if (nextSceneBuildIndex >= 0)
        {
            Debug.Log($"[BackroomsDoorTrigger] Loading scene by build index: {nextSceneBuildIndex}");
            SceneManager.LoadScene(nextSceneBuildIndex);
        }
        else
        {
            Debug.Log($"[BackroomsDoorTrigger] Loading scene by name: '{nextSceneName}'");
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
