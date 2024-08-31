using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Water;

[RequireComponent(typeof(WaterSurface))]
public class TestOceanMesh : MonoBehaviour
{
    private WaterSurface waterSurface;
    public Transform viewer;
    public Material oceanMaterial;
    public bool updateMaterialProperties;
    public bool showMaterialLods;

    public float lengthScale = 10;
    [Range(1, 100)]
    public int vertexDensity = 30;
    [Range(0, 8)]
    public int clipLevels = 8;
    [Range(0, 100)]
    public float skirtSize = 50;

    List<Element> rings = new List<Element>();
    List<Element> trims = new List<Element>();
    Element center;
    Element skirt;
    Quaternion[] trimRotations;
    int previousVertexDensity;
    float previousSkirtSize;

    Material[] materials;

    Camera _depthCam;
    RenderTexture _depthTex;

    private void Awake()
    {
        waterSurface = GetComponent<WaterSurface>();
    }

    private void Start()
    {
        if (viewer == null)
            viewer = Camera.main.transform;

        CaptureDepthMap();

        oceanMaterial.SetTexture("_Displacement_c0", waterSurface.cascade0.displacement);
        oceanMaterial.SetTexture("_Derivatives_c0", waterSurface.cascade0.derivatives);
        oceanMaterial.SetTexture("_Turbulence_c0", waterSurface.cascade0.turbulence);

        oceanMaterial.SetTexture("_Displacement_c1", waterSurface.cascade1.displacement);
        oceanMaterial.SetTexture("_Derivatives_c1", waterSurface.cascade1.derivatives);
        oceanMaterial.SetTexture("_Turbulence_c1", waterSurface.cascade1.turbulence);

        oceanMaterial.SetTexture("_Displacement_c2", waterSurface.cascade2.displacement);
        oceanMaterial.SetTexture("_Derivatives_c2", waterSurface.cascade2.derivatives);
        oceanMaterial.SetTexture("_Turbulence_c2", waterSurface.cascade2.turbulence);


        materials = new Material[3];
        materials[0] = new Material(oceanMaterial);
        materials[0].EnableKeyword("CLOSE");

        materials[1] = new Material(oceanMaterial);
        materials[1].EnableKeyword("MID");
        materials[1].DisableKeyword("CLOSE");

        materials[2] = new Material(oceanMaterial);
        materials[2].DisableKeyword("MID");
        materials[2].DisableKeyword("CLOSE");

        trimRotations = new Quaternion[]
        {
            Quaternion.AngleAxis(180, Vector3.up),
            Quaternion.AngleAxis(90, Vector3.up),
            Quaternion.AngleAxis(270, Vector3.up),
            Quaternion.identity,
        };

        InstantiateMeshes();
    }

    private void Update()
    {
        if (rings.Count != clipLevels || trims.Count != clipLevels
            || previousVertexDensity != vertexDensity || !Mathf.Approximately(previousSkirtSize, skirtSize))
        {
            InstantiateMeshes();
            previousVertexDensity = vertexDensity;
            previousSkirtSize = skirtSize;
        }

        UpdatePositions();
        UpdateMaterials();
    }

    void UpdateMaterials()
    {
        if (updateMaterialProperties && !showMaterialLods)
        {
            for (int i = 0; i < 3; i++)
            {
                materials[i].CopyPropertiesFromMaterial(oceanMaterial);
            }
            materials[0].EnableKeyword("CLOSE");
            materials[1].EnableKeyword("MID");
            materials[1].DisableKeyword("CLOSE");
            materials[2].DisableKeyword("MID");
            materials[2].DisableKeyword("CLOSE");
        }
        if (showMaterialLods)
        {
            materials[0].SetColor("_Color", Color.red * 0.6f);
            materials[1].SetColor("_Color", Color.green * 0.6f);
            materials[2].SetColor("_Color", Color.blue * 0.6f);
        }

        int activeLevels = ActiveLodlevels();
        center.MeshRenderer.material = GetMaterial(clipLevels - activeLevels - 1);

        for (int i = 0; i < rings.Count; i++)
        {
            rings[i].MeshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
            trims[i].MeshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
        }
    }

