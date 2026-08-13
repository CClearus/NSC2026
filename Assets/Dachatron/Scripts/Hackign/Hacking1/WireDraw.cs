using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// WireGame.cs
/// SETUP:
///   1. Attach to any GameObject in your Canvas.
///   2. Assign gridPanel  → your "Grid" RectTransform (315x275, pivot 0,0)
///   3. Assign cellStart  → your start Button
///   4. Assign cellEnd    → your end Button
///   5. Assign uiToClose  → panel to hide on win
///   6. Assign objectToHide → world object to disable on win
/// </summary>
public class WireGame : MonoBehaviour
{
    [Header("References")]
    public RectTransform gridPanel;
    public Button        cellStart;
    public Button        cellEnd;
    public GameObject    uiToClose;
    public GameObject    objectToHide;

    [Header("Wire Look")]
    public Color wireColor     = new Color(0.2f, 0.9f, 1f, 1f);
    [Range(0.05f, 0.9f)]
    public float wireThickness = 0.35f;

    // grid constants
    private const int COLS = 8;
    private const int ROWS = 8;

    private float cellW, cellH;
    private bool  isDrawing;
    private Camera uiCam;

    private Vector2Int headCell;
    private Vector2Int startCell;
    private Vector2Int endCell;

    private List<Vector2Int>    path     = new List<Vector2Int>();
    private HashSet<Vector2Int> visited  = new HashSet<Vector2Int>();
    private List<GameObject>    wireSegs = new List<GameObject>();
    private GameObject          headDot;

    // ── Start ────────────────────────────────────────────────────
    void Start()
    {
        if (gridPanel == null){ Debug.LogError("[WireGame] gridPanel is NULL"); enabled=false; return; }
        if (cellStart  == null){ Debug.LogError("[WireGame] cellStart is NULL");  enabled=false; return; }
        if (cellEnd    == null){ Debug.LogError("[WireGame] cellEnd is NULL");    enabled=false; return; }

        Canvas canvas = gridPanel.GetComponentInParent<Canvas>();
        uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;

        RefreshCellSize();
        Debug.Log($"[WireGame] Grid rect={gridPanel.rect}  cellW={cellW:F2}  cellH={cellH:F2}");

        // Hook start button with EventTrigger (PointerDown = hold to draw)
        AddPointerDown(cellStart.gameObject, OnStartPressed);

        // Pre-compute start / end cells from button positions
        startCell = ButtonToCell(cellStart);
        endCell   = ButtonToCell(cellEnd);
        Debug.Log($"[WireGame] startCell={startCell}  endCell={endCell}");
    }

    // ── Update ───────────────────────────────────────────────────
    void Update()
    {
        if (!isDrawing) return;

        if (Input.GetMouseButtonUp(0))
        {
            Debug.Log("[WireGame] Mouse released – cancelling wire.");
            CancelDrawing();
            return;
        }

        RefreshCellSize();
        Vector2Int mc = ScreenToCell(Input.mousePosition);
        mc.x = Mathf.Clamp(mc.x, 0, COLS-1);
        mc.y = Mathf.Clamp(mc.y, 0, ROWS-1);
        AdvanceHead(mc);
    }

    // ── Called when cellStart button is pressed down ─────────────
    void OnStartPressed()
    {
        Debug.Log("[WireGame] cellStart pressed – beginning wire draw.");
        ClearWire();
        RefreshCellSize();

        isDrawing = true;
        headCell  = startCell;
        path.Add(headCell);
        visited.Add(headCell);

        SpawnHeadDot(headCell);
        Debug.Log($"[WireGame] Wire started at cell {headCell}, headDot created={headDot != null}");
    }

    // ── Move head one 90° step toward mouse ──────────────────────
    void AdvanceHead(Vector2Int target)
    {
        if (target == headCell) return;

        int dx = target.x - headCell.x;
        int dy = target.y - headCell.y;
        Vector2Int step = Mathf.Abs(dx) >= Mathf.Abs(dy)
            ? new Vector2Int((int)Mathf.Sign(dx), 0)
            : new Vector2Int(0, (int)Mathf.Sign(dy));

        Vector2Int next = new Vector2Int(
            Mathf.Clamp(headCell.x + step.x, 0, COLS-1),
            Mathf.Clamp(headCell.y + step.y, 0, ROWS-1)
        );
        if (next == headCell) return;

        // Retract?
        if (path.Count >= 2 && next == path[path.Count - 2])
        {
            Retract(); return;
        }

        if (visited.Contains(next)) return;

        // Extend
        Vector2Int prev = headCell;
        headCell = next;
        path.Add(headCell);
        visited.Add(headCell);
        SpawnSegment(prev, headCell);
        MoveHeadDot(headCell);

        if (headCell == endCell) Complete();
    }

    void Retract()
    {
        visited.Remove(headCell);
        path.RemoveAt(path.Count - 1);
        headCell = path[path.Count - 1];
        if (wireSegs.Count > 0)
        {
            Destroy(wireSegs[wireSegs.Count - 1]);
            wireSegs.RemoveAt(wireSegs.Count - 1);
        }
        MoveHeadDot(headCell);
    }

