using UnityEngine;

public class CodeClueDisplay : MonoBehaviour
{
    public enum Letter { A, B, C, D }

    [Header("Clue")]
    public Letter letter = Letter.A;
    public string format = "{0}-{1}"; // e.g. A-1

    [Header("Text Target")]
    [Tooltip("Optional. If empty, uses first TextMesh on this object or children.")]
    public Component textTarget;

    void OnEnable()
    {
        if (CodePuzzleManager.Instance != null)
            CodePuzzleManager.Instance.OnCodeGenerated += Refresh;
    }

    void OnDisable()
    {
        if (CodePuzzleManager.Instance != null)
            CodePuzzleManager.Instance.OnCodeGenerated -= Refresh;
    }

    void Start() => Refresh();

    public void Refresh()
    {
        var mgr = CodePuzzleManager.Instance;
        if (mgr == null) return;

        int digit = mgr.GetDigit(letter.ToString()[0]);
        string s = string.Format(format, letter.ToString(), digit);

        SetText(s);
    }

    void SetText(string value)
    {
        Component t = textTarget;

        if (t == null)
            t = GetComponentInChildren<TextMesh>();

        if (t == null)
            t = GetComponentInChildren<UnityEngine.UI.Text>();

        // TMP support via reflection (no compile dependency)
        if (t == null)
        {
            var tmpType = System.Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            if (tmpType != null)
            {
                var tmp = GetComponentInChildren(tmpType);
                if (tmp != null) t = (Component)tmp;
            }
        }

        if (t == null)
        {
            Debug.LogWarning("[CodeClueDisplay] No text component found to write clue.");
            return;
        }

        // TextMesh
        var tm = t as TextMesh;
        if (tm != null)
        {
            tm.text = value;
            return;
        }

        // UI.Text
        var uiText = t as UnityEngine.UI.Text;
        if (uiText != null)
        {
            uiText.text = value;
            return;
        }

        // TMP_Text via reflection
        var type = t.GetType();
        var prop = type.GetProperty("text");
        if (prop != null && prop.CanWrite)
            prop.SetValue(t, value);
    }
}
