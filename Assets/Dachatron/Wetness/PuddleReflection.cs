using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Live mirror reflections for Wet Lit floors. Put it on the (flat) floor object.
// A second camera renders the scene mirrored under the top of this floor every frame. Wet Lit materials then show
// that image only where they are wet (e.g. inside your Wet Zones), face up and sit at this floor's height.
// Tune strength / fresnel / ripple per material ("Live Reflection" settings on the Wet Lit material).
[RequireComponent(typeof(Renderer))]
public class PuddleReflection : MonoBehaviour
{
    static readonly int TexId = Shader.PropertyToID("_PlanarReflectionTex");
    static readonly int ReadyId = Shader.PropertyToID("_PlanarReflectionReady");
    static readonly int HeightId = Shader.PropertyToID("_PlanarReflectionHeight");

    [Tooltip("Reflection resolution compared to the screen. Lower = cheaper, blurrier.")]
    [Range(0.25f, 1f)] public float resolutionScale = 0.5f;
    public LayerMask reflectLayers = ~0;
    public bool reflectShadows = true;

    Camera reflectionCamera;
    RenderTexture target;
    float planeY;

    void OnEnable()
    {
        planeY = GetComponent<Renderer>().bounds.max.y;
        Shader.SetGlobalFloat(HeightId, planeY);
        BuildCamera();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        Shader.SetGlobalFloat(ReadyId, 0f);

        if (reflectionCamera != null) Destroy(reflectionCamera.gameObject);
        if (target != null) { target.Release(); Destroy(target); }
    }

    void BuildCamera()
    {
        var go = new GameObject(name + " Reflection Camera");
        reflectionCamera = go.AddComponent<Camera>();

        var data = reflectionCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = reflectShadows;
        data.requiresColorTexture = false;
        data.requiresDepthTexture = false;
        data.antialiasing = AntialiasingMode.None;
    }

    void EnsureTarget(Camera main)
    {
        int w = Mathf.Max(64, Mathf.RoundToInt(main.pixelWidth * resolutionScale));
        int h = Mathf.Max(64, Mathf.RoundToInt(main.pixelHeight * resolutionScale));
        if (target != null && target.width == w && target.height == h) return;

        if (target != null) { target.Release(); Destroy(target); }
        target = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "Puddle Reflection" };
        reflectionCamera.targetTexture = target;
        Shader.SetGlobalTexture(TexId, target);
        Shader.SetGlobalFloat(ReadyId, 1f);
    }

    void LateUpdate()
    {
        // render the reflection before the main camera each frame
        var main = Camera.main;
        if (main == null || reflectionCamera == null) return;
        reflectionCamera.depth = main.depth - 1f;
        EnsureTarget(main);
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam != reflectionCamera) return;
        var main = Camera.main;
        if (main == null) return;

        // the wet floor must not read the texture this camera is drawing into
        Shader.SetGlobalFloat(ReadyId, 0f);

        // mirror the main camera under the floor (the image comes out flipped left/right; the shader undoes it)
        Vector3 p = main.transform.position;
        Vector3 f = main.transform.forward;
        Vector3 u = main.transform.up;
        p.y = 2f * planeY - p.y;
        f.y = -f.y;
        u.y = -u.y;
        cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(f, u));

        cam.orthographic = main.orthographic;
        cam.orthographicSize = main.orthographicSize;
        cam.fieldOfView = main.fieldOfView;
        cam.nearClipPlane = main.nearClipPlane;
        cam.farClipPlane = main.farClipPlane;
        cam.clearFlags = main.clearFlags;
        cam.backgroundColor = main.backgroundColor;
        cam.cullingMask = main.cullingMask & reflectLayers;

        // oblique near plane just above the floor, so the floor itself and anything under it are cut away
        cam.ResetProjectionMatrix();
        Matrix4x4 view = cam.worldToCameraMatrix;
        Vector3 cp = view.MultiplyPoint(new Vector3(p.x, planeY + 0.02f, p.z));
        Vector3 cn = view.MultiplyVector(Vector3.up).normalized;
        cam.projectionMatrix = cam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
    }

    void OnEndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == reflectionCamera && target != null) Shader.SetGlobalFloat(ReadyId, 1f);
    }
}
