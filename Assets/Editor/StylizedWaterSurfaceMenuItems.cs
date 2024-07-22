using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

static class StylizedWaterSurfaceMenuItems
{
    [MenuItem("GameObject/3D Object/Stylized Water/Stylized Ocean")]
    static void CreateStylizedOcean(MenuCommand menuCommand)
    {
        var go = CoreEditorUtils.CreateGameObject("Stylized ocean", menuCommand.context);

        // Place it at origin and set its scale
        go.transform.position = new Vector3(0.0f, 0.0f, 0.0f);

        // Add the water surface component
        // TODO: uncommment after implementing
        // var waterSurface = go.AddComponent<StylizedWaterSurface>();
        // StylizedWaterSurfacePresets.ApplyWaterOceanPreset(waterSurface);
        var mesh = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();

        var shader = Resources.Load<Shader>("Runtime/Shaders/SumOfSines");
        meshRenderer.material = new Material(shader);
        var procGrid = go.AddComponent<ProceduralMesh>();
        procGrid.enabled = true;
    }
}
