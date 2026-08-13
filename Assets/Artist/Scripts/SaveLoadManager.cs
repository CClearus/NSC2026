using UnityEngine;

public class SaveLoadManager : MonoBehaviour
{
    [Header("Game References")]
    [SerializeField] private Transform playerTransform;
    // Add references to your health systems, inventory systems, etc.

    private GameData currentData;

    private void Start()
    {
        // Automatically load game on startup
        Load();
    }

    // Call this via UI Buttons or Autosave triggers
    public void Save()
    {
        if (currentData == null) currentData = new GameData();

        // 1. Gather current state from scene objects
        if (playerTransform != null)
        {
            currentData.playerX = playerTransform.position.x;
            currentData.playerY = playerTransform.position.y;
            currentData.playerZ = playerTransform.position.z;
        }
        
        // (Gather health, inventory, etc. here)

        // 2. Pass data to the SaveSystem to write to disk
        SaveSystem.SaveGame(currentData);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Debug.Log("Saving...");
            Save();
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            Debug.Log("Loading...");
            Load();
        }
    }

    // Call this via UI Buttons or on Scene Start
    public void Load()
    {
        // 1. Retrieve data from disk
        currentData = SaveSystem.LoadGame();

        // 2. Apply the loaded data to scene objects
        if (playerTransform != null)
        {
            Vector3 targetPosition = new Vector3(currentData.playerX, currentData.playerY, currentData.playerZ);
            playerTransform.position = targetPosition;
        }

        // (Apply health, inventory, etc. here)
    }
}
