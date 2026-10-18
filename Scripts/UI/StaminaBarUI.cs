using UnityEngine;
using UnityEngine.UI;

public class StaminaBarUI : MonoBehaviour
{
    public PlayerMotorBasic motor;
    public Image fillImage;

    void Awake()
    {
        if (fillImage == null) fillImage = GetComponent<Image>();
    }

    void Update()
    {
        if (motor == null || fillImage == null) return;
        fillImage.fillAmount = motor.Stamina01;
    }
}
