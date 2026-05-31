using UnityEngine;
using System.Collections.Generic; // Required for List

// Phase 11.1 (Revised): UnitSelectionManager Class
// Purpose: Handles player input for unit selection and commanding selected units
// to move using a single mouse button. Informs the UnitCommander which unit is
// currently active and where to move it. Adheres to Single Responsibility Principle.
public class EntitySelectionManager : MonoBehaviour
{
    [Header("Raycast Layers")]
    [Tooltip("Layer(s) containing your Unit GameObjects. Crucial for detecting unit clicks.")]
    public LayerMask unitLayer;

    [Tooltip("Layer(s) containing your ground/grid GameObjects. Crucial for detecting hex clicks.")]
    public LayerMask groundLayer;

    public Color TargetHexagonHighlightedColour;
    public Color PathHexagonHighlightedColour;

    private Vector2Int hoveredHexCoords;
    private SimpleHexGrid hoveredHexGrid;
 
    
    private EntityCommander entityCommander;
    private MultiGridPathfinder pathfinder;
    
    private void Awake()
    {
        entityCommander = GetComponent<EntityCommander>();
        pathfinder = GetComponent<MultiGridPathfinder>();
    }
    
    void Update()
    {
        // Only listen for the left mouse button click for all actions
        if (Input.GetMouseButtonDown(0)) // 0 is the left mouse button
        {
            HandleLeftClick();
        }
        else
        {
            HandleMouseHover();
        }
    }

    /// <summary>
    /// Processes a left-click, prioritizing unit selection over movement commands.
    /// </summary>
    void HandleLeftClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // --- Priority 1: Try to select a Unit ---
        // We perform a raycast specifically for the Unit Layer first.
        // If it hits a unit, we select it and stop processing this click.
        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            // Check the layer of the hit object
            int hitLayer = hit.collider.gameObject.layer;

