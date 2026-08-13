using UnityEngine;

public class WireManager : MonoBehaviour
{
    public static WireManager Instance;

    public RectTransform lineSegmentPrefab; // UI Image, pivot (0, 0.5)
    public Transform lineParent;            // usually GridPanel or Canvas
    public float lineThickness = 6f;

    private CellController startCell;
    private RectTransform currentLine;
    private Camera uiCamera; // null for Overlay canvas

    void Awake() => Instance = this;

    public void BeginDrag(CellController cell)
    {
        startCell = cell;
        currentLine = Instantiate(lineSegmentPrefab, lineParent);
    }

    public void UpdateDrag(CellController cell)
    {
        if (startCell == null || currentLine == null) return;
        DrawLine((RectTransform)startCell.transform, Input.mousePosition);
    }

    void Update()
    {
        if (startCell != null && currentLine != null)
            DrawLine((RectTransform)startCell.transform, Input.mousePosition);
    }

    public void EndDrag(CellController cell)
    {
        if (startCell == null) return;

        if (cell != null && cell.cellType == CellType.End)
        {
            // success!
            GameManager.Instance.OnWireComplete();
        }
        else
        {
            if (currentLine != null) Destroy(currentLine.gameObject);
        }

        startCell = null;
        currentLine = null;
    }

    void DrawLine(RectTransform from, Vector2 screenPos)
    {
        RectTransform parentRect = (RectTransform)lineParent;
        Vector2 localMouse;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, screenPos, uiCamera, out localMouse);

        Vector2 startPos = from.anchoredPosition;
        Vector2 dir = localMouse - startPos;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        currentLine.anchoredPosition = startPos;
        currentLine.sizeDelta = new Vector2(distance, lineThickness);
        currentLine.localRotation = Quaternion.Euler(0, 0, angle);
    }
}