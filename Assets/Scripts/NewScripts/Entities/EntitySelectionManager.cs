using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization; // Required for List

// Phase 11.1 (Revised): UnitSelectionManager Class
// Purpose: Handles player input for unit selection and commanding selected units
// to move using a single mouse button. Informs the UnitCommander which unit is
// currently active and where to move it. Adheres to Single Responsibility Principle.
public class EntitySelectionManager : MonoBehaviour
{
    private GameLevelManager gameLevelManager;
    
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
    
    private string[] layerNames = { "HexagonCollider", "VehicleCollider", "UnitCollider", "CraftCollider" };
    
    private Vector3 lastMousePosition;

    private Camera camera;
    
    private void Awake()
    {
        gameLevelManager = GetComponent<GameLevelManager>();
        hexOverlayManager = GetComponent<HexOverlayManager>();
        pathfinder = GetComponent<MultiGridPathfinder>();
        camera = Camera.main;
    }

    void Update()
    {
        Entity commanded = EntityCommander.GetEntityInCommand();
    
        // If we have a commanded entity, but its grid is no longer active
        // OR the entity is null, we must wipe the selection.
        if (commanded != null && commanded.currentGridBase != gameLevelManager.ActiveGrid)
        {
            EntityCommander.SetEntityToCommand(null);
            return; 
        }
        
        // Only track mouse on screen
        if (Input.mousePosition.x < 0 || Input.mousePosition.x > Screen.width ||
            Input.mousePosition.y < 0 || Input.mousePosition.y > Screen.height)
        {
            // Mouse is off-screen. Stop everything.
            return;
        }

        // Only listen for the left mouse button click for all actions
        if (Input.GetMouseButtonDown(0)) // 0 is the left mouse button
        {
            HandleLeftClick();
        }
        else
        {
            if (Input.mousePosition != lastMousePosition)
            {
                HandleMouseHover();
                lastMousePosition = Input.mousePosition;
            }
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
    
    public static void SelectCraft(Entity entity)
    {
        if (EntityCommander.GetEntityInCommand() != null && EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Craft)
        {
            CraftEntity previousCraft = (CraftEntity)EntityCommander.GetEntityInCommand();
            previousCraft.EntitySelected(false);
        }
        
        Debug.Log($"EntitySelectionManager: Selected {entity.name}");
        EntityCommander.SetEntityToCommand(entity);
        CraftEntity craft = (CraftEntity)entity;
        craft.EntitySelected(true);
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
    
        if (grid == vehicle.InteriorGridBase)
            return;

        EntityCommander.SetTargetGridAndCoordinates(grid, selectedHexCoords);
        EntityCommander.CommandUnitToMove();
    }
    
    private void SelectHexWithCraftActive(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Selected Ground {coords} with Craft Active");
        if (selectedHexCoords == coords && _selectedHexGridBase == grid) return;

        selectedHexCoords = coords;
        _selectedHexGridBase = grid;
    
        CraftEntity craft = EntityCommander.GetEntityInCommand() as CraftEntity;
    
        if (grid == craft.InteriorGridBase)
            return;

        EntityCommander.SetTargetGridAndCoordinates(grid, selectedHexCoords);
        EntityCommander.CommandUnitToMove();
    }
    
    private void HoverVehicle(Entity entity)
    {
        Debug.Log($"EntitySelectionManager: Hovered {entity.name}");
    }
    
    private void HoverCraft(Entity entity)
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
    
    private void HoverHexWithCraftActive(Vector2Int coords, SimpleHexGridBase grid)
    {
        Debug.Log($"EntitySelectionManager: Hovered Ground {coords} with Craft Active");
    }


    /// <summary>
    /// Processes a left-click, prioritizing unit selection over movement commands.
    void HandleLeftClick()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
            return;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        int mask = LayerMask.GetMask(layerNames);
        RaycastHit[] hits = Physics.RaycastAll(ray, MultiGridPathfinder.MaxRaycastPathDistance, mask);

        float minUnitDist = float.MaxValue;
        float minHexDist = float.MaxValue;
        float minVehicleDist = float.MaxValue;
        float minCraftDist = float.MaxValue;

        Entity closestUnitSelected = null!;
        HexVisualTile closetHexSelected = null!;
        Entity closestVehicleSelected = null!;
        Entity closestCraftSelected = null!;

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
            else if (layer == LayerMask.NameToLayer("CraftCollider"))
            {
                if (!(hit.distance < minCraftDist)) continue;
                minCraftDist = hit.distance;
                closestCraftSelected = hit.collider.GetComponent<Entity>() ?? hit.collider.GetComponentInParent<Entity>() ?? hit.collider.GetComponentInChildren<Entity>();
            }
        }
        
        // --- NEW: Physics-less Fallback for Ground Grids ---
        if (closetHexSelected == null)
        {
            if (gameLevelManager.ActiveGrid != null && gameLevelManager.ActiveGrid.TryGetHexFromRay(ray, out HexData data, MultiGridPathfinder.MaxRaycastPathDistance))
            {
                groundHexData = data;
                groundGridSelected = gameLevelManager.ActiveGrid;
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
                if (vehicle.InteriorGridBase != closestHexGridBase)
                {
                    SelectVehicle(closestVehicleSelected);
                    return;
                }
            }
        }
        
        if (closestCraftSelected != null)
        {
            // Keep your existing vehicle/interior logic
            if (closetHexSelected != null)
            {
                SimpleHexGridBase closestHexGridBase = closetHexSelected.gridBaseReference;
                CraftEntity craft = closestCraftSelected as CraftEntity;
                if (craft.InteriorGridBase != closestHexGridBase)
                {
                    SelectCraft(closestCraftSelected);
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
                
                if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Craft)
                {
                    if (closetHexSelected != null) SelectHexWithCraftActive(closetHexSelected.GridCoordinates, closetHexSelected.gridBaseReference);
                    else SelectHexWithCraftActive(targetCoords, targetGrid);
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
        if (Cursor.lockState == CursorLockMode.Locked)
            return;
        
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        int mask = LayerMask.GetMask(layerNames);
        RaycastHit[] hits = Physics.RaycastAll(ray, MultiGridPathfinder.MaxRaycastPathDistance, mask);

        float minUnitDist = float.MaxValue;
        float minHexDist = float.MaxValue;
        float minVehicleDist = float.MaxValue;
        float minCraftDist = float.MaxValue;
        
        Entity closestUnitHovered = null!;
        Entity closetVehicleHovered = null!;
        Entity closestCraftHovered = null!;
        HexVisualTile closetHexHovered = null!;

        // Reset ground tracking
        _groundHexDataHovered = default;
        _hoveredGroundGrid = null;
        
        // -------------------------------------------------------------

        VehicleEntity vehicleAlreadySelected = null;
        UnitEntity unitAlreadySelected = null;
        CraftEntity craftAlreadySelected = null;
        
        if (EntityCommander.GetEntityInCommand() != null)
        {
            if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
                vehicleAlreadySelected = EntityCommander.GetEntityInCommand() as VehicleEntity;
            if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Craft)
                craftAlreadySelected = EntityCommander.GetEntityInCommand() as CraftEntity;
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
            else if (layer == LayerMask.NameToLayer("CraftCollider"))
            {
                if (!(hit.distance < minCraftDist)) continue;
                minCraftDist = hit.distance;
                closestCraftHovered = hit.collider.GetComponent<Entity>() ??
                                      hit.collider.GetComponentInParent<Entity>() ??
                                      hit.collider.GetComponentInChildren<Entity>();
            }
        }

        // This deosnt work, needs more thought
       // Entity currentHoveredObject = closestUnitHovered ?? closetVehicleHovered ?? closetHexHovered;
        
        // if (currentHoveredObject == _lastHoveredObject && currentHoveredObject != null)
        // {
        //     // We are hovering over the exact same object as last frame, do nothing.
        //     return;
        // }

        // 2. If NO specific HexTile collider was hit, use the Math fallback
        if (closetHexHovered == null)
        {
            if (gameLevelManager.ActiveGrid != null 
                && gameLevelManager.ActiveGrid.TryGetHexFromRay(ray, out HexData data, MultiGridPathfinder.MaxRaycastPathDistance))
            {
                _groundHexDataHovered = data;
                _hoveredGroundGrid = gameLevelManager.ActiveGrid;
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
            if (closetHexHovered != null)
            {
                if (closetHexHovered.gridBaseReference == closetVehicleHovered.currentGridBase)
                {
                    return;
                }
            }
        }
        
        
        if (closestCraftHovered != null)
        {
            HoverCraft(closestCraftHovered);
            if (closetHexHovered != null)
            {
                if (closetHexHovered.gridBaseReference == closestCraftHovered.currentGridBase)
                {
                    return;
                }
            }
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
                // --- ADD THIS DISTANCE PRE-CHECK ---
                Entity commander = EntityCommander.GetEntityInCommand();
                Vector3 startPos = commander.transform.position;
                Vector3 targetWorldPos = hexGridBase.GetHexWorldPosition(targetCoords, 0);

                // If target is further than your limit, clear path and exit
                if (Vector3.Distance(startPos, targetWorldPos) > MultiGridPathfinder.MaxRaycastPathDistance)
                {
                    CachedMovementPath = null;
                    hexOverlayManager.ClearAll(); // Clear visuals if we were too far
                    return; 
                }
                // ------------------------------------
                
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
                
                        VehiclePathMover mover = EntityCommander.GetEntityInCommand().GetComponent<VehiclePathMover>();
                        finalPath = (mover != null) ? mover.GetSmoothPathForVehicle(rawPath) : rawPath;
                    }
                    else if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Craft)
                    {
                        if (craftAlreadySelected.GetDriver() == null) return;
                
                        // Call overload if ground, else original
                        if (closetHexHovered != null)
                        {
                            HoverHexWithCraftActive(closetHexHovered.GridCoordinates,
                                closetHexHovered.gridBaseReference);
                        }
                        else
                        {
                            HoverHexWithCraftActive(targetCoords, hexGridBase);
                        }
                
                        finalPath = rawPath;
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
            else if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Craft)
                SelectHexWithCraftActive(coords, grid);
        }
        else
        {
            SelectHex(coords, grid);
        }
    }
    
    [ContextMenu("Try Jump Through Portal")]
    public void TryJumpThroughPortal()
    {
        Entity entity = EntityCommander.GetEntityInCommand();
        if (entity == null) return;

        Debug.Log($"fuck entity = {entity}");
        
        // Use the entity's current location to jump
        LevelPortalManager.Instance.EnterPortal(entity.currentGridBase, entity.CurrentGridCoordinates);
    }
    
    
}