    Material GetMaterial(int lodLevel)
    {
        if (lodLevel - 2 <= 0)
            return materials[0];

        if (lodLevel - 2 <= 2)
            return materials[1];

        return materials[2];
    }

    void UpdatePositions()
    {
        int k = GridSize();
        int activeLevels = ActiveLodlevels();

        float scale = ClipLevelScale(-1, activeLevels);
        Vector3 previousSnappedPosition = Snap(viewer.position, scale * 2);
        center.Transform.position = previousSnappedPosition + OffsetFromCenter(-1, activeLevels);
        center.Transform.localScale = new Vector3(scale, 1, scale);

        for (int i = 0; i < clipLevels; i++)
        {
            rings[i].Transform.gameObject.SetActive(i < activeLevels);
            trims[i].Transform.gameObject.SetActive(i < activeLevels);
            if (i >= activeLevels) continue;

            scale = ClipLevelScale(i, activeLevels);
            Vector3 centerOffset = OffsetFromCenter(i, activeLevels);
            Vector3 snappedPosition = Snap(viewer.position, scale * 2);

            Vector3 trimPosition = centerOffset + snappedPosition + scale * (k - 1) / 2 * new Vector3(1, 0, 1);
            int shiftX = previousSnappedPosition.x - snappedPosition.x < float.Epsilon ? 1 : 0;
            int shiftZ = previousSnappedPosition.z - snappedPosition.z < float.Epsilon ? 1 : 0;
            trimPosition += shiftX * (k + 1) * scale * Vector3.right;
            trimPosition += shiftZ * (k + 1) * scale * Vector3.forward;
            trims[i].Transform.position = trimPosition;
            trims[i].Transform.rotation = trimRotations[shiftX + 2 * shiftZ];
            trims[i].Transform.localScale = new Vector3(scale, 1, scale);

            rings[i].Transform.position = snappedPosition + centerOffset;
            rings[i].Transform.localScale = new Vector3(scale, 1, scale);
            previousSnappedPosition = snappedPosition;
        }

        scale = lengthScale * 2 * Mathf.Pow(2, clipLevels);
        skirt.Transform.position = new Vector3(-1, 0, -1) * scale * (skirtSize + 0.5f - 0.5f / GridSize()) + previousSnappedPosition;
        skirt.Transform.localScale = new Vector3(scale, 1, scale);
    }

    int ActiveLodlevels()
    {
        return clipLevels - Mathf.Clamp((int)Mathf.Log((1.7f * Mathf.Abs(viewer.position.y) + 1) / lengthScale, 2), 0, clipLevels);
    }

    float ClipLevelScale(int level, int activeLevels)
    {
        return lengthScale / GridSize() * Mathf.Pow(2, clipLevels - activeLevels + level + 1);
    }

    Vector3 OffsetFromCenter(int level, int activeLevels)
    {
        return (Mathf.Pow(2, clipLevels) + GeometricProgressionSum(2, 2, clipLevels - activeLevels + level + 1, clipLevels - 1))
               * lengthScale / GridSize() * (GridSize() - 1) / 2 * new Vector3(-1, 0, -1);
    }

    float GeometricProgressionSum(float b0, float q, int n1, int n2)
    {
        return b0 / (1 - q) * (Mathf.Pow(q, n2) - Mathf.Pow(q, n1));
    }

    int GridSize()
    {
        return 4 * vertexDensity + 1;
    }

    Vector3 Snap(Vector3 coords, float scale)
    {
        if (coords.x >= 0)
            coords.x = Mathf.Floor(coords.x / scale) * scale;
        else
            coords.x = Mathf.Ceil((coords.x - scale + 1) / scale) * scale;

        if (coords.z < 0)
            coords.z = Mathf.Floor(coords.z / scale) * scale;
        else
            coords.z = Mathf.Ceil((coords.z - scale + 1) / scale) * scale;

        coords.y = 0;
        return coords;
    }