    // ── Spawn wire segment between two adjacent cells ─────────────
    void SpawnSegment(Vector2Int from, Vector2Int to)
    {
        GameObject seg = new GameObject($"Wire_{from}->{to}");
        seg.transform.SetParent(gridPanel, false);
        seg.transform.SetAsFirstSibling();

        Image img = seg.AddComponent<Image>();
        img.color = wireColor;
        img.raycastTarget = false;

        RectTransform rt = seg.GetComponent<RectTransform>();

        // Anchor + pivot = bottom-left of gridPanel  (pivot 0,0)
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.zero;
        rt.pivot            = new Vector2(0.5f, 0.5f);

        Vector2 cA = CellCenter(from);
        Vector2 cB = CellCenter(to);
        rt.anchoredPosition = (cA + cB) * 0.5f;

        bool horiz = (from.y == to.y);
        float thick = Mathf.Min(cellW, cellH) * wireThickness;
        rt.sizeDelta = horiz
            ? new Vector2(cellW,  thick)
            : new Vector2(thick,  cellH);

        wireSegs.Add(seg);
        Debug.Log($"[WireGame] Segment {from}→{to}  pos={rt.anchoredPosition}  size={rt.sizeDelta}");
    }

    // ── Head dot ──────────────────────────────────────────────────
    void SpawnHeadDot(Vector2Int cell)
    {
        if (headDot != null) Destroy(headDot);
        headDot = new GameObject("WireHead");
        headDot.transform.SetParent(gridPanel, false);

        Image img = headDot.AddComponent<Image>();
        img.color = wireColor;
        img.raycastTarget = false;

        RectTransform rt = headDot.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.zero;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = CellCenter(cell);

        float dot = Mathf.Min(cellW, cellH) * wireThickness * 1.5f;
        rt.sizeDelta = new Vector2(dot, dot);

        Debug.Log($"[WireGame] HeadDot at {cell} → anchoredPos={rt.anchoredPosition}  size={rt.sizeDelta}");
    }

    void MoveHeadDot(Vector2Int cell)
    {
        if (headDot == null) { SpawnHeadDot(cell); return; }
        headDot.GetComponent<RectTransform>().anchoredPosition = CellCenter(cell);
    }

    // ── Complete / Cancel ─────────────────────────────────────────
    void Complete()
    {
        isDrawing = false;
        if (headDot != null) Destroy(headDot);
        Debug.Log("[WireGame] COMPLETE!");

        if (uiToClose != null)
        {
            CanvasGroup cg = uiToClose.GetComponent<CanvasGroup>();
            if (cg != null){ cg.alpha=0; cg.interactable=false; cg.blocksRaycasts=false; }
            else uiToClose.SetActive(false);
        }
        if (objectToHide != null) objectToHide.SetActive(false);
    }

    void CancelDrawing()
    {
        isDrawing = false;
        ClearWire();
    }

    void ClearWire()
    {
        foreach (var s in wireSegs) if (s) Destroy(s);
        wireSegs.Clear();
        if (headDot != null) { Destroy(headDot); headDot = null; }
        path.Clear();
        visited.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────

    void RefreshCellSize()
    {
        cellW = gridPanel.rect.width  / COLS;
        cellH = gridPanel.rect.height / ROWS;
    }

    /// Local anchoredPosition of the center of a cell.
    /// gridPanel pivot=(0,0) → origin is bottom-left.
    /// Row 0 = BOTTOM row (Unity UI y goes up).
    Vector2 CellCenter(Vector2Int cell)
    {
        return new Vector2((cell.x + 0.5f) * cellW,
                           (cell.y + 0.5f) * cellH);
    }

    /// Screen point → grid cell (col 0=left, row 0=bottom)
    Vector2Int ScreenToCell(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridPanel, screenPos, uiCam, out Vector2 local);

        // With pivot (0,0), local.x and local.y are already relative to bottom-left
        int col = Mathf.FloorToInt(local.x / cellW);
        int row = Mathf.FloorToInt(local.y / cellH);
        return new Vector2Int(col, row);
    }

    /// Converts a Button's screen position to grid cell
    Vector2Int ButtonToCell(Button btn)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, btn.transform.position);
        return ScreenToCell(screen);
    }

    /// Adds a PointerDown EventTrigger to a GameObject
    void AddPointerDown(GameObject go, System.Action callback)
    {
        EventTrigger et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener(_ => callback());
        et.triggers.Add(entry);
    }

    // ── Gizmos ────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (gridPanel == null) return;
        RefreshCellSize();
        Gizmos.color = new Color(0,1,1,0.3f);
        // pivot (0,0) so TransformPoint(0,0,0) = bottom-left corner
        Vector3 bl = gridPanel.TransformPoint(Vector3.zero);
        Vector3 rx = gridPanel.TransformDirection(Vector3.right);
        Vector3 ry = gridPanel.TransformDirection(Vector3.up);
        for(int c=0;c<=COLS;c++)
            Gizmos.DrawLine(bl+rx*(c*cellW), bl+rx*(c*cellW)+ry*(ROWS*cellH));
        for(int r=0;r<=ROWS;r++)
            Gizmos.DrawLine(bl+ry*(r*cellH), bl+ry*(r*cellH)+rx*(COLS*cellW));
    }
#endif
}