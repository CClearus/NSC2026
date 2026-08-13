using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum CellType { Empty, Start, End }

public class CellController : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
{
    public CellType cellType = CellType.Empty;
    public int gridX, gridY;
    private Image img;

    void Awake()
    {
        img = GetComponent<Image>();
        img.raycastTarget = true;
    }

    public void MarkAsRed()
    {
        img.color = Color.red;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (cellType == CellType.Start)
            WireManager.Instance.BeginDrag(this);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        WireManager.Instance.UpdateDrag(this);
    }

    public void OnPointerUp(PointerEventData e)
    {
        WireManager.Instance.EndDrag(this);
    }
}