            // You can then compare this integer to specific layer numbers or use LayerMask.NameToLayer
            if (hitLayer == LayerMask.NameToLayer("UnitCollider"))
            {
                Entity clickedEntity = hit.collider.GetComponent<Entity>();

                if (clickedEntity != null)
                {
                    Debug.Log("Hit an object on UnitCollider Layer!");

                    // A unit was clicked, so we select it.
                    if (entityCommander != null)
                    {
                        entityCommander.entityToCommand = clickedEntity;
                        Debug.Log($"UnitSelectionManager: Selected Unit: {clickedEntity.name}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            "UnitSelectionManager: unitCommander reference not set. Cannot assign selected unit.");
                    }
                }
            }
            else if (hitLayer == LayerMask.NameToLayer("HexagonCollider"))
            {
                Debug.Log("Hit an object on HexagonCollider Layer!");

                HexVisualTile clickedHexagon = hit.collider.GetComponent<HexVisualTile>();

                SimpleHexGrid hexGrid = clickedHexagon.GridReference;

                string gameObjectTag = hexGrid.gameObject.tag;

                switch (gameObjectTag)
                {
                    case "GroundGrid":
                        Debug.Log("Hit GroundGrid tag!");
                        break;
                    case "CarGrid":
                        Debug.Log("Hit CarGrid tag!");
                        break;
                }

                // --- Priority 2: If no Unit was clicked, and a Unit IS currently selected, try to command it to move ---
                // This part only executes if a unit was NOT hit by the raycast.
                if (entityCommander != null && entityCommander.entityToCommand != null)
                {
                    SimpleHexGrid targetGrid = null;
                    Vector2Int targetCoords = Vector2Int.zero;
                    HexData foundHexData = default; // Will contain the hex data if found
                    
                    // Ask each grid if the hit point falls within one of its hexes.
                    if (hexGrid.GetHexAtWorldPosition(hit.point, out foundHexData))
                    {
                        targetGrid = hexGrid;
                        targetCoords = foundHexData.GridCoordinates;
                        // Optional future improvement: Check if foundHexData.IsWalkable here
                        // if (!foundHexData.IsWalkable) { Debug.Log("UnitSelectionManager: Clicked on an unwalkable hex!"); return; }
                    }
                    
                    if (targetGrid != null)
                    {
                        // We successfully found a target hex!
                        // Command the *currently selected* unit to move there.
                        Debug.Log(
                            $"UnitSelectionManager: Commanding unit '{entityCommander.entityToCommand.name}' to move to hex {targetCoords} on grid '{targetGrid.name}'.");

                        entityCommander.targetGrid = targetGrid;
                        entityCommander.targetCoordinates = targetCoords;
                        entityCommander.CommandUnitToMove();
                        
                        // Highlight TargetHex
                        clickedHexagon.SetColor(TargetHexagonHighlightedColour);
                    }
                    else
                    {
                        Debug.LogWarning(
                            "UnitSelectionManager: Clicked on ground, but no valid hex found at that position.");
                    }
                }
            }
            else
            {
                Debug.Log("Hit an object on layer: " + LayerMask.LayerToName(hitLayer));
            }
        }
    }

    void HandleMouseHover()
    {
           Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // --- Priority 1: Try to select a Unit ---
        // We perform a raycast specifically for the Unit Layer first.
        // If it hits a unit, we select it and stop processing this click.
        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            // Check the layer of the hit object
            int hitLayer = hit.collider.gameObject.layer;

            // You can then compare this integer to specific layer numbers or use LayerMask.NameToLayer
            if (hitLayer == LayerMask.NameToLayer("UnitCollider"))
            {
                Entity clickedEntity = hit.collider.GetComponent<Entity>();
                
             //   Debug.Log($"UnitSelectionManager: HOvering Unit: {clickedEntity.name}");
                /*

                if (clickedUnit != null)
                {
                    Debug.Log("Hit an object on UnitCollider Layer!");

                    // A unit was clicked, so we select it.
                    if (unitCommander != null)
                    {
                        unitCommander.unitToCommand = clickedUnit;
                        Debug.Log($"UnitSelectionManager: Selected Unit: {clickedUnit.name}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            "UnitSelectionManager: unitCommander reference not set. Cannot assign selected unit.");
                    }
                }*/
            }
            else if (hitLayer == LayerMask.NameToLayer("HexagonCollider"))
            {
                HexVisualTile hoveredHexagon = hit.collider.GetComponent<HexVisualTile>();

                if (hoveredHexCoords == hoveredHexagon.GridCoordinates)
                    return;
                
                hoveredHexCoords = hoveredHexagon.GridCoordinates;
                
              //  Debug.Log($"UnitSelectionManager: HOvering Hexagon: {hoveredHexagon.GridCoordinates}");

                SimpleHexGrid hexGrid = hoveredHexagon.GridReference;

                foreach (SimpleHexGrid otherGrid in HexGridManager.Instance.GetAllGrids())
                {
                    HexGridVisualizer gridVisualizer = otherGrid.HexGridVisualiser;
                    gridVisualizer.ResetCurrentColouredHexs();
                }
                
                hoveredHexGrid = hexGrid;
                
                string gameObjectTag = hexGrid.gameObject.tag;

                switch (gameObjectTag)
                {
                    case "GroundGrid":
                     //   Debug.Log("Hit GroundGrid tag!");
                        break;
                    case "CarGrid":
                    //    Debug.Log("Hit CarGrid tag!");
                        break;
                }

                // --- Priority 2: If no Unit was clicked, and a Unit IS currently selected, try to command it to move ---
                // This part only executes if a unit was NOT hit by the raycast.
                if (entityCommander != null && entityCommander.entityToCommand != null)
                {
                    SimpleHexGrid targetGrid = null;
                    Vector2Int targetCoords = Vector2Int.zero;
                    HexData foundHexData = default; // Will contain the hex data if found
                    
                    // Ask each grid if the hit point falls within one of its hexes.
                    if (hexGrid.GetHexAtWorldPosition(hit.point, out foundHexData))
                    {
                        targetGrid = hexGrid;
                        targetCoords = foundHexData.GridCoordinates;
                        // Optional future improvement: Check if foundHexData.IsWalkable here
                        // if (!foundHexData.IsWalkable) { Debug.Log("UnitSelectionManager: Clicked on an unwalkable hex!"); return; }
                    }
                    else
                    {
                        Debug.Log("FUCK FAIL!!");
                    }
                    
                    // Inside your EntitySelectionManager.cs script, HandleMouseHover() method
                    if (targetGrid != null)
                    {
                        // 1. Get the path from the pathfinder
                        PathNode startNode = new PathNode(entityCommander.entityToCommand.currentGridCoordinates, entityCommander.entityToCommand.currentGrid);
                        PathNode endNode = new PathNode(targetCoords, targetGrid);
                        List<PathNode> rawPath = pathfinder.FindPath(startNode, endNode);

                        if (rawPath != null && rawPath.Count > 0)
                        {
                            List<PathNode> finalPath;
        
                            // 2. Get the correct mover and smooth the path if it's a vehicle
                            if (entityCommander.entityToCommand.EntityType == EntitySpawner.EntityType.Vehicle)
                            {
                                VehiclePathMover mover = entityCommander.entityToCommand.GetComponent<VehiclePathMover>();
                                if (mover != null)
                                {
                                    finalPath = mover.SmoothPathForVehicle(rawPath);
                                }
                                else
                                {
                                    finalPath = rawPath;
                                }
                            }
                            else
                            {
                                // For units, the raw path is the final path
                                finalPath = rawPath;
                            }

                            // 3. Visualize the final path
                            HexGridVisualizer gridVisualizer = hoveredHexGrid.HexGridVisualiser;
        
                            foreach (PathNode pathNode in finalPath)
                            {
                                foreach (SimpleHexGrid otherGrid in HexGridManager.Instance.GetAllGrids())
                                {
                                    if (pathNode.GridReference == otherGrid)
                                    {
                                        otherGrid.HexGridVisualiser.HighlightHex(pathNode.GridCoordinates, PathHexagonHighlightedColour);
                                    }
                                }
                            }
        
                            // Highlight the target hex
                            gridVisualizer.HighlightHex(targetCoords, TargetHexagonHighlightedColour);
                        }
                    }
                }
            }
            else
            {
                Debug.Log("Hit an object on layer: " + LayerMask.LayerToName(hitLayer));
            }
        }
    }
}
