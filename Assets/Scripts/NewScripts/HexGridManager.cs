using UnityEngine;
using System.Collections.Generic; // Required for List

// Phase 2.1: HexGridManager Class
// Purpose: A singleton manager responsible for keeping track of all active SimpleHexGrid instances in the scene.
// Provides a central point for other systems (like pathfinding) to query all available grids.
public class HexGridManager : MonoBehaviour
{
    private float maxJumpHeight = 2f; // Adjust this value in the Inspector
    
    public SimpleHexGridBase ActiveGrid;
    
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
        
        Debug.Log("HexGridManager Initialized.");
    }

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
    
    public enum GridType
    {
        Interior,
        Terrain,
        World,
        System,
        Galaxy,
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