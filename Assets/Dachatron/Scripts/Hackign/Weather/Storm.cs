using UnityEngine;
using System.Collections;

public class Thunder : MonoBehaviour
{
    public bool StormOn = false;
    private GameObject gane;
    private Vector3 GroundSize;


    
    private void Awake()
    {
            GroundSize = gane.GetComponent<Collider>().bounds.size;
    }

    // Call this from whatever trigger should start the storm.
    public void StartStorm()
    {
        if (StormOn) return; // don't restart if already running

        StormOn = true;
        Debug.Log("[Storm] Storm started!");


    }   
}
