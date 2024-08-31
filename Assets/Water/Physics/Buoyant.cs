using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using Water;
using Water.Spectrum;

public class Buoyant : MonoBehaviour
{
    public WaterSurface displacementSource;
    private Rigidbody rb;
    private MeshFilter mf;
    private BuoyantPoint[] buoyantPoints;

    public float buoyantForce = 1;
    public float underwaterDrag = 1;

    private float drag;

    public Vector3 Test;

    public float WaterMass = 1000000;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mf = GetComponent<MeshFilter>();
        drag = rb.linearDamping;

        buoyantPoints = GetComponentsInChildren<BuoyantPoint>();
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        if (displacementSource == null) return;

        if (buoyantPoints.Length == 0)
        {
            ApplyBuoyantForce(transform.position);
        }

        foreach (var point in buoyantPoints)
        {
            ApplyBuoyantForce(point.transform.position);
        }
    }

    void ApplyBuoyantForce(Vector3 position)
    {
        Vector3 displacement = displacementSource.GetWaterDisplacement(position);

        var gravity = displacementSource.spectrum.g;

        var windVector = new Vector2(0, 0);
        var windSpeed = 0.0f;
        if (displacementSource.spectrum is JONSWAPSpectrumSettings jonswap)
        {
            // 0 -x cos(0) = 1 sin(0) = 0
            // 90 -z cos(90) = 0
            var localWind = new Vector2(
                Mathf.Cos(Mathf.Deg2Rad * jonswap.localBand.windDirection),
                Mathf.Sin(Mathf.Deg2Rad * jonswap.localBand.windDirection)
            ) * jonswap.localBand.scale;

            var swellWind = new Vector2(
                Mathf.Cos(Mathf.Deg2Rad * jonswap.swellBand.windDirection),
                Mathf.Sin(Mathf.Deg2Rad * jonswap.swellBand.windDirection)
            ) * jonswap.swellBand.scale;

            windVector = -(localWind + swellWind);
            var doa = localWind.x * swellWind.x + localWind.y * swellWind.y;
            windSpeed = doa * jonswap.localBand.windSpeed + jonswap.swellBand.windSpeed;
        }
        else if (displacementSource.spectrum is PhillipsSpectrumSettings phillips)
        {
            windVector.x = Mathf.Cos(Mathf.Deg2Rad * phillips.windDirection);
            windVector.y = Mathf.Sin(Mathf.Deg2Rad * phillips.windDirection);
            windSpeed = phillips.windSpeed;
        }
        windVector.Normalize();


        // TODO: See atlas slides for more info

        var force = new Vector3(displacement.x * buoyantForce,
                               (displacement.y - position.y) * buoyantForce * gravity,
                                 displacement.z * buoyantForce);

        force.x += windVector.x * windSpeed * 0.1f;
        force.z += windVector.y * windSpeed * 0.1f;

        if (buoyantPoints.Length > 0)
            force /= buoyantPoints.Length;
        Test = force;

        rb.linearDamping = underwaterDrag;
        rb.AddForceAtPosition(force, position, ForceMode.Acceleration);
    }
}
