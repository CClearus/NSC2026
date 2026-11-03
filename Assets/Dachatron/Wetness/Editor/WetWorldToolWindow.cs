using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Dachatron > Wet World Tool
// 1) check/enable URP reflection + shadow settings  2) convert materials to Wet Lit and tune them
// 3) place + bake reflection probes                 4) global wetness slider
public class WetWorldToolWindow : EditorWindow
{
    const string WetShaderName = "Dachatron/Wet Lit";
    const string LitShaderName = "Universal Render Pipeline/Lit";
    const string BakeFolder = "Assets/Dachatron/Wetness/ProbeBakes";

    enum ProbeMode { BakedToCustom, Realtime }
    enum PlaceMode { FitToSelection, GridOverSelection }

    Vector2 scroll;

    // probe settings
    PlaceMode placeMode = PlaceMode.FitToSelection;
    ProbeMode probeMode = ProbeMode.BakedToCustom;
    int resolution = 256;
    float gridSpacing = 8f;
    float padding = 0.5f;
    float blendDistance = 1.5f;
    int importance = 1;

    [MenuItem("Tools/Dachatron/Wet World Tool")]
    static void Open() => GetWindow<WetWorldToolWindow>("Wet World");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawPipelineSection();
        DrawMaterialSection();
        DrawZoneSection();
        DrawProbeSection();
        DrawControllerSection();
        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ 1. pipeline
    static RenderPipelineAsset ActiveAsset =>
        QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;

    static void DrawStatus(string label, bool ok) =>
        EditorGUILayout.LabelField(label, ok ? "OK" : "OFF", ok ? EditorStyles.label : EditorStyles.boldLabel);

    void DrawPipelineSection()
    {
        EditorGUILayout.LabelField("1. URP settings", EditorStyles.boldLabel);
        var asset = ActiveAsset;
        if (asset == null)
        {
            EditorGUILayout.HelpBox("No render pipeline asset is active.", MessageType.Warning);
            return;
        }

        var so = new SerializedObject(asset);
        EditorGUILayout.ObjectField("Active asset", asset, typeof(RenderPipelineAsset), false);

        DrawStatus("Probe blending", Bool(so, "m_ReflectionProbeBlending"));
        DrawStatus("Probe box projection", Bool(so, "m_ReflectionProbeBoxProjection"));
        DrawStatus("Probe atlas", Bool(so, "m_ReflectionProbeAtlas"));
        EditorGUILayout.LabelField("Main light shadow res", Int(so, "m_MainLightShadowmapResolution").ToString());
        EditorGUILayout.LabelField("Shadow distance", Float(so, "m_ShadowDistance").ToString("0"));
        DrawStatus("Soft shadows", Bool(so, "m_SoftShadowsSupported"));

        if (GUILayout.Button("Enable probe blending / box projection / atlas"))
        {
            Undo.RecordObject(asset, "Enable reflection probe settings");
            Set(so, "m_ReflectionProbeBlending", true);
            Set(so, "m_ReflectionProbeBoxProjection", true);
            Set(so, "m_ReflectionProbeAtlas", true);
        }

        if (GUILayout.Button("High quality shadows (4096, soft, 4 cascades, 60m)"))
        {
            Undo.RecordObject(asset, "High quality shadows");
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", 3);
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetFloat(so, "m_ShadowDistance", 60f);
        }
        EditorGUILayout.Space(8);
    }

    static bool Bool(SerializedObject so, string n) { var p = so.FindProperty(n); return p != null && p.boolValue; }
    static int Int(SerializedObject so, string n) { var p = so.FindProperty(n); return p != null ? p.intValue : 0; }
    static float Float(SerializedObject so, string n) { var p = so.FindProperty(n); return p != null ? p.floatValue : 0f; }

    static void Apply(SerializedObject so, string n, System.Action<SerializedProperty> set)
    {
        var p = so.FindProperty(n);
        if (p == null) { Debug.LogWarning($"[Wet World] '{n}' not found on {so.targetObject.name}"); return; }
        so.Update();
        set(p);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }
    static void Set(SerializedObject so, string n, bool v) => Apply(so, n, p => p.boolValue = v);
    static void SetInt(SerializedObject so, string n, int v) => Apply(so, n, p => p.intValue = v);
    static void SetFloat(SerializedObject so, string n, float v) => Apply(so, n, p => p.floatValue = v);

