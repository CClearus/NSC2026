using UnityEngine;

public class Turning : MonoBehaviour
{
    public Transform player;
    void Start()
    {
        
    }

    void Update()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float playerProj = Vector3.Dot(player.position, Vector3.right);
        float mouseProj = Vector3.Dot(mousePos, Vector3.right);
        //Debug.Log(playerProj);
        //Debug.Log(mouseProj);
        if (mouseProj < playerProj)
        {
            //Debug.Log("Mouse behind player");
        }
    }
}
