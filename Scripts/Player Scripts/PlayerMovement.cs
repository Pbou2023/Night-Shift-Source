using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMotorBasic : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset actionsAsset;
    public string actionMapName = "Player";
    public string moveActionName = "Move";
    public string crouchActionName = "Crouch";
    public string sprintActionName = "Sprint";

    [Header("View (yaw source + crouch offset)")]
    public Transform yawSource; // assign: ViewRoot

    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float acceleration = 25f;
    public float friction = 18f;

    [Header("Sprint")]
    public float sprintMultiplier = 1.6f;
    public bool sprintIsHold = true;

    [Header("Stamina")]
    public bool useStamina = true;
    public float staminaMax = 5f;               // "seconds" worth of sprint
    public float staminaDrainPerSec = 1.0f;     // per second while sprinting
    public float staminaRegenPerSec = 0.8f;     // per second while not sprinting
    public float staminaRegenDelay = 0.6f;      // delay after sprint stops
    public float minStaminaToStartSprint = 0.15f;

    [Header("Ground Feel")]
    public float deceleration = 45f;     // stopping power
    public float stopSpeed = 0.08f;      // snap to 0 under this speed
    public float reverseBoost = 1.5f;    // extra brake when input opposes velocity

    [Header("Ground Check")]
    public LayerMask groundMask = ~0;
    public float probeDist = 0.2f;
    public float maxGroundAngle = 60f;

    [Header("Collision Mask (for standing / crouch checks)")]
    public LayerMask worldMask = ~0;

    [Header("Crouch")]
    public bool crouchIsHold = true;
    [Range(0.4f, 1f)] public float crouchHeightMultiplier = 0.55f;
    public float crouchSpeedMultiplier = 0.65f;
    public float viewCrouchOffset = 0.6f;
    public float standCheckPadding = 0.02f;

    [Header("Wall Stick Fix (optional)")]
    public bool enableWallSlideFix = true;

    Rigidbody rb;
    CapsuleCollider capsule;

    InputAction moveAction;
    InputAction crouchAction;
    InputAction sprintAction;

    Vector2 moveInput;

    bool isGrounded;
    bool isCrouching;
    bool crouchToggleState;

    bool isSprinting;
    bool sprintToggleState;

    float baseCapsuleHeight;
    Vector3 baseCapsuleCenter;
    Vector3 baseViewLocalPos;

    float stamina;
    float regenDelayTimer;

    // UI can read this
    public float Stamina01 => staminaMax <= 0f ? 0f : Mathf.Clamp01(stamina / staminaMax);
    public float Stamina => stamina;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = true;

        baseCapsuleHeight = capsule.height;
        baseCapsuleCenter = capsule.center;

        if (yawSource != null)
            baseViewLocalPos = yawSource.localPosition;

        stamina = staminaMax;

        if (actionsAsset == null)
        {
            Debug.LogError("[PlayerMotorBasic] No InputActionAsset assigned.");
            enabled = false;
            return;
        }

        var map = actionsAsset.FindActionMap(actionMapName, throwIfNotFound: true);
        moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
        crouchAction = map.FindAction(crouchActionName, throwIfNotFound: true);
        sprintAction = map.FindAction(sprintActionName, throwIfNotFound: true);
    }

    void OnEnable()
    {
        moveAction.Enable();
        crouchAction.Enable();
        sprintAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        crouchAction.Disable();
        sprintAction.Disable();
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        // Crouch
        if (crouchIsHold)
        {
            SetCrouch(crouchAction.IsPressed());
        }
        else
        {
            if (crouchAction.WasPressedThisFrame())
                crouchToggleState = !crouchToggleState;

            SetCrouch(crouchToggleState);
        }

        // Sprint input intent
        bool sprintWanted;
        if (sprintIsHold)
        {
            sprintWanted = sprintAction.IsPressed();
        }
        else
        {
            if (sprintAction.WasPressedThisFrame())
                sprintToggleState = !sprintToggleState;

            sprintWanted = sprintToggleState;
        }

        // Base rule: no sprint if crouching or not moving
        bool isMoving = moveInput.sqrMagnitude >= 0.01f;
        if (isCrouching || !isMoving)
            sprintWanted = false;

        // Stamina gating
        if (useStamina)
        {
            // Can only start/continue sprint if we have stamina
            if (sprintWanted && stamina > 0f)
            {
                // prevent "micro sprint" spam when basically empty
                if (!isSprinting && stamina < minStaminaToStartSprint)
                    sprintWanted = false;
            }
        }

        isSprinting = sprintWanted;

        // Stamina drain/regen
        if (useStamina)
        {
            float dt = Time.deltaTime;

            if (isSprinting && isMoving)
            {
                stamina -= staminaDrainPerSec * dt;
                regenDelayTimer = staminaRegenDelay;

                if (stamina <= 0f)
                {
                    stamina = 0f;
                    isSprinting = false;

                    // if toggle sprint, force it off so it doesn't auto-restart
                    sprintToggleState = false;
                }
            }
            else
            {
                if (regenDelayTimer > 0f)
                {
                    regenDelayTimer -= dt;
                }
                else
                {
                    stamina += staminaRegenPerSec * dt;
                    if (stamina > staminaMax) stamina = staminaMax;
                }
            }
        }
    }

    void FixedUpdate()
    {
        UpdateGrounded();
        HandleMove();
    }

    void UpdateGrounded()
    {
        Vector3 worldCenter = transform.TransformPoint(capsule.center);
        float radius = capsule.radius * 0.95f;
        float halfHeight = capsule.height * 0.5f;

        Vector3 foot = worldCenter - Vector3.up * (halfHeight - radius - 0.01f);

        if (Physics.SphereCast(foot, radius, Vector3.down, out RaycastHit hit, probeDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            float ang = Vector3.Angle(hit.normal, Vector3.up);
            isGrounded = ang <= maxGroundAngle;
        }
        else
        {
            isGrounded = false;
        }
    }

    void HandleMove()
    {
        float yaw = (yawSource != null ? yawSource.eulerAngles.y : transform.eulerAngles.y);
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

        Vector3 inputDir = yawRot * new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        float speedMul = 1f;
        if (isCrouching) speedMul *= crouchSpeedMultiplier;
        else if (isSprinting) speedMul *= sprintMultiplier;

        float speed = moveSpeed * speedMul;

        Vector3 vel = rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);

        Vector3 targetFlat = inputDir * speed;

        if (inputDir.sqrMagnitude > 0.0001f)
        {
            // Accelerate toward target
            flatVel = Vector3.MoveTowards(flatVel, targetFlat, acceleration * Time.fixedDeltaTime);

            // If trying to reverse vs current motion, brake harder (kills "drifty turns")
            if (Vector3.Dot(flatVel, targetFlat) < 0f)
            {
                flatVel = Vector3.MoveTowards(
                    flatVel,
                    Vector3.zero,
                    deceleration * reverseBoost * Time.fixedDeltaTime
                );
            }
        }
        else
        {
            // No input → strong braking to 0
            flatVel = Vector3.MoveTowards(flatVel, Vector3.zero, deceleration * Time.fixedDeltaTime);

            // Snap to zero to remove tiny drift
            if (flatVel.magnitude < stopSpeed)
                flatVel = Vector3.zero;
        }

        vel.x = flatVel.x;
        vel.z = flatVel.z;

        if (isGrounded && vel.y < 0f)
            vel.y = -2f; // stick to ground

        rb.linearVelocity = vel;
    }


    void SetCrouch(bool wantCrouch)
    {
        if (wantCrouch == isCrouching) return;

        if (!wantCrouch && !CanStandUp())
            return;

        isCrouching = wantCrouch;

        float targetHeight = baseCapsuleHeight * (isCrouching ? crouchHeightMultiplier : 1f);
        float heightDelta = baseCapsuleHeight - targetHeight;

        capsule.height = targetHeight;
        capsule.center = baseCapsuleCenter - new Vector3(0f, heightDelta * 0.5f, 0f);

        if (yawSource != null)
            yawSource.localPosition = baseViewLocalPos + (isCrouching ? Vector3.down * viewCrouchOffset : Vector3.zero);
    }

    bool CanStandUp()
    {
        float radius = capsule.radius;
        Vector3 worldCenter = transform.TransformPoint(baseCapsuleCenter);

        float halfHeight = baseCapsuleHeight * 0.5f;
        float pointOffset = halfHeight - radius;

        Vector3 p1 = worldCenter + Vector3.up * pointOffset;
        Vector3 p2 = worldCenter - Vector3.up * pointOffset;

        return !Physics.CheckCapsule(p1, p2, radius + standCheckPadding, worldMask, QueryTriggerInteraction.Ignore);
    }

    void OnCollisionStay(Collision collision)
    {
        if (!enableWallSlideFix) return;

        foreach (var contact in collision.contacts)
        {
            Vector3 n = contact.normal;

            if (Vector3.Dot(n, Vector3.up) > 0.6f)
                continue;

            Vector3 v = rb.linearVelocity;
            float intoWall = Vector3.Dot(v, -n);

            if (intoWall > 0f)
            {
                v += n * intoWall;
                rb.linearVelocity = v;
            }

            break;
        }
    }
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public bool IsCrouching => isCrouching;
    public float FlatSpeed => new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;


}
