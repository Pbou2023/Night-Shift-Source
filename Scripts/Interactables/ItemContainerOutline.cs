using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class InteractableOutline : MonoBehaviour
{
    [Header("Toggle")]
    [SerializeField] private bool enableOutline = true;

    [Tooltip("If true, outline is always visible (simple). If false, visibility rules below apply.")]
    [SerializeField] private bool alwaysOn = true;

    [Header("Visibility Rules (only used when Always On = false)")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float maxDistance = 12f;

    [Tooltip("If enabled, we only show outline when the object is inside the camera frustum.")]
    [SerializeField] private bool requireInFrustum = true;

    [Tooltip("If enabled, we raycast to check if walls block line of sight.")]
    [SerializeField] private bool requireLineOfSight = true;

    [Tooltip("Layers that can block visibility (walls, props). Don't include the container layer.")]
    [SerializeField] private LayerMask occlusionMask = ~0;

    [Tooltip("How often to update visibility checks (seconds). Higher = cheaper.")]
    [SerializeField] private float visibilityCheckInterval = 0.08f;

    [Header("Outline Look")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField, Min(0f)] private float thicknessWorld = 0.02f;

    private readonly List<Renderer> sourceRenderers = new();
    private readonly List<Renderer> outlineRenderers = new();

    private float nextCheckTime;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");

    void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;

        CollectRenderers();
        BuildOutlines();
        ApplyMaterialProps();

        UpdateOutlineState(force: true);
    }

    void OnEnable()
    {
        UpdateOutlineState(force: true);
    }

    void OnDisable()
    {
        SetOutlineVisible(false);
    }

    void Update()
    {
        if (!enableOutline)
        {
            SetOutlineVisible(false);
            return;
        }

        if (alwaysOn)
        {
            SetOutlineVisible(true);
            return;
        }

        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + visibilityCheckInterval;

        bool visible = IsVisibleToPlayerCamera();
        SetOutlineVisible(visible);
    }

    public void SetEnabled(bool enabled)
    {
        enableOutline = enabled;
        UpdateOutlineState(force: true);
    }

    public void SetAlwaysOn(bool value)
    {
        alwaysOn = value;
        UpdateOutlineState(force: true);
    }

    private void UpdateOutlineState(bool force)
    {
        if (!enableOutline)
        {
            SetOutlineVisible(false);
            return;
        }

        if (alwaysOn)
        {
            SetOutlineVisible(true);
            return;
        }

        if (force) nextCheckTime = 0f; // force immediate re-eval
    }

    private void CollectRenderers()
    {
        sourceRenderers.Clear();

        // MeshRenderer + SkinnedMeshRenderer both derive from Renderer
        GetComponentsInChildren(true, sourceRenderers);

        // Don’t outline already-created outline hulls
        sourceRenderers.RemoveAll(r => r.gameObject.name.EndsWith("_OUTLINE", System.StringComparison.Ordinal));
    }

    private void BuildOutlines()
    {
        outlineRenderers.Clear();

        if (!outlineMaterial)
        {
            Debug.LogWarning($"[{nameof(InteractableOutline)}] No outlineMaterial assigned on {name}. Outline will not render.");
            return;
        }

        foreach (var r in sourceRenderers)
        {
            if (!r) continue;

            // Duplicate renderer object as a child
            var go = new GameObject(r.gameObject.name + "_OUTLINE");
            go.transform.SetParent(r.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = r.gameObject.layer; // IMPORTANT

            Renderer outlineR = null;

            // Handle SkinnedMeshRenderer
            if (r is SkinnedMeshRenderer smr)
            {
                var newSmr = go.AddComponent<SkinnedMeshRenderer>();
                newSmr.sharedMesh = smr.sharedMesh;
                newSmr.rootBone = smr.rootBone;
                newSmr.bones = smr.bones;
                newSmr.updateWhenOffscreen = true;
                outlineR = newSmr;
            }
            else if (r is MeshRenderer)
            {
                // Need MeshFilter too
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) { Destroy(go); continue; }

                var newMf = go.AddComponent<MeshFilter>();
                newMf.sharedMesh = mf.sharedMesh;

                var newMr = go.AddComponent<MeshRenderer>();
                outlineR = newMr;
            }
            else
            {
                Destroy(go);
                continue;
            }

            outlineR.sharedMaterial = outlineMaterial;
            outlineR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineR.receiveShadows = false;

            // Ensure outline draws even if the original renderer is culled by LOD etc.
            outlineR.enabled = enableOutline;

            outlineRenderers.Add(outlineR);
        }
    }

    private void ApplyMaterialProps()
    {
        if (!outlineMaterial) return;

        // NOTE: This edits the material asset reference.
        // If you want per-object unique colors, use material instances instead.
        outlineMaterial.SetColor(BaseColorId, outlineColor);
        outlineMaterial.SetFloat(ThicknessId, thicknessWorld);
    }

    private bool IsVisibleToPlayerCamera()
    {
        if (!playerCamera) return false;

        // Distance gate (fast)
        float sqrMax = maxDistance * maxDistance;
        Vector3 camPos = playerCamera.transform.position;
        Vector3 center = GetCombinedBoundsCenter();
        if ((center - camPos).sqrMagnitude > sqrMax) return false;

        // Frustum gate
        if (requireInFrustum)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
            var b = GetCombinedBounds();
            if (!GeometryUtility.TestPlanesAABB(planes, b)) return false;
        }

        // Occlusion gate
        if (requireLineOfSight)
        {
            Vector3 dir = (center - camPos);
            float dist = dir.magnitude;
            if (dist < 0.0001f) return true;

            dir /= dist;

            // Raycast and see if something blocks it before we reach the container
            if (Physics.Raycast(camPos, dir, out RaycastHit hit, dist, occlusionMask, QueryTriggerInteraction.Ignore))
            {
                // If we hit something that's NOT part of this object hierarchy, it's occluded.
                if (!hit.transform.IsChildOf(transform)) return false;
            }
        }

        return true;
    }

    private Bounds GetCombinedBounds()
    {
        bool has = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);

        foreach (var r in sourceRenderers)
        {
            if (!r) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!has) b = new Bounds(transform.position, Vector3.one * 0.25f);
        return b;
    }

    private Vector3 GetCombinedBoundsCenter() => GetCombinedBounds().center;

    private void SetOutlineVisible(bool visible)
    {
        for (int i = 0; i < outlineRenderers.Count; i++)
        {
            if (outlineRenderers[i]) outlineRenderers[i].enabled = visible;
        }
    }
}
