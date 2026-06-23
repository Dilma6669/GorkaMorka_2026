using UnityEngine;

public class PendingEntityTransfer : MonoBehaviour
{
    public static PendingEntityTransfer Instance;
    
    public EntityData storedUnitData; // Store the data, not the GameObject

    private void Awake() => Instance = this;
}