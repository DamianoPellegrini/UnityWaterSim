using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMesh : MonoBehaviour
{
    [Range(1, short.MaxValue)]
    public int resolution = 10;
    public float sizeX = 5f;
    public float sizeY = 5f;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private void OnValidate()
    {
        GenerateMesh();
    }

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
    }

    private void GenerateMesh()
    {
        if (meshFilter == null)
            return;

        Mesh mesh = new Mesh();
        meshFilter.mesh = mesh;

        int vertexCount = (resolution + 1) * (resolution + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];

        int triCount = resolution * resolution * 6;
        int[] triangles = new int[triCount];

        float halfSizeX = sizeX * 0.5f;
        float halfSizeY = sizeY * 0.5f;

        float stepSizeX = sizeX / resolution;
        float stepSizeY = sizeY / resolution;

        // Generate vertices, normals, and UVs
        for (int z = 0, vi = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++, vi++)
            {
                float xPos = x * stepSizeX - halfSizeX;
                float zPos = z * stepSizeY - halfSizeY;

                vertices[vi] = new Vector3(xPos, 0, zPos);
                normals[vi] = Vector3.up;
                uv[vi] = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }

        // Generate triangles
        for (int ti = 0, vi = 0, z = 0; z < resolution; z++, vi++)
        {
            for (int x = 0; x < resolution; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + resolution + 1;
                triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 1;
                triangles[ti + 4] = vi + resolution + 1;
                triangles[ti + 5] = vi + resolution + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();

        // Update Mesh Collider if present
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }
}
