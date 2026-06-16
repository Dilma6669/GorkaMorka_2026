using UnityEngine;
using System.Collections.Generic; // Required for List

// Phase 2.1: HexGridManager Class
// Purpose: A singleton manager responsible for keeping track of all active SimpleHexGrid instances in the scene.
// Provides a central point for other systems (like pathfinding) to query all available grids.
public class HexGridManager : MonoBehaviour
{
    
    private float maxJumpHeight = 2f; // Adjust this value in the Inspector
        
    // Inside HexGridManager.cs
    public float unwalkableRadius = 1.5f; // Add this public variable to the class
    
    private HexOverlayManager hexOverlayManager;
    
    // --- Singleton Pattern ---
    private static HexGridManager _instance;

    public static HexGridManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<HexGridManager>(); // Try to find an existing instance
                if (_instance == null)
                {
                    // If no instance exists, create a new GameObject and add the manager to it
                    GameObject singletonObject = new GameObject("HexGridManager");
                    _instance = singletonObject.AddComponent<HexGridManager>();
                }
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(_instance.gameObject);
                }
            }
            return _instance;
        }
    }

    // --- Grid Storage ---
    private static List<SimpleHexGridBase> registeredGrids = new List<SimpleHexGridBase>();

    // --- MonoBehaviour Lifecycle ---
    private void Awake()
    {
        // Ensure only one instance exists. Destroy self if a duplicate is found.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        
        hexOverlayManager = GetComponent<HexOverlayManager>();
        
        Debug.Log("HexGridManager Initialized.");
    }

    // --- Public Registration Methods ---

    /// <summary>
    /// Registers a SimpleHexGrid with the manager. Called by SimpleHexGrid's Awake.
    /// </summary>
    /// <param name="gridBase">The SimpleHexGrid instance to register.</param>
    public void RegisterGrid(SimpleHexGridBase gridBase)
    {
        if (!registeredGrids.Contains(gridBase))
        {
            registeredGrids.Add(gridBase);
            Debug.Log($"HexGridManager: Registered grid '{gridBase.name}'. Total grids: {registeredGrids.Count}");
        }
    }

    /// <summary>
    /// Unregisters a SimpleHexGrid from the manager. Called by SimpleHexGrid's OnDestroy.
    /// </summary>
    /// <param name="gridBase">The SimpleHexGrid instance to unregister.</param>
    public void UnregisterGrid(SimpleHexGridBase gridBase)
    {
        if (registeredGrids.Remove(gridBase))
        {
            Debug.Log($"HexGridManager: Unregistered grid '{gridBase.name}'. Total grids: {registeredGrids.Count}");
        }
    }

    /// <summary>
    /// Returns a list of all currently registered SimpleHexGrid instances.
    /// </summary>
    /// <returns>A List of SimpleHexGrid objects.</returns>
    public List<SimpleHexGridBase> GetAllGrids()
    {
        return new List<SimpleHexGridBase>(registeredGrids); // Return a copy to prevent external modification of the list
    }


    // public void UpdateUnwalkableHexagons(SimpleHexGridBase higherGridBase, SimpleHexGridBase lowerGridBase)
    // {
    //     if (higherGridBase == null || lowerGridBase == null)
    //     {
    //         Debug.LogError("UpdateUnwalkableHexagons: One or both grids are null.");
    //         return;
    //     }
    //
    //     // Step 1: Reset all hexagons on the lower grid to be walkable.
    //     var lowerHexKeys = new List<Vector2Int>(lowerGridBase.HexagonsInGrid.Keys);
    //     foreach (var hexCoords in lowerHexKeys)
    //     {
    //         HexData hexData = lowerGridBase.HexagonsInGrid[hexCoords];
    //         hexData.SetIsWalkable(true);
    //         lowerGridBase.HexagonsInGrid[hexCoords] = hexData;
    //     }
    //
    //     // Step 2: Iterate through every hex on the floating grid.
    //     foreach (var higherHexData in higherGridBase.HexagonsInGrid.Values)
    //     {
    //         Vector3 higherHexWorldPos =
    //             higherGridBase.GetHexWorldPosition(higherHexData.GridCoordinates, higherHexData.Height);
    //
    //         // Inside the main loop that iterates over higherGrid hexes...
    //         // Get a snapshot of the lower grid's hex values to safely iterate.
    //         var lowerHexValues = new List<HexData>(lowerGridBase.HexagonsInGrid.Values);
    //
    //         foreach (var lowerHexData in lowerHexValues)
    //         {
    //             Vector3 lowerHexWorldPos =
    //                 lowerGridBase.GetHexWorldPosition(lowerHexData.GridCoordinates, lowerHexData.Height);
    //
    //             // Step 4: Check if the two hexes are close in both horizontal and vertical distance.
    //             float horizontalDistance = Vector2.Distance(
    //                 new Vector2(higherHexWorldPos.x, higherHexWorldPos.z),
    //                 new Vector2(lowerHexWorldPos.x, lowerHexWorldPos.z)
    //             );
    //             float verticalDistance = Mathf.Abs(higherHexWorldPos.y - lowerHexWorldPos.y);
    //
    //             if (horizontalDistance < unwalkableRadius && verticalDistance < maxJumpHeight)
    //             {
    //                 // Set the ground hex as unwalkable.
    //                 HexData hexToModify = lowerGridBase.HexagonsInGrid[lowerHexData.GridCoordinates];
    //                 hexToModify.SetIsWalkable(false);
    //                 lowerGridBase.HexagonsInGrid[lowerHexData.GridCoordinates] = hexToModify;
    //             }
    //         }
    //     }
    // }
    
    
    public enum GridType
    {
        Ground,
        Floating
    }
    
    public readonly struct HexGridAndCoords : System.IEquatable<HexGridAndCoords>
    {
        public readonly Vector2Int Coords;
        public readonly SimpleHexGridBase GridBase;

        public HexGridAndCoords(Vector2Int coords, SimpleHexGridBase gridBase)
        {
            Coords = coords;
            GridBase = gridBase;
        }

        public bool Equals(HexGridAndCoords other) => Coords == other.Coords && GridBase == other.GridBase;
        public override bool Equals(object obj) => obj is HexGridAndCoords other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(Coords, GridBase);
    }
}