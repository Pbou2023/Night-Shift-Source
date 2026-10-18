using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialTipUI : MonoBehaviour
{
    public enum Channel { Crouch, Crafting }

    [System.Serializable]
    public class ChannelUI
    {
        public Channel channel;
        public TMP_Text tipText;
        public CanvasGroup canvasGroup;
    }

    public static TutorialTipUI Instance;

    [Header("Channels")]
    public ChannelUI[] channels;

    [Header("Timing")]
    public float defaultShowSeconds = 4f;
    public float fadeSpeed = 10f;

    [Header("One-time")]
    public bool persistAcrossSessions = false;
    public string prefsPrefix = "TIP_";

    readonly Dictionary<Channel, ChannelUI> map = new();
    readonly HashSet<string> shownThisSession = new();
    Dictionary<Channel, Coroutine> running = new();

    void Awake()
    {
        Instance = this;

        map.Clear();
        foreach (var c in channels)
        {
            if (c == null || c.tipText == null || c.canvasGroup == null) continue;
            map[c.channel] = c;
            c.canvasGroup.alpha = 0f;
            c.canvasGroup.blocksRaycasts = false;
            c.canvasGroup.interactable = false;
        }
    }

    public static void ShowOnce(Channel channel, string key, string message, float seconds = -1f)
    {
        if (Instance == null) return;
        Instance.ShowInternal(channel, key, message, seconds);
    }

    void ShowInternal(Channel channel, string key, string message, float seconds)
    {
        if (string.IsNullOrEmpty(key)) key = message;

        string prefKey = prefsPrefix + key;

        if (persistAcrossSessions)
        {
            if (PlayerPrefs.GetInt(prefKey, 0) == 1) return;
            PlayerPrefs.SetInt(prefKey, 1);
        }
        else
        {
            if (shownThisSession.Contains(key)) return;
            shownThisSession.Add(key);
        }

        if (!map.TryGetValue(channel, out var ui)) return;

        if (running.TryGetValue(channel, out var co) && co != null)
            StopCoroutine(co);

        running[channel] = StartCoroutine(RunTip(ui, message, seconds > 0 ? seconds : defaultShowSeconds));

    }

    IEnumerator RunTip(ChannelUI ui, string msg, float showSeconds)
    {
        ui.tipText.text = msg;

        // fade in
        while (ui.canvasGroup.alpha < 0.99f)
        {
            ui.canvasGroup.alpha = Mathf.MoveTowards(ui.canvasGroup.alpha, 1f, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
        ui.canvasGroup.alpha = 1f;

        float t = 0f;
        while (t < showSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // fade out
        while (ui.canvasGroup.alpha > 0.01f)
        {
            ui.canvasGroup.alpha = Mathf.MoveTowards(ui.canvasGroup.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
        ui.canvasGroup.alpha = 0f;
    }
}
