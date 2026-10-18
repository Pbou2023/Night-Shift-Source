using UnityEngine;

public class Night2Director : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private GameObject enemyRoot;
    [SerializeField] private int enableEnemyAfterCompletedOrders = 3;

    [Header("Optional: enemy activation tip")]
    [SerializeField] private bool showEnemyTip = true;

    void Start()
    {
        if (enemyRoot) enemyRoot.SetActive(false);

        if (ShiftManager.Instance != null)
        {
            ShiftManager.Instance.OnOrdersCompletedChanged += HandleOrdersCompleted;
            HandleOrdersCompleted(ShiftManager.Instance.OrdersCompleted); // in case already progressed
        }
        else
        {
            Debug.LogWarning("[Night2Director] No ShiftManager in scene.");
        }
    }

    void OnDestroy()
    {
        if (ShiftManager.Instance != null)
            ShiftManager.Instance.OnOrdersCompletedChanged -= HandleOrdersCompleted;
    }

    void HandleOrdersCompleted(int completed)
    {
        if (enemyRoot && !enemyRoot.activeSelf && completed >= enableEnemyAfterCompletedOrders)
        {
            enemyRoot.SetActive(true);

            if (showEnemyTip)
            {
                TutorialTipUI.ShowOnce(
                    TutorialTipUI.Channel.Crafting,
                    "enemy_active",
                    "Something is moving in the backrooms...",
                    4f
                );
            }
        }
    }
}
