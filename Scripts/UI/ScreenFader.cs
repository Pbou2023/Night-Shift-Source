using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image image;
    [SerializeField] private float fadeDuration = 0.6f;

    Coroutine routine;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        if (!image) image = GetComponentInChildren<Image>(true);

        // Safety defaults
        if (group) group.alpha = 0f;
        if (group)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        // Optional but recommended: keep one fader across scenes
        DontDestroyOnLoad(gameObject);
    }

    public void FadeOutThenLoad(string sceneName)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeOutThenLoadRoutine(sceneName));
    }

    IEnumerator FadeOutThenLoadRoutine(string sceneName)
    {
        yield return FadeTo(1f);
        SceneManager.LoadScene(sceneName);
    }

    public IEnumerator FadeTo(float targetAlpha)
    {
        if (!group) yield break;

        float start = group.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }

        group.alpha = targetAlpha;
    }
}
