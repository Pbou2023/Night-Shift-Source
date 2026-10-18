using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook_ViewRoot : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset actionsAsset;
    public string actionMapName = "Player";
    public string lookActionName = "Look";

    [Header("Yaw / Pitch Targets")]
    public Transform yawRoot;      // assign: Player root (the parent object)
    public Transform pitchPivot;   // assign: THIS ViewRoot transform (or a child pivot)

    [Header("Sensitivity")]
    public float mouseSensitivity = 0.10f;   // mouse delta (no dt)
    public float stickSensitivity = 180f;    // deg/sec (uses dt)
    public float verticalClamp = 88f;
    public bool invertY = false;

    InputAction lookAction;
    float yaw, pitch;

    void Awake()
    {
        var map = actionsAsset.FindActionMap(actionMapName, true);
        lookAction = map.FindAction(lookActionName, true);

        if (yawRoot == null) yawRoot = transform.parent;
        if (pitchPivot == null) pitchPivot = transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = yawRoot.eulerAngles.y;

        float p = pitchPivot.localEulerAngles.x;
        if (p > 180f) p -= 360f;
        pitch = Mathf.Clamp(p, -verticalClamp, verticalClamp);
    }

    void OnEnable() => lookAction.Enable();
    void OnDisable() => lookAction.Disable();

    void Update()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        var dev = lookAction.activeControl?.device;
        bool isPointer = dev is Pointer;

        float ySign = invertY ? 1f : -1f;

        if (isPointer)
        {
            yaw += look.x * mouseSensitivity;
            pitch += look.y * mouseSensitivity * ySign;
        }
        else
        {
            float dt = Time.unscaledDeltaTime;
            yaw += look.x * stickSensitivity * dt;
            pitch += look.y * stickSensitivity * dt * ySign;
        }

        pitch = Mathf.Clamp(pitch, -verticalClamp, verticalClamp);

        // yaw on Player, pitch on ViewRoot
        yawRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void LateUpdate()
    {
        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
