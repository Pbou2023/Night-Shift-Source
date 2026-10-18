using UnityEngine;

public class TriggerOneShotSfx : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip clip;
    [Range(0f, 1f)][SerializeField] private float volume = 1f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);

    [Header("3D / 2D")]
    [Tooltip("0 = 2D, 1 = 3D")]
    [Range(0f, 1f)][SerializeField] private float spatialBlend = 0f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool oneShot = true;

    bool fired;

    void OnTriggerEnter(Collider other)
    {
        if (fired && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        fired = true;

        if (!clip) return;

        // Spawn a temporary audio object so it keeps playing even if THIS trigger gets disabled
        GameObject go = new GameObject("OneShotSFX");
        go.transform.position = transform.position;

        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = spatialBlend;
        a.dopplerLevel = 0f;
        a.rolloffMode = AudioRolloffMode.Logarithmic;

        a.pitch = Random.Range(pitchRange.x, pitchRange.y);
        a.PlayOneShot(clip, volume);

        Destroy(go, clip.length + 0.1f);
    }
}
