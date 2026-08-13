using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public GameObject cellPrefab;
    public int columns = 8, rows = 8;

    [Header("Set which cell is Start/End")]
    public Vector2Int startCoord = new Vector2Int(0, 0);
    public Vector2Int endCoord   = new Vector2Int(7, 7);

    void Start()
    {
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                GameObject go = Instantiate(cellPrefab, transform);
                CellController cc = go.GetComponent<CellController>();
                cc.gridX = x;
                cc.gridY = y;

                if (x == startCoord.x && y == startCoord.y)
                {
                    cc.cellType = CellType.Start;
                    cc.MarkAsRed();
                }
                else if (x == endCoord.x && y == endCoord.y)
                {
                    cc.cellType = CellType.End;
                    cc.MarkAsRed();
                }
            }
        }
    }
}