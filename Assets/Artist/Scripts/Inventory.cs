using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;


public class Inventory : MonoBehaviour
{
    public GameObject inventoryParent;
    private int slotsN = 10;
    public Sprite emptySprite;
    private Outline[] inventorySlots;
    private Image[] slotImages;
    private Color defaultOutlineColor = new Color(0.047f, 0.639f, 0.573f, 1f);
    private Color selectedOutlineColor = new Color(0.639f, 0.157f, 0.075f, 1f);
    private GameObject[] inventory;
    private int selectedSlot = 0;
    [SerializeField] private float dropOffset = 2f;

    private void Start()
    {
        inventory = new GameObject[slotsN];
        slotImages = inventoryParent.GetComponentsInChildren<Image>();
        inventorySlots = new Outline[slotsN];
        for (int i = 0; i < slotsN; i++)
        {
            inventorySlots[i] = inventoryParent.transform.GetChild(i).gameObject.GetComponent<Outline>();
        }
        UpdateInventory();
    }

    private void Update()
    {
        Collider[] items = Physics.OverlapSphere(transform.position, 5f, LayerMask.GetMask("Item"));

        foreach (var item in items)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                PickUpItem(item.gameObject);
            }
        }

        ScrollInventory(Convert.ToInt32(Input.mouseScrollDelta.y));

        if (Input.GetKeyDown(KeyCode.Q))
        {
            DropItem();
        }
    }

    private void UpdateInventory()
    {
        for (int i = 0; i < slotsN; i++)
        {
            inventorySlots[i].effectColor = new Color(0.07f, 0.11f, 0.56f, 0.5f);

            Image spriteImage = inventorySlots[i].transform.GetChild(0).gameObject.GetComponent<Image>();
            if (inventory[i] == null)
            {
                spriteImage.sprite = null;
            }
            else
            {
                spriteImage.sprite = inventory[i].GetComponent<SpriteRenderer>().sprite;
            }
        }
        inventorySlots[selectedSlot].effectColor = new Color(1f, 0.5f, 0.01f, 0.5f);
    }

    public void ScrollInventory(int scrollDelta)
    {
        selectedSlot += scrollDelta;
        selectedSlot = Numbers.WrapAround(selectedSlot, 0, slotsN - 1);
        UpdateInventory();
    }

    private void DropItem()
    {
        if (inventory[selectedSlot] != null)
        {
            Plane xyPlane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
            float enter = 0f;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Vector3 mousePos;
            if (xyPlane.Raycast(ray, out enter))
            {
                mousePos = ray.GetPoint(enter);
                mousePos.z = transform.position.z;
                Vector3 dir = (mousePos - transform.position).normalized;
                Vector3 dropDir = Vector3.Project(dir, Vector3.left);
                GameObject obj = inventory[selectedSlot];
                obj.transform.position = transform.position + dropOffset * dropDir;
                obj.SetActive(true);
                inventory[selectedSlot] = null;
            }
        }
    }

    private int FindEmptySlot()
    {
        int index = -1;
        for (int i = 0; i < slotsN; i++)
        {
            if (inventory[i] == null)
            {
                index = i;
                break;
            }
        }
        return index;
    }

    public void PickUpItem(GameObject item)
    {
        int index = FindEmptySlot();
        if (index != -1)
        {
            if (inventory[selectedSlot] == null)
            {
                inventory[selectedSlot] = item;
            }
            else
            {
                inventory[index] = item;
            }
            item.gameObject.SetActive(false);
        }
    }
}