    // ------------------------------------------------------------------ 2. materials
    // property, label, min, max
    static readonly (string prop, string label, float min, float max)[] WetBasics =
    {
        ("_Wetness", "Wetness (coverage)", 0f, 1f),
        ("_WetSmoothness", "Wet smoothness", 0f, 1f),
        ("_WetDarken", "Wet darken", 0f, 1f),
        ("_WetFlatten", "Normal flatten", 0f, 1f),
        ("_WetNoiseScale", "Puddle noise scale", 0.05f, 5f),
        ("_WetSoftness", "Edge softness", 0.01f, 0.5f),
        ("_WetUpFacingOnly", "Only up-facing", 0f, 1f),
        ("_WetUpFacingMin", "Up-facing threshold", 0f, 1f),
        ("_WetMaskStrength", "Mask strength", 0f, 1f),
    };
    static readonly (string prop, string label, float min, float max)[] WetShape =
    {
        ("_WetNoiseOctaves", "Noise layers", 1f, 6f),
        ("_WetNoiseLacunarity", "Layer frequency step", 1.5f, 3f),
        ("_WetNoiseGain", "Layer strength falloff", 0.2f, 0.8f),
        ("_WetNoiseContrast", "Noise contrast", 0.5f, 3f),
        ("_WetNoiseWarp", "Edge warp", 0f, 2f),
        ("_WetDetailScale", "Fine layer scale", 1f, 16f),
        ("_WetDetailAmount", "Fine layer amount", 0f, 1f),
        ("_WetNoiseTexStrength", "Noise texture blend", 0f, 1f),
    };

    bool shapeFoldout = true;

    static void DrawFloats(List<Material> wet, (string prop, string label, float min, float max)[] list)
    {
        foreach (var (prop, label, min, max) in list)
        {
            float cur = wet[0].GetFloat(prop);
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.Slider(label, cur, min, max);
            if (EditorGUI.EndChangeCheck())
                foreach (var m in wet) { Undo.RecordObject(m, "Wet property"); m.SetFloat(prop, v); EditorUtility.SetDirty(m); }
        }
    }

