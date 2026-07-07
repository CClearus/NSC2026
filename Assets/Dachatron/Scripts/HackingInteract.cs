using TMPro;
using UnityEngine;

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
            if (Input.GetKeyDown(Fuckyouartist))
            {
                
            }
        }
        
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == PlayerLayer)
        {
            Debug.Log("NUttasit Eat com");
            InteractUI.SetActive(false);
        }
    }
}