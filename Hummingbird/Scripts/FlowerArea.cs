using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerArea : MonoBehaviour
{
    // The diameter of the area where the agent and flowers can be
    // used for observing relative distance from agent to flower
    public const float AreaDiameter = 20f;

    // List of all flower plants in the flower area (flower plants have multiple flowers)
    private List<GameObject> flowerPlants;

    // Dictionary for looking up a flower from a nectar collider in constant time
    private Dictionary<Collider, Flower> nectarFlowerDictionary;

    // List of all flowers in the flower area
    public List<Flower> Flowers {get; private set; }

    public void ResetFlowers()
    {
       // Rotate each flower plant around the Y axis and subtly around X/Z axis
       foreach (GameObject flowerPlant in flowerPlants)
       {
           float xRotation = UnityEngine.Random.Range(-5f, 5f);
           float yRotation = UnityEngine.Random.Range(-180f, 180f);
           float zRotation = UnityEngine.Random.Range(-5f, 5f);
           flowerPlant.transform.localRotation = Quaternion.Euler(xRotation, yRotation, zRotation);
       } 

       // Reset each flower
       foreach (Flower flower in Flowers)
       {
           flower.ResetFlower();
       }
    }

    // Gets the Flower that a nectar collider belongs to
    public Flower GetFlowerFromNectar(Collider collider)
    {
        return nectarFlowerDictionary[collider];
    }

    // Called when the area wakes up
    private void Awake()
    {
        // Initialize the variables relevant to the area
        flowerPlants = new List<GameObject>();
        nectarFlowerDictionary = new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();
    }

    // Called when the game is started
    private void Start()
    {
        // Find all flowers that are children of this GameObject/Transform
        FindChildFlowers(transform);
    }

    // Recursively finds all flowers and plants that are children of a parent transform
    private void FindChildFlowers(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.CompareTag("flower_plant"))
            {
                // Found a flower plant, add it to the flowerPlants list
                flowerPlants.Add(child.gameObject);

                // Look for flowers within the flower plant
                FindChildFlowers(child);
            }

            else
            {
                // Not a flower plant, look for a Flower component
                Flower flower = child.GetComponent<Flower>();

                if (flower != null)
                {
                    // Found a flower, add it to the Flowers list
                    Flowers.Add(flower);

                    // Add the nectar collider to the dictionary
                    nectarFlowerDictionary.Add(flower.nectarCollider, flower);

                    // Flowers cannot have a child flower, so stop recursion here
                }

                else
                {
                    // Flower component not found, so check the children
                    FindChildFlowers(child);
                }
            }
        }
    }
}
