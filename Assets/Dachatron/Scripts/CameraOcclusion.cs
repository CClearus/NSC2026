using UnityEngine;
using System.Collections.Generic;

public class CameraOcclusionTransparency : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Exclude Layers")]
    public LayerMask excludeLayers;
    

    [Header("Transparency Settings")]
    [Range(0f, 1f)]
    public float hiddenAlpha = 0.2f;

    public float fadeOutSpeed = 8f;

    public float fadeInSpeed = 4f;

    // Store Cache
    private List<Renderer> currentlyBlockingRenderers = new List<Renderer>();
    private List<Renderer> fadingBackRenderers = new List<Renderer>();

    private Dictionary<Renderer, float> originalAlphaValues = new Dictionary<Renderer, float>();

    private void Update()
    {
        if (player == null)
        {
            Debug.LogError("[Occlusion] Player is not assigned in the Inspector!");
            return;
        }

        currentlyBlockingRenderers.Clear();

        // Raycast
        Vector3 directionToPlayer = player.position - transform.position;
        float distanceToPlayer    = directionToPlayer.magnitude;
        int raycastMask           = ~excludeLayers;

        RaycastHit[] hits = Physics.RaycastAll(
            transform.position,
            directionToPlayer.normalized,
            distanceToPlayer,
            raycastMask
        );

        // Fade out
        foreach (RaycastHit hit in hits)
        {
            Renderer hitRenderer = hit.collider.GetComponent<Renderer>();

            if (hitRenderer == null)
            {
                continue;
            }

    
            if (originalAlphaValues.ContainsKey(hitRenderer) == false)
            {
                float originalAlpha = hitRenderer.material.color.a;
                originalAlphaValues[hitRenderer] = originalAlpha;

                if (hitRenderer.material.HasProperty("_Surface") && hitRenderer.material.GetFloat("_Surface") < 1f)
                {
                    Debug.LogWarning("[Occlusion] '" + hit.collider.gameObject.name +
                        "' Surface Type is Opaque. Open the material and set Surface Type = Transparent.");
                }
            }

            // stop recusrive
            if (fadingBackRenderers.Contains(hitRenderer))
            {
                fadingBackRenderers.Remove(hitRenderer);
            }

            currentlyBlockingRenderers.Add(hitRenderer);

            // Fade toward hidden alpha
            Color color = hitRenderer.material.color;
            color.a     = Mathf.Lerp(color.a, hiddenAlpha, Time.deltaTime * fadeOutSpeed);
            hitRenderer.material.color = color;
        }

        foreach (Renderer storedRenderer in originalAlphaValues.Keys)
        {
            bool isStillBlocking  = currentlyBlockingRenderers.Contains(storedRenderer);
            bool isAlreadyFading  = fadingBackRenderers.Contains(storedRenderer);

            if (isStillBlocking == false && isAlreadyFading == false)
            {
                fadingBackRenderers.Add(storedRenderer);
            }
        }

        // Return
        List<Renderer> fullyRestoredRenderers = new List<Renderer>();

        foreach (Renderer fadingRenderer in fadingBackRenderers)
        {
            if (originalAlphaValues.ContainsKey(fadingRenderer) == false)
            {
                continue;
            }

            float targetAlpha = originalAlphaValues[fadingRenderer];
            Color color       = fadingRenderer.material.color;

            color.a = Mathf.Lerp(color.a, targetAlpha, Time.deltaTime * fadeInSpeed);
            fadingRenderer.material.color = color;

            if (Mathf.Abs(color.a - targetAlpha) < 0.01f)
            {
                color.a = targetAlpha;
                fadingRenderer.material.color = color;
                fullyRestoredRenderers.Add(fadingRenderer);
            }
        }

        foreach (Renderer restoredRenderer in fullyRestoredRenderers)
        {
            fadingBackRenderers.Remove(restoredRenderer);
            originalAlphaValues.Remove(restoredRenderer);
        }
    }
}