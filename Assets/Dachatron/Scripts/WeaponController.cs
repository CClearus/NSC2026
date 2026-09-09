using UnityEngine;

// Toggles "aim mode": pressing equipKey shows the crosshair and enables the Gun's
// firing (via Gun.enabled - its Update, and therefore shooting, only runs while
// equipped). Pressing it again reverses both. Other systems (e.g. the inventory)
// can also force this off, such as while a menu is open.
public class WeaponController : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode equipKey = KeyCode.Alpha1;

    [Header("References")]
    [Tooltip("The Crosshair UI object - shown only while the weapon is equipped/aiming.")]
    public GameObject crosshair;
    [Tooltip("The Gun component - only fires while equipped.")]
    public Gun gun;

    public bool IsEquipped { get; private set; }

    private void Awake()
    {
        IsEquipped = false;
        ApplyState(); // start unequipped: crosshair hidden, gun disabled, cursor visible
    }

    private void Update()
    {
        if (Input.GetKeyDown(equipKey))
        {
            IsEquipped = !IsEquipped;
            ApplyState();
        }
    }

    public void ForceUnequip()
    {
        if (!IsEquipped) return;
        IsEquipped = false;
        ApplyState();
    }

    private void ApplyState()
    {
        if (crosshair != null) crosshair.SetActive(IsEquipped);
        if (gun != null) gun.enabled = IsEquipped;
        Cursor.visible = !IsEquipped;
    }
}
