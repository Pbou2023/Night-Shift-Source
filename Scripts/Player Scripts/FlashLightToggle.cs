using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FlashlightToggle : MonoBehaviour
{
    [Header("Optional (auto-finds if empty)")]
    public Light flashlight;

    [Header("State")]
    public bool startOn = true;

    [Header("Optional SFX")]
    public AudioSource click;

    void Awake()
    {
        if (!flashlight) flashlight = GetComponent<Light>();
        if (flashlight) flashlight.enabled = startOn;
    }

    void Update()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            pressed = true;
#else
        if (Input.GetKeyDown(KeyCode.F))
            pressed = true;
#endif

        if (!pressed || !flashlight) return;

        flashlight.enabled = !flashlight.enabled;
        if (click) click.Play();
    }
}
