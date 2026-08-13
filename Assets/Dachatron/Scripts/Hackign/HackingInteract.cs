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
    private bool UIisOpen = false;
    private bool IsPlayerInside = false;
      public CanvasGroup PanelBase;
     private Coroutine currentFadeRoutine; // Tracks running fades to prevent flickering
     


    private void Awake()
    {
        PlayerLayer = LayerMask.NameToLayer("Player");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == PlayerLayer)
        {
            Debug.Log("NUTTASIT EAT DIH");
            InteractUI.SetActive(true);
            IsPlayerInside = true;
            StartFadeIn(0.5f); 
            if (Input.GetKeyDown(Fuckyouartist) && !UIisOpen)
            {
                HackingUI.SetActive(true);
                UIisOpen = true;
                Debug.Log("Easy Artist");
            }
            else if (Input.GetKeyDown(Fuckyouartist) && UIisOpen)
            {
                HackingUI.SetActive(false);
                UIisOpen = false;
            }
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
        float elapsedTime = 0f;
        float startAlpha = PanelBase.alpha; 

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            PanelBase.alpha = Mathf.Lerp(startAlpha, 1f, elapsedTime / duration);
            yield return null; 
        }

        PanelBase.alpha = 1f; 
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

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            PanelBase.alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / duration);
            yield return null; 
        }

        PanelBase.alpha = 0f; 
        InteractUI.SetActive(false); 
    }
}