using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flower : MonoBehaviour
{
    [Tooltip("The color when the flower is full")]
    public Color fullFlowerColor = new Color(1f, 0f, .3f);

    [Tooltip("The color when the flower is empty")]
    public Color emptyFlowerColor = new Color(.5f, 0f, 1f);

    
    // The trigger collider representing the nectar
    [HideInInspector]
    public Collider nectarCollider;

    // The solid collider representing the flower petals"
    private Collider flowerCollider;

    // The flower's material
    private Material flowerMaterial;

    // A vector pointing straight out of the flower
    public Vector3 FlowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }

    // The center position of the nectar collider
    public Vector3 FlowerCenterPosition
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }

    // Amount of nectar remaining in the flower
    public float NectarAmount {get; private set; }

    // Whether the flower has any nectar remaining
    public bool HasNectar
    {
        get
        {
            return NectarAmount > 0f;
        }
    }

    // Attempt to remove nectar from the flower, return actual amount removed successfully
    public float Feed(float amount)
    {
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);
        NectarAmount -= amount;

        if (NectarAmount <= 0)
        {
            NectarAmount = 0;
            
            // Disable the flower and nectar colliders
            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            // Change flower color to indicate it has no more nectar
            flowerMaterial.SetColor("_BaseColor", Color.blue);

        }

        return nectarTaken;
    }

    public void ResetFlower()
    {
        NectarAmount = 1f;
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);

        flowerMaterial.SetColor("_BaseColor", fullFlowerColor);
    }

    private void Awake()
    {
        // Find the flower's mesh renderer and get the main material
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        flowerMaterial = meshRenderer.material;

        // Find flower and nectar colliders
        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}
