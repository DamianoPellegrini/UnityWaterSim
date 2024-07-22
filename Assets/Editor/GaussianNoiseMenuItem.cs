using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

static class GaussianNoiseMenuItem
{
    static float NormalRandom()
    {
        return Mathf.Cos(2 * Mathf.PI * Random.value) * Mathf.Sqrt(-2 * Mathf.Log(Random.value));
    }

    [MenuItem("Assets/Create/Noise/Gaussian noise")]
    static void CreateStylizedOcean(MenuCommand menuCommand)
    {
        var size = 1024;

        Texture2D noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true);
        noise.filterMode = FilterMode.Point;
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom()));
            }
        }
        noise.Apply();

        var filename = "GaussianNoiseTexture" + size.ToString() + "x" + size.ToString();
        var path = "Assets/";
        AssetDatabase.CreateAsset(noise, path + filename + ".asset");
        Debug.Log("Texture \"" + filename + "\" was created at path \"" + path + "\".");
    }
}
