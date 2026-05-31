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
                DontDestroyOnLoad(_instance.gameObject); // Keep manager alive across scene loads if needed
            }
            return _instance;
        }
    }

    // --- Grid Storage ---
    private static List<SimpleHexGrid> registeredGrids = new List<SimpleHexGrid>();

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
        DontDestroyOnLoad(gameObject); // Keep manager alive across scene loads if needed
        Debug.Log("HexGridManager Initialized.");
    }

    private void Start()
    {
        //UpdateUnwalkableHexagonsOnAllGrids();
    }

    public void UpdateUnwalkableHexagonsOnAllGrids()
    {
        List<SimpleHexGrid> groundGrids = new List<SimpleHexGrid>();
        List<SimpleHexGrid> floatingGrids = new List<SimpleHexGrid>();
        
        foreach (var grid in registeredGrids)
        {
            if (grid.GridType == GridType.Ground)
            {
                groundGrids.Add(grid);
            }
            
            if (grid.GridType == GridType.Floating)
            {
                floatingGrids.Add(grid);
            }
        }

        foreach (var groundGrid in groundGrids)
        {
            foreach (var floatingGrid in floatingGrids)
            {
                UpdateUnwalkableHexagons(floatingGrid, groundGrid);
            }
        }
    }

    // --- Public Registration Methods ---

    /// <summary>
    /// Registers a SimpleHexGrid with the manager. Called by SimpleHexGrid's Awake.
    /// </summary>
    /// <param name="grid">The SimpleHexGrid instance to register.</param>
    public void RegisterGrid(SimpleHexGrid grid)
    {
        if (!registeredGrids.Contains(grid))
        {
            registeredGrids.Add(grid);
            Debug.Log($"HexGridManager: Registered grid '{grid.name}'. Total grids: {registeredGrids.Count}");
        }
    }

    /// <summary>
    /// Unregisters a SimpleHexGrid from the manager. Called by SimpleHexGrid's OnDestroy.
    /// </summary>
    /// <param name="grid">The SimpleHexGrid instance to unregister.</param>
    public void UnregisterGrid(SimpleHexGrid grid)
    {
        if (registeredGrids.Remove(grid))
        {
            Debug.Log($"HexGridManager: Unregistered grid '{grid.name}'. Total grids: {registeredGrids.Count}");
        }
    }

    /// <summary>
    /// Returns a list of all currently registered SimpleHexGrid instances.
    /// </summary>
    /// <returns>A List of SimpleHexGrid objects.</returns>
    public List<SimpleHexGrid> GetAllGrids()
    {
        return new List<SimpleHexGrid>(registeredGrids); // Return a copy to prevent external modification of the list
    }
    

public void UpdateUnwalkableHexagons(SimpleHexGrid higherGrid, SimpleHexGrid lowerGrid)
{
    if (higherGrid == null || lowerGrid == null)
    {
        Debug.LogError("UpdateUnwalkableHexagons: One or both grids are null.");
        return;
    }

    // Step 1: Reset all hexagons on the lower grid to be walkable.
    var lowerHexKeys = new List<Vector2Int>(lowerGrid.HexagonsInGrid.Keys);
    foreach (var hexCoords in lowerHexKeys)
    {
        HexData hexData = lowerGrid.HexagonsInGrid[hexCoords];
        hexData.SetIsWalkable(true);
        lowerGrid.HexagonsInGrid[hexCoords] = hexData;
        // Reset visualization to a default color if you have one.
         lowerGrid.HexGridVisualiser.HighlightHex(hexCoords, Color.white, false);
    }
    
    // Step 2: Iterate through every hex on the floating grid.
    foreach (var higherHexData in higherGrid.HexagonsInGrid.Values)
    {
        Vector3 higherHexWorldPos = higherGrid.GetHexWorldPosition(higherHexData.GridCoordinates, higherHexData.Height);
        
        // Inside the main loop that iterates over higherGrid hexes...
        // Get a snapshot of the lower grid's hex values to safely iterate.
        var lowerHexValues = new List<HexData>(lowerGrid.HexagonsInGrid.Values);

        foreach (var lowerHexData in lowerHexValues)
        {
            Vector3 lowerHexWorldPos = lowerGrid.GetHexWorldPosition(lowerHexData.GridCoordinates, lowerHexData.Height);
    
            // Step 4: Check if the two hexes are close in both horizontal and vertical distance.
            float horizontalDistance = Vector2.Distance(
                new Vector2(higherHexWorldPos.x, higherHexWorldPos.z),
                new Vector2(lowerHexWorldPos.x, lowerHexWorldPos.z)
            );
            float verticalDistance = Mathf.Abs(higherHexWorldPos.y - lowerHexWorldPos.y);

            if (horizontalDistance < unwalkableRadius && verticalDistance < maxJumpHeight)
            {
                // Set the ground hex as unwalkable.
                HexData hexToModify = lowerGrid.HexagonsInGrid[lowerHexData.GridCoordinates];
                hexToModify.SetIsWalkable(false);
                lowerGrid.HexagonsInGrid[lowerHexData.GridCoordinates] = hexToModify;
                lowerGrid.HexGridVisualiser.HighlightHex(lowerHexData.GridCoordinates, Color.red, true);
            }
        }
    }
}
    
    [ContextMenu("Toggle Edge Visualization")]
    public void ToggleEdgeVisualization()
    {
        // You can modify this to target a specific grid or all grids.
        // Here, we'll assume we want to visualize all grids.
        foreach (SimpleHexGrid hexGrid in registeredGrids)
        {
            Debug.Log("fuck name of grid = " + hexGrid.gameObject.name);
            HexGridVisualizer gridVisualizer = hexGrid.HexGridVisualiser;
            
            if (gridVisualizer == null)
            {
                Debug.LogError("HexGridVisualizer reference is null.");
                return;
            }
            
            gridVisualizer.VisualizeEdgeHexes();
        }
    }
    
    public enum GridType
    {
        Ground,
        Floating
    }
}