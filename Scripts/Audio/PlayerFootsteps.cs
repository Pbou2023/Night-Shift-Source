using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    public PlayerMotorBasic motor;

    public enum SurfaceType { Office, Backrooms }

    [Header("Clips")]
    public AudioClip[] officeClips;
    public AudioClip[] backroomsClips;

    [Header("Default Surface")]
    public SurfaceType currentSurface = SurfaceType.Office;

    [Header("Timing (seconds between steps)")]
    public float walkInterval = 0.50f;
    public float sprintInterval = 0.33f;
    public float crouchInterval = 0.65f;

    [Header("Conditions")]
    public float minSpeed = 0.25f;

    [Header("Sound")]
    public float volume = 0.9f;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    AudioSource src;
    float timer;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;

        if (motor == null)
            motor = GetComponentInParent<PlayerMotorBasic>();
    }

    void Update()
    {
        if (motor == null) return;

        var clips = GetCurrentClips();
        if (clips == null || clips.Length == 0) return;

        if (!motor.IsGrounded || motor.FlatSpeed < minSpeed)
        {
            timer = 0f;
            return;
        }

        float baseInterval =
            motor.IsCrouching ? crouchInterval :
            motor.IsSprinting ? sprintInterval :
            walkInterval;

        float speed01 = motor.moveSpeed > 0.01f
            ? Mathf.Clamp(motor.FlatSpeed / motor.moveSpeed, 0.6f, 2.0f)
            : 1f;

        float interval = baseInterval / speed01;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            PlayStep(clips);
            timer = interval;
        }
    }

    AudioClip[] GetCurrentClips()
    {
        return currentSurface == SurfaceType.Backrooms ? backroomsClips : officeClips;
    }

    void PlayStep(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        var clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        src.pitch = Random.Range(pitchRange.x, pitchRange.y);
        src.PlayOneShot(clip, volume);
    }

    // -------- Zone switching --------
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<FootstepZone>(out var zone))
        {
            currentSurface = zone.surface == FootstepZone.SurfaceType.Backrooms
                ? SurfaceType.Backrooms
                : SurfaceType.Office;
        }
    }
}
