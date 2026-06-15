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
    private SimpleHexGridBase _hoveredHexGridBase;
    
    private Vector2Int selectedHexCoords;
    private SimpleHexGridBase _selectedHexGridBase;
    
    private MultiGridPathfinder pathfinder;
    private HexOverlayManager hexOverlayManager;
    
    private HexData _groundHexDataHovered;
    private SimpleHexGridBase _hoveredGroundGrid;
    
    public static List<PathNode> CachedMovementPath = new List<PathNode>();
    public Vector2Int _cachedTargetCoords;
    public SimpleHexGridBase _cachedTargetGrid;
    
    private object _lastHoveredObject = null;
    
    private void Awake()
    {
        pathfinder = GetComponent<MultiGridPathfinder>();
        hexOverlayManager = GetComponent<HexOverlayManager>();
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
            previousVehicle.EntitySelected(false);
        }
        
        Debug.Log($"EntitySelectionManager: Selected {entity.name}");
        EntityCommander.SetEntityToCommand(entity);
        VehicleEntity vehicle = (VehicleEntity)entity;
        vehicle.EntitySelected(true);
    }
    
    public static void SelectUnit(Entity entity)
    {
        if (EntityCommander.GetEntityInCommand() != null && EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
        {
            VehicleEntity previousVehicle = (VehicleEntity)EntityCommander.GetEntityInCommand();
            previousVehicle.EntitySelected(false);
        }
        
        Debug.Log($"EntitySelectionManager: Selected {entity.name}");
        EntityCommander.SetEntityToCommand(entity);
    }

// Overload for Ground Hexagons
    private void SelectHex(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Selected Ground {coords}");
    }

    private void SelectHexWithUnitActive(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Selected Ground {coords} with Unit Active");
        if (selectedHexCoords == coords && _selectedHexGridBase == grid) return;
    
        selectedHexCoords = coords;
        _selectedHexGridBase = grid;
    
        EntityCommander.SetTargetGridAndCoordinates(grid, selectedHexCoords);
        EntityCommander.CommandUnitToMove();
    }

    private void SelectHexWithVehicleActive(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Selected Ground {coords} with Vehicle Active");
        if (selectedHexCoords == coords && _selectedHexGridBase == grid) return;

        selectedHexCoords = coords;
        _selectedHexGridBase = grid;
    
        VehicleEntity vehicle = EntityCommander.GetEntityInCommand() as VehicleEntity;
    
        if (grid == vehicle.vehicleInteriorGridBase)
            return;

        EntityCommander.SetTargetGridAndCoordinates(grid, selectedHexCoords);
        EntityCommander.CommandUnitToMove();
    }
    
    private void HoverVehicle(Entity entity)
    {
        Debug.Log($"EntitySelectionManager: Hovered {entity.name}");
    }
    
    private void HoverUnit(Entity entity)
    {
        Debug.Log($"EntitySelectionManager: Hovered {entity.name}");
    }

    private void HoverHex(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Hovered Ground {coords}");
    }

    private void HoverHexWithUnitActive(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Hovered Ground {coords} with Unit Active");
    }

    private void HoverHexWithVehicleActive(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Hovered Ground {coords} with Vehicle Active");
    }

    /// <summary>
    /// Processes a left-click, prioritizing unit selection over movement commands.
    void HandleLeftClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);

        float minUnitDist = float.MaxValue;
        float minHexDist = float.MaxValue;
        float minVehicleDist = float.MaxValue;

        Entity closestUnitSelected = null!;
        HexVisualTile closetHexSelected = null!;
        Entity closestVehicleSelected = null!;

        // --- NEW: Ground Data Tracking ---
        HexData groundHexData = default;
        SimpleHexGridBase groundGridSelected = null;
        
        Vector2Int targetCoords;
        SimpleHexGridBase targetGrid;

        foreach (var hit in hits)
        {
            int layer = hit.collider.gameObject.layer;
        
            if (layer == LayerMask.NameToLayer("UnitCollider"))
            {
                if (!(hit.distance < minUnitDist)) continue;
                minUnitDist = hit.distance;
                closestUnitSelected = hit.collider.GetComponent<Entity>() ?? hit.collider.GetComponentInParent<Entity>() ?? hit.collider.GetComponentInChildren<Entity>();
            } 
            else if (layer == LayerMask.NameToLayer("HexagonCollider"))
            {
                if (!(hit.distance < minHexDist)) continue;
                minHexDist = hit.distance;
                closetHexSelected = hit.collider.GetComponent<HexVisualTile>() ?? hit.collider.GetComponentInParent<HexVisualTile>() ?? hit.collider.GetComponentInChildren<HexVisualTile>();
            }
            else if (layer == LayerMask.NameToLayer("VehicleCollider"))
            {
                if (!(hit.distance < minVehicleDist)) continue;
                minVehicleDist = hit.distance;
                closestVehicleSelected = hit.collider.GetComponent<Entity>() ?? hit.collider.GetComponentInParent<Entity>() ?? hit.collider.GetComponentInChildren<Entity>();
            }
        }
        
        // --- NEW: Physics-less Fallback for Ground Grids ---
        if (closetHexSelected == null)
        {
            var groundGrid = FindObjectOfType<SimpleHexGridGround>();
            if (groundGrid != null && groundGrid.TryGetHexFromRay(ray, out HexData data))
            {
                groundHexData = data;
                groundGridSelected = groundGrid;
            }
        }
        
        if (closestUnitSelected != null)
        {
            SelectUnit(closestUnitSelected);
            return;
        }

        if (closestVehicleSelected != null)
        {
            // Keep your existing vehicle/interior logic
            if (closetHexSelected != null)
            {
                SimpleHexGridBase closestHexGridBase = closetHexSelected.gridBaseReference;
                VehicleEntity vehicle = closestVehicleSelected as VehicleEntity;
                if (vehicle.vehicleInteriorGridBase != closestHexGridBase)
                {
                    SelectVehicle(closestVehicleSelected);
                    return;
                }
            }
        }
    
        // --- Unified Selection Logic ---
        if (closetHexSelected != null || groundGridSelected != null)
        {
            targetCoords = (closetHexSelected != null) ? closetHexSelected.GridCoordinates : groundHexData.GridCoordinates;
            targetGrid = (closetHexSelected != null) ? closetHexSelected.gridBaseReference : groundGridSelected;

            if (EntityCommander.GetEntityInCommand() != null)
            {
                if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
                {
                    // Call the correct overload based on what was hit
                    if (closetHexSelected != null)
                    {
                        SelectHexWithUnitActive(closetHexSelected.GridCoordinates, closetHexSelected.gridBaseReference);
                    }
                    else
                    {
                        SelectHexWithUnitActive(targetCoords, targetGrid);
                    }
                    return;
                }

                if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
                {
                    if (closetHexSelected != null) SelectHexWithVehicleActive(closetHexSelected.GridCoordinates, closetHexSelected.gridBaseReference);
                    else SelectHexWithVehicleActive(targetCoords, targetGrid);
                    return;
                }
            }
            else
            {
                if (closetHexSelected != null) SelectHex(closetHexSelected.GridCoordinates, closetHexSelected.gridBaseReference);
                else SelectHex(targetCoords, targetGrid);
            }
        }
        
        // Catch for batched hexagons
        if (closestUnitSelected == null && closestVehicleSelected == null && closetHexSelected == null)
        {
            // If the hover system has a cached target, treat this click as a selection
            if (_cachedTargetCoords != Vector2Int.zero && _cachedTargetGrid != null)
            {
                // Inject the cached data into our selection logic
                targetCoords = _cachedTargetCoords;
                targetGrid = _cachedTargetGrid;
            
                // Now proceed as if we clicked a hex
                ProcessHexSelection(targetCoords, targetGrid);
            }
        }
    }

    void HandleMouseHover()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);

        float minUnitDist = float.MaxValue;
        float minHexDist = float.MaxValue;
        float minVehicleDist = float.MaxValue;

        Entity closestUnitHovered = null!;
        HexVisualTile closetHexHovered = null!;
        Entity closetVehicleHovered = null!;

        // Reset ground tracking
        _groundHexDataHovered = default;
        _hoveredGroundGrid = null;
        
        // -------------------------------------------------------------

        VehicleEntity vehicleAlreadySelected = null;
        UnitEntity unitAlreadySelected = null;
        if (EntityCommander.GetEntityInCommand() != null)
        {
            if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
                vehicleAlreadySelected = EntityCommander.GetEntityInCommand() as VehicleEntity;
            if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
                unitAlreadySelected = EntityCommander.GetEntityInCommand() as UnitEntity;
        }

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
                // If we hit a physical tile, clear the mathematical ground hover
                _hoveredGroundGrid = null;
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

        // This deosnt work, needs more thought
        object currentHoveredObject = (object)closestUnitHovered ?? (object)closetVehicleHovered ?? (object)closetHexHovered;
        
        // if (currentHoveredObject == _lastHoveredObject && currentHoveredObject != null)
        // {
        //     // We are hovering over the exact same object as last frame, do nothing.
        //     return;
        // }

        // 2. If NO specific HexTile collider was hit, use the Math fallback
        if (closetHexHovered == null)
        {
            var groundGrid = FindObjectOfType<SimpleHexGridGround>();
            if (groundGrid != null && groundGrid.TryGetHexFromRay(ray, out HexData data))
            {
                _groundHexDataHovered = data;
                _hoveredGroundGrid = groundGrid;
            }
        }

        if (closestUnitHovered != null)
        {
            HoverUnit(closestUnitHovered);
            _lastHoveredObject = currentHoveredObject;
            return;
        }

        if (closetVehicleHovered != null)
        {
            HoverVehicle(closetVehicleHovered);
        }

        SimpleHexGridBase hexGridBase = null;
        Vector2Int targetCoords = Vector2Int.zero;
        bool foundHex = false;

        if (closetHexHovered != null)
        {
            targetCoords = closetHexHovered.GridCoordinates;
            hexGridBase = closetHexHovered.gridBaseReference;
            foundHex = true;
        }
        else if (_hoveredGroundGrid != null)
        {
            targetCoords = _groundHexDataHovered.GridCoordinates;
            hexGridBase = _hoveredGroundGrid;
            foundHex = true;
        }
   
        if (foundHex)
        {
            if (hoveredHexCoords == targetCoords && _hoveredHexGridBase == hexGridBase) return;

            hoveredHexCoords = targetCoords;
            _hoveredHexGridBase = hexGridBase;

            hexOverlayManager.ClearAll();

            if (EntityCommander.GetEntityInCommand())
            {
                PathNode startNode = new PathNode(EntityCommander.GetEntityInCommand().CurrentGridCoordinates,
                    EntityCommander.GetEntityInCommand().currentGridBase);
                PathNode endNode = new PathNode(targetCoords, hexGridBase);
                List<PathNode> rawPath = pathfinder.FindPath(startNode, endNode);

                if (rawPath != null && rawPath.Count > 0)
                {
                    List<PathNode> finalPath;

                    if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
                    {
                        if (vehicleAlreadySelected.GetDriver() == null) return;
                
                        // Call overload if ground, else original
                        if (closetHexHovered != null)
                        {
                            HoverHexWithVehicleActive(closetHexHovered.GridCoordinates,
                                closetHexHovered.gridBaseReference);
                        }
                        else
                        {
                            HoverHexWithVehicleActive(targetCoords, hexGridBase);
                        }
                        
                        _lastHoveredObject = currentHoveredObject;
                
                        VehiclePathMover mover = EntityCommander.GetEntityInCommand().GetComponent<VehiclePathMover>();
                        finalPath = (mover != null) ? mover.GetSmoothPathForVehicle(rawPath) : rawPath;
                    }
                    else if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
                    {
                        if (closetHexHovered != null)
                        {
                            HoverHexWithUnitActive(closetHexHovered.GridCoordinates,
                                closetHexHovered.gridBaseReference);
                        }
                        else
                        {
                            HoverHexWithUnitActive(targetCoords, hexGridBase);
                        }
                        _lastHoveredObject = currentHoveredObject;
                        finalPath = rawPath;
                    }
                    else
                    {
                        finalPath = rawPath;
                    }
                    
                    CachedMovementPath = finalPath; 
                    _cachedTargetCoords = targetCoords;
                    _cachedTargetGrid = hexGridBase;

                    foreach (PathNode pathNode in finalPath)
                    {
                        hexOverlayManager.SetOverlay(
                            new HexGridManager.HexGridAndCoords(pathNode.GridCoordinates, pathNode.GridBaseReference),
                            PathHexagonHighlightedColour, true);
                    }

                    hexOverlayManager.SetOverlay(new HexGridManager.HexGridAndCoords(targetCoords, hexGridBase),
                        TargetHexagonHighlightedColour, true);
                }
                else
                {
                    CachedMovementPath = null;
                }
            }
            else 
            {
                // Updated non-commanded hover call
                if (closetHexHovered != null)
                {
                    HoverHex(closetHexHovered.GridCoordinates, closetHexHovered.gridBaseReference);
                }
                else
                {
                    HoverHex(targetCoords, hexGridBase);
                }
                _lastHoveredObject = currentHoveredObject;
            }
        }
    }
    
    private void ProcessHexSelection(Vector2Int coords, SimpleHexGridBase grid)
    {
        if (EntityCommander.GetEntityInCommand() != null)
        {
            if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
                SelectHexWithUnitActive(coords, grid);
            else if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
                SelectHexWithVehicleActive(coords, grid);
        }
        else
        {
            SelectHex(coords, grid);
        }
    }
}
