using UnityEngine;

public class Hacking1 : MonoBehaviour 
{
    public GameObject CellPrefab;
    public GameObject CellEnd;
    public Transform GridPanel;
    private Vector2 spawnPosition;
    //call amount from SO
    public GameDataSO Hacking1Data;
    [SerializeField] private int Lockedvariations = 0;
    private int x,y;
    private int i = 0;  
    private int amount;
    private int randomvariable = 0;
    private float CellX = 39.3f;
    private float CellY = 34.3f;

    [SerializeField] private bool LockedVariation = false;


    // if database cell is 1,1, it will be position at 39.3, 34.3 in real world position, there will be 8 cell in the grid, totaling 64 cell

    private void Awake() //Spawning All the need Point
    {
            //random variation from all the min to man
            randomvariable = Random.Range(0,Hacking1Data.variations.Count);
            Debug.Log(randomvariable);
            if (LockedVariation)
            {
                amount = Hacking1Data.variations[Lockedvariations].amount;
                
                while (i<amount)
                {
                    x = Hacking1Data.variations[Lockedvariations].positions[i].startPosition.x;
                    y = Hacking1Data.variations[Lockedvariations].positions[i].startPosition.y;
                    SpawnObject(x, y);
                    x = Hacking1Data.variations[Lockedvariations].positions[i].endPosition.x;
                    y = Hacking1Data.variations[Lockedvariations].positions[i].endPosition.y;
                    SpawnObject(x, y);
                    i++;
                }

            }
            else //Random Variations
            {
                amount = Hacking1Data.variations[randomvariable].amount;
                {
                    x = Hacking1Data.variations[randomvariable].positions[i].startPosition.x;
                    y = Hacking1Data.variations[randomvariable].positions[i].startPosition.y;
                    SpawnObject(x, y);
                    x = Hacking1Data.variations[randomvariable].positions[i].endPosition.x;
                    y = Hacking1Data.variations[randomvariable].positions[i].endPosition.y;
                    SpawnObject(x, y);
                    i++;
                }
            }
        



    }




    //MIGUEL DONT EDIT THIS
void SpawnObject(int a, int b)
{
    spawnPosition = new Vector2((a * CellX) / 2, (b * CellY) / 2);

        GameObject newUIObject = Instantiate(CellPrefab, GridPanel);
        
         RectTransform rect = newUIObject.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.anchoredPosition = spawnPosition; 
            
            rect.localScale = Vector3.one; 
        }
    }

}

//probably use [] for variabel counting and use matching array