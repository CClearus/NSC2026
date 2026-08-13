using UnityEngine;

public class Crosshair : MonoBehaviour
{
    private RectTransform rectTransform;
    public Canvas canvas;
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        Cursor.visible = false;
    }

    void Update()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, 
            Input.mousePosition, 
            canvas.worldCamera, 
            out Vector2 localPoint
        );

        rectTransform.anchoredPosition = localPoint;
    }
}
