using UnityEngine;

public class Night1TutorialDirector : MonoBehaviour
{
    [SerializeField] private CustomerSpawner spawner;

    void Awake()
    {
        if (!spawner) spawner = FindObjectOfType<CustomerSpawner>();
    }

    void OnEnable()
    {
        if (spawner) spawner.OnCustomerSpawnedCount += HandleSpawn;
    }

    void OnDisable()
    {
        if (spawner) spawner.OnCustomerSpawnedCount -= HandleSpawn;
    }

    void HandleSpawn(int count)
    {
        if (count == 2)
        {
            TutorialTipUI.ShowOnce(
                TutorialTipUI.Channel.Crafting,
                "crafting_intro",
                "Sometimes customers will request items you need To craft.\nUse the crafting bench To combine ingredients.",
                6f
            );
        }
    }
}