    void InstantiateMeshes()
    {
        foreach (var child in gameObject.GetComponentsInChildren<Transform>())
        {
            if (child != transform)
                Destroy(child.gameObject);
        }
        rings.ForEach(r => Destroy(r.Transform.gameObject));
        trims.ForEach(t => Destroy(t.Transform.gameObject));
        rings.Clear();
        trims.Clear();

        int k = GridSize();
        center = InstantiateElement("Center", CreatePlaneMesh(2 * k, 2 * k, 1, Seams.All), materials[materials.Length - 1]);
        Mesh ring = CreateRingMesh(k, 1);
        Mesh trim = CreateTrimMesh(k, 1);
        for (int i = 0; i < clipLevels; i++)
        {
            rings.Add(InstantiateElement("Ring " + i, ring, materials[materials.Length - 1]));
            trims.Add(InstantiateElement("Trim " + i, trim, materials[materials.Length - 1]));
        }
        skirt = InstantiateElement("Skirt", CreateSkirtMesh(k, skirtSize), materials[materials.Length - 1]);
    }

    Element InstantiateElement(string name, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject();
        go.name = name;
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        meshRenderer.material = mat;
        meshRenderer.allowOcclusionWhenDynamic = false;
        return new Element(go.transform, meshRenderer);
    }

    Mesh CreateSkirtMesh(int k, float outerBorderScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap skirt";
        CombineInstance[] combine = new CombineInstance[8];

        Mesh quad = CreatePlaneMesh(1, 1, 1);
        Mesh hStrip = CreatePlaneMesh(k, 1, 1);
        Mesh vStrip = CreatePlaneMesh(1, k, 1);


        Vector3 cornerQuadScale = new Vector3(outerBorderScale, 1, outerBorderScale);
        Vector3 midQuadScaleVert = new Vector3(1f / k, 1, outerBorderScale);
        Vector3 midQuadScaleHor = new Vector3(outerBorderScale, 1, 1f / k);

        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, cornerQuadScale);
        combine[0].mesh = quad;

