using UnityEngine;
using UnityEngine.UI;

public class Hacking1 : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject CellPrefab; // spawned for the "start" dot of each pair
    public GameObject CellEnd;    // spawned for the "end" dot of each pair (falls back to CellPrefab if empty)

    [Header("References")]
    public Transform GridPanel;
    public WireGame wireGame; // optional: spawned pairs are auto-registered here so they become playable wires

    //call amount from SO
    public GameDataSO Hacking1Data;
    [SerializeField] private int Lockedvariations = 0;
    [SerializeField] private bool LockedVariation = false;

    private float CellX = 39.3f;
    private float CellY = 34.3f;

    // Fallback colors used when a position's wireColor hasn't been set in the data asset.
    static readonly Color[] FallbackPalette =
    {
        new Color(0.90f, 0.15f, 0.15f), new Color(0.95f, 0.85f, 0.10f),
        new Color(0.55f, 0.25f, 0.85f), new Color(0.15f, 0.55f, 0.95f),
        new Color(0.95f, 0.30f, 0.55f), new Color(0.20f, 0.75f, 0.30f),
        new Color(0.95f, 0.55f, 0.15f), new Color(0.20f, 0.85f, 0.85f),
    };

    // if database cell is 1,1, it will be position at 39.3, 34.3 in real world position, there will be 8 cell in the grid, totaling 64 cell

    private void Awake() // Spawning all the needed points for the chosen puzzle variation
    {
        if (GridPanel == null) GridPanel = transform; // this component lives on the grid panel itself
        if (wireGame == null) wireGame = GetComponent<WireGame>();
        if (wireGame == null) wireGame = FindObjectOfType<WireGame>();
        if (wireGame == null) Debug.LogWarning("[Hacking1] No WireGame found - spawned dots will show but won't be draggable. Assign the 'wireGame' field in the Inspector.");

        if (Hacking1Data == null || Hacking1Data.variations.Count == 0)
        {
            Debug.LogWarning("[Hacking1] No GameDataSO variations assigned - nothing to spawn.");
            return;
        }

        int variationIndex = LockedVariation
            ? Mathf.Clamp(Lockedvariations, 0, Hacking1Data.variations.Count - 1)
            : Random.Range(0, Hacking1Data.variations.Count);

        SpawnVariation(Hacking1Data.variations[variationIndex]);
    }

    private void SpawnVariation(GameDataSO.VariationData variation)
    {
        int count = Mathf.Min(variation.amount, variation.positions.Count);
        for (int i = 0; i < count; i++)
        {
            GameDataSO.PositionData posData = variation.positions[i];
            Color color = posData.wireColor.a > 0f ? posData.wireColor : FallbackPalette[i % FallbackPalette.Length];

            GameObject startDot = SpawnObject(CellPrefab, posData.startPosition, color);
            GameObject endDot = SpawnObject(CellEnd != null ? CellEnd : CellPrefab, posData.endPosition, color);

            if (wireGame != null && startDot != null && endDot != null)
            {
                Button startBtn = startDot.GetComponent<Button>();
                Button endBtn = endDot.GetComponent<Button>();
                if (startBtn != null && endBtn != null)
                {
                    wireGame.pairs.Add(new WireGame.WirePair { start = startBtn, end = endBtn, color = color });
                }
                else
                {
                    Debug.LogWarning("[Hacking1] CellPrefab/CellEnd needs a Button component to be playable by WireGame.");
                }
            }
        }

        // WireGame.Start() may already have run (with an empty pairs list) before we
        // got here, e.g. if this spawner only wakes up when the hacking panel opens.
        // Re-run its setup now so the dots we just spawned actually become draggable.
        if (wireGame != null) wireGame.SetupPairs();
    }

    //MIGUEL DONT EDIT THIS
    GameObject SpawnObject(GameObject prefab, Vector2Int gridPos, Color color)
    {
        if (prefab == null) return null;

        Vector2 spawnPosition = new Vector2((gridPos.x * CellX) / 2, (gridPos.y * CellY) / 2);

        GameObject newUIObject = Instantiate(prefab, GridPanel);

        RectTransform rect = newUIObject.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.anchoredPosition = spawnPosition;

            rect.localScale = Vector3.one;
        }

        Image img = newUIObject.GetComponent<Image>();
        if (img != null) img.color = color;

        return newUIObject;
    }

}

//probably use [] for variabel counting and use matching array
