using TMPro;
using UnityEngine;
using System.Collections;

public class Interact : MonoBehaviour
{

    [SerializeField]
    Camera PlayerCamera;

    [SerializeField]
    TextMeshProUGUI InteractionText;

    private int PlayerLayer;
    public KeyCode Fuckyouartist = KeyCode.E;

    public string InteractableObjectTag = "Interactable";
    public bool IsInsideRadius = false;
    public GameObject InteractUI;
    public GameObject HackingUI;
    public WireGame HackingGame; // optional: gives the player a fresh puzzle each time the panel opens
    private bool UIisOpen = false;
    private bool IsPlayerInside = false;
      public CanvasGroup PanelBase;
     private Coroutine currentFadeRoutine; // Tracks running fades to prevent flickering

    [Header("Interact Hint Slide (optional)")]
    [Tooltip("The keybind icon (e.g. the 'E' box) - the hint label starts hidden behind it and slides out from here.")]
    public RectTransform keybindIcon;
    [Tooltip("The 'to interact' label - keep it placed at its final (shown) position in the Editor, it'll be moved to start hidden automatically.")]
    public RectTransform interactHintLabel;
    [Tooltip("How long the hint label takes to slide out, once it starts. It only starts sliding after the keybind icon has fully faded in.")]
    public float hintSlideDuration = 0.25f;

    private Vector2 hintShownPos;
    private bool hasHint;



    private void Awake()
    {
        PlayerLayer = LayerMask.NameToLayer("Player");

        if (interactHintLabel != null && keybindIcon != null)
        {
            hintShownPos = interactHintLabel.anchoredPosition; // wherever it's placed in the Editor is the "revealed" spot
            interactHintLabel.anchoredPosition = keybindIcon.anchoredPosition; // start hidden, overlapping the keybind
            hasHint = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == PlayerLayer)
        {
            Debug.Log("NUTTASIT EAT DIH");
            InteractUI.SetActive(true);
            IsPlayerInside = true;
            StartFadeIn(0.5f);
            // Toggling HackingUI on the E key is handled once, every frame, in Update() below -
            // duplicating it here as well caused the panel to flicker open then immediately
            // close again on the exact frame the player walked in while holding E.
        }

    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == PlayerLayer)
        {
            Debug.Log("NUttasit Eat coom");

            IsPlayerInside = false;
            StartFadeOut(0.5f);
        }
    }

    private void Update()
    {
        if (IsPlayerInside)
        {
            if (Input.GetKeyDown(Fuckyouartist) && !UIisOpen && IsPlayerInside)
            {
                HackingUI.SetActive(true);
                UIisOpen = true;
                if (HackingGame != null) HackingGame.ResetPuzzle();
                Debug.Log("Easy Artist open");
            }
            else if (Input.GetKeyDown(Fuckyouartist) && UIisOpen)
            {
                HackingUI.SetActive(false);
                UIisOpen = false;
                Debug.Log("Chicken Artist close");
            }  
        }
        else
        {
            HackingUI.SetActive(false);
            UIisOpen = false;
        }
    }

      public void StartFadeIn(float duration)
    {
        if (PanelBase == null) return;
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeInRoutine(duration));
    }

    private IEnumerator FadeInRoutine(float duration)
    {
        // Phase 1: fade the panel (keybind icon included) in on its own - the hint label
        // stays hidden, overlapping the icon, until the icon has fully appeared.
        float elapsedTime = 0f;
        float startAlpha = PanelBase.alpha;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            PanelBase.alpha = Mathf.SmoothStep(startAlpha, 1f, t);
            yield return null;
        }

        PanelBase.alpha = 1f;

        // Phase 2: now that the keybind icon is fully visible, slide the hint label out.
        if (hasHint)
        {
            elapsedTime = 0f;
            Vector2 hintStartPos = interactHintLabel.anchoredPosition;

            while (elapsedTime < hintSlideDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / hintSlideDuration);
                interactHintLabel.anchoredPosition = Vector2.Lerp(hintStartPos, hintShownPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            interactHintLabel.anchoredPosition = hintShownPos;
        }
    }

    public void StartFadeOut(float duration)
    {
        if (PanelBase == null) return;
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float elapsedTime = 0f;
        float startAlpha = PanelBase.alpha;
        Vector2 hintStartPos = hasHint ? interactHintLabel.anchoredPosition : default;
        Vector2 hintHiddenPos = hasHint ? keybindIcon.anchoredPosition : default;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            PanelBase.alpha = Mathf.SmoothStep(startAlpha, 0f, t);

            if (hasHint)
                interactHintLabel.anchoredPosition = Vector2.Lerp(hintStartPos, hintHiddenPos, Mathf.SmoothStep(0f, 1f, t));

            yield return null;
        }

        PanelBase.alpha = 0f;
        if (hasHint) interactHintLabel.anchoredPosition = hintHiddenPos;
        InteractUI.SetActive(false);
    }
}