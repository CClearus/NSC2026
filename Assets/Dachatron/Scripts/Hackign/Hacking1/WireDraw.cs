using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// WireGame.cs
/// A Flow-Free style "connect the dots" grid puzzle. Supports any number of
/// colored dot pairs on the same grid: drag from one dot of a pair, snake an
/// orthogonal wire through the grid cells, and release on the matching dot.
/// Wires cannot cross cells already used by another pair. When every pair is
/// connected, the puzzle is solved.
///
/// SETUP:
///   1. Attach to any GameObject in your Canvas.
///   2. Assign gridPanel -> your "Grid" RectTransform (pivot 0,0).
///   3. Either:
///        a) fill in "pairs" by hand in the Inspector (one entry per colored
///           dot pair, each with its Start/End Button), or
///        b) leave "pairs" empty and let Hacking1.cs populate it at runtime
///           from the level data (it registers pairs before this script's
///           Start() runs), or
///        c) keep using the legacy single cellStart/cellEnd/wireColor fields
///           for a one-wire puzzle - they're auto-added as an extra pair.
///   4. Assign uiToClose -> panel to hide when ALL pairs are connected.
///   5. Assign objectToHide -> world object to disable on win.
/// </summary>
public class WireGame : MonoBehaviour
{
    [System.Serializable]
    public class WirePair
    {
        public Button start;
        public Button end;
        public Color color = Color.white;

        [HideInInspector] public Vector2Int startCell;
        [HideInInspector] public Vector2Int endCell;
        [HideInInspector] public bool isComplete;
        [HideInInspector] public List<Vector2Int> path = new List<Vector2Int>();
        [HideInInspector] public List<GameObject> wireSegs = new List<GameObject>();
        [HideInInspector] public GameObject headDot;
    }

    [Header("References")]
    public RectTransform gridPanel;
    public GameObject uiToClose;
    public GameObject objectToHide;

    [Header("Legacy single-pair fields (optional - kept for backwards compatibility)")]
    public Button cellStart;
    public Button cellEnd;
    public Color wireColor = new Color(0.2f, 0.9f, 1f, 1f);

    [Header("Colored dot pairs - one entry per wire to connect")]
    public List<WirePair> pairs = new List<WirePair>();

    [Header("Grid size")]
    public int columns = 8;
    public int rows = 8;

    [Header("Wire Look")]
    [Range(0.05f, 0.9f)]
    public float wireThickness = 0.35f;

    [Header("Grid Look")]
    public bool drawGridLines = true;
    public Color gridLineColor = new Color(1f, 1f, 1f, 0.15f);
    public float gridLineThickness = 2f;

    [Header("Events")]
    public UnityEvent onAllConnected;

    static readonly Color[] Palette =
    {
        new Color(0.90f, 0.15f, 0.15f), // red
        new Color(0.95f, 0.85f, 0.10f), // yellow
        new Color(0.55f, 0.25f, 0.85f), // purple
        new Color(0.15f, 0.55f, 0.95f), // blue
        new Color(0.95f, 0.30f, 0.55f), // pink
        new Color(0.20f, 0.75f, 0.30f), // green
        new Color(0.95f, 0.55f, 0.15f), // orange
        new Color(0.20f, 0.85f, 0.85f), // cyan
    };

    private float cellW, cellH;
    private Camera uiCam;

    private WirePair activePair;
    private bool isDrawing;
    private Vector2Int headCell;
    private Vector2Int activeTargetCell;

    // Which pair "owns" a given cell (either a permanent endpoint, or a cell
    // currently part of that pair's drawn path). Used to block other wires
    // from crossing through it.
    private readonly Dictionary<Vector2Int, WirePair> cellOwner = new Dictionary<Vector2Int, WirePair>();
    private readonly HashSet<Vector2Int> endpointCells = new HashSet<Vector2Int>();
    private readonly List<GameObject> gridLineObjects = new List<GameObject>();
    private bool initialized;
    private bool gridLinesBuilt;

    // ── Start ────────────────────────────────────────────────────
    void Start()
    {
        EnsureInitialized();
        SetupPairs();
    }

