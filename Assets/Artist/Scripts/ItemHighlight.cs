using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ItemHighlight : MonoBehaviour
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 0.02f;

    private static Shader outlineShader;
    private GameObject outlineObject;
    private bool isHighlighted;

    private void Awake()
    {
        if (outlineShader == null)
        {
            outlineShader = Shader.Find("Custom/OutlineWhite");
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (outlineShader == null || meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(transform, false);

        MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = meshFilter.sharedMesh;

        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        Material outlineMaterial = new Material(outlineShader);
        outlineMaterial.SetColor("_OutlineColor", outlineColor);
        outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
        outlineRenderer.sharedMaterial = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;

        outlineObject.SetActive(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (isHighlighted == highlighted || outlineObject == null)
        {
            return;
        }

        isHighlighted = highlighted;
        outlineObject.SetActive(highlighted);
    }
}
