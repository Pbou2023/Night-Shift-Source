using TMPro;
using UnityEngine;

public class ScoreTextUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private string prefix = "Score: ";

    void Reset()
    {
        scoreText = GetComponent<TextMeshProUGUI>();
    }

    void Awake()
    {
        if (scoreText == null)
            scoreText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (scoreText == null) return;
        scoreText.text = prefix + ScoreManager.Score;
    }
}
