using UnityEngine;

public class LockHipsTranslation : MonoBehaviour
{
    [Header("Auto-find")]
    [SerializeField] private string hipsName = "mixamorig:Hips";
    [SerializeField] private bool searchInactive = true;

    [Header("What to lock")]
    public bool lockX = true;
    public bool lockZ = true;
    public bool lockY = false; // keep Y bob by default

    Transform hips;
    Vector3 initialLocalPos;
    bool bound;

    void OnEnable()
    {
        TryBind();
    }

    // Call this after you spawn/replace the visual model
    public void Rebind()
    {
        bound = false;
        hips = null;
        TryBind();
    }

    void TryBind()
    {
        if (bound) return;

        hips = FindChildByName(transform, hipsName, searchInactive);
        if (!hips) return;

        initialLocalPos = hips.localPosition;
        bound = true;
    }

    void LateUpdate()
    {
        // model might spawn later, so keep trying until found
        if (!bound)
        {
            TryBind();
            return;
        }

        var p = hips.localPosition;
        if (lockX) p.x = initialLocalPos.x;
        if (lockY) p.y = initialLocalPos.y;
        if (lockZ) p.z = initialLocalPos.z;
        hips.localPosition = p;
    }

    static Transform FindChildByName(Transform root, string containsName, bool includeInactive)
    {
        // includeInactive is only relevant for GetComponentsInChildren
        var all = root.GetComponentsInChildren<Transform>(includeInactive);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == containsName) return all[i];
        }

        // fallback: contains search (some rigs add suffixes)
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != null && all[i].name.Contains(containsName))
                return all[i];
        }

        return null;
    }
}
