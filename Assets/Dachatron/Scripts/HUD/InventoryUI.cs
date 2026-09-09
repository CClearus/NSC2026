using UnityEngine;
using System.Collections;

public class InventoryUI : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode inventoryKey = KeyCode.I;

    [Header("Vignette (fades in as two separate images)")]
    [Tooltip("Add a Canvas Group component to each vignette image and assign it here.")]
    public CanvasGroup vignetteGroup;
    public CanvasGroup vignetteGroup2;

    [Header("Slot Group (fades in/out only, no movement)")]
    [Tooltip("Canvas Group on the Slot object - controls its fade.")]
    public CanvasGroup slotGroup;

    [Header("Hidden while inventory is open")]
    public GameObject focus;
    public GameObject nameLabel;
    public GameObject descriptionLabel;

    [Header("Locked while inventory is open")]
    [Tooltip("Player can't move until the inventory is closed again.")]
    public PlayerMovement playerMovement;
    [Tooltip("Optional - the weapon is force-unequipped (crosshair hidden, firing disabled) while the inventory is open.")]
    public WeaponController weaponController;

    [Header("Timing")]
    [Tooltip("How long the vignette takes to fade. On open it fades first, before the slot group starts. On close it fades last, after the slot group has finished.")]
    public float vignetteFadeDuration = 0.25f;
    [Tooltip("How long the slot group takes to fade. On open it starts once the vignette has fully faded in. On close it fades out first.")]
    public float slotFadeDuration = 0.25f;

    private bool isOpen = false;
    private Coroutine fadeRoutine;

    // Whatever active state each of these was in right before Open() hid them - could
    // already be off (manually, or because some other script only shows them when
    // aiming at an interactable). Close() restores this instead of forcing them on.
    private bool wasFocusActive;
    private bool wasNameActive;
    private bool wasDescriptionActive;

    private void Awake()
    {
        // Everything starts hidden/closed, whatever alpha/position was left in the Editor.
        if (vignetteGroup != null) vignetteGroup.alpha = 0f;
        if (vignetteGroup2 != null) vignetteGroup2.alpha = 0f;
        if (slotGroup != null)
        {
            slotGroup.alpha = 0f;
            slotGroup.interactable = false;
            slotGroup.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(inventoryKey))
        {
            if (isOpen) Close();
            else Open();
        }
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        wasFocusActive = focus != null && focus.activeSelf;
        wasNameActive = nameLabel != null && nameLabel.activeSelf;
        wasDescriptionActive = descriptionLabel != null && descriptionLabel.activeSelf;

        if (focus != null) focus.SetActive(false);
        if (nameLabel != null) nameLabel.SetActive(false);
        if (descriptionLabel != null) descriptionLabel.SetActive(false);

        if (playerMovement != null) playerMovement.SetMovementLocked(true);
        if (weaponController != null) weaponController.ForceUnequip();

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(OpenRoutine());
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (focus != null) focus.SetActive(wasFocusActive);
        if (nameLabel != null) nameLabel.SetActive(wasNameActive);
        if (descriptionLabel != null) descriptionLabel.SetActive(wasDescriptionActive);

        if (playerMovement != null) playerMovement.SetMovementLocked(false);

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CloseRoutine());
    }

    // Vignette fades in fully first, then the slot group fades in.
    private IEnumerator OpenRoutine()
    {
        yield return FadeVignette(true, vignetteFadeDuration);
        yield return FadeSlot(true, slotFadeDuration);
    }

    // Reverse order: slot group fades out first, then the vignette fades out.
    private IEnumerator CloseRoutine()
    {
        yield return FadeSlot(false, slotFadeDuration);
        yield return FadeVignette(false, vignetteFadeDuration);
    }

    private IEnumerator FadeVignette(bool show, float duration)
    {
        float startVignette1 = vignetteGroup != null ? vignetteGroup.alpha : 0f;
        float startVignette2 = vignetteGroup2 != null ? vignetteGroup2.alpha : 0f;
        float targetAlpha = show ? 1f : 0f;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / duration));

            if (vignetteGroup != null) vignetteGroup.alpha = Mathf.Lerp(startVignette1, targetAlpha, t);
            if (vignetteGroup2 != null) vignetteGroup2.alpha = Mathf.Lerp(startVignette2, targetAlpha, t);

            yield return null;
        }

        if (vignetteGroup != null) vignetteGroup.alpha = targetAlpha;
        if (vignetteGroup2 != null) vignetteGroup2.alpha = targetAlpha;
    }

    private IEnumerator FadeSlot(bool show, float duration)
    {
        float startAlpha = slotGroup != null ? slotGroup.alpha : 0f;
        float targetAlpha = show ? 1f : 0f;

        // While showing, the slot group needs to be clickable right away; while hiding,
        // switch it off immediately so it stops eating clicks/raycasts as it fades out.
        if (slotGroup != null)
        {
            slotGroup.interactable = show;
            slotGroup.blocksRaycasts = show;
        }

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / duration));

            if (slotGroup != null) slotGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        if (slotGroup != null) slotGroup.alpha = targetAlpha;
    }
}
