using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fake volumetric light shaft. Put it on a cube: the shaft runs along the cube's local Z (blue axis),
/// so rotate the cube to aim it. Uses the Dachatron/LightShaft shader (needs URP Depth Texture on).
/// "Shape To Hole" fires a grid of rays from the start face and rebuilds the mesh so it only exists
/// where light gets through the wall, ending where each ray lands.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class LightShaft : MonoBehaviour
{
    [Header("Look")]
    public Color color = new Color(1f, 0.95f, 0.8f, 1f);
    [Range(0f, 5f)] public float intensity = 0.5f;
    [Range(0.1f, 8f)] public float edgeSoftness = 2f;
    [Range(0f, 1f)] public float startFade = 0.1f;
    [Range(0f, 1f)] public float endFade = 0.6f;
    [Range(0.01f, 5f)] public float depthFade = 0.5f;

    [Header("Ray Streaks")]
    [Range(0f, 1f)] public float rayStrength = 0.6f;
    [Range(1f, 100f)] public float rayScale = 25f;
    [Range(0f, 2f)] public float raySpeed = 0.15f;

    [Header("Shape To Hole")]
    [Tooltip("Rays per side across the shaft. Higher = smoother hole outline.")]
    [Range(8, 64)] public int resolution = 32;
    [Tooltip("A ray that hits something within this distance of the start face is blocked by the wall. Set it a bit more than the wall thickness.")]
    public float wallDepth = 1f;
    [Tooltip("Keep only the opening at the cube's center. Turn off to keep every gap the rays find.")]
    public bool onlyCenterOpening = true;
    [Tooltip("Optional. If set, only these objects (and their children) can block the light. Tick wall pieces only, not a parent that also holds the floor.")]
    public List<GameObject> walls = new List<GameObject>();

    [Header("Fit Length")]
    public float maxFitDistance = 50f;
    public LayerMask fitMask = ~0;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int StartFadeId = Shader.PropertyToID("_StartFade");
    static readonly int EndFadeId = Shader.PropertyToID("_EndFade");
    static readonly int DepthFadeId = Shader.PropertyToID("_DepthFade");
    static readonly int RayStrengthId = Shader.PropertyToID("_RayStrength");
    static readonly int RayScaleId = Shader.PropertyToID("_RayScale");
    static readonly int RaySpeedId = Shader.PropertyToID("_RaySpeed");

    MaterialPropertyBlock block;
    MeshRenderer meshRenderer;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    public void Apply()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (block == null) block = new MaterialPropertyBlock();

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        meshRenderer.GetPropertyBlock(block);
        block.SetColor(ColorId, color);
        block.SetFloat(IntensityId, intensity);
        block.SetFloat(EdgeSoftnessId, edgeSoftness);
        block.SetFloat(StartFadeId, startFade);
        block.SetFloat(EndFadeId, endFade);
        block.SetFloat(DepthFadeId, depthFade);
        block.SetFloat(RayStrengthId, rayStrength);
        block.SetFloat(RayScaleId, rayScale);
        block.SetFloat(RaySpeedId, raySpeed);
        meshRenderer.SetPropertyBlock(block);
    }

    Vector3 StartFaceCenter => transform.position - transform.forward * (transform.lossyScale.z * 0.5f);

    /// <summary>
    /// Keeps the start (the end at the hole) where it is and trims the length to the first
    /// surface hit along the shaft direction. Returns false if nothing was hit.
    /// </summary>
    public bool FitLength()
    {
        Vector3 dir = transform.forward;
        Vector3 start = StartFaceCenter;

        if (!FirstHit(start, dir, out RaycastHit hit)) return false;
        SetLength(start, hit.distance);
        return true;
    }

    /// <summary>
    /// Builds a mesh shaped like the opening in the wall. The cube's start face (the end opposite
    /// the blue arrow) should sit just outside the wall, covering the whole hole.
    /// Returns null if no ray got through.
    /// </summary>
    public Mesh BuildHoleMesh()
    {
        int res = Mathf.Max(2, resolution);
        Vector3 dir = transform.forward;
        Vector3 right = transform.right * transform.lossyScale.x;
        Vector3 up = transform.up * transform.lossyScale.y;
        Vector3 startCenter = StartFaceCenter;

        // Cast one ray per cell from the start face.
        var open = new bool[res, res];
        var length = new float[res, res];
        float longest = 0f;
        for (int i = 0; i < res; i++)
        for (int j = 0; j < res; j++)
        {
            Vector3 p = startCenter + right * ((i + 0.5f) / res - 0.5f) + up * ((j + 0.5f) / res - 0.5f);
            float d = maxFitDistance;
            if (FirstHit(p, dir, out RaycastHit hit))
            {
                bool blocked = hit.distance < wallDepth && (walls.Count == 0 || IsWall(hit.transform));
                if (blocked) continue;
                d = hit.distance;
            }
            open[i, j] = true;
            length[i, j] = d;
            longest = Mathf.Max(longest, d);
        }
        if (longest <= 0f) return null;

        // Keep only the opening connected to the cube's center, so rays that slip past the
        // outside edges of the wall don't count as light coming through the hole.
        if (onlyCenterOpening)
        {
            open = KeepCenterRegion(open, res);
            longest = 0f;
            for (int i = 0; i < res; i++)
            for (int j = 0; j < res; j++)
                if (open[i, j]) longest = Mathf.Max(longest, length[i, j]);
            if (longest <= 0f) return null;
        }

        // Each corner ends at the average length of the open cells around it, so the far end
        // follows the floor smoothly instead of in steps.
        var cornerLength = new float[res + 1, res + 1];
        for (int ci = 0; ci <= res; ci++)
        for (int cj = 0; cj <= res; cj++)
        {
            float sum = 0f;
            int count = 0;
            for (int di = -1; di <= 0; di++)
            for (int dj = -1; dj <= 0; dj++)
            {
                int i = ci + di, j = cj + dj;
                if (i < 0 || j < 0 || i >= res || j >= res || !open[i, j]) continue;
                sum += length[i, j];
                count++;
            }
            cornerLength[ci, cj] = count > 0 ? sum / count : 0f;
        }

        // Stretch the object to the longest ray so the start/end fades span the whole shaft.
        SetLength(startCenter, longest);

        // Build the outer walls of the open cells. Each side quad lies in a local X or Y plane,
        // so its normal is just that local axis.
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i < res; i++)
        for (int j = 0; j < res; j++)
        {
            if (!open[i, j]) continue;
            float x0 = (float)i / res - 0.5f, x1 = (float)(i + 1) / res - 0.5f;
            float y0 = (float)j / res - 0.5f, y1 = (float)(j + 1) / res - 0.5f;
            float z00 = cornerLength[i, j] / longest - 0.5f;
            float z10 = cornerLength[i + 1, j] / longest - 0.5f;
            float z01 = cornerLength[i, j + 1] / longest - 0.5f;
            float z11 = cornerLength[i + 1, j + 1] / longest - 0.5f;

            if (i == 0 || !open[i - 1, j]) AddSide(verts, normals, tris, new Vector3(x0, y0, z00), new Vector3(x0, y1, z01), Vector3.left);
            if (i == res - 1 || !open[i + 1, j]) AddSide(verts, normals, tris, new Vector3(x1, y0, z10), new Vector3(x1, y1, z11), Vector3.right);
            if (j == 0 || !open[i, j - 1]) AddSide(verts, normals, tris, new Vector3(x0, y0, z00), new Vector3(x1, y0, z10), Vector3.down);
            if (j == res - 1 || !open[i, j + 1]) AddSide(verts, normals, tris, new Vector3(x0, y1, z01), new Vector3(x1, y1, z11), Vector3.up);
        }

        var mesh = new Mesh { name = name + "_HoleShape" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Flood fill from the open cell nearest the center; everything not reached is closed.
    static bool[,] KeepCenterRegion(bool[,] open, int res)
    {
        int si = -1, sj = -1;
        float bestDist = float.MaxValue;
        float c = (res - 1) * 0.5f;
        for (int i = 0; i < res; i++)
        for (int j = 0; j < res; j++)
        {
            if (!open[i, j]) continue;
            float d = (i - c) * (i - c) + (j - c) * (j - c);
            if (d < bestDist) { bestDist = d; si = i; sj = j; }
        }

        var keep = new bool[res, res];
        if (si < 0) return keep;

        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(si, sj));
        keep[si, sj] = true;
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up })
            {
                var q = p + d;
                if (q.x < 0 || q.y < 0 || q.x >= res || q.y >= res) continue;
                if (!open[q.x, q.y] || keep[q.x, q.y]) continue;
                keep[q.x, q.y] = true;
                stack.Push(q);
            }
        }
        return keep;
    }

    // a and b are the two corners on the start face (x, y) with their own end z.
    static void AddSide(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 a, Vector3 b, Vector3 normal)
    {
        int k = verts.Count;
        verts.Add(new Vector3(a.x, a.y, -0.5f));
        verts.Add(new Vector3(b.x, b.y, -0.5f));
        verts.Add(b);
        verts.Add(a);
        for (int n = 0; n < 4; n++) normals.Add(normal);
        tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
        tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
    }

    void SetLength(Vector3 start, float worldLength)
    {
        Vector3 dir = transform.forward;
        Vector3 s = transform.localScale;
        float parentScale = transform.parent ? transform.parent.lossyScale.z : 1f;
        s.z = worldLength / Mathf.Max(parentScale, 0.0001f);
        transform.localScale = s;
        transform.position = start + dir * worldLength * 0.5f;
    }

    bool FirstHit(Vector3 origin, Vector3 dir, out RaycastHit best)
    {
        best = default;
        bool found = false;
        foreach (var h in Physics.RaycastAll(origin, dir, maxFitDistance, fitMask, QueryTriggerInteraction.Ignore))
        {
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (!found || h.distance < best.distance) { best = h; found = true; }
        }
        return found;
    }

    bool IsWall(Transform hit)
    {
        foreach (var w in walls)
            if (w != null && (hit == w.transform || hit.IsChildOf(w.transform))) return true;
        return false;
    }
}
