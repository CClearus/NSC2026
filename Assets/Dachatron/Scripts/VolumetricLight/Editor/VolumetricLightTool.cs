using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sets up and drives the URP Volumetric Fog package (com.cqf.urpvolumetricfog):
/// renderer feature, fog volume, camera post-processing and which lights make rays.
/// Only spot/point lights with a VolumetricAdditionalLight component make rays.
/// </summary>
public class VolumetricLightTool : EditorWindow
{
    const string ProfilePath = "Assets/Dachatron/Mats/VolumetricFogProfile.asset";
    const string VolumeName = "Volumetric Fog Volume";

    Vector2 scroll;

    [MenuItem("Tools/Volumetric Light")]
    static void Open() => GetWindow<VolumetricLightTool>("Volumetric Light");

    void OnHierarchyChange() => Repaint();
    void OnSelectionChange() => Repaint();

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawSetup();
        EditorGUILayout.Space(8);
        DrawFog();
        EditorGUILayout.Space(8);
        DrawLights();
        EditorGUILayout.Space(8);
        DrawOldShafts();

        EditorGUILayout.EndScrollView();
    }

    // ---------- Setup ----------

    void DrawSetup()
    {
        EditorGUILayout.LabelField("1. Setup", EditorStyles.boldLabel);

        bool featureOk = AllRenderersHaveFeature();
        bool volumeOk = FindFogComponent() != null;
        bool cameraOk = MainCameraHasPostProcessing();

        Status("Renderer feature on URP renderers", featureOk);
        Status("Fog volume in this scene", volumeOk);
        Status("Post-processing on Main Camera", cameraOk);

        if (!(featureOk && volumeOk && cameraOk))
        {
            if (GUILayout.Button("Set Up Everything", GUILayout.Height(28)))
            {
                if (!featureOk) AddFeatureToRenderers();
                if (!volumeOk) CreateFogVolume();
                if (!cameraOk) EnableCameraPostProcessing();
            }
        }
        EditorGUILayout.HelpBox("In the Scene view, turn on the effects toggle (the layered icon in the Scene toolbar) to see the fog while editing.", MessageType.None);
    }

    static void Status(string label, bool ok)
    {
        EditorGUILayout.LabelField((ok ? "✔ " : "✘ ") + label);
    }

    static List<UniversalRendererData> FindRenderers()
    {
        var list = new List<UniversalRendererData>();
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null) list.Add(data);
        }
        return list;
    }

    static bool AllRenderersHaveFeature()
    {
        foreach (var data in FindRenderers())
            if (!data.rendererFeatures.Exists(f => f is VolumetricFogRendererFeature)) return false;
        return true;
    }

    static void AddFeatureToRenderers()
    {
        foreach (var data in FindRenderers())
        {
            if (data.rendererFeatures.Exists(f => f is VolumetricFogRendererFeature)) continue;

            var feature = CreateInstance<VolumetricFogRendererFeature>();
            feature.name = "VolumetricFog";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets();

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                Debug.LogError("Volumetric Light: could not add the feature to " + data.name, data);
                continue;
            }

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            int i = features.arraySize;
            features.arraySize = i + 1;
            features.GetArrayElementAtIndex(i).objectReferenceValue = feature;
            map.arraySize = i + 1;
            map.GetArrayElementAtIndex(i).longValue = localId;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }

    static bool MainCameraHasPostProcessing()
    {
        var cam = Camera.main;
        if (cam == null) return false;
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        return data != null && data.renderPostProcessing;
    }

    static void EnableCameraPostProcessing()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Volumetric Light: no camera tagged MainCamera in this scene. Turn on Post Processing on your camera by hand.");
            return;
        }
        var data = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(data, "Enable Post Processing");
        data.renderPostProcessing = true;
    }

    // ---------- Fog ----------

    static VolumetricFogVolumeComponent FindFogComponent()
    {
        foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var profile = v.sharedProfile;
            if (profile != null && profile.TryGet(out VolumetricFogVolumeComponent fog)) return fog;
        }
        return null;
    }

    static void CreateFogVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        if (!profile.TryGet(out VolumetricFogVolumeComponent fog))
        {
            fog = profile.Add<VolumetricFogVolumeComponent>(true);
            fog.name = "VolumetricFog";
            AssetDatabase.AddObjectToAsset(fog, profile);
        }

        // Spot-light rays only by default; the sun can be turned on below.
        fog.enabled.value = true;
        fog.enableAdditionalLightsContribution.value = true;
        fog.enableMainLightContribution.value = false;
        fog.density.value = 0.2f;
        fog.distance.value = 64f;
        fog.baseHeight.value = -50f;
        fog.maximumHeight.value = 100f;
        EditorUtility.SetDirty(fog);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var go = new GameObject(VolumeName);
        Undo.RegisterCreatedObjectUndo(go, "Create Fog Volume");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    void DrawFog()
    {
        EditorGUILayout.LabelField("2. Fog", EditorStyles.boldLabel);
        var fog = FindFogComponent();
        if (fog == null)
        {
            EditorGUILayout.LabelField("No fog volume yet. Use Set Up Everything above.");
            return;
        }

        EditorGUI.BeginChangeCheck();
        bool on = EditorGUILayout.Toggle("Fog Enabled", fog.enabled.value);
        bool spots = EditorGUILayout.Toggle("Spot/Point Light Rays", fog.enableAdditionalLightsContribution.value);
        bool sun = EditorGUILayout.Toggle("Sun Rays (Main Light)", fog.enableMainLightContribution.value);
        float density = EditorGUILayout.Slider("Density", fog.density.value, 0f, 1f);
        float distance = EditorGUILayout.Slider("Max Distance", fog.distance.value, 1f, 512f);
        float baseH = EditorGUILayout.FloatField("Fog Bottom Height", fog.baseHeight.value);
        float maxH = EditorGUILayout.FloatField("Fog Top Height", fog.maximumHeight.value);
        int steps = EditorGUILayout.IntSlider("Quality (Max Steps)", fog.maxSteps.value, 8, 256);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(fog, "Edit Volumetric Fog");
            Set(fog.enabled, on);
            Set(fog.enableAdditionalLightsContribution, spots);
            Set(fog.enableMainLightContribution, sun);
            Set(fog.density, density);
            Set(fog.distance, distance);
            Set(fog.baseHeight, baseH);
            Set(fog.maximumHeight, maxH);
            Set(fog.maxSteps, steps);
            EditorUtility.SetDirty(fog);
        }

        if (GUILayout.Button("Select Fog Profile (all settings)"))
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
    }

    static void Set<T>(VolumeParameter<T> p, T value)
    {
        p.overrideState = true;
        p.value = value;
    }

    // ---------- Lights ----------

    void DrawLights()
    {
        EditorGUILayout.LabelField("3. Lights That Make Rays", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Only spot/point lights in this list make rays. Put a spot light outside the hole and aim it through; the wall's shadow shapes the rays.", MessageType.None);

        if (GUILayout.Button("Create Volumetric Spot Light", GUILayout.Height(24)))
            CreateSpotLight();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selected Lights")) SetSelectedVolumetric(true);
            if (GUILayout.Button("Remove Selected Lights")) SetSelectedVolumetric(false);
        }

        var lights = FindObjectsByType<VolumetricAdditionalLight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (lights.Length == 0)
        {
            EditorGUILayout.LabelField("No volumetric lights in this scene.");
            return;
        }

        foreach (var vl in lights)
        {
            var light = vl.GetComponent<Light>();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(vl.name, EditorStyles.boldLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeGameObject = vl.gameObject;
                }

                if (light.shadows == LightShadows.None)
                {
                    EditorGUILayout.HelpBox("Shadows are off, so walls won't cut the rays.", MessageType.Warning);
                    if (GUILayout.Button("Turn On Shadows"))
                    {
                        Undo.RecordObject(light, "Turn On Shadows");
                        light.shadows = LightShadows.Soft;
                    }
                }
                if (light.lightmapBakeType == LightmapBakeType.Baked)
                    EditorGUILayout.HelpBox("This light is Baked, so it won't make rays. Set Mode to Realtime or Mixed.", MessageType.Warning);

                EditorGUI.BeginChangeCheck();
                Color color = EditorGUILayout.ColorField("Color", light.color);
                float intensity = EditorGUILayout.FloatField("Light Intensity", light.intensity);
                float angle = EditorGUILayout.Slider("Spot Angle", light.spotAngle, 1f, 179f);
                float range = EditorGUILayout.FloatField("Range", light.range);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(light, "Edit Light");
                    light.color = color;
                    light.intensity = intensity;
                    light.spotAngle = angle;
                    light.range = range;
                }

                var so = new SerializedObject(vl);
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("scattering"), new GUIContent("Ray Brightness"));
                EditorGUILayout.PropertyField(so.FindProperty("anisotropy"), new GUIContent("Glow Toward Light"));
                EditorGUILayout.PropertyField(so.FindProperty("radius"), new GUIContent("Soft Start Radius"));
                so.ApplyModifiedProperties();
            }
        }
    }

    static void CreateSpotLight()
    {
        var go = new GameObject("Volumetric Spot Light");
        Undo.RegisterCreatedObjectUndo(go, "Create Volumetric Spot Light");

        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.shadows = LightShadows.Soft;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.color = new Color(1f, 0.95f, 0.85f);
        light.intensity = 50f;
        light.range = 20f;
        light.spotAngle = 45f;
        go.AddComponent<VolumetricAdditionalLight>();

        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            go.transform.position = view.pivot;
            go.transform.rotation = Quaternion.Euler(45f, view.rotation.eulerAngles.y, 0f);
        }
        Selection.activeGameObject = go;
    }

    static void SetSelectedVolumetric(bool on)
    {
        foreach (var go in Selection.gameObjects)
        {
            var light = go.GetComponent<Light>();
            if (light == null) continue;
            var vl = go.GetComponent<VolumetricAdditionalLight>();

            if (on && vl == null)
            {
                if (light.type == LightType.Directional)
                {
                    Debug.LogWarning("Volumetric Light: " + go.name + " is a Directional Light. Use 'Sun Rays (Main Light)' in the Fog section instead.", go);
                    continue;
                }
                Undo.AddComponent<VolumetricAdditionalLight>(go);
                if (light.shadows == LightShadows.None)
                {
                    Undo.RecordObject(light, "Turn On Shadows");
                    light.shadows = LightShadows.Soft;
                }
            }
            else if (!on && vl != null)
            {
                Undo.DestroyObjectImmediate(vl);
            }
        }
    }

    // ---------- Old light shafts ----------

    void DrawOldShafts()
    {
        var shafts = new List<GameObject>();
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == "LightShaft") shafts.Add(mb.gameObject);
        if (shafts.Count == 0) return;

        EditorGUILayout.LabelField("Old Light Shafts", EditorStyles.boldLabel);
        if (GUILayout.Button("Hide " + shafts.Count + " Old Light Shaft(s)"))
        {
            foreach (var go in shafts)
            {
                Undo.RecordObject(go, "Hide Light Shaft");
                go.SetActive(false);
            }
        }
    }
}
