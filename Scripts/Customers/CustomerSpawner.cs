using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [Serializable]
    public class ItemOption
    {
        public string itemId = "MetalScrap";
        [Min(1)] public int weight = 1;

        [Tooltip("0 = no cap (up to maxTotalItems). Set to 1 for things like Battery.")]
        [Min(0)] public int maxPerOrder = 0;
    }

    [Header("Spawn")]
    public GameObject customerPrefab;
    public Transform spawnPoint;
    public float spawnDelay = 1.0f;

    public Transform deskTarget; // drag CustomerPath here in inspector
    public Transform exitTarget; // drag your door/exit point here (or reuse spawnPoint)

    [Header("Random Order Generation")]
    [Min(1)] public int maxTotalItems = 3;
    [Min(1)] public int minTotalItems = 1;
    public ItemOption[] possibleItems;

    [Header("Crafted Output Limits (prevents impossible combos)")]
    [Tooltip("Total crafted OUTPUT items allowed in one order. 2 = good for 3-slot inventory.")]
    [Min(0)] public int maxCraftedOutputsPerOrder = 2; // 0 = unlimited

    [Header("Auto-limit crafted outputs")]
    public bool autoLimitCraftedOutputs = true;
    [Min(1)] public int inventorySlots = 3;  // your inventory is 3 slots

    [Header("Patience / Reward (simple scaling)")]
    public float basePatienceSeconds = 18f;
    public float patiencePerItem = 6f;
    public int baseScoreReward = 10;
    public int rewardPerItem = 5;

    public enum OrderMode { RandomOnly, ScriptedOnly, Mixed }

    [Header("Order Mode")]
    public OrderMode orderMode = OrderMode.RandomOnly;

    [Header("Scripted Orders")]
    public ScriptedOrder[] scriptedOrders;

    [Tooltip("Only applies to ScriptedOnly. Mixed always continues with random after scripted runs out.")]
    public bool stopSpawningAfterScripted = true;

    public event Action<int> OnCustomerSpawnedCount; // 1-based count
    int spawnedCount = 0;

    GameObject currentCustomer;
    float spawnTimer;

    // outputItemId -> ingredientCount (2 or 3 etc.)
    Dictionary<string, int> craftedInputCount = new Dictionary<string, int>();

    [Serializable]
    public class ScriptedOrder
    {
        public string debugName = "Order";

        public enum TriggerType
        {
            CustomerSpawnNumber,     // Ex: spawn #3
            AfterOrdersCompleted     // Ex: after 4 completed, NEXT customer uses this order
        }

        [Header("Trigger")]
        public TriggerType trigger = TriggerType.CustomerSpawnNumber;

        [Tooltip("Used when Trigger = CustomerSpawnNumber (1-based). Example: 3 means the 3rd spawned customer.")]
        public int customerSpawnNumber = 1;

        [Tooltip("Used when Trigger = AfterOrdersCompleted. Example: 4 means: once OrdersCompleted >= 4, NEXT spawn uses this scripted order.")]
        public int afterOrdersCompleted = 0;

        [Header("Order Lines")]
        public CustomerOrder.OrderLine[] lines;

        [Header("Overrides")]
        [Tooltip("If < 0, spawner will auto-calc patience like random orders.")]
        public float patienceOverride = -1f;

        [Tooltip("If < 0, spawner will auto-calc reward like random orders.")]
        public int rewardOverride = -1;

        [NonSerialized] public bool consumed; // runtime
    }

    void Awake()
    {
        if (autoLimitCraftedOutputs || maxCraftedOutputsPerOrder > 0)
            RebuildCraftedLookup();
    }

    void Update()
    {
        if (ShiftManager.Instance == null || !ShiftManager.Instance.ShiftRunning) return;
        if (customerPrefab == null || spawnPoint == null) return;

        if (OrderManager.Instance != null && OrderManager.Instance.HasActiveOrder)
            return;

        if (currentCustomer != null) return;

        if (orderMode == OrderMode.ScriptedOnly && stopSpawningAfterScripted && ScriptedExhausted())
            return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;

        SpawnCustomer();
        spawnTimer = spawnDelay;
    }

    bool ScriptedExhausted()
    {
        if (scriptedOrders == null || scriptedOrders.Length == 0) return true;
        for (int i = 0; i < scriptedOrders.Length; i++)
            if (scriptedOrders[i] != null && !scriptedOrders[i].consumed)
                return false;
        return true;
    }

    void SpawnCustomer()
    {
        currentCustomer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
        spawnedCount++;
        OnCustomerSpawnedCount?.Invoke(spawnedCount);

        var mover = currentCustomer.GetComponent<CustomerMover>();
        if (mover != null)
        {
            Transform exit = (exitTarget != null) ? exitTarget : spawnPoint;
            mover.Init(deskTarget, exit);
        }

        var co = currentCustomer.GetComponent<CustomerOrder>();
        if (co != null)
        {
            // Decide whether this customer is scripted or random
            ScriptedOrder scripted = PickScriptedForThisSpawn();

            CustomerOrder.OrderLine[] lines;
            int totalItems;

            if (scripted != null)
            {
                lines = (scripted.lines != null && scripted.lines.Length > 0)
                    ? scripted.lines
                    : GenerateRandomOrderLines(out totalItems); // safety fallback

                totalItems = CountTotalItems(lines);

                float patience = (scripted.patienceOverride >= 0f)
                    ? scripted.patienceOverride
                    : (basePatienceSeconds + patiencePerItem * totalItems);

                int reward = (scripted.rewardOverride >= 0)
                    ? scripted.rewardOverride
                    : (baseScoreReward + rewardPerItem * totalItems);

                co.SetOrder(lines, patience, reward);
                scripted.consumed = true;

                Debug.Log($"[SPAWNER] Customer spawned (SCRIPTED): {scripted.debugName}");
            }
            else
            {
                lines = GenerateRandomOrderLines(out totalItems);

                float patience = basePatienceSeconds + patiencePerItem * totalItems;
                int reward = baseScoreReward + rewardPerItem * totalItems;

                co.SetOrder(lines, patience, reward);

                Debug.Log("[SPAWNER] Customer spawned (RANDOM).");
            }
        }
        else
        {
            Debug.LogWarning("[SPAWNER] Customer prefab has no CustomerOrder component.");
        }
    }

    ScriptedOrder PickScriptedForThisSpawn()
    {
        if (orderMode == OrderMode.RandomOnly) return null;
        if (scriptedOrders == null || scriptedOrders.Length == 0) return null;

        // ScriptedOnly: always try to use a scripted order if any remain.
        // Mixed: use scripted ONLY when its trigger condition is met.
        int completed = (ShiftManager.Instance != null) ? ShiftManager.Instance.OrdersCompleted : 0;

        ScriptedOrder best = null;

        for (int i = 0; i < scriptedOrders.Length; i++)
        {
            var s = scriptedOrders[i];
            if (s == null || s.consumed) continue;

            bool match = false;

            if (s.trigger == ScriptedOrder.TriggerType.CustomerSpawnNumber)
            {
                match = (spawnedCount == Mathf.Max(1, s.customerSpawnNumber));
            }
            else if (s.trigger == ScriptedOrder.TriggerType.AfterOrdersCompleted)
            {
                match = (completed >= Mathf.Max(0, s.afterOrdersCompleted));
            }

            if (orderMode == OrderMode.ScriptedOnly)
            {
                // In ScriptedOnly mode, we don't require match — we just take next available.
                match = true;
            }

            if (!match) continue;

            // Prefer the earliest/most specific match in the list
            best = s;
            break;
        }

        // In Mixed, if nothing matched, return null (random order)
        return best;
    }

    bool IsCraftedOutput(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        return craftedInputCount.ContainsKey(itemId);
    }

    void RebuildCraftedLookup()
    {
        craftedInputCount.Clear();

        var benches = FindObjectsOfType<CraftingBench>(true);
        foreach (var bench in benches)
        {
            if (bench == null || bench.recipes == null) continue;

            foreach (var r in bench.recipes)
            {
                if (string.IsNullOrEmpty(r.output)) continue;

                int inputs = 0;
                if (!string.IsNullOrEmpty(r.inputA)) inputs++;
                if (!string.IsNullOrEmpty(r.inputB)) inputs++;
                if (!string.IsNullOrEmpty(r.inputC)) inputs++;

                if (inputs <= 0) continue;

                if (craftedInputCount.TryGetValue(r.output, out int existing))
                    craftedInputCount[r.output] = Mathf.Max(existing, inputs);
                else
                    craftedInputCount.Add(r.output, inputs);
            }
        }
    }

    int GetAutoCapForItem(string itemId)
    {
        if (!autoLimitCraftedOutputs) return maxTotalItems;

        if (craftedInputCount.TryGetValue(itemId, out int inputs))
        {
            int cap = inventorySlots - (inputs - 1);
            return Mathf.Clamp(cap, 1, maxTotalItems);
        }

        return maxTotalItems;
    }

    CustomerOrder.OrderLine[] GenerateRandomOrderLines(out int totalItems)
    {
        totalItems = 0;

        if (possibleItems == null || possibleItems.Length == 0)
        {
            totalItems = 1;
            return new CustomerOrder.OrderLine[]
            {
                new CustomerOrder.OrderLine { itemId = "Cube", amount = 1 }
            };
        }

        Dictionary<string, int> caps = new Dictionary<string, int>();
        for (int i = 0; i < possibleItems.Length; i++)
        {
            var opt = possibleItems[i];
            if (opt == null || string.IsNullOrEmpty(opt.itemId)) continue;

            int cap = maxTotalItems;

            if (opt.maxPerOrder > 0)
                cap = Mathf.Clamp(opt.maxPerOrder, 1, maxTotalItems);

            cap = Mathf.Min(cap, GetAutoCapForItem(opt.itemId));

            if (caps.TryGetValue(opt.itemId, out int existing))
                caps[opt.itemId] = Mathf.Min(existing, cap);
            else
                caps.Add(opt.itemId, cap);
        }

        int craftCapSum = 0, nonCraftCapSum = 0;
        foreach (var kvp in caps)
        {
            if (IsCraftedOutput(kvp.Key)) craftCapSum += Mathf.Max(0, kvp.Value);
            else nonCraftCapSum += Mathf.Max(0, kvp.Value);
        }

        int craftBudget = (maxCraftedOutputsPerOrder <= 0)
            ? craftCapSum
            : Mathf.Min(craftCapSum, maxCraftedOutputsPerOrder);

        int feasibleMax = Mathf.Clamp(nonCraftCapSum + craftBudget, 1, maxTotalItems);

        int minT = Mathf.Clamp(minTotalItems, 1, feasibleMax);
        int maxT = Mathf.Clamp(maxTotalItems, minT, feasibleMax);

        int targetTotal = UnityEngine.Random.Range(minT, maxT + 1);

        Dictionary<string, int> counts = new Dictionary<string, int>();
        int craftedCount = 0;

        for (int k = 0; k < targetTotal; k++)
        {
            string id = PickWeightedAvailableItemId(counts, caps, craftedCount);
            if (string.IsNullOrEmpty(id)) break;

            counts.TryGetValue(id, out int current);
            int cap = caps.TryGetValue(id, out int c) ? c : 0;
            if (cap <= 0) { k--; continue; }
            if (current >= cap) { k--; continue; }

            counts[id] = current + 1;
            if (maxCraftedOutputsPerOrder > 0 && IsCraftedOutput(id))
                craftedCount++;
        }

        var lines = new List<CustomerOrder.OrderLine>();
        int actualTotal = 0;

        foreach (var kvp in counts)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0) continue;
            actualTotal += kvp.Value;
            lines.Add(new CustomerOrder.OrderLine { itemId = kvp.Key, amount = kvp.Value });
        }

        if (actualTotal <= 0)
        {
            totalItems = 1;
            return new CustomerOrder.OrderLine[]
            {
                new CustomerOrder.OrderLine { itemId = "Cube", amount = 1 }
            };
        }

        totalItems = actualTotal;
        return lines.ToArray();
    }

    string PickWeightedAvailableItemId(Dictionary<string, int> counts, Dictionary<string, int> caps, int craftedCount)
    {
        int totalWeight = 0;

        for (int i = 0; i < possibleItems.Length; i++)
        {
            var opt = possibleItems[i];
            if (opt == null || string.IsNullOrEmpty(opt.itemId)) continue;

            int cap = caps.TryGetValue(opt.itemId, out int c) ? c : 0;
            if (cap <= 0) continue;

            counts.TryGetValue(opt.itemId, out int current);
            if (current >= cap) continue;

            if (maxCraftedOutputsPerOrder > 0 && IsCraftedOutput(opt.itemId) && craftedCount >= maxCraftedOutputsPerOrder)
                continue;

            totalWeight += Mathf.Max(1, opt.weight);
        }

        if (totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);

        for (int i = 0; i < possibleItems.Length; i++)
        {
            var opt = possibleItems[i];
            if (opt == null || string.IsNullOrEmpty(opt.itemId)) continue;

            int cap = caps.TryGetValue(opt.itemId, out int c) ? c : 0;
            if (cap <= 0) continue;

            counts.TryGetValue(opt.itemId, out int current);
            if (current >= cap) continue;

            if (maxCraftedOutputsPerOrder > 0 && IsCraftedOutput(opt.itemId) && craftedCount >= maxCraftedOutputsPerOrder)
                continue;

            roll -= Mathf.Max(1, opt.weight);
            if (roll < 0)
                return opt.itemId;
        }

        return null;
    }

    int CountTotalItems(CustomerOrder.OrderLine[] lines)
    {
        if (lines == null) return 0;
        int sum = 0;
        for (int i = 0; i < lines.Length; i++)
            sum += Mathf.Max(0, lines[i].amount);
        return sum;
    }
}
