using UnityEngine;
using UnityEngine.Audio;

public class CustomerMover : MonoBehaviour
{
    [Header("Targets")]
    public Transform deskTarget;
    public Transform exitTarget;
    [Tooltip("Optional: specific point to look at when waiting (e.g., player or DeskLookPoint). If null, faces deskTarget.")]
    public Transform deskLookPoint;

    [Header("Visual")]
    [Tooltip("The transform to rotate for facing (usually your Visual child/root of the model).")]
    [SerializeField] private Transform visualYaw;
    [SerializeField] private bool rotateVisualOnly = true;
    [Tooltip("If your model's forward axis is wrong for MOVEMENT, tweak this (0, 90, 180, -90).")]
    [SerializeField] private float visualYawOffsetDegrees = 0f;
    [Tooltip("Separate offset for LOOKING (usually 0). Only change if look direction is off.")]
    [SerializeField] private float lookYawOffsetDegrees = 0f;
    [Tooltip("Lock visual child position to prevent animation root motion sliding.")]
    [SerializeField] private bool lockVisualPosition = true;

    [Header("Look At (while waiting)")]
    [SerializeField] private Transform lookAtTarget; // player
    [SerializeField] private float lookTurnSpeedDegPerSec = 360f;
    [SerializeField] private bool snapLookOnArrive = false;
    [Tooltip("If true, don't calculate look direction - just keep whatever rotation visual has on arrival.")]
    [SerializeField] private bool usePrefabRotationAtDesk = true;
    [SerializeField] private float lookHeightOffset = 1.5f; // eye-ish height

    [Header("Move")]
    public float moveSpeed = 1.6f;
    public float arriveDistance = 0.25f;
    public float turnSpeedDegPerSec = 540f;

    [Header("Anti-wobble")]
    [Tooltip("Higher = snappier facing, lower = smoother. 10-20 is usually good.")]
    [SerializeField] private float directionSmooth = 18f;
    [Tooltip("Ignore tiny direction changes to prevent jitter near destination.")]
    [SerializeField] private float minAngleToTurn = 3f;
    [Tooltip("Stop updating facing direction when very close to target to prevent spinning.")]
    [SerializeField] private float stopRotateDistance = 0.5f;
    [Tooltip("When direction change is less than this angle, don't update smoothedDir at all.")]
    [SerializeField] private float directionChangeThreshold = 0.5f;

    [Header("Animator")]
    [Tooltip("Optional: Animator component to control walk/idle animations.")]
    [SerializeField] private Animator animator;
    [Tooltip("Use 'MoveSpeed' (float) or 'IsWalking' (bool) parameter.")]
    [SerializeField] private bool useIsWalkingBool = false;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip[] enterClips;
    public AudioClip[] leaveClips;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = false;

    enum State { ToDesk, WaitingAtDesk, ToExit }
    State state;

    bool enterPlayed;
    bool leavePlayed;

    Vector3 smoothedDir = Vector3.forward;
    Vector3 lastValidMoveDir = Vector3.forward;

    // Track the look direction separately to avoid cross-contamination
    Vector3 lastLookDir = Vector3.forward;

    // Store the initial local position of visualYaw to lock it
    Vector3 visualLocalPosLocked = Vector3.zero;
    bool visualPosInitialized = false;

    public void Init(Transform desk, Transform exit, Transform lookTarget = null)
    {
        deskTarget = desk;
        exitTarget = exit;

        if (lookTarget != null)
            lookAtTarget = lookTarget;

        state = State.ToDesk;

        // Initialize direction based on spawn position
        if (desk != null)
        {
            Vector3 toDesk = desk.position - transform.position;
            toDesk.y = 0f;
            if (toDesk.sqrMagnitude > 0.0001f)
            {
                smoothedDir = toDesk.normalized;
                lastValidMoveDir = smoothedDir;
            }
        }

        PlayEnterOnce();
    }

    public void BeginExit()
    {
        if (state == State.ToExit) return;
        state = State.ToExit;

        // Reset direction for exit movement
        if (exitTarget != null)
        {
            Vector3 toExit = exitTarget.position - transform.position;
            toExit.y = 0f;
            if (toExit.sqrMagnitude > 0.0001f)
            {
                smoothedDir = toExit.normalized;
                lastValidMoveDir = smoothedDir;
            }
        }
    }

    void Awake()
    {
        if (!visualYaw) visualYaw = transform;
        if (!animator) animator = GetComponentInChildren<Animator>();

        // Store initial local position to lock it if needed
        if (lockVisualPosition && visualYaw && visualYaw != transform)
        {
            visualLocalPosLocked = visualYaw.localPosition;
            visualPosInitialized = true;
        }
    }