    // One-time setup (canvas camera lookup, legacy single-pair fold-in). Safe to
    // call more than once - later calls are no-ops.
    void EnsureInitialized()
    {
        if (initialized) return;
        if (gridPanel == null) { Debug.LogError("[WireGame] gridPanel is NULL"); enabled = false; return; }

        // Fold the legacy single-pair fields into the pairs list if set.
        if (cellStart != null && cellEnd != null && !pairs.Exists(p => p.start == cellStart && p.end == cellEnd))
            pairs.Insert(0, new WirePair { start = cellStart, end = cellEnd, color = wireColor });

        Canvas canvas = gridPanel.GetComponentInParent<Canvas>();
        uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;

        initialized = true;
    }

    /// (Re)builds click handlers and cell ownership for every entry currently in
    /// "pairs". Call this again after adding pairs at runtime (e.g. from a
    /// spawner like Hacking1.cs) so newly added dots become draggable - Start()
    /// only runs once and may fire before a spawner has added anything.
    public void SetupPairs()
    {
        EnsureInitialized();
        if (gridPanel == null) return;

        RefreshCellSize();
        BuildGridLines();

        cellOwner.Clear();
        endpointCells.Clear();

        for (int i = 0; i < pairs.Count; i++)
        {
            WirePair pair = pairs[i];
            if (pair == null || pair.start == null || pair.end == null)
            {
                Debug.LogWarning($"[WireGame] pairs[{i}] is missing a Start or End button - skipping.");
                continue;
            }

            if (pair.color.a <= 0f) pair.color = Palette[i % Palette.Length];

            pair.startCell = ButtonToCell(pair.start);
            pair.endCell = ButtonToCell(pair.end);
            pair.isComplete = false;
            pair.path.Clear();

            endpointCells.Add(pair.startCell);
            endpointCells.Add(pair.endCell);
            cellOwner[pair.startCell] = pair;
            cellOwner[pair.endCell] = pair;

            SetDotColor(pair.start, pair.color);
            SetDotColor(pair.end, pair.color);

            WirePair captured = pair; // local copy so each closure keeps its own pair
            AddPointerDown(pair.start.gameObject, () => OnDotPressed(captured, true));
            AddPointerDown(pair.end.gameObject, () => OnDotPressed(captured, false));
        }
    }

    // ── Update ───────────────────────────────────────────────────
    void Update()
    {
        if (!isDrawing || activePair == null) return;

        if (Input.GetMouseButtonUp(0))
        {
            CancelDrawing();
            return;
        }

        RefreshCellSize();
        Vector2Int mc = ScreenToCell(Input.mousePosition);
        mc.x = Mathf.Clamp(mc.x, 0, columns - 1);
        mc.y = Mathf.Clamp(mc.y, 0, rows - 1);
        AdvanceHead(mc);
    }

    // ── Called when a pair's Start or End dot is pressed down ────
    void OnDotPressed(WirePair pair, bool fromStart)
    {
        ClearPairWire(pair);
        RefreshCellSize();

        activePair = pair;
        isDrawing = true;
        headCell = fromStart ? pair.startCell : pair.endCell;
        activeTargetCell = fromStart ? pair.endCell : pair.startCell;
        pair.path.Add(headCell);

        SpawnHeadDot(pair, headCell);
    }

    // ── Move head one 90° step toward mouse ──────────────────────
    void AdvanceHead(Vector2Int target)
    {
        WirePair pair = activePair;
        if (target == headCell) return;

        int dx = target.x - headCell.x;
        int dy = target.y - headCell.y;
        Vector2Int step = Mathf.Abs(dx) >= Mathf.Abs(dy)
            ? new Vector2Int((int)Mathf.Sign(dx), 0)
            : new Vector2Int(0, (int)Mathf.Sign(dy));

        Vector2Int next = new Vector2Int(
            Mathf.Clamp(headCell.x + step.x, 0, columns - 1),
            Mathf.Clamp(headCell.y + step.y, 0, rows - 1)
        );
        if (next == headCell) return;

        // Retract?
        if (pair.path.Count >= 2 && next == pair.path[pair.path.Count - 2])
        {
            Retract(pair);
            return;
        }

        // Blocked: cell belongs to another pair's dot or wire.
        if (cellOwner.TryGetValue(next, out WirePair owner) && owner != pair) return;

        // Blocked: cell is already part of this pair's own path.
        if (pair.path.Contains(next)) return;

        Vector2Int prev = headCell;
        headCell = next;
        pair.path.Add(headCell);
        if (!endpointCells.Contains(headCell)) cellOwner[headCell] = pair;

        SpawnSegment(pair, prev, headCell);
        MoveHeadDot(pair, headCell);

        if (headCell == activeTargetCell) Complete(pair);
    }

