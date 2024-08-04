
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using Water;
using Water.Spectrum;

public class BuoyantMesh : MonoBehaviour
{
    public WaterSurface displacementSource;
    private Rigidbody rb;
    private MeshFilter meshFilter;

    public float buoyantForce = 1;
    public float underwaterDrag = 1;

    private float drag;
    
    private List<Vector3> vector3s = new List<Vector3>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        meshFilter = GetComponent<MeshFilter>();
        drag = rb.linearDamping;


        meshFilter.mesh.GetVertices(vector3s);
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        if (displacementSource == null) return;




        foreach (var point in vector3s)
        {
            ApplyBuoyantForce(point);
        }
    }

    void ApplyBuoyantForce(Vector3 position) {
        Vector3 displacement = displacementSource.GetWaterDisplacement(position);

            if (position.y <= displacement.y)
            {
                var gravity = displacementSource.spectrum.g;

                var windVector = new Vector2(0, 0);
                var windSpeed = 0.0f;
                if (displacementSource.spectrum is JONSWAPSpectrumSettings jonswap)
                {
                    windVector.x = -Mathf.Cos(jonswap.localBand.windDirection);
                    windVector.y = -Mathf.Sin(jonswap.localBand.windDirection);
                    windSpeed = jonswap.localBand.windSpeed;
                }
                else if (displacementSource.spectrum is PhillipsSpectrumSettings phillips)
                {
                    windVector.x = Mathf.Cos(phillips.windDirection);
                    windVector.y = Mathf.Sin(phillips.windDirection);
                    windSpeed = phillips.windSpeed;
                }

                var force = new Vector3(displacement.x * buoyantForce,
                                       (displacement.y - position.y) * buoyantForce * gravity,
                                         displacement.z * buoyantForce);

                force.x += windVector.x * windSpeed * 0.01f;
                force.z += windVector.y * windSpeed * 0.01f;

                if (vector3s.Count > 0)
                    force /= vector3s.Count;

                rb.linearDamping = underwaterDrag;
                rb.AddForceAtPosition(force, position, ForceMode.Acceleration);
            }
            else
            {
                rb.linearDamping = drag;
            }
    }
}
