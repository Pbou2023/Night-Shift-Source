using UnityEngine;

public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance { get; private set; }

    public bool HasActiveOrder => activeCustomer != null;

    public float TimeRemaining => timeRemaining;
    public float TimeLimit => timeLimit;

    public CustomerOrder ActiveCustomer => activeCustomer;
    public CustomerOrder.OrderLine[] ActiveLines => activeLines;

    float timeRemaining;
    float timeLimit;

    CustomerOrder activeCustomer;
    CustomerOrder.OrderLine[] activeLines;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (!HasActiveOrder) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
            FailOrder();
    }

    public bool TryAcceptOrder(CustomerOrder customer, float patienceSeconds)
    {
        if (HasActiveOrder) return false;

        activeCustomer = customer;

        timeLimit = Mathf.Max(1f, patienceSeconds);
        timeRemaining = timeLimit;

        activeLines = customer != null ? customer.GetOrderLines() : null;

        return true;
    }

    public bool IsActiveCustomer(CustomerOrder customer) => activeCustomer == customer;

    public void ClearOrder()
    {
        activeCustomer = null;
        activeLines = null;
        timeRemaining = 0f;
        timeLimit = 0f;
    }

    void FailOrder()
    {
        Debug.Log("[ORDER] FAILED (timer ran out).");

        if (activeCustomer != null)
            activeCustomer.OnOrderFailed();

        ClearOrder();
    }
}
