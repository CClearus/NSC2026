using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string saveFileName = "gamesave.json";
    private static string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    public static void SaveGame(GameData data)
    {
        try
        {
            // Convert C# object data into a JSON string
            string json = JsonUtility.ToJson(data, true); // true enables pretty-print format
            
            // Write the JSON string to file
            File.WriteAllText(SavePath, json);
            Debug.Log($"Game saved successfully to: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save game: {e.Message}");
        }
    }

    public static GameData LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning("Save file not found. Initializing new game data.");
            return new GameData(); // Return default data if no save exists
        }

        try
        {
            // Read JSON string from file
            string json = File.ReadAllText(SavePath);
            
            // Convert JSON string back into a C# object
            GameData data = JsonUtility.FromJson<GameData>(json);
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load game: {e.Message}");
            return null;
        }
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }
    }
}
