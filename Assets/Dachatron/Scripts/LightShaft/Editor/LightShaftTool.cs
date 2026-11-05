using UnityEditor;
using UnityEngine;

public class LightShaftTool : EditorWindow
{
    const string ShaderName = "Dachatron/LightShaft";
    const string MaterialPath = "Assets/Dachatron/Mats/LightShaft.mat";
    const string MeshFolder = "Assets/Dachatron/Mats/LightShaftMeshes";

    LightShaft editing;
    Transform aimTarget;
    Vector2 scroll;

    [MenuItem("Tools/Light Shaft")]
    static void Open() => GetWindow<LightShaftTool>("Light Shaft");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("The shaft points along the cube's blue (Z) axis. Rotate the cube with the normal gizmo, or use the controls below.", MessageType.Info);

        if (GUILayout.Button("Create New Light Shaft", GUILayout.Height(28)))
            CreateShaft();
        if (GUILayout.Button("Convert Selected Object(s) To Light Shaft"))
            ConvertSelected();

        EditorGUILayout.Space();
        // Remember the last selected shaft so you can select walls without losing it.
        var selected = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<LightShaft>() : null;
        if (selected != null) editing = selected;
        var shaft = editing;
        if (shaft == null)
        {
            EditorGUILayout.LabelField("Select a Light Shaft to edit it.");
            return;
        }
        EditorGUILayout.LabelField("Editing: " + shaft.name, EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
        Vector3 e = shaft.transform.eulerAngles;
        float pitch = NormalizeAngle(e.x), yaw = e.y;

        EditorGUI.BeginChangeCheck();
        pitch = EditorGUILayout.Slider("Pitch (down = +)", pitch, -90f, 90f);
        yaw = EditorGUILayout.Slider("Yaw", yaw, 0f, 360f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(shaft.transform, "Aim Light Shaft");
            shaft.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        aimTarget = (Transform)EditorGUILayout.ObjectField("Point At Target", aimTarget, typeof(Transform), true);
        using (new EditorGUI.DisabledScope(aimTarget == null))
        {
            if (GUILayout.Button("Aim At Target"))
            {
                Undo.RecordObject(shaft.transform, "Aim Light Shaft");
                shaft.transform.rotation = Quaternion.LookRotation(aimTarget.position - shaft.transform.position);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape To Hole", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Put the cube's start face (opposite the blue arrow) just outside the wall, big enough to cover the whole hole, then click Shape To Hole.", MessageType.None);
        if (GUILayout.Button("Shape To Hole", GUILayout.Height(28)))
            ShapeToHole(shaft);
        if (GUILayout.Button("Reset To Box"))
            ResetToBox(shaft);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Walls (optional)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select wall pieces in the Hierarchy (this shaft stays the one being edited), then add them. Don't add a parent that also holds the floor.", MessageType.None);
        if (GUILayout.Button("Add Selected Objects As Walls"))
            AddSelectedAsWalls(shaft);
        if (GUILayout.Button("Clear Walls"))
        {
            Undo.RecordObject(shaft, "Clear Walls");
            shaft.walls.Clear();
            shaft.Apply();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Fit Length To Surface"))
            FitLength(shaft);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
        var editor = UnityEditor.Editor.CreateEditor(shaft);
        editor.DrawDefaultInspector();
        DestroyImmediate(editor);

        EditorGUILayout.EndScrollView();
    }

    void OnSelectionChange() => Repaint();

    static void AddSelectedAsWalls(LightShaft shaft)
    {
        Undo.RecordObject(shaft, "Add Walls");
        foreach (var go in Selection.gameObjects)
            if (go != shaft.gameObject && !shaft.walls.Contains(go)) shaft.walls.Add(go);
        shaft.Apply();
    }

    internal static void ShapeToHole(LightShaft shaft)
    {
        Undo.RecordObject(shaft.transform, "Shape Light Shaft To Hole");
        var mesh = shaft.BuildHoleMesh();
        if (mesh == null)
        {
            Debug.LogWarning("Light Shaft: no ray got through. Move the start face outside the wall, make the cube cover the hole, or lower Wall Depth. If you set Walls, make sure the floor isn't one of them.", shaft);
            return;
        }

        // Save the mesh as an asset so the scene keeps it.
        var filter = shaft.GetComponent<MeshFilter>();
        string existingPath = filter.sharedMesh ? AssetDatabase.GetAssetPath(filter.sharedMesh) : null;
        if (!string.IsNullOrEmpty(existingPath) && existingPath.StartsWith(MeshFolder))
        {
            var existing = filter.sharedMesh;
            EditorUtility.CopySerialized(mesh, existing);
            DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        else
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/Dachatron/Mats", "LightShaftMeshes");
            string path = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/" + shaft.name + "_HoleShape.asset");
            AssetDatabase.CreateAsset(mesh, path);
        }
        AssetDatabase.SaveAssets();

        Undo.RecordObject(filter, "Shape Light Shaft To Hole");
        filter.sharedMesh = mesh;
    }

    internal static void ResetToBox(LightShaft shaft)
    {
        var filter = shaft.GetComponent<MeshFilter>();
        Undo.RecordObject(filter, "Reset Light Shaft");
        filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
    }

    static float NormalizeAngle(float a) => a > 180f ? a - 360f : a;

    internal static void FitLength(LightShaft shaft)
    {
        Undo.RecordObject(shaft.transform, "Fit Light Shaft");
        if (!shaft.FitLength())
            Debug.LogWarning("Light Shaft: nothing hit along its direction within Max Fit Distance.", shaft);
    }

    static Material GetMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;

        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("Light Shaft: shader '" + ShaderName + "' not found.");
            return null;
        }
        mat = new Material(shader) { name = "LightShaft" };
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }

    static void CreateShaft()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "LightShaft";
        Undo.RegisterCreatedObjectUndo(go, "Create Light Shaft");

        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            go.transform.position = view.pivot;
            go.transform.rotation = Quaternion.Euler(60f, view.rotation.eulerAngles.y, 0f);
        }
        go.transform.localScale = new Vector3(1f, 1f, 4f);

        Setup(go);
        Selection.activeGameObject = go;
    }

    static void ConvertSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent<MeshRenderer>() == null) continue;
            Setup(go);
        }
    }

    static void Setup(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Undo.DestroyObjectImmediate(col);

        var mr = go.GetComponent<MeshRenderer>();
        var mat = GetMaterial();
        if (mat != null) { Undo.RecordObject(mr, "Light Shaft Material"); mr.sharedMaterial = mat; }

        if (go.GetComponent<LightShaft>() == null) Undo.AddComponent<LightShaft>(go);
        go.GetComponent<LightShaft>().Apply();
    }
}

[CustomEditor(typeof(LightShaft))]
public class LightShaftInspector : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Shape To Hole"))
            foreach (var t in targets) LightShaftTool.ShapeToHole((LightShaft)t);
        if (GUILayout.Button("Reset To Box"))
            foreach (var t in targets) LightShaftTool.ResetToBox((LightShaft)t);
        if (GUILayout.Button("Fit Length To Surface"))
            foreach (var t in targets) LightShaftTool.FitLength((LightShaft)t);
    }
}
