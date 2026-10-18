using UnityEngine;

public static class ScoreManager
{
    public static int Score { get; private set; }

    public static void Add(int amount)
    {
        Score += amount;
        Debug.Log($"[SCORE] {Score}");
    }
}
