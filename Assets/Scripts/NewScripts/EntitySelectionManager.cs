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
    
    private Vector2Int selectedHexCoords;
    private SimpleHexGrid selectedHexGrid;
    
    private MultiGridPathfinder pathfinder;
    
    private void Awake()
    {
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

    public static void SelectVehicle(Entity entity)
    {
        if (EntityCommander.GetEntityInCommand() != null && EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
        {
            VehicleEntity previousVehicle = (VehicleEntity)EntityCommander.GetEntityInCommand();
            previousVehicle.SetSelected(false);
        }
        
        Debug.Log($"EntitySelectionManager: Selected {entity.name}");
        EntityCommander.SetEntityToCommand(entity);
        VehicleEntity vehicle = (VehicleEntity)entity;
        vehicle.SetSelected(true);
    }
    
    public static void SelectUnit(Entity entity)
    {
        if (EntityCommander.GetEntityInCommand() != null && EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
        {
            VehicleEntity previousVehicle = (VehicleEntity)EntityCommander.GetEntityInCommand();
            previousVehicle.SetSelected(false);
        }
        
        Debug.Log($"EntitySelectionManager: Selected {entity.name}");
        EntityCommander.SetEntityToCommand(entity);
    }

    private static void SelectHex(HexVisualTile hex)
    {
        Debug.Log($"EntitySelectionManager: Selected {hex.GridCoordinates}");
    }
    
    private void SelectHexWithUnitActive(HexVisualTile hex)
    {
        Debug.Log($"EntitySelectionManager: Selected {hex.GridCoordinates} with Unit Active");
        if (selectedHexCoords == hex.GridCoordinates) return;
        selectedHexCoords = hex.GridCoordinates;

        SimpleHexGrid closestHexGrid = hex.GridReference;
        selectedHexGrid = closestHexGrid;
        EntityCommander.SetTargetGridAndCoordinates(closestHexGrid, selectedHexCoords);
        EntityCommander.CommandUnitToMove();
        hex.SetHighlightColour(TargetHexagonHighlightedColour, true);
    }
    
    private void SelectHexWithVehicleActive(HexVisualTile hex)
    {
        Debug.Log($"EntitySelectionManager: Selected {hex.GridCoordinates} with Vehicle Active");
        if (selectedHexCoords == hex.GridCoordinates) return;
        selectedHexCoords = hex.GridCoordinates;

        SimpleHexGrid closestHexGrid = hex.GridReference;
        selectedHexGrid = closestHexGrid;
        
        if (closestHexGrid == EntityCommander.GetEntityInCommand().EntityGrid)
            return;

        EntityCommander.SetTargetGridAndCoordinates(closestHexGrid, selectedHexCoords);
        EntityCommander.CommandUnitToMove();
        hex.SetHighlightColour(TargetHexagonHighlightedColour, true);
    }
    
    private void HoverVehicle(Entity entity)
    {
        Debug.Log($"EntitySelectionManager: Hovered {entity.name}");
    }
    
    private void HoverUnit(Entity entity)
    {
        Debug.Log($"EntitySelectionManager: Hovered {entity.name}");
    }

    private void HoverHex(HexVisualTile hex)
    {
        Debug.Log($"EntitySelectionManager: Hovered {hex.GridCoordinates}");
    }
    
    private void HoverHexWithUnitActive(HexVisualTile hex)
    {
        Debug.Log($"EntitySelectionManager: Hovered {hex.GridCoordinates} with Unit Active");
    }

    private void HoverHexWithVehicleActive(HexVisualTile hex)
    {
        Debug.Log($"EntitySelectionManager: Hovered {hex.GridCoordinates} with Vehicle Active");
    }

    /// <summary>
    /// Processes a left-click, prioritizing unit selection over movement commands.
    void HandleLeftClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
        
        RaycastHit hitResult = default;

        float minUnitDist = float.MaxValue;
        float minHexDist = float.MaxValue;
        float minVehicleDist = float.MaxValue;

        Entity closestUnitSelected = null!;
        HexVisualTile closetHexSelected = null!;
        Entity closetVehicleSelected = null!;
        
        foreach (var hit in hits)
        {
            Debug.Log($"Ray hit: {hit.collider.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}) at distance: {hit.distance}");
            
            int layer = hit.collider.gameObject.layer;
        
            if (layer == LayerMask.NameToLayer("UnitCollider"))
            {
                if (!(hit.distance < minUnitDist)) continue;
                
                minUnitDist = hit.distance;
                closestUnitSelected = hit.collider.GetComponent<Entity>() ??
                                      hit.collider.GetComponentInParent<Entity>() ??
                                      hit.collider.GetComponentInChildren<Entity>();
            } 
            else if (layer == LayerMask.NameToLayer("HexagonCollider"))
            {
                if (!(hit.distance < minHexDist)) continue;
                
                minHexDist = hit.distance;
                closetHexSelected = hit.collider.GetComponent<HexVisualTile>() ??
                                    hit.collider.GetComponentInParent<HexVisualTile>() ??
                                    hit.collider.GetComponentInChildren<HexVisualTile>();
            }
            else if (layer == LayerMask.NameToLayer("VehicleCollider"))
            {
                if (!(hit.distance < minVehicleDist)) continue;
                
                minVehicleDist = hit.distance;
                closetVehicleSelected = hit.collider.GetComponent<Entity>() ??
                                        hit.collider.GetComponentInParent<Entity>() ??
                                        hit.collider.GetComponentInChildren<Entity>();
            }
        }
        
        if (closestUnitSelected != null)
        {
            SelectUnit(closestUnitSelected);
            return;
        }
        
        if (closetVehicleSelected != null)
        {
            if (closetHexSelected != null)
            {
                SimpleHexGrid closestHexGrid = closetHexSelected.GridReference;
                selectedHexGrid = closestHexGrid;

                if (closetVehicleSelected.EntityGrid != closestHexGrid)
                {
                    SelectVehicle(closetVehicleSelected);
                    return;
                }
            }
        }
        
        if (closetHexSelected != null)
        {
            //Debug.Log($"EntitySelectionManager: Hexagon Hovered {closetHexHovered.name}");

            if (EntityCommander.GetEntityInCommand() != null)
            {
                Vector2Int targetCoords = closetHexSelected.GridCoordinates;
                

                if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
                {
                    SelectHexWithUnitActive(closetHexSelected);
                    return;
                }

                if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
                {
                    SelectHexWithVehicleActive(closetHexSelected);
                    return;
                }
            }
            else
            {
                SelectHex(closetHexSelected);
            }
        }
    }

    void HandleMouseHover()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        // 1. Hit EVERYTHING under the mouse
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);

        // 2. Find the Hexagon in the list
        RaycastHit hitResult = default;
        
        float minUnitDist = float.MaxValue;
        float minHexDist = float.MaxValue;
        float minVehicleDist = float.MaxValue;

        Entity closestUnitHovered = null!;
        HexVisualTile closetHexHovered = null!;
        Entity closetVehicleHovered = null!;
        
        foreach (var hit in hits)
        {
            int layer = hit.collider.gameObject.layer;
        
            if (layer == LayerMask.NameToLayer("UnitCollider"))
            {
                if (!(hit.distance < minUnitDist)) continue;
                
                minUnitDist = hit.distance;
                closestUnitHovered = hit.collider.GetComponent<Entity>() ??
                                     hit.collider.GetComponentInParent<Entity>() ??
                                     hit.collider.GetComponentInChildren<Entity>();
            } 
            else if (layer == LayerMask.NameToLayer("HexagonCollider"))
            {
                if (!(hit.distance < minHexDist)) continue;
                
                minHexDist = hit.distance;
                closetHexHovered = hit.collider.GetComponent<HexVisualTile>() ??
                                   hit.collider.GetComponentInParent<HexVisualTile>() ??
                                   hit.collider.GetComponentInChildren<HexVisualTile>();
            }
            else if (layer == LayerMask.NameToLayer("VehicleCollider"))
            {
                if (!(hit.distance < minVehicleDist)) continue;
                
                minVehicleDist = hit.distance;
                closetVehicleHovered = hit.collider.GetComponent<Entity>() ??
                                       hit.collider.GetComponentInParent<Entity>() ??
                                       hit.collider.GetComponentInChildren<Entity>();
            }
        }
        
        if (closestUnitHovered != null)
        {
            HoverUnit(closestUnitHovered);
            return;
        }
        
        if (closetVehicleHovered != null)
        {
            HoverVehicle(closetVehicleHovered);
        }
        

        // 3. Only run the visualization logic if we actually found a hex
        if (closetHexHovered != null)
        {
            //Debug.Log($"EntitySelectionManager: Hexagon Hovered {closetHexHovered.name}");
            
            if (hoveredHexCoords == closetHexHovered.GridCoordinates) return;
            hoveredHexCoords = closetHexHovered.GridCoordinates;

            SimpleHexGrid hexGrid = closetHexHovered.GridReference;
            hoveredHexGrid = hexGrid;

            // Clear existing highlights
            foreach (SimpleHexGrid otherGrid in HexGridManager.Instance.GetAllGrids())
            {
                otherGrid.HexGridVisualiser.ClearOverlayHighlights();
            }

            // --- Visualization Logic ---
            if (EntityCommander.GetEntityInCommand())
            {
                Vector2Int targetCoords = closetHexHovered.GridCoordinates;
            
                // Re-use your pathfinding/visualization code here using hexHitResult.point
                PathNode startNode = new PathNode(EntityCommander.GetEntityInCommand().currentGridCoordinates, EntityCommander.GetEntityInCommand().currentGrid);
                PathNode endNode = new PathNode(targetCoords, hexGrid);
                List<PathNode> rawPath = pathfinder.FindPath(startNode, endNode);

                if (rawPath != null && rawPath.Count > 0)
                {

                    List<PathNode> finalPath;

                    // 2. Get the correct mover and smooth the path if it's a vehicle
                    if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
                    {
                        HoverHexWithVehicleActive(closetHexHovered);
                        VehiclePathMover mover = EntityCommander.GetEntityInCommand().GetComponent<VehiclePathMover>();
                        if (mover != null)
                        {
                            finalPath = mover.GetSmoothPathForVehicle(rawPath);
                        }
                        else
                        {
                            finalPath = rawPath;
                        }
                    }
                    else if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
                    {
                        HoverHexWithUnitActive(closetHexHovered);
                        finalPath = rawPath;
                    }
                    else // Add crafts and crap here
                    {
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
                                otherGrid.HexGridVisualiser.HighlightHexOverlay(pathNode.GridCoordinates,
                                    PathHexagonHighlightedColour);
                            }
                        }
                    }

                    // Highlight the target hex
                    gridVisualizer.HighlightHexOverlay(targetCoords, TargetHexagonHighlightedColour);

                }
            }
            else
            {
              HoverHex(closetHexHovered);
            }
        }
    }
}
