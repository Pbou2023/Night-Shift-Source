using UnityEngine;

[ExecuteAlways]
public class VhsBlit : MonoBehaviour
{
    [Header("Refs")]
    public Material vhsMaterial;

    [Header("Intensity (0..1)")]
    [Range(0f, 1f)] public float intensity = 0f;

    static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    static readonly int TimeID = Shader.PropertyToID("_TimeX");

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (vhsMaterial == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        vhsMaterial.SetFloat(IntensityID, intensity);
        vhsMaterial.SetFloat(TimeID, Time.time);

        Graphics.Blit(src, dest, vhsMaterial);
    }
}
