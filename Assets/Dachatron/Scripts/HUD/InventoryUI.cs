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
    [Tooltip("Any number of UI elements - each is SetActive(false) while the inventory is open, and restored to whatever active state it was in before, once it closes.")]
    public GameObject[] hideWhileOpen;

    [Header("Shown while inventory is open")]
    [Tooltip("Any number of Canvas Groups that fade in alongside the Slot Group when the inventory opens, and fade out with it when it closes. Each needs a Canvas Group component.")]
    public CanvasGroup[] showWhileOpen;

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

    // Whatever active state each hideWhileOpen entry was in right before Open() hid it -
    // could already be off (manually, or because some other script only shows it when
    // aiming at an interactable). Close() restores this instead of forcing them all on.
    private bool[] hideWhileOpenPrevState;

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

        // showWhileOpen groups fade in/out together with slotGroup - same starting state.
        for (int i = 0; i < showWhileOpen.Length; i++)
        {
            CanvasGroup g = showWhileOpen[i];
            if (g == null) continue;
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
        }

        hideWhileOpenPrevState = new bool[hideWhileOpen != null ? hideWhileOpen.Length : 0];
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

        for (int i = 0; i < hideWhileOpen.Length; i++)
        {
            GameObject go = hideWhileOpen[i];
            if (go == null) continue;
            hideWhileOpenPrevState[i] = go.activeSelf;
            go.SetActive(false);
        }

        if (playerMovement != null) playerMovement.SetMovementLocked(true);
        if (weaponController != null) weaponController.ForceUnequip();

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(OpenRoutine());
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        for (int i = 0; i < hideWhileOpen.Length; i++)
        {
            GameObject go = hideWhileOpen[i];
            if (go == null) continue;
            go.SetActive(hideWhileOpenPrevState[i]);
        }

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

    // Fades slotGroup and every showWhileOpen Canvas Group together, on the same
    // timing/curve, so they all appear/disappear as one cohesive "slot" reveal.
    private IEnumerator FadeSlot(bool show, float duration)
    {
        float slotStartAlpha = slotGroup != null ? slotGroup.alpha : 0f;
        float[] showStartAlphas = new float[showWhileOpen.Length];
        for (int i = 0; i < showWhileOpen.Length; i++)
        {
            showStartAlphas[i] = showWhileOpen[i] != null ? showWhileOpen[i].alpha : 0f;
        }

        float targetAlpha = show ? 1f : 0f;

        // While showing, groups need to be clickable right away; while hiding, switch
        // them off immediately so they stop eating clicks/raycasts as they fade out.
        SetInteractable(slotGroup, show);
        for (int i = 0; i < showWhileOpen.Length; i++) SetInteractable(showWhileOpen[i], show);

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / duration));

            if (slotGroup != null) slotGroup.alpha = Mathf.Lerp(slotStartAlpha, targetAlpha, t);
            for (int i = 0; i < showWhileOpen.Length; i++)
            {
                if (showWhileOpen[i] != null) showWhileOpen[i].alpha = Mathf.Lerp(showStartAlphas[i], targetAlpha, t);
            }

            yield return null;
        }

        if (slotGroup != null) slotGroup.alpha = targetAlpha;
        for (int i = 0; i < showWhileOpen.Length; i++)
        {
            if (showWhileOpen[i] != null) showWhileOpen[i].alpha = targetAlpha;
        }
    }

    private void SetInteractable(CanvasGroup group, bool value)
    {
        if (group == null) return;
        group.interactable = value;
        group.blocksRaycasts = value;
    }
}
