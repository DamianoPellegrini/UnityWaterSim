using UnityEngine;

/// <summary>
/// Used to enable depth for edit-mode
/// </summary>
[ExecuteInEditMode]
public class EnableDepthTexture : MonoBehaviour
{
    void OnEnable()
    {
        Camera.main.depthTextureMode = DepthTextureMode.DepthNormals;
    }
}