    void Update()
    {
        // Lock visual position if enabled (prevents animation root motion sliding)
        if (lockVisualPosition && visualPosInitialized && visualYaw && visualYaw != transform)
        {
            visualYaw.localPosition = visualLocalPosLocked;
        }

        // WAITING: continuously face look target (yaw only)
        if (state == State.WaitingAtDesk)
        {
            UpdateAnimator(0f); // idle

            // Option: use prefab rotation and don't calculate look direction
            if (!usePrefabRotationAtDesk)
            {
                FaceLookTarget();
            }

            return;
        }

        Transform target = (state == State.ToExit) ? exitTarget : deskTarget;
        if (!target) return;

        Vector3 to = target.position - transform.position;
        to.y = 0f;

        float dist = to.magnitude;

        // ARRIVE
        if (dist <= arriveDistance)
        {
            if (state == State.ToDesk)
            {
                state = State.WaitingAtDesk;
                UpdateAnimator(0f); // idle

                // If using prefab rotation, ensure visual is at Y=90 (or whatever your prefab has)
                if (usePrefabRotationAtDesk)
                {
                    Transform rotTarget = (rotateVisualOnly && visualYaw) ? visualYaw : transform;
                    // Reset to prefab's local rotation (preserve what you set in prefab)
                    // This assumes visualYaw is a child - adjust if needed
                    if (rotateVisualOnly && visualYaw)
                    {
                        // Keep the Y rotation you set in the prefab
                        Vector3 currentEuler = rotTarget.localEulerAngles;
                        currentEuler.y = 90f; // Force Y to 90 as you want
                        rotTarget.localEulerAngles = currentEuler;
                    }
                }
                else
                {
                    // Initialize look direction based on current forward
                    Transform rotTarget = (rotateVisualOnly && visualYaw) ? visualYaw : transform;
                    lastLookDir = rotTarget.forward;
                    lastLookDir.y = 0f;
                    lastLookDir = FlattenAndNormalize(lastLookDir, Vector3.forward);

                    // Optional: snap immediately when they first arrive
                    if (snapLookOnArrive)
                        SnapFaceLookTarget();
                }

                return;
            }

            if (state == State.ToExit)
            {
                UpdateAnimator(0f); // idle
                PlayLeaveOnceAtExit();
                Destroy(gameObject);
                return;
            }
        }

        // MOVE DIR (stable + smoothed)
        Vector3 desiredDir = (dist > 0.0001f) ? (to / dist) : lastValidMoveDir;
        desiredDir = FlattenAndNormalize(desiredDir, lastValidMoveDir);

        // Only update direction if we're far enough from target to avoid spinning
        if (dist > stopRotateDistance)
        {
            // Check if direction change is significant enough to care about
            float dirAngleChange = Vector3.Angle(smoothedDir, desiredDir);

            if (dirAngleChange > directionChangeThreshold)
            {
                // Smooth direction to reduce wobble
                float t = 1f - Mathf.Exp(-directionSmooth * Time.deltaTime);
                smoothedDir = Vector3.Slerp(smoothedDir, desiredDir, t);
                smoothedDir = FlattenAndNormalize(smoothedDir, desiredDir);
                lastValidMoveDir = smoothedDir;
            }
            // else: direction change too small, keep current smoothedDir

            // Rotate to movement direction (yaw only, with angle threshold)
            RotateTowards(smoothedDir, turnSpeedDegPerSec, isMovement: true);
        }

        // Move
        transform.position += smoothedDir * (moveSpeed * Time.deltaTime);

        UpdateAnimator(moveSpeed);

        // Lock visual position AGAIN after animation updates (late lock)
        if (lockVisualPosition && visualPosInitialized && visualYaw && visualYaw != transform)
        {
            visualYaw.localPosition = visualLocalPosLocked;
        }

        // Debug visualization
        if (showDebugRays)
        {
            Debug.DrawRay(transform.position + Vector3.up * 0.5f, smoothedDir * 2f, Color.green);
            Transform rotTarget = (rotateVisualOnly && visualYaw) ? visualYaw : transform;
            Debug.DrawRay(transform.position + Vector3.up * 0.5f, rotTarget.forward * 1.5f, Color.blue);
        }
    }

    // ---------------- Facing helpers ----------------