        combine[1].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale, Quaternion.identity, midQuadScaleVert);
        combine[1].mesh = hStrip;

        combine[2].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[2].mesh = quad;

        combine[3].transform = Matrix4x4.TRS(Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
        combine[3].mesh = vStrip;

        combine[4].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
            + Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
        combine[4].mesh = vStrip;

        combine[5].transform = Matrix4x4.TRS(Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[5].mesh = quad;

        combine[6].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale
            + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, midQuadScaleVert);
        combine[6].mesh = hStrip;

        combine[7].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
            + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[7].mesh = quad;
        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    Mesh CreateTrimMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap trim";
        CombineInstance[] combine = new CombineInstance[2];

        combine[0].mesh = CreatePlaneMesh(k + 1, 1, lengthScale, Seams.None, 1);
        combine[0].transform = Matrix4x4.TRS(new Vector3(-k - 1, 0, -1) * lengthScale, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePlaneMesh(1, k, lengthScale, Seams.None, 1);
        combine[1].transform = Matrix4x4.TRS(new Vector3(-1, 0, -k - 1) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    Mesh CreateRingMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap ring";
        if ((2 * k + 1) * (2 * k + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        CombineInstance[] combine = new CombineInstance[4];

        combine[0].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left);
        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Top | Seams.Right | Seams.Left);
        combine[1].transform = Matrix4x4.TRS(new Vector3(0, 0, k + 1 + (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[2].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Left);
        combine[2].transform = Matrix4x4.TRS(new Vector3(0, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[3].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Right);
        combine[3].transform = Matrix4x4.TRS(new Vector3(k + 1 + (k - 1) / 2, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    Mesh CreatePlaneMesh(int width, int height, float lengthScale, Seams seams = Seams.None, int trianglesShift = 0)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap plane";
        if ((width + 1) * (height + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        int[] triangles = new int[width * height * 2 * 3];
        Vector3[] normals = new Vector3[(width + 1) * (height + 1)];

        for (int i = 0; i < height + 1; i++)
        {
            for (int j = 0; j < width + 1; j++)
            {
                int x = j;
                int z = i;

                if ((i == 0 && seams.HasFlag(Seams.Bottom)) || (i == height && seams.HasFlag(Seams.Top)))
                    x = x / 2 * 2;
                if ((j == 0 && seams.HasFlag(Seams.Left)) || (j == width && seams.HasFlag(Seams.Right)))
                    z = z / 2 * 2;

                vertices[j + i * (width + 1)] = new Vector3(x, 0, z) * lengthScale;
                normals[j + i * (width + 1)] = Vector3.up;
            }
        }

        int tris = 0;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                int k = j + i * (width + 1);
                if ((i + j + trianglesShift) % 2 == 0)
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;

                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 2;
                    triangles[tris++] = k + 1;
                }
                else
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + 1;

                    triangles[tris++] = k + 1;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        return mesh;
    }

    private void CaptureDepthMap()
    {
        //Generate the camera
        if (_depthCam == null)
        {
            var go =
                new GameObject("WaterDepthCamera") { hideFlags = HideFlags.DontSave }; //create the cameraObject
            _depthCam = go.AddComponent<Camera>();
        }

        var additionalCamData = _depthCam.GetUniversalAdditionalCameraData();
        additionalCamData.renderShadows = false;
        additionalCamData.requiresColorOption = CameraOverrideOption.Off;
        additionalCamData.requiresDepthOption = CameraOverrideOption.Off;

        var t = _depthCam.transform;
        var depthExtra = 4.0f;
        t.position = Vector3.up * (transform.position.y + depthExtra);//center the camera on this water plane height
        t.up = Vector3.forward;//face the camera down

        _depthCam.enabled = true;
        _depthCam.orthographic = true;
        _depthCam.orthographicSize = 256;//hardcoded = 1k area - TODO
        _depthCam.nearClipPlane = 0.01f;
        _depthCam.farClipPlane = 20 /*MaxDepth*/ + depthExtra;
        _depthCam.allowHDR = false;
        _depthCam.allowMSAA = false;
        _depthCam.cullingMask = 1 << 10;
        //Generate RT
        if (!_depthTex)
            _depthTex = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
        {
            _depthTex.filterMode = FilterMode.Point;
        }
        _depthTex.wrapMode = TextureWrapMode.Clamp;
        _depthTex.name = "WaterDepthMap";
        //do depth capture
        _depthCam.targetTexture = _depthTex;
        _depthCam.Render();

        oceanMaterial.SetTexture("_WaterDepthMap", _depthTex);
        // set depth bufferParams for depth cam(since it doesnt exist and only temporary)
        var _params = new Vector4(t.position.y, 256, 0, 0);
        //Vector4 zParams = new Vector4(1-f/n, f/n, (1-f/n)/f, (f/n)/f);//2015
        oceanMaterial.SetVector("_WaterZBufferParams", _params);

#if UNITY_EDITOR
        Texture2D tex2D = new Texture2D(1024, 1024, TextureFormat.Alpha8, false);
        Graphics.CopyTexture(_depthTex, tex2D);
        // byte[] image = tex2D.EncodeToPNG();
        // System.IO.File.WriteAllBytes(Application.dataPath + "/WaterDepth.png", image);
        if (AssetDatabase.FindAssets("Assets/Depth.asset").Length == 0)
            AssetDatabase.CreateAsset(tex2D, "Assets/Depth.asset");
        else
            AssetDatabase.SaveAssetIfDirty(tex2D);
#endif

        _depthCam.enabled = false;
        _depthCam.targetTexture = null;
    }

    class Element
    {
        public Transform Transform;
        public MeshRenderer MeshRenderer;

        public Element(Transform transform, MeshRenderer meshRenderer)
        {
            Transform = transform;
            MeshRenderer = meshRenderer;
        }
    }


    [System.Flags]
    enum Seams
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        All = Left | Right | Top | Bottom
    };
}
