using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    // Player Stats
    public int currentHealth;
    public int maxHealth;

    // Position (Unity's Vector3 isn't natively fully serializable, use individual floats)
    public float playerX;
    public float playerY;
    public float playerZ;

    // Constructor to initialize default values for a New Game
    public GameData()
    {
        currentHealth = 100;
        maxHealth = 100;
        playerX = 0f;
        playerY = 1f;
        playerZ = 0f;
    }
}