    void FaceLookTarget()
    {
        // Determine what to look at: deskLookPoint > lookAtTarget > deskTarget
        Transform actualLookTarget = deskLookPoint ? deskLookPoint : (lookAtTarget ? lookAtTarget : deskTarget);

        if (!actualLookTarget) return;

        Transform rotTarget = (rotateVisualOnly && visualYaw) ? visualYaw : transform;
        Vector3 eyePos = rotTarget.position;
        eyePos.y += lookHeightOffset;

        Vector3 lookPos = actualLookTarget.position;
        // Match height for yaw-only look
        lookPos.y = eyePos.y;

        Vector3 dir = lookPos - eyePos;
        dir.y = 0f;

        dir = FlattenAndNormalize(dir, lastLookDir);

        // Only update if direction is valid and significantly different
        if (dir.sqrMagnitude > 0.0001f)
        {
            float angleDiff = Vector3.Angle(lastLookDir, dir);
            if (angleDiff > minAngleToTurn * 0.5f) // Use smaller threshold for look
            {
                lastLookDir = dir;
                RotateTowards(dir, lookTurnSpeedDegPerSec, isMovement: false);
            }
        }

        // Debug visualization
        if (showDebugRays)
        {
            Debug.DrawLine(eyePos, lookPos, Color.yellow);
            Debug.DrawRay(eyePos, rotTarget.forward * 1.5f, Color.cyan);
        }
    }

    void SnapFaceLookTarget()
    {
        // Determine what to look at
        Transform actualLookTarget = deskLookPoint ? deskLookPoint : (lookAtTarget ? lookAtTarget : deskTarget);

        if (!actualLookTarget) return;

        Transform rotTarget = (rotateVisualOnly && visualYaw) ? visualYaw : transform;
        Vector3 eyePos = rotTarget.position;
        eyePos.y += lookHeightOffset;

        Vector3 lookPos = actualLookTarget.position;
        lookPos.y = eyePos.y;

        Vector3 dir = lookPos - eyePos;
        dir.y = 0f;

        dir = FlattenAndNormalize(dir, lastLookDir);

        if (dir.sqrMagnitude > 0.0001f)
        {
            lastLookDir = dir;
            ApplyRotation(dir, snap: true);
        }
    }

    void RotateTowards(Vector3 dir, float speedDeg, bool isMovement)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        // Apply appropriate offset: movement uses visualYawOffset, looking uses lookYawOffset
        float offsetToUse = isMovement ? visualYawOffsetDegrees : lookYawOffsetDegrees;
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, offsetToUse, 0f);

        Transform rotTarget = (rotateVisualOnly && visualYaw) ? visualYaw : transform;
        float angle = Quaternion.Angle(rotTarget.rotation, targetRot);

        // Use threshold only for movement to prevent wobble
        if (isMovement && angle < minAngleToTurn) return;

        rotTarget.rotation = Quaternion.RotateTowards(rotTarget.rotation, targetRot, speedDeg * Time.deltaTime);
    }

    void ApplyRotation(Vector3 dir, bool snap)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        // Use lookYawOffset for snap (only used when arriving at desk)
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, lookYawOffsetDegrees, 0f);
        Transform rotTarget = (rotateVisualOnly && visualYaw) ? visualYaw : transform;

        rotTarget.rotation = targetRot;
    }

    static Vector3 FlattenAndNormalize(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.000001f)
        {
            fallback.y = 0f;
            return (fallback.sqrMagnitude < 0.000001f) ? Vector3.forward : fallback.normalized;
        }
        return v.normalized;
    }

    // ---------------- Animator ----------------

    void UpdateAnimator(float speed)
    {
        if (!animator) return;

        if (useIsWalkingBool)
        {
            animator.SetBool("IsWalking", speed > 0.01f);
        }
        else
        {
            animator.SetFloat("MoveSpeed", speed);
        }
    }

    // ---------------- Audio ----------------

    void PlayEnterOnce()
    {
        if (enterPlayed) return;
        enterPlayed = true;
        PlayRandomOnSource(enterClips);
    }

    void PlayLeaveOnceAtExit()
    {
        if (leavePlayed) return;
        leavePlayed = true;

        Vector3 pos = exitTarget != null ? exitTarget.position : transform.position;
        PlayRandomAtPoint(leaveClips, pos);
    }

    void PlayRandomOnSource(AudioClip[] clips)
    {
        if (sfxSource == null || clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        sfxSource.PlayOneShot(clip, volume);
    }

    void PlayRandomAtPoint(AudioClip[] clips, Vector3 pos)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        float pitch = Random.Range(pitchRange.x, pitchRange.y);

        var go = new GameObject("CustomerLeaveSFX");
        go.transform.position = pos;

        var a = go.AddComponent<AudioSource>();

        if (sfxSource != null)
        {
            a.spatialBlend = sfxSource.spatialBlend;
            a.rolloffMode = sfxSource.rolloffMode;
            a.minDistance = sfxSource.minDistance;
            a.maxDistance = sfxSource.maxDistance;
            a.dopplerLevel = sfxSource.dopplerLevel;
            a.spread = sfxSource.spread;
            a.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
        }
        else
        {
            a.spatialBlend = 1f;
        }

        a.pitch = pitch;
        a.PlayOneShot(clip, volume);

        Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
    }

    public void Leave() => BeginExit();
}