using TMPro;
using UnityEngine;

public class OrderUI : MonoBehaviour
{
    public TextMeshProUGUI orderText;
    public string title = "Order:";

    void Awake()
    {
        if (orderText == null)
            orderText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        var om = OrderManager.Instance;
        if (orderText == null || om == null) return;

        if (!om.HasActiveOrder)
        {
            orderText.text = $"{title}\n-";
            return;
        }

        var lines = om.ActiveLines;
        int secs = Mathf.CeilToInt(om.TimeRemaining);

        string body = "";
        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i].itemId)) continue;
                body += $"{lines[i].itemId} x{Mathf.Max(1, lines[i].amount)}\n";
            }
        }

        orderText.text = $"{title}\n{body}Time: {secs}s";
    }
}
