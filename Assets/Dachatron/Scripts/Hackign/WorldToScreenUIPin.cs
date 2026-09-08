using UnityEngine;

/// <summary>
/// Pins a UI element on a Screen Space - Overlay canvas to the on-screen
/// projection of a world-space marker (e.g. an empty GameObject placed where
/// you want the "Press E" prompt to hover, such as just above a door).
///
/// The marker itself needs no visuals - an empty GameObject with no Renderer
/// already draws nothing, so there's nothing to hide.
///
/// SETUP:
///   1. Create an empty GameObject where you want the prompt to appear
///      (e.g. child it to the door, offset upward) - this is "worldTarget".
///   2. Put the Canvas that hosts this UI in Render Mode: Screen Space - Overlay.
///   3. Attach this script to the UI element you want pinned (e.g. "Diddy").
///   4. Assign worldTarget, trackingCamera (your player/main camera) and
///      canvasRect (the Canvas's own RectTransform - the root, not this object).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WorldToScreenUIPin : MonoBehaviour
{
    [Tooltip("Empty world-space marker to track - place it where you want the UI to appear.")]
    public Transform worldTarget;

    [Tooltip("Camera used to project worldTarget into screen space.")]
    public Camera trackingCamera;

    [Tooltip("The Screen Space - Overlay canvas's own RectTransform (the root canvas, not this element).")]
    public RectTransform canvasRect;

    [Tooltip("Extra offset in screen pixels, applied after projecting the world position.")]
    public Vector2 screenOffset;

    [Tooltip("Keep the UI fully on-screen, snapping to the nearest edge when the target is out of the camera's view instead of disappearing off-canvas.")]
    public bool clampToScreen = true;

    [Tooltip("Extra breathing room (in canvas units) kept between the UI's edge and the screen edge when clamping.")]
    public float clampMargin = 0f;

    [Tooltip("Log the computed screen/local point about once a second - handy while debugging, turn off when done.")]
    public bool debugLog = false;

    private RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (trackingCamera == null) trackingCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (worldTarget == null || trackingCamera == null || canvasRect == null)
        {
            if (debugLog && Time.frameCount % 60 == 0)
                Debug.LogWarning($"[WorldToScreenUIPin] Missing reference - worldTarget={worldTarget}, trackingCamera={trackingCamera}, canvasRect={canvasRect}");
            return;
        }

        Vector3 screenPoint = trackingCamera.WorldToScreenPoint(worldTarget.position);
        bool behindCamera = screenPoint.z < 0f;

        // Target is behind the camera - projecting it would flip it to the
        // wrong side of the screen, so just leave the UI where it last was.
        if (behindCamera)
        {
            if (debugLog && Time.frameCount % 60 == 0)
                Debug.Log($"[WorldToScreenUIPin] {worldTarget.name} is BEHIND the camera (screenPoint={screenPoint}) - UI frozen at last position {rt.anchoredPosition}.");
            return;
        }

        // Screen Space - Overlay contract: pass a null camera here.
        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
        if (converted)
        {
            Vector2 finalPos = localPoint + screenOffset;

            if (clampToScreen)
            {
                Rect canvasLocalRect = canvasRect.rect;
                Vector2 halfSize = rt.rect.size * 0.5f;

                float minX = canvasLocalRect.xMin + halfSize.x + clampMargin;
                float maxX = canvasLocalRect.xMax - halfSize.x - clampMargin;
                float minY = canvasLocalRect.yMin + halfSize.y + clampMargin;
                float maxY = canvasLocalRect.yMax - halfSize.y - clampMargin;

                finalPos.x = Mathf.Clamp(finalPos.x, minX, maxX);
                finalPos.y = Mathf.Clamp(finalPos.y, minY, maxY);
            }

            rt.anchoredPosition = finalPos;
        }

        if (debugLog && Time.frameCount % 60 == 0)
            Debug.Log($"[WorldToScreenUIPin] target={worldTarget.name} worldPos={worldTarget.position} screenPoint={screenPoint} " +
                      $"screenSize=({Screen.width}x{Screen.height}) camPixelSize=({trackingCamera.pixelWidth}x{trackingCamera.pixelHeight}) " +
                      $"canvasRect(w/h)=({canvasRect.rect.width}x{canvasRect.rect.height}) converted={converted} localPoint={localPoint} -> anchoredPosition={rt.anchoredPosition}");
    }

    void OnDrawGizmos()
    {
        if (worldTarget == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(worldTarget.position, 0.15f);
        if (trackingCamera != null)
            Gizmos.DrawLine(trackingCamera.transform.position, worldTarget.position);
    }
}
