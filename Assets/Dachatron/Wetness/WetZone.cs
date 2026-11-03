using System.Collections.Generic;
using UnityEngine;

// A box or ellipsoid that makes surfaces inside it wet (for materials with Wet Zone Mode = Only In Wet Zones).
// Move / rotate / scale the transform to shape it. Up to 16 zones are active at once.
[ExecuteAlways]
public class WetZone : MonoBehaviour
{
    public enum Shape { Box, Ellipsoid }

    const int MaxZones = 16;
    static readonly int CountId = Shader.PropertyToID("_WetZoneCount");
    static readonly int MatricesId = Shader.PropertyToID("_WetZoneWorldToLocal");
    static readonly int ParamsId = Shader.PropertyToID("_WetZoneParams");

    static readonly List<WetZone> Zones = new List<WetZone>();
    static readonly Matrix4x4[] Matrices = new Matrix4x4[MaxZones];
    static readonly Vector4[] Params = new Vector4[MaxZones];
    static int lastPushFrame = -1;

    public Shape shape = Shape.Box;
    [Range(0.01f, 1f)] public float softness = 0.25f;
    [Range(0f, 1f)] public float strength = 1f;
    [Tooltip("Makes the edge ragged / organic instead of a clean shape")]
    [Range(0f, 1f)] public float edgeNoise = 0.4f;

    void OnEnable() { Zones.Add(this); Push(true); }
    void OnDisable() { Zones.Remove(this); Push(true); }
    void OnValidate() => Push(true);
    void LateUpdate() => Push(false);

    static void Push(bool force)
    {
        if (!force && lastPushFrame == Time.frameCount) return;
        lastPushFrame = Time.frameCount;

        int count = 0;
        foreach (var z in Zones)
        {
            if (z == null || !z.isActiveAndEnabled) continue;
            if (count >= MaxZones) break;
            Matrices[count] = z.transform.worldToLocalMatrix;
            Params[count] = new Vector4(z.shape == Shape.Box ? 0f : 1f, z.softness, z.strength, z.edgeNoise);
            count++;
        }

        Shader.SetGlobalFloat(CountId, count);
        if (count > 0)
        {
            Shader.SetGlobalMatrixArray(MatricesId, Matrices);
            Shader.SetGlobalVectorArray(ParamsId, Params);
        }
    }

    void OnDrawGizmos() => Draw(new Color(0.2f, 0.8f, 1f, 0.25f), false);
    void OnDrawGizmosSelected() => Draw(new Color(0.2f, 0.8f, 1f, 0.15f), true);

    void Draw(Color c, bool fill)
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = c;
        if (shape == Shape.Box)
        {
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            if (fill) Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
        else
        {
            Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            if (fill) Gizmos.DrawSphere(Vector3.zero, 0.5f);
        }
    }
}
