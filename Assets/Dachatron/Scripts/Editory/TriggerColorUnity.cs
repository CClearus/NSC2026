using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ColoredTriggerGizmo : MonoBehaviour
{
    [Header("Gizmo Settings")]
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.3f); // Semi-transparent green
    public bool drawOnlyWhenSelected = false;

    private BoxCollider boxCollider;

    void Awake() 
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected) return;
        DrawTriggerGizmo();
    }

    void OnDrawGizmosSelected()
    {
        if (drawOnlyWhenSelected)
        {
            DrawTriggerGizmo();
        }
    }

    void DrawTriggerGizmo()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) return;

        // Set the color code (RGBA)
        Gizmos.color = gizmoColor;

        // Match the collider's center and size
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        // Draw a solid cube and/or wire cube
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);
        
        Gizmos.matrix = oldMatrix;
    }
}
