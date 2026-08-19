using UnityEngine;

public class RainStart : MonoBehaviour
{
    [Tooltip("Drag the GameObject that has the Thunder (Storm) script on it here.")]
    public Thunder storm;

    [Tooltip("Only trigger when this tag walks into the collider (leave empty to allow anything).")]
    public string playerTag = "Player";

    void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            return;

        Debug.Log("[RainStart] Player entered storm trigger.");

        if (storm != null)
        {
            storm.StartStorm();
        }
        else
        {
            Debug.LogWarning("[RainStart] No Storm reference assigned!");
        }
    }
}