    static List<Material> SelectedMaterials()
    {
        var set = new HashSet<Material>();
        foreach (var m in Selection.GetFiltered<Material>(SelectionMode.Assets)) set.Add(m);
        foreach (var go in Selection.gameObjects)
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m != null) set.Add(m);
        return new List<Material>(set);
    }

    void DrawMaterialSection()
    {
        EditorGUILayout.LabelField("2. Wet materials", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select material assets, or objects in the scene, then convert. Only URP/Lit materials are converted.", MessageType.None);

        var wetShader = Shader.Find(WetShaderName);
        var litShader = Shader.Find(LitShaderName);
        if (wetShader == null)
        {
            EditorGUILayout.HelpBox($"Shader '{WetShaderName}' not found. Check the console for shader compile errors.", MessageType.Error);
            return;
        }

        var mats = SelectedMaterials();
        EditorGUILayout.LabelField("Materials in selection", mats.Count.ToString());

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Convert to Wet Lit"))
                foreach (var m in mats)
                    if (m.shader == litShader) { Undo.RecordObject(m, "Convert to Wet Lit"); m.shader = wetShader; EditorUtility.SetDirty(m); }
            if (GUILayout.Button("Revert to Lit"))
                foreach (var m in mats)
                    if (m.shader == wetShader) { Undo.RecordObject(m, "Revert to Lit"); m.shader = litShader; EditorUtility.SetDirty(m); }
        }

        var wet = mats.FindAll(m => m.shader == wetShader);
        if (wet.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Tuning {wet.Count} wet material(s)", EditorStyles.miniBoldLabel);
            DrawFloats(wet, WetBasics);

            shapeFoldout = EditorGUILayout.Foldout(shapeFoldout, "Puddle shape (noise layers)", true);
            if (shapeFoldout)
            {
                DrawFloats(wet, WetShape);

                EditorGUI.BeginChangeCheck();
                var noiseTex = (Texture)EditorGUILayout.ObjectField("Noise texture (R)", wet[0].GetTexture("_WetNoiseTex"), typeof(Texture), false);
                if (EditorGUI.EndChangeCheck())
                    foreach (var m in wet) { Undo.RecordObject(m, "Wet noise texture"); m.SetTexture("_WetNoiseTex", noiseTex); EditorUtility.SetDirty(m); }

                if (GUILayout.Button("Generate tileable noise textures"))
                    NoiseTextureGenerator.Generate();
            }

            EditorGUI.BeginChangeCheck();
            bool zonesOnly = EditorGUILayout.Toggle("Only inside Wet Zones", wet[0].GetFloat("_WetZoneMode") > 0.5f);
            if (EditorGUI.EndChangeCheck())
                foreach (var m in wet) { Undo.RecordObject(m, "Wet zone mode"); m.SetFloat("_WetZoneMode", zonesOnly ? 1f : 0f); EditorUtility.SetDirty(m); }

            EditorGUI.BeginChangeCheck();
            var mask =(Texture)EditorGUILayout.ObjectField("Wet mask (R)", wet[0].GetTexture("_WetMaskMap"), typeof(Texture), false);
            if (EditorGUI.EndChangeCheck())
                foreach (var m in wet) { Undo.RecordObject(m, "Wet mask"); m.SetTexture("_WetMaskMap", mask); EditorUtility.SetDirty(m); }
        }
        EditorGUILayout.Space(8);
    }

    // ------------------------------------------------------------------ 2b. wet zones
    void DrawZoneSection()
    {
        EditorGUILayout.LabelField("Wet zones (custom shapes)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Tick 'Only inside Wet Zones' on the material above, then place zones. Anything inside a zone's box/ellipsoid gets wet, " +
            "so a flat box makes a floor puddle and a tall one wets the bottom of a wall. Move/rotate/scale to shape it. Max 16.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New Box zone")) CreateZone(WetZone.Shape.Box);
            if (GUILayout.Button("New Ellipsoid zone")) CreateZone(WetZone.Shape.Ellipsoid);
        }
        EditorGUILayout.Space(8);
    }

    [MenuItem("GameObject/Dachatron/Wet Zone", false, 10)]
    static void MenuCreateZone() => CreateZone(WetZone.Shape.Box);

    static void CreateZone(WetZone.Shape shape)
    {
        var go = new GameObject(shape == WetZone.Shape.Box ? "Wet Zone (Box)" : "Wet Zone (Ellipsoid)");
        Undo.RegisterCreatedObjectUndo(go, "Create wet zone");

        // Fit to the selection if there is one, otherwise drop it in front of the scene camera.
        if (TryGetBounds(out var b))
        {
            go.transform.position = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z);
            go.transform.localScale = new Vector3(Mathf.Min(b.size.x, 4f), 0.4f, Mathf.Min(b.size.z, 4f));
        }
        else
        {
            var view = SceneView.lastActiveSceneView;
            go.transform.position = view != null ? view.pivot : Vector3.zero;
            go.transform.localScale = new Vector3(3f, 0.4f, 3f);
        }

        go.AddComponent<WetZone>().shape = shape;
        Selection.activeGameObject = go;
    }

    // ------------------------------------------------------------------ 3. probes
    void DrawProbeSection()
    {
        EditorGUILayout.LabelField("3. Reflection probes", EditorStyles.boldLabel);
        placeMode = (PlaceMode)EditorGUILayout.EnumPopup("Placement", placeMode);
        if (placeMode == PlaceMode.GridOverSelection)
            gridSpacing = Mathf.Max(2f, EditorGUILayout.FloatField("Grid spacing (m)", gridSpacing));
        probeMode = (ProbeMode)EditorGUILayout.EnumPopup("Mode", probeMode);
        resolution = EditorGUILayout.IntPopup("Resolution", resolution,
            new[] { "64", "128", "256", "512" }, new[] { 64, 128, 256, 512 });
        padding = EditorGUILayout.Slider("Box padding (m)", padding, 0f, 5f);
        blendDistance = EditorGUILayout.Slider("Blend distance (m)", blendDistance, 0f, 5f);
        importance = EditorGUILayout.IntSlider("Importance", importance, 0, 10);

        EditorGUILayout.HelpBox(
            "Select a room (parent object) and press Create. Fit = one box probe around the selection. " +
            "Grid = several probes across it. Baked mode renders the cubemap now and stores it in " + BakeFolder + ". " +
            "Mark the geometry Static (Reflection Probe Static) so it appears in the bake.", MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create probes from selection")) CreateProbes();
            if (GUILayout.Button("Re-bake all probes")) RebakeAll();
        }
        EditorGUILayout.Space(8);
    }

    static bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        var renderers = new List<Renderer>();
        foreach (var go in Selection.gameObjects) renderers.AddRange(go.GetComponentsInChildren<Renderer>());
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }

    void CreateProbes()
    {
        if (!TryGetBounds(out var b))
        {
            EditorUtility.DisplayDialog("Wet World", "Select one or more objects with renderers (e.g. a room root).", "OK");
            return;
        }
        b.Expand(padding * 2f);

        var created = new List<ReflectionProbe>();
        if (placeMode == PlaceMode.FitToSelection)
        {
            created.Add(MakeProbe(b.center, b.size, "Wet Probe"));
        }
        else
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(b.size.x / gridSpacing));
            int ny = Mathf.Max(1, Mathf.CeilToInt(b.size.y / gridSpacing));
            int nz = Mathf.Max(1, Mathf.CeilToInt(b.size.z / gridSpacing));
            if (nx * ny * nz > 64)
            {
                EditorUtility.DisplayDialog("Wet World", $"That would create {nx * ny * nz} probes. Increase the grid spacing.", "OK");
                return;
            }
            var cell = new Vector3(b.size.x / nx, b.size.y / ny, b.size.z / nz);
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                    for (int z = 0; z < nz; z++)
                    {
                        var c = b.min + new Vector3((x + 0.5f) * cell.x, (y + 0.5f) * cell.y, (z + 0.5f) * cell.z);
                        created.Add(MakeProbe(c, cell, $"Wet Probe {x}_{y}_{z}"));
                    }
        }

        if (probeMode == ProbeMode.BakedToCustom)
            foreach (var p in created) Bake(p);

        Selection.objects = created.ConvertAll(p => (Object)p.gameObject).ToArray();
    }

    ReflectionProbe MakeProbe(Vector3 center, Vector3 size, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create reflection probe");
        go.transform.position = center;
        var p = go.AddComponent<ReflectionProbe>();
        p.size = size;
        p.center = Vector3.zero;
        p.boxProjection = true;
        p.blendDistance = blendDistance;
        p.resolution = resolution;
        p.importance = importance;
        p.hdr = true;
        p.mode = ReflectionProbeMode.Realtime;
        p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        return p;
    }

    static void Bake(ReflectionProbe p)
    {
        if (!AssetDatabase.IsValidFolder(BakeFolder))
            AssetDatabase.CreateFolder("Assets/Dachatron/Wetness", "ProbeBakes");

        string scene = SceneManager.GetActiveScene().name;
        string file = $"{scene}_{p.gameObject.name}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}".Replace(' ', '_') + ".exr";
        string path = Path.Combine(BakeFolder, file).Replace('\\', '/');

        p.mode = ReflectionProbeMode.Custom;
        if (!Lightmapping.BakeReflectionProbe(p, path))
        {
            Debug.LogWarning($"[Wet World] Baking '{p.name}' failed.");
            return;
        }
        AssetDatabase.ImportAsset(path);
        p.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
        EditorUtility.SetDirty(p);
    }

    void RebakeAll()
    {
        foreach (var p in Object.FindObjectsByType<ReflectionProbe>())
        {
            if (!p.gameObject.name.StartsWith("Wet Probe")) continue;
            p.resolution = resolution;
            Bake(p);
        }
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ 4. controller
    void DrawControllerSection()
    {
        EditorGUILayout.LabelField("4. Global wetness", EditorStyles.boldLabel);
        var ctrl = Object.FindAnyObjectByType<WetnessController>();
        if (ctrl == null)
        {
            if (GUILayout.Button("Add WetnessController to scene"))
            {
                var go = new GameObject("WetnessController");
                Undo.RegisterCreatedObjectUndo(go, "Add WetnessController");
                go.AddComponent<WetnessController>();
            }
            return;
        }

        EditorGUI.BeginChangeCheck();
        float v = EditorGUILayout.Slider("Wetness", ctrl.wetness, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(ctrl, "Wetness");
            ctrl.SetWetness(v);
            EditorUtility.SetDirty(ctrl);
        }
        if (GUILayout.Button("Select controller")) Selection.activeObject = ctrl;
    }
}
