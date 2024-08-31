using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Water.Physics
{
    [RequireComponent(typeof(Rigidbody))]
    public class BuoyantBody : MonoBehaviour
    {
        const float kDampner = 0.005f;
        const float kWaterDensity = 1000;

        public WaterSurface displacementSource;
        Rigidbody rb;
        Collider[] colliders;

        [NonSerialized] public float Density;
        [NonSerialized] public float Volume;
        [NonSerialized] public float PercentSubmerged;

        public float voxelResolution = 0.51f;

        public Vector3 centerOfMassOffset = Vector3.zero;

        Bounds voxelBounds;

        float baseDrag;
        float baseAngularDrag;
        int guid;
        float3 localArchimedesForce;

        Vector3[] voxels;

        NativeArray<float3> samplePoints;
        float3[] velocity;

        Bounds VoxelBounds()
        {
            var bounds = new Bounds();
            foreach (var nextCollider in colliders)
            {
                bounds.Encapsulate(nextCollider.bounds);
            }
            return bounds;
        }

        struct DebugDrawing
        {
            public Vector3 Force;
            public Vector3 Position;
            public float WaterHeight;
        }

        DebugDrawing[] debugInfo;

        void OnEnable()
        {
            guid = gameObject.GetInstanceID();
            rb = GetComponent<Rigidbody>();
            SetupColliders();
            SetupVoxels();
            SetupArrays();
            SetupPhysics();
        }

        void Update()
        {
            LocalToWorldJob.CompleteJob(guid);
            // Get velocity at sample points
            for (var i = 0; i < voxels.Length; i++) { velocity[i] = rb.GetPointVelocity(samplePoints[i]); }
        }

        void FixedUpdate()
        {
            LocalToWorldJob.CompleteJob(guid);
            var submergedAmount = 0f;

            // Apply all forces and update once all have been applied
            UnityEngine.Physics.autoSyncTransforms = false;
            for (var i = 0; i < voxels.Length; i++)
            {
                BuoyancyForce(samplePoints[i], velocity[i], displacementSource.GetWaterHeight(samplePoints[i]), ref submergedAmount, ref debugInfo[i]);
            }
            UnityEngine.Physics.SyncTransforms();
            UnityEngine.Physics.autoSyncTransforms = true;

            UpdateDrag(submergedAmount);
        }

        void LateUpdate()
        {
            var transformMatrix = transform.localToWorldMatrix;
            LocalToWorldJob.ScheduleJob(guid, transformMatrix);
        }

        void OnDisable()
        {
            LocalToWorldJob.Cleanup(guid);
        }

        void OnDrawGizmosSelected()
        {
            LocalToWorldJob.CompleteJob(guid);
            const float kGizmoSize = 0.05f;
            var t = transform;
            var matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);

            // Draw voxels
            if (samplePoints != null)
            {
                Gizmos.color = Color.yellow;

                foreach (var p in samplePoints)
                {
                    Gizmos.DrawCube(p, new Vector3(voxelResolution - kGizmoSize, voxelResolution - kGizmoSize, voxelResolution - kGizmoSize));
                }
            }

            // Draw bounds
            Gizmos.matrix = matrix;
            if (voxelResolution >= 0.1f)
            {
                Gizmos.DrawWireCube(voxelBounds.center, voxelBounds.size);
                Vector3 center = voxelBounds.center;

                // Draw bottom grid
                float y = center.y - voxelBounds.extents.y;
                for (float x = -voxelBounds.extents.x + voxelResolution; x < voxelBounds.extents.x; x += voxelResolution)
                {
                    Gizmos.DrawLine(new Vector3(x, y, center.z - voxelBounds.extents.z), new Vector3(x, y, center.z + voxelBounds.extents.z));
                }
                for (float z = -voxelBounds.extents.z + voxelResolution; z < voxelBounds.extents.z; z += voxelResolution)
                {
                    Gizmos.DrawLine(new Vector3(center.x - voxelBounds.extents.x, y, z), new Vector3(center.x + voxelBounds.extents.x, y, z));
                }
            }
            else
                voxelBounds = VoxelBounds();

            // Draw center of mass
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(voxelBounds.center + centerOfMassOffset, 0.2f);

            Gizmos.matrix = Matrix4x4.identity; Gizmos.matrix = Matrix4x4.identity;

            if (debugInfo != null)
            {
                foreach (DebugDrawing debug in debugInfo)
                {
                    // Draw sample point
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(debug.Position, new Vector3(kGizmoSize, kGizmoSize, kGizmoSize));

                    // Draw Water height displacement
                    var water = debug.Position;
                    water.y = debug.WaterHeight;
                    Gizmos.DrawLine(debug.Position, water);
                    Gizmos.DrawSphere(water, kGizmoSize * 2f);

                    // Draw force
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(debug.Position, debug.Force / rb.mass); // draw force
                }
            }

        }

        void SetupColliders()
        {
            colliders = GetComponentsInChildren<Collider>();

            if (colliders.Length != 0) return;

            colliders = new Collider[1];
            colliders[0] = gameObject.AddComponent<BoxCollider>();
            Debug.LogWarning($"{nameof(BuoyantBody)}: Object \"{name}\" had no coll. {nameof(BoxCollider)} has been added.");
        }

        void SetupVoxels()
        {
            var t = transform;
            var rot = t.rotation;
            var pos = t.position;
            var size = t.localScale;

            // Set to origin for calculations
            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;

            voxels = null;
            var points = new List<Vector3>();

            var rawBounds = VoxelBounds();
            voxelBounds = rawBounds;
            voxelBounds.size = RoundVector(rawBounds.size, voxelResolution);
            for (var ix = -voxelBounds.extents.x; ix < voxelBounds.extents.x; ix += voxelResolution)
            {
                for (var iy = -voxelBounds.extents.y; iy < voxelBounds.extents.y; iy += voxelResolution)
                {
                    for (var iz = -voxelBounds.extents.z; iz < voxelBounds.extents.z; iz += voxelResolution)
                    {
                        var x = (voxelResolution * 0.5f) + ix;
                        var y = (voxelResolution * 0.5f) + iy;
                        var z = (voxelResolution * 0.5f) + iz;

                        var p = new Vector3(x, y, z) + voxelBounds.center;

                        var inside = false;
                        foreach (var coll in colliders)
                        {
                            if (PointIsInsideCollider(coll, p))
                            {
                                inside = true;
                                break;
                            }
                        }
                        if (inside)
                            points.Add(p);
                    }
                }
            }

            voxels = points.ToArray();

            // Restore original transform
            t.SetPositionAndRotation(pos, rot);
            t.localScale = size;

            // Calculate total voxel volume
            var voxelVolume = Mathf.Pow(voxelResolution, 3f) * voxels.Length;
            var rawVolume = rawBounds.size.x * rawBounds.size.y * rawBounds.size.z;
            Volume = Mathf.Min(rawVolume, voxelVolume);
            Density = rb.mass / Volume;
        }

        void SetupArrays()
        {
            debugInfo = new DebugDrawing[voxels.Length];
            samplePoints = new NativeArray<float3>(voxels.Length, Allocator.Persistent);
        }

        void SetupPhysics()
        {
            rb.centerOfMass = centerOfMassOffset + voxelBounds.center;
            baseDrag = rb.linearDamping;
            baseAngularDrag = rb.angularDamping;

            velocity = new float3[voxels.Length];
            var archimedesForceMagnitude = kWaterDensity * Mathf.Abs(UnityEngine.Physics.gravity.y) * Volume;
            localArchimedesForce = new float3(0, archimedesForceMagnitude, 0) / voxels.Length;
            LocalToWorldJob.SetupJob(guid, voxels, ref samplePoints);
        }

        void BuoyancyForce(Vector3 position, float3 velocity, float waterHeight, ref float submergedAmount, ref DebugDrawing debug)
        {
            debug.Position = position;
            debug.WaterHeight = waterHeight;
            debug.Force = Vector3.zero;

            if (!(position.y - voxelResolution < waterHeight)) return;

            var k = math.clamp(waterHeight - (position.y - voxelResolution), 0f, 1f);

            submergedAmount += k / voxels.Length;

            var localDampingForce = kDampner * rb.mass * -velocity;
            var force = localDampingForce + math.sqrt(k) * localArchimedesForce;
            rb.AddForceAtPosition(force, position);

            debug.Force = force; // For drawing force Gizmos
            // Debug.Log(string.Format("Position: {0:f1} -- Force: {1:f2} -- Height: {2:f2}\nVelocity: {3:f2} -- Damp: {4:f2} -- Mass: {5:f1} -- K: {6:f2}", wp, force, waterLevel, velocity, localDampingForce, RB.mass, localArchimedesForce));
        }

        void UpdateDrag(float submergedAmount)
        {
            PercentSubmerged = math.lerp(PercentSubmerged, submergedAmount, 0.25f);
            rb.linearDamping = baseDrag + baseDrag * (PercentSubmerged * 10f);
            rb.angularDamping = baseAngularDrag + PercentSubmerged * 0.5f;
        }

        static Vector3 RoundVector(Vector3 vec, float rounding)
        {
            return new Vector3(Mathf.Ceil(vec.x / rounding) * rounding, Mathf.Ceil(vec.y / rounding) * rounding, Mathf.Ceil(vec.z / rounding) * rounding);
        }

        static bool PointIsInsideCollider(Collider c, Vector3 p)
        {
            var cp = UnityEngine.Physics.ClosestPoint(p, c, Vector3.zero, Quaternion.identity);
            return Vector3.Distance(cp, p) < 0.01f;
        }
    }
}