    void Retract(WirePair pair)
    {
        Vector2Int leaving = headCell;
        if (!endpointCells.Contains(leaving)) cellOwner.Remove(leaving);

        pair.path.RemoveAt(pair.path.Count - 1);
        headCell = pair.path[pair.path.Count - 1];

        if (pair.wireSegs.Count > 0)
        {
            Destroy(pair.wireSegs[pair.wireSegs.Count - 1]);
            pair.wireSegs.RemoveAt(pair.wireSegs.Count - 1);
        }
        MoveHeadDot(pair, headCell);
    }

    // ── Complete / Cancel ─────────────────────────────────────────
    void Complete(WirePair pair)
    {
        isDrawing = false;
        activePair = null;
        pair.isComplete = true;

        if (pair.headDot != null) { Destroy(pair.headDot); pair.headDot = null; }

        Debug.Log($"[WireGame] Pair connected: {pair.start.name} -> {pair.end.name}");

        if (AllPairsComplete()) OnAllConnected();
    }

    bool AllPairsComplete()
    {
        if (pairs.Count == 0) return false;
        foreach (WirePair p in pairs)
        {
            if (p == null || p.start == null || p.end == null) continue;
            if (!p.isComplete) return false;
        }
        return true;
    }

    void OnAllConnected()
    {
        Debug.Log("[WireGame] ALL WIRES CONNECTED - puzzle solved!");

        if (uiToClose != null)
        {
            CanvasGroup cg = uiToClose.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 0; cg.interactable = false; cg.blocksRaycasts = false; }
            else uiToClose.SetActive(false);
        }
        if (objectToHide != null) objectToHide.SetActive(false);

