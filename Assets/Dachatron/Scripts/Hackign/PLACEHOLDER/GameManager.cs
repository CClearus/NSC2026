using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GameObject objectToDisappear; // set in Inspector

    void Awake() => Instance = this;

    public void OnWireComplete()
    {
        if (objectToDisappear != null)
            objectToDisappear.SetActive(false);

        Debug.Log("Wire connected! Puzzle solved.");
    }
}