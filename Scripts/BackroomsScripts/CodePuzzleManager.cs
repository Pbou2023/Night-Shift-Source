using System;
using System.Collections.Generic;
using UnityEngine;

public class CodePuzzleManager : MonoBehaviour
{
    public static CodePuzzleManager Instance { get; private set; }

    [Header("Generation")]
    [Tooltip("If true, all 4 digits will be unique.")]
    public bool uniqueDigits = false;

    [Tooltip("If true, generates in Awake. If false, call Regenerate() manually.")]
    public bool generateOnAwake = true;

    [Header("Debug")]
    [SerializeField] private int[] digits = new int[4]; // A,B,C,D

    public event Action OnCodeGenerated;

    public int A => digits[0];
    public int B => digits[1];
    public int C => digits[2];
    public int D => digits[3];

    public string CodeString => $"{A}{B}{C}{D}";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (generateOnAwake)
            Regenerate();
    }

    public void Regenerate()
    {
        if (uniqueDigits)
        {
            // sample 4 unique digits from 0..9
            List<int> pool = new List<int>(10);
            for (int i = 0; i <= 9; i++) pool.Add(i);

            for (int k = 0; k < 4; k++)
            {
                int pick = UnityEngine.Random.Range(0, pool.Count);
                digits[k] = pool[pick];
                pool.RemoveAt(pick);
            }
        }
        else
        {
            for (int k = 0; k < 4; k++)
                digits[k] = UnityEngine.Random.Range(0, 10);
        }

        Debug.Log($"[CodePuzzle] Generated: A-{A} B-{B} C-{C} D-{D} => Code {CodeString}");
        OnCodeGenerated?.Invoke();
    }

    public int GetDigit(char letter)
    {
        switch (char.ToUpperInvariant(letter))
        {
            case 'A': return A;
            case 'B': return B;
            case 'C': return C;
            case 'D': return D;
            default: return -1;
        }
    }

    public bool IsCorrect(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        // strip spaces just in case
        input = input.Trim();

        return input == CodeString;
    }
}
