using UnityEngine;
using UnityEngine.UI;

public class HackingUIClose : MonoBehaviour 
{
    public GameObject Panel;
    public void CloseUI()
    {
        Panel.SetActive(false);
    }
}