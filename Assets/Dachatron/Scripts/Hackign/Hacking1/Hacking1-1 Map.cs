using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Hacking1", menuName = "Game/Game Data")]
public class GameDataSO : ScriptableObject
{
    public List<VariationData> variations = new();

    [Serializable]
    public class VariationData
    {
        public string variationName;

        [Min(0)]
        public int amount;

        public List<PositionData> positions = new();

#if UNITY_EDITOR
        public void Validate()
        {
            while (positions.Count < amount)
                positions.Add(new PositionData());

            while (positions.Count > amount)
                positions.RemoveAt(positions.Count - 1);
        }
#endif
    }

    [Serializable]
    public class PositionData
    {
        public Vector2Int startPosition;
        public Vector2Int endPosition;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        foreach (var variation in variations)
            variation.Validate();
    }
#endif
}