        onAllConnected?.Invoke();
    }

    void CancelDrawing()
    {
        isDrawing = false;
        if (activePair != null) ClearPairWire(activePair);
        activePair = null;
    }

    void ClearPairWire(WirePair pair)
    {
        foreach (GameObject s in pair.wireSegs) if (s) Destroy(s);
        pair.wireSegs.Clear();
        if (pair.headDot != null) { Destroy(pair.headDot); pair.headDot = null; }

        foreach (Vector2Int cell in pair.path)
            if (!endpointCells.Contains(cell)) cellOwner.Remove(cell);

        pair.path.Clear();
        pair.isComplete = false;
    }

    /// Clears every wire and lets the player try the puzzle again.
    public void ResetPuzzle()
    {
        foreach (WirePair pair in pairs)
            if (pair != null) ClearPairWire(pair);

        isDrawing = false;
        activePair = null;
    }

    // ── Spawn wire segment between two adjacent cells ─────────────
    void SpawnSegment(WirePair pair, Vector2Int from, Vector2Int to)
    {
        GameObject seg = new GameObject($"Wire_{pair.start.name}_{from}->{to}");
        seg.transform.SetParent(gridPanel, false);
        seg.transform.SetAsFirstSibling();

        Image img = seg.AddComponent<Image>();
        img.color = pair.color;
        img.raycastTarget = false;

        RectTransform rt = seg.GetComponent<RectTransform>();

        // Anchor + pivot = bottom-left of gridPanel  (pivot 0,0)
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 cA = CellCenter(from);
        Vector2 cB = CellCenter(to);
        rt.anchoredPosition = (cA + cB) * 0.5f;

        bool horiz = (from.y == to.y);
        float thick = Mathf.Min(cellW, cellH) * wireThickness;
        rt.sizeDelta = horiz
            ? new Vector2(cellW, thick)
            : new Vector2(thick, cellH);

        pair.wireSegs.Add(seg);
    }

    // ── Head dot ──────────────────────────────────────────────────
    void SpawnHeadDot(WirePair pair, Vector2Int cell)
    {
        if (pair.headDot != null) Destroy(pair.headDot);
        pair.headDot = new GameObject("WireHead");
        pair.headDot.transform.SetParent(gridPanel, false);

        Image img = pair.headDot.AddComponent<Image>();
        img.color = pair.color;
        img.raycastTarget = false;

        RectTransform rt = pair.headDot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = CellCenter(cell);

        float dot = Mathf.Min(cellW, cellH) * wireThickness * 1.5f;
        rt.sizeDelta = new Vector2(dot, dot);
    }

    void MoveHeadDot(WirePair pair, Vector2Int cell)
    {
        if (pair.headDot == null) { SpawnHeadDot(pair, cell); return; }
        pair.headDot.GetComponent<RectTransform>().anchoredPosition = CellCenter(cell);
    }

    void SetDotColor(Button btn, Color color)
    {
        if (btn == null) return;
        Image img = btn.targetGraphic as Image;
        if (img == null) img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    // ── Helpers ───────────────────────────────────────────────────

    void RefreshCellSize()
    {
        cellW = gridPanel.rect.width / columns;
        cellH = gridPanel.rect.height / rows;
    }

    /// Local anchoredPosition of the center of a cell.
    /// gridPanel pivot=(0,0) -> origin is bottom-left.
    /// Row 0 = BOTTOM row (Unity UI y goes up).
    Vector2 CellCenter(Vector2Int cell)
    {
        return new Vector2((cell.x + 0.5f) * cellW,
                           (cell.y + 0.5f) * cellH);
    }

    /// Screen point -> grid cell (col 0=left, row 0=bottom)
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

    /// Adds a PointerDown EventTrigger to a GameObject. Safe to call more than
    /// once on the same object (e.g. if SetupPairs() re-runs) - any previous
    /// PointerDown entry is replaced instead of stacking duplicates.
    void AddPointerDown(GameObject go, System.Action callback)
    {
        EventTrigger et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        et.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerDown);
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener(_ => callback());
        et.triggers.Add(entry);
    }

    /// Draws faint grid lines at runtime so the play field actually looks like a
    /// grid (the gizmo grid below only shows up in the editor's Scene view).
    void BuildGridLines()
    {
        if (gridLinesBuilt || !drawGridLines) return;
        gridLinesBuilt = true;

        for (int c = 0; c <= columns; c++)
        {
            GameObject line = new GameObject($"GridLine_V{c}");
            line.transform.SetParent(gridPanel, false);
            line.transform.SetAsFirstSibling();

            Image img = line.AddComponent<Image>();
            img.color = gridLineColor;
            img.raycastTarget = false;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(c * cellW, rows * cellH * 0.5f);
            rt.sizeDelta = new Vector2(gridLineThickness, rows * cellH);

            gridLineObjects.Add(line);
        }

        for (int r = 0; r <= rows; r++)
        {
            GameObject line = new GameObject($"GridLine_H{r}");
            line.transform.SetParent(gridPanel, false);
            line.transform.SetAsFirstSibling();

            Image img = line.AddComponent<Image>();
            img.color = gridLineColor;
            img.raycastTarget = false;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(columns * cellW * 0.5f, r * cellH);
            rt.sizeDelta = new Vector2(columns * cellW, gridLineThickness);

            gridLineObjects.Add(line);
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (gridPanel == null) return;
        RefreshCellSize();
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        // pivot (0,0) so TransformPoint(0,0,0) = bottom-left corner
        Vector3 bl = gridPanel.TransformPoint(Vector3.zero);
        Vector3 rx = gridPanel.TransformDirection(Vector3.right);
        Vector3 ry = gridPanel.TransformDirection(Vector3.up);
        for (int c = 0; c <= columns; c++)
            Gizmos.DrawLine(bl + rx * (c * cellW), bl + rx * (c * cellW) + ry * (rows * cellH));
        for (int r = 0; r <= rows; r++)
            Gizmos.DrawLine(bl + ry * (r * cellH), bl + ry * (r * cellH) + rx * (columns * cellW));
    }
#endif
}
