using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for .Select() and .Join()

public class UnitSelectionController : MonoBehaviour
{
    [Header("Selection Settings")]
    public Material selectedMaterial;  // Material to show when unit/vehicle is selected
    public Material normalMaterial;    // Normal unit/vehicle material
    
    [Header("Selection Indicator")]
    public GameObject selectionIndicatorPrefab; // Optional: a ring/circle to show around selected unit/vehicle

    [Header("Raycast Settings")]
    [Tooltip("Layers that the mouse raycast should interact with (e.g., Units, Vehicles, and HexagonColliders).")]
    public LayerMask clickableLayers; // LayerMask for raycasting

    [Header("Hover Highlight Decals")]
    [Tooltip("Prefab for hexagons reachable directly (e.g., green).")]
    public GameObject directMoveDecalPrefab;
    [Tooltip("Prefab for hexagons requiring a loop (e.g., yellow/orange).")]
    public GameObject loopingMoveDecalPrefab;
    [Tooltip("Prefab for unreachable hexagons (e.g., red).")]
    public GameObject unreachableDecalPrefab;

    private UnitController currentSelectedUnit;
    private VehicleController currentSelectedVehicle; // New: To track selected vehicle

    private UnitPathFinder unitPathfinder; // Renamed for clarity
    private VehiclePathFinderHybrid vehiclePathfinder; // MODIFIED: Reference to VehiclePathFinderCatmullRom
    private HexagonController hexController;
    private Camera playerCamera;
    
    // Store original materials for each selectable object
    private Dictionary<MonoBehaviour, Material> originalMaterials = new Dictionary<MonoBehaviour, Material>(); // Changed to MonoBehaviour

    // New: Cache the last hovered hexagon's grid position to prevent redundant checks
    private Vector2Int lastHoveredHexGridPos = new Vector2Int(-1, -1); // Initialize with an invalid position
    private GameObject currentHighlightDecalInstance; // Tracks the currently displayed highlight decal

    void Start()
    {
        playerCamera = Camera.main;
        unitPathfinder = FindObjectOfType<UnitPathFinder>();
        vehiclePathfinder = FindObjectOfType<VehiclePathFinderHybrid>(); // MODIFIED: Find the new vehicle pathfinder
        hexController = FindObjectOfType<HexagonController>();
        
        if (unitPathfinder == null)
        {
            Debug.LogError("UnitSelectionManager: No HexPathfinder found in scene! Ensure it's on an active GameObject.");
        }
        if (vehiclePathfinder == null) // Check for vehicle pathfinder
        {
            Debug.LogError("UnitSelectionManager: No VehiclePathFinderHybrid found in scene! Ensure it's on an active GameObject."); // MODIFIED: Error message
        }
        if (hexController == null)
        {
            Debug.LogError("UnitSelectionManager: No HexagonController found in scene! Ensure it's on an active GameObject.");
        }

        // Initialize clickableLayers if it's not set in the Inspector (e.g., for safety)
        if (clickableLayers.value == 0)
        {
            if (hexController != null && hexController.colliderLayer.value != 0)
            {
                clickableLayers = hexController.colliderLayer;
                Debug.LogWarning("clickableLayers was not set. Defaulting to HexagonController's colliderLayer. Please also add Unit and Vehicle layers.");
            }
            else
            {
                clickableLayers = LayerMask.GetMask("Default");
                Debug.LogWarning("clickableLayers not set and HexagonController's colliderLayer not found. Using 'Default' layer. Please configure in Inspector (Units, Vehicles, HexagonColliders).");
            }
        }
    }
    
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse click
        {
            HandleMouseClick();
        }

        // Handle mouse hover for selected vehicle reachability prediction
        HandleMouseHover();
    }
    
    void HandleMouseClick()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableLayers))
        {
            Debug.Log($"Raycast hit: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)} at {hit.point}");

            UnitController clickedUnit = hit.collider.GetComponent<UnitController>();
            VehicleController clickedVehicle = hit.collider.GetComponent<VehicleController>(); // Check for VehicleController

            if (clickedUnit != null && clickedUnit.CanBeSelected)
            {
                SelectObject(clickedUnit); // Select the unit
            }
            else if (clickedVehicle != null && clickedVehicle.CanBeSelected) // If a vehicle was clicked
            {
                SelectObject(clickedVehicle); // Select the vehicle
            }
            else if (currentSelectedUnit != null && currentSelectedUnit.CanMove)
            {
                // Unit is selected and clicked on terrain
                MoveSelectedUnitToPosition(hit.point);
            }
            else if (currentSelectedVehicle != null && currentSelectedVehicle.CanMove)
            {
                // Vehicle is selected and clicked on terrain
                MoveSelectedVehicleToPosition(hit.point);
            }
            else
            {
                // Clicked on something else, or clicked on a non-selectable object, deselect if anything is currently selected
                DeselectAll();
            }
        }
        else // If raycast didn't hit anything
        {
            Debug.Log("Raycast did not hit anything on clickable layers.");
            DeselectAll(); // Deselect if nothing was hit
        }
    }

    /// <summary>
    /// Handles mouse hovering to provide feedback on vehicle reachability.
    /// </summary>
    void HandleMouseHover()
    {
        // If no vehicle is selected or essential pathfinding components are missing, clear any existing highlight and return.
        if (currentSelectedVehicle == null || vehiclePathfinder == null || hexController == null)
        {
            ClearCurrentHighlight();
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Raycast only against the HexagonCollider layer for hovering feedback
        // Assuming HexagonController.colliderLayer is the layer for hexagons
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, hexController.colliderLayer))
        {
            Vector2Int hoveredGridPos = hexController.WorldToGridPosition(hit.point);
            
            // Only proceed if the hovered hexagon is different from the last one
            if (hoveredGridPos == lastHoveredHexGridPos)
            {
                return; // Same hexagon, no need to re-evaluate
            }

            // Update the cached hovered hexagon
            lastHoveredHexGridPos = hoveredGridPos;

            // Get the world position of the center of the hovered hex, adjusted for height
            Vector3 hoveredWorldPos = hexController.GetHexWorldPosition(hoveredGridPos.x, hoveredGridPos.y);
            // CORRECTED: Add half the hex mesh height and a small Z-fighting offset.
            // Removed the incorrect 'heightOffset' which was causing the decal to float too high.
            hoveredWorldPos.y += hexController.hexMeshHeight * 0.5f + 0.05f; 

            // Check if a path exists to this hex first (A* based)
            if (!vehiclePathfinder.PathExists(currentSelectedVehicle.transform.position, hoveredWorldPos))
            {
                Debug.Log($"Hovered Hex ({hoveredGridPos}): Unreachable (No A* Path)");
                DisplayHighlightDecal(unreachableDecalPrefab, hoveredWorldPos);
                return;
            }

            // Get the predicted movement type using the VehiclePathFinderHybrid's method
            VehiclePathFinderHybrid.MovementType moveType = vehiclePathfinder.GetMovementType(
                currentSelectedVehicle.transform.position,
                hoveredWorldPos,
                currentSelectedVehicle.transform.forward,
                vehiclePathfinder.movementSpeed
            );

            // Print the result to the console
            Debug.Log($"Hovered Hex ({hoveredGridPos}): Movement Type - {moveType}");

            // Display the appropriate highlight decal
            switch (moveType)
            {
                case VehiclePathFinderHybrid.MovementType.DirectClose:
                case VehiclePathFinderHybrid.MovementType.DirectFar:
                case VehiclePathFinderHybrid.MovementType.SmoothDirect:
                    DisplayHighlightDecal(directMoveDecalPrefab, hoveredWorldPos);
                    break;
                case VehiclePathFinderHybrid.MovementType.RequiresLooping:
                    DisplayHighlightDecal(loopingMoveDecalPrefab, hoveredWorldPos);
                    break;
                case VehiclePathFinderHybrid.MovementType.Unreachable: // Should be caught by PathExists check, but as a fallback
                    DisplayHighlightDecal(unreachableDecalPrefab, hoveredWorldPos);
                    break;
            }
        }
        else // If raycast didn't hit any hexagon
        {
            ClearCurrentHighlight();
        }
    }

    /// <summary>
    /// Instantiates and positions the appropriate highlight decal.
    /// Destroys any existing decal before creating a new one.
    /// </summary>
    /// <param name="decalPrefab">The prefab for the decal to display.</param>
    /// <param name="position">The world position to place the decal.</param>
    void DisplayHighlightDecal(GameObject decalPrefab, Vector3 position)
    {
        // Destroy any existing decal first
        if (currentHighlightDecalInstance != null)
        {
            Destroy(currentHighlightDecalInstance);
            currentHighlightDecalInstance = null;
        }

        if (decalPrefab != null)
        {
            currentHighlightDecalInstance = Instantiate(decalPrefab, position, Quaternion.identity);
            // Optional: Parent the decal to something for cleaner hierarchy, e.g., a dedicated "Highlights" empty GameObject
            // currentHighlightDecalInstance.transform.SetParent(someHighlightsContainer.transform);
        }
    }

    /// <summary>
    /// Clears the currently displayed highlight decal and resets the last hovered hex.
    /// </summary>
    void ClearCurrentHighlight()
    {
        if (currentHighlightDecalInstance != null)
        {
            Destroy(currentHighlightDecalInstance);
            currentHighlightDecalInstance = null;
        }
        if (lastHoveredHexGridPos != new Vector2Int(-1, -1))
        {
            lastHoveredHexGridPos = new Vector2Int(-1, -1);
            Debug.Log("Mouse no longer hovering over a hexagon."); // Optional: indicate no hex is hovered
        }
    }
    
    // Generic method to select either a Unit or a Vehicle
    public void SelectObject(MonoBehaviour selectableObject)
    {
        // Deselect current unit/vehicle if a different one is being selected
        if (currentSelectedUnit != null && currentSelectedUnit != selectableObject)
        {
            DeselectCurrentUnit();
        }
        if (currentSelectedVehicle != null && currentSelectedVehicle != selectableObject)
        {
            DeselectCurrentVehicle();
        }

        if (selectableObject is UnitController unit && unit.CanBeSelected)
        {
            if (currentSelectedUnit != unit) // Only re-select if it's a different unit
            {
                currentSelectedUnit = unit;
                currentSelectedVehicle = null; // Ensure only one type is selected at a time
                ApplySelectionVisual(unit);
                unit.OnSelected();
                Debug.Log($"Selected unit: {unit.name}");
            }
        }
        else if (selectableObject is VehicleController vehicle && vehicle.CanBeSelected)
        {
            if (currentSelectedVehicle != vehicle) // Only re-select if it's a different vehicle
            {
                currentSelectedVehicle = vehicle;
                currentSelectedUnit = null; // Ensure only one type is selected at a time
                ApplySelectionVisual(vehicle);
                vehicle.OnSelected();
                Debug.Log($"Selected vehicle: {vehicle.name}");
            }
        }
        // Ensure highlight is cleared when a new object is selected (or deselected implicitly)
        ClearCurrentHighlight();
    }

    void DeselectCurrentUnit()
    {
        if (currentSelectedUnit != null)
        {
            RemoveSelectionVisual(currentSelectedUnit);
            currentSelectedUnit.OnDeselected();
            currentSelectedUnit = null;
            Debug.Log("Unit deselected.");
        }
    }

    void DeselectCurrentVehicle()
    {
        if (currentSelectedVehicle != null)
        {
            RemoveSelectionVisual(currentSelectedVehicle);
            currentSelectedVehicle.OnDeselected();
            currentSelectedVehicle = null;
            Debug.Log("Vehicle deselected.");
        }
        // Ensure highlight is cleared when vehicle is deselected
        ClearCurrentHighlight();
    }
    
    // Generic method to apply selection visual
    void ApplySelectionVisual(MonoBehaviour selectableObject)
    {
        Renderer renderer = selectableObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Store original material if not already stored
            if (!originalMaterials.ContainsKey(selectableObject))
            {
                originalMaterials[selectableObject] = renderer.material;
            }
            // Apply selected material
            if (selectedMaterial != null)
            {
                renderer.material = selectedMaterial;
            }
        }
        
        if (selectionIndicatorPrefab != null)
        {
            GameObject indicator = Instantiate(selectionIndicatorPrefab, selectableObject.transform);
            indicator.name = "SelectionIndicator";
            indicator.transform.localPosition = Vector3.zero + Vector3.up * 0.1f;
        }
    }
    
    // Generic method to remove selection visual
    void RemoveSelectionVisual(MonoBehaviour selectableObject)
    {
        Renderer renderer = selectableObject.GetComponent<Renderer>();
        if (renderer != null && originalMaterials.ContainsKey(selectableObject))
        {
            renderer.material = originalMaterials[selectableObject];
            originalMaterials.Remove(selectableObject);
        }
        
        Transform indicator = selectableObject.transform.Find("SelectionIndicator");
        if (indicator != null)
        {
            Destroy(indicator.gameObject);
        }
    }
    
    void MoveSelectedUnitToPosition(Vector3 worldPosition)
    {
        if (currentSelectedUnit == null || unitPathfinder == null) return;
        
        // Now FindPath for units returns List<HexagonPathfinder.UnitPathStep>
        List<UnitPathFinder.UnitPathStep> pathSteps = unitPathfinder.FindPath(currentSelectedUnit.transform.position, worldPosition);
        
        if (pathSteps.Count > 0)
        {
            // Pass the new pathSteps to MoveAlongPath
            unitPathfinder.MoveAlongPath(currentSelectedUnit.gameObject, pathSteps, () => {
                Debug.Log($"{currentSelectedUnit.name} reached destination!");
                currentSelectedUnit.OnMovementComplete();
            });
            currentSelectedUnit.OnMovementStarted();
        }
        else
        {
            Debug.Log("No path found for unit to clicked position!");
        }
        // Clear highlight after initiating movement
        ClearCurrentHighlight();
    }

    void MoveSelectedVehicleToPosition(Vector3 targetPosition)
    {
        if (currentSelectedVehicle == null || vehiclePathfinder == null) return;

        Vector3 vehicleWorldPosition = currentSelectedVehicle.transform.position;
        Vector2Int currentVehicleGridPos = hexController.WorldToGridPosition(vehicleWorldPosition);

        // Check if movement is possible and get movement data
        VehiclePathFinderHybrid.VehicleMovementData movementData =
            vehiclePathfinder.CalculateMovement(vehicleWorldPosition, targetPosition);

        if (movementData.pathExists)
        {
            // Move directly to destination
            vehiclePathfinder.MoveToDestination(currentSelectedVehicle, movementData,
                () => { Debug.Log("Vehicle reached destination!"); });
        }
        // Clear highlight after initiating movement
        ClearCurrentHighlight();
    }

    // Public method to deselect all (useful for other scripts)
    public void DeselectAll()
    {
        DeselectCurrentUnit();
        DeselectCurrentVehicle();
        ClearCurrentHighlight(); // Ensure highlights are cleared when everything is deselected
    }
}




// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq; // Added for .Select() and .Join()
//
// public class UnitSelectionController : MonoBehaviour
// {
//     [Header("Selection Settings")]
//     public Material selectedMaterial;  // Material to show when unit/vehicle is selected
//     public Material normalMaterial;    // Normal unit/vehicle material
//     
//     [Header("Selection Indicator")]
//     public GameObject selectionIndicatorPrefab; // Optional: a ring/circle to show around selected unit/vehicle
//
//     [Header("Raycast Settings")]
//     [Tooltip("Layers that the mouse raycast should interact with (e.g., Units, Vehicles, and HexagonColliders).")]
//     public LayerMask clickableLayers; // LayerMask for raycasting
//
//     private UnitController currentSelectedUnit;
//     private VehicleController currentSelectedVehicle; // New: To track selected vehicle
//
//     private UnitPathFinder unitPathfinder; // Renamed for clarity
//     private VehiclePathFinderHybrid vehiclePathfinder; // MODIFIED: Reference to VehiclePathFinderCatmullRom
//     private HexagonController hexController;
//     private Camera playerCamera;
//     
//     // Store original materials for each selectable object
//     private Dictionary<MonoBehaviour, Material> originalMaterials = new Dictionary<MonoBehaviour, Material>(); // Changed to MonoBehaviour
//
//     // New: Cache the last hovered hexagon's grid position to prevent redundant checks
//     private Vector2Int lastHoveredHexGridPos = new Vector2Int(-1, -1); // Initialize with an invalid position
//
//     void Start()
//     {
//         playerCamera = Camera.main;
//         unitPathfinder = FindObjectOfType<UnitPathFinder>();
//         vehiclePathfinder = FindObjectOfType<VehiclePathFinderHybrid>(); // MODIFIED: Find the new vehicle pathfinder
//         hexController = FindObjectOfType<HexagonController>();
//         
//         if (unitPathfinder == null)
//         {
//             Debug.LogError("UnitSelectionManager: No HexPathfinder found in scene! Ensure it's on an active GameObject.");
//         }
//         if (vehiclePathfinder == null) // Check for vehicle pathfinder
//         {
//             Debug.LogError("UnitSelectionManager: No VehiclePathFinderHybrid found in scene! Ensure it's on an active GameObject."); // MODIFIED: Error message
//         }
//         if (hexController == null)
//         {
//             Debug.LogError("UnitSelectionManager: No HexagonController found in scene! Ensure it's on an active GameObject.");
//         }
//
//         // Initialize clickableLayers if it's not set in the Inspector (e.g., for safety)
//         if (clickableLayers.value == 0)
//         {
//             if (hexController != null && hexController.colliderLayer.value != 0)
//             {
//                 clickableLayers = hexController.colliderLayer;
//                 Debug.LogWarning("clickableLayers was not set. Defaulting to HexagonController's colliderLayer. Please also add Unit and Vehicle layers.");
//             }
//             else
//             {
//                 clickableLayers = LayerMask.GetMask("Default");
//                 Debug.LogWarning("clickableLayers not set and HexagonController's colliderLayer not found. Using 'Default' layer. Please configure in Inspector (Units, Vehicles, HexagonColliders).");
//             }
//         }
//     }
//     
//     void Update()
//     {
//         if (Input.GetMouseButtonDown(0)) // Left mouse click
//         {
//             HandleMouseClick();
//         }
//
//         // New: Handle mouse hover for selected vehicle reachability prediction
//         HandleMouseHover();
//     }
//     
//     void HandleMouseClick()
//     {
//         Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
//         RaycastHit hit;
//         
//         if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableLayers))
//         {
//             Debug.Log($"Raycast hit: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)} at {hit.point}");
//
//             UnitController clickedUnit = hit.collider.GetComponent<UnitController>();
//             VehicleController clickedVehicle = hit.collider.GetComponent<VehicleController>(); // Check for VehicleController
//
//             if (clickedUnit != null && clickedUnit.CanBeSelected)
//             {
//                 SelectObject(clickedUnit); // Select the unit
//             }
//             else if (clickedVehicle != null && clickedVehicle.CanBeSelected) // If a vehicle was clicked
//             {
//                 SelectObject(clickedVehicle); // Select the vehicle
//             }
//             else if (currentSelectedUnit != null && currentSelectedUnit.CanMove)
//             {
//                 // Unit is selected and clicked on terrain
//                 MoveSelectedUnitToPosition(hit.point);
//             }
//             else if (currentSelectedVehicle != null && currentSelectedVehicle.CanMove)
//             {
//                 // Vehicle is selected and clicked on terrain
//                 MoveSelectedVehicleToPosition(hit.point);
//             }
//             else
//             {
//                 // Clicked on something else, or clicked on a non-selectable object, deselect if anything is currently selected
//                 DeselectAll();
//             }
//         }
//         else // If raycast didn't hit anything
//         {
//             Debug.Log("Raycast did not hit anything on clickable layers.");
//             DeselectAll(); // Deselect if nothing was hit
//         }
//     }
//
//     /// <summary>
//     /// Handles mouse hovering to provide feedback on vehicle reachability.
//     /// </summary>
//     void HandleMouseHover()
//     {
//         // Only proceed if a vehicle is currently selected and we have a pathfinder
//         if (currentSelectedVehicle == null || vehiclePathfinder == null || hexController == null)
//         {
//             // If no vehicle is selected, reset the last hovered hex so it prints next time a vehicle is selected
//             if (lastHoveredHexGridPos != new Vector2Int(-1, -1))
//             {
//                 lastHoveredHexGridPos = new Vector2Int(-1, -1);
//             }
//             return;
//         }
//
//         Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
//         RaycastHit hit;
//         
//         // Raycast only against the HexagonCollider layer for hovering feedback
//         // Assuming HexagonController.colliderLayer is the layer for hexagons
//         if (Physics.Raycast(ray, out hit, Mathf.Infinity, hexController.colliderLayer))
//         {
//             Vector2Int hoveredGridPos = hexController.WorldToGridPosition(hit.point);
//             
//             // Only proceed if the hovered hexagon is different from the last one
//             if (hoveredGridPos == lastHoveredHexGridPos)
//             {
//                 return; // Same hexagon, no need to re-evaluate
//             }
//
//             // Update the cached hovered hexagon
//             lastHoveredHexGridPos = hoveredGridPos;
//
//             // Get the world position of the center of the hovered hex, adjusted for height
//             Vector3 hoveredWorldPos = hexController.GetHexWorldPosition(hoveredGridPos.x, hoveredGridPos.y);
//             hoveredWorldPos.y += hexController.hexMeshHeight * 0.5f + vehiclePathfinder.heightOffset; // Match vehicle height
//
//             // Check if a path exists to this hex first (A* based)
//             if (!vehiclePathfinder.PathExists(currentSelectedVehicle.transform.position, hoveredWorldPos))
//             {
//                 Debug.Log($"Hovered Hex ({hoveredGridPos}): Unreachable (No A* Path)");
//                 return;
//             }
//
//             // Get the predicted movement type using the VehiclePathFinderHybrid's method
//             VehiclePathFinderHybrid.MovementType moveType = vehiclePathfinder.GetMovementType(
//                 currentSelectedVehicle.transform.position,
//                 hoveredWorldPos,
//                 currentSelectedVehicle.transform.forward,
//                 vehiclePathfinder.movementSpeed
//             );
//
//             // Print the result to the console
//             Debug.Log($"Hovered Hex ({hoveredGridPos}): Movement Type - {moveType}");
//         }
//         else // If raycast didn't hit any hexagon
//         {
//             // Reset the last hovered hex if the mouse is no longer over a hexagon
//             if (lastHoveredHexGridPos != new Vector2Int(-1, -1))
//             {
//                 lastHoveredHexGridPos = new Vector2Int(-1, -1);
//                 Debug.Log("Mouse no longer hovering over a hexagon."); // Optional: indicate no hex is hovered
//             }
//         }
//     }
//     
//     // Generic method to select either a Unit or a Vehicle
//     public void SelectObject(MonoBehaviour selectableObject)
//     {
//         // Deselect current unit/vehicle if a different one is being selected
//         if (currentSelectedUnit != null && currentSelectedUnit != selectableObject)
//         {
//             DeselectCurrentUnit();
//         }
//         if (currentSelectedVehicle != null && currentSelectedVehicle != selectableObject)
//         {
//             DeselectCurrentVehicle();
//         }
//
//         if (selectableObject is UnitController unit && unit.CanBeSelected)
//         {
//             if (currentSelectedUnit != unit) // Only re-select if it's a different unit
//             {
//                 currentSelectedUnit = unit;
//                 currentSelectedVehicle = null; // Ensure only one type is selected at a time
//                 ApplySelectionVisual(unit);
//                 unit.OnSelected();
//                 Debug.Log($"Selected unit: {unit.name}");
//             }
//         }
//         else if (selectableObject is VehicleController vehicle && vehicle.CanBeSelected)
//         {
//             if (currentSelectedVehicle != vehicle) // Only re-select if it's a different vehicle
//             {
//                 currentSelectedVehicle = vehicle;
//                 currentSelectedUnit = null; // Ensure only one type is selected at a time
//                 ApplySelectionVisual(vehicle);
//                 vehicle.OnSelected();
//                 Debug.Log($"Selected vehicle: {vehicle.name}");
//             }
//         }
//     }
//
//     void DeselectCurrentUnit()
//     {
//         if (currentSelectedUnit != null)
//         {
//             RemoveSelectionVisual(currentSelectedUnit);
//             currentSelectedUnit.OnDeselected();
//             currentSelectedUnit = null;
//             Debug.Log("Unit deselected.");
//         }
//     }
//
//     void DeselectCurrentVehicle()
//     {
//         if (currentSelectedVehicle != null)
//         {
//             RemoveSelectionVisual(currentSelectedVehicle);
//             currentSelectedVehicle.OnDeselected();
//             currentSelectedVehicle = null;
//             Debug.Log("Vehicle deselected.");
//         }
//     }
//     
//     // Generic method to apply selection visual
//     void ApplySelectionVisual(MonoBehaviour selectableObject)
//     {
//         Renderer renderer = selectableObject.GetComponent<Renderer>();
//         if (renderer != null)
//         {
//             // Store original material if not already stored
//             if (!originalMaterials.ContainsKey(selectableObject))
//             {
//                 originalMaterials[selectableObject] = renderer.material;
//             }
//             // Apply selected material
//             if (selectedMaterial != null)
//             {
//                 renderer.material = selectedMaterial;
//             }
//         }
//         
//         if (selectionIndicatorPrefab != null)
//         {
//             GameObject indicator = Instantiate(selectionIndicatorPrefab, selectableObject.transform);
//             indicator.name = "SelectionIndicator";
//             indicator.transform.localPosition = Vector3.zero + Vector3.up * 0.1f;
//         }
//     }
//     
//     // Generic method to remove selection visual
//     void RemoveSelectionVisual(MonoBehaviour selectableObject)
//     {
//         Renderer renderer = selectableObject.GetComponent<Renderer>();
//         if (renderer != null && originalMaterials.ContainsKey(selectableObject))
//         {
//             renderer.material = originalMaterials[selectableObject];
//             originalMaterials.Remove(selectableObject);
//         }
//         
//         Transform indicator = selectableObject.transform.Find("SelectionIndicator");
//         if (indicator != null)
//         {
//             Destroy(indicator.gameObject);
//         }
//     }
//     
//     void MoveSelectedUnitToPosition(Vector3 worldPosition)
//     {
//         if (currentSelectedUnit == null || unitPathfinder == null) return;
//         
//         // Now FindPath for units returns List<HexagonPathfinder.UnitPathStep>
//         List<UnitPathFinder.UnitPathStep> pathSteps = unitPathfinder.FindPath(currentSelectedUnit.transform.position, worldPosition);
//         
//         if (pathSteps.Count > 0)
//         {
//             // Pass the new pathSteps to MoveAlongPath
//             unitPathfinder.MoveAlongPath(currentSelectedUnit.gameObject, pathSteps, () => {
//                 Debug.Log($"{currentSelectedUnit.name} reached destination!");
//                 currentSelectedUnit.OnMovementComplete();
//             });
//             currentSelectedUnit.OnMovementStarted();
//         }
//         else
//         {
//             Debug.Log("No path found for unit to clicked position!");
//         }
//     }
//
//     void MoveSelectedVehicleToPosition(Vector3 targetPosition)
//     {
//         if (currentSelectedVehicle == null || vehiclePathfinder == null) return;
//
//         Vector3 vehicleWorldPosition = currentSelectedVehicle.transform.position;
//         Vector2Int currentVehicleGridPos = hexController.WorldToGridPosition(vehicleWorldPosition);
//
//         // Check if movement is possible and get movement data
//         VehiclePathFinderHybrid.VehicleMovementData movementData =
//             vehiclePathfinder.CalculateMovement(vehicleWorldPosition, targetPosition);
//
//         if (movementData.pathExists)
//         {
//             // Move directly to destination
//             vehiclePathfinder.MoveToDestination(currentSelectedVehicle, movementData,
//                 () => { Debug.Log("Vehicle reached destination!"); });
//         }
//     }
//
//     // Public method to deselect all (useful for other scripts)
//     public void DeselectAll()
//     {
//         DeselectCurrentUnit();
//         DeselectCurrentVehicle();
//     }
// }






// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq; // Added for .Select() and .Join()
//
// public class UnitSelectionController : MonoBehaviour
// {
//     [Header("Selection Settings")]
//     public Material selectedMaterial;  // Material to show when unit/vehicle is selected
//     public Material normalMaterial;    // Normal unit/vehicle material
//     
//     [Header("Selection Indicator")]
//     public GameObject selectionIndicatorPrefab; // Optional: a ring/circle to show around selected unit/vehicle
//
//     [Header("Raycast Settings")]
//     [Tooltip("Layers that the mouse raycast should interact with (e.g., Units, Vehicles, and HexagonColliders).")]
//     public LayerMask clickableLayers; // LayerMask for raycasting
//
//     private UnitController currentSelectedUnit;
//     private VehicleController currentSelectedVehicle; // New: To track selected vehicle
//
//     private UnitPathFinder unitPathfinder; // Renamed for clarity
//     private VehiclePathFinderHybrid vehiclePathfinder; // MODIFIED: Reference to VehiclePathFinderCatmullRom
//     private HexagonController hexController;
//     private Camera playerCamera;
//     
//     // Store original materials for each selectable object
//     private Dictionary<MonoBehaviour, Material> originalMaterials = new Dictionary<MonoBehaviour, Material>(); // Changed to MonoBehaviour
//
//     void Start()
//     {
//         playerCamera = Camera.main;
//         unitPathfinder = FindObjectOfType<UnitPathFinder>();
//         vehiclePathfinder = FindObjectOfType<VehiclePathFinderHybrid>(); // MODIFIED: Find the new vehicle pathfinder
//         hexController = FindObjectOfType<HexagonController>();
//         
//         if (unitPathfinder == null)
//         {
//             Debug.LogError("UnitSelectionManager: No HexPathfinder found in scene! Ensure it's on an active GameObject.");
//         }
//         if (vehiclePathfinder == null) // Check for vehicle pathfinder
//         {
//             Debug.LogError("UnitSelectionManager: No VehiclePathFinderHybrid found in scene! Ensure it's on an active GameObject."); // MODIFIED: Error message
//         }
//         if (hexController == null)
//         {
//             Debug.LogError("UnitSelectionManager: No HexagonController found in scene! Ensure it's on an active GameObject.");
//         }
//
//         // Initialize clickableLayers if it's not set in the Inspector (e.g., for safety)
//         if (clickableLayers.value == 0)
//         {
//             if (hexController != null && hexController.colliderLayer.value != 0)
//             {
//                 clickableLayers = hexController.colliderLayer;
//                 Debug.LogWarning("clickableLayers was not set. Defaulting to HexagonController's colliderLayer. Please also add Unit and Vehicle layers.");
//             }
//             else
//             {
//                 clickableLayers = LayerMask.GetMask("Default");
//                 Debug.LogWarning("clickableLayers not set and HexagonController's colliderLayer not found. Using 'Default' layer. Please configure in Inspector (Units, Vehicles, HexagonColliders).");
//             }
//         }
//     }
//     
//     void Update()
//     {
//         if (Input.GetMouseButtonDown(0)) // Left mouse click
//         {
//             HandleMouseClick();
//         }
//
//         // New: Handle mouse hover for selected vehicle reachability prediction
//         HandleMouseHover();
//     }
//     
//     void HandleMouseClick()
//     {
//         Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
//         RaycastHit hit;
//         
//         if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableLayers))
//         {
//             Debug.Log($"Raycast hit: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)} at {hit.point}");
//
//             UnitController clickedUnit = hit.collider.GetComponent<UnitController>();
//             VehicleController clickedVehicle = hit.collider.GetComponent<VehicleController>(); // Check for VehicleController
//
//             if (clickedUnit != null && clickedUnit.CanBeSelected)
//             {
//                 SelectObject(clickedUnit); // Select the unit
//             }
//             else if (clickedVehicle != null && clickedVehicle.CanBeSelected) // If a vehicle was clicked
//             {
//                 SelectObject(clickedVehicle); // Select the vehicle
//             }
//             else if (currentSelectedUnit != null && currentSelectedUnit.CanMove)
//             {
//                 // Unit is selected and clicked on terrain
//                 MoveSelectedUnitToPosition(hit.point);
//             }
//             else if (currentSelectedVehicle != null && currentSelectedVehicle.CanMove)
//             {
//                 // Vehicle is selected and clicked on terrain
//                 MoveSelectedVehicleToPosition(hit.point);
//             }
//             else
//             {
//                 // Clicked on something else, or clicked on a non-selectable object, deselect if anything is currently selected
//                 DeselectAll();
//             }
//         }
//         else // If raycast didn't hit anything
//         {
//             Debug.Log("Raycast did not hit anything on clickable layers.");
//             DeselectAll(); // Deselect if nothing was hit
//         }
//     }
//
//     /// <summary>
//     /// Handles mouse hovering to provide feedback on vehicle reachability.
//     /// </summary>
//     void HandleMouseHover()
//     {
//         // Only proceed if a vehicle is currently selected and we have a pathfinder
//         if (currentSelectedVehicle == null || vehiclePathfinder == null || hexController == null)
//         {
//             return;
//         }
//
//         Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
//         RaycastHit hit;
//         
//         // Raycast only against the HexagonCollider layer for hovering feedback
//         // Assuming HexagonController.colliderLayer is the layer for hexagons
//         if (Physics.Raycast(ray, out hit, Mathf.Infinity, hexController.colliderLayer))
//         {
//             Vector2Int hoveredGridPos = hexController.WorldToGridPosition(hit.point);
//             
//             // Get the world position of the center of the hovered hex, adjusted for height
//             Vector3 hoveredWorldPos = hexController.GetHexWorldPosition(hoveredGridPos.x, hoveredGridPos.y);
//             hoveredWorldPos.y += hexController.hexMeshHeight * 0.5f + vehiclePathfinder.heightOffset; // Match vehicle height
//
//             // Check if a path exists to this hex first (A* based)
//             if (!vehiclePathfinder.PathExists(currentSelectedVehicle.transform.position, hoveredWorldPos))
//             {
//                 Debug.Log($"Hovered Hex ({hoveredGridPos}): Unreachable (No A* Path)");
//                 return;
//             }
//
//             // Get the predicted movement type using the VehiclePathFinderHybrid's method
//             VehiclePathFinderHybrid.MovementType moveType = vehiclePathfinder.GetMovementType(
//                 currentSelectedVehicle.transform.position,
//                 hoveredWorldPos,
//                 currentSelectedVehicle.transform.forward,
//                 vehiclePathfinder.movementSpeed
//             );
//
//             // Print the result to the console
//             Debug.Log($"Hovered Hex ({hoveredGridPos}): Movement Type - {moveType}");
//         }
//     }
//     
//     // Generic method to select either a Unit or a Vehicle
//     public void SelectObject(MonoBehaviour selectableObject)
//     {
//         // Deselect current unit/vehicle if a different one is being selected
//         if (currentSelectedUnit != null && currentSelectedUnit != selectableObject)
//         {
//             DeselectCurrentUnit();
//         }
//         if (currentSelectedVehicle != null && currentSelectedVehicle != selectableObject)
//         {
//             DeselectCurrentVehicle();
//         }
//
//         if (selectableObject is UnitController unit && unit.CanBeSelected)
//         {
//             if (currentSelectedUnit != unit) // Only re-select if it's a different unit
//             {
//                 currentSelectedUnit = unit;
//                 currentSelectedVehicle = null; // Ensure only one type is selected at a time
//                 ApplySelectionVisual(unit);
//                 unit.OnSelected();
//                 Debug.Log($"Selected unit: {unit.name}");
//             }
//         }
//         else if (selectableObject is VehicleController vehicle && vehicle.CanBeSelected)
//         {
//             if (currentSelectedVehicle != vehicle) // Only re-select if it's a different vehicle
//             {
//                 currentSelectedVehicle = vehicle;
//                 currentSelectedUnit = null; // Ensure only one type is selected at a time
//                 ApplySelectionVisual(vehicle);
//                 vehicle.OnSelected();
//                 Debug.Log($"Selected vehicle: {vehicle.name}");
//             }
//         }
//     }
//
//     void DeselectCurrentUnit()
//     {
//         if (currentSelectedUnit != null)
//         {
//             RemoveSelectionVisual(currentSelectedUnit);
//             currentSelectedUnit.OnDeselected();
//             currentSelectedUnit = null;
//             Debug.Log("Unit deselected.");
//         }
//     }
//
//     void DeselectCurrentVehicle()
//     {
//         if (currentSelectedVehicle != null)
//         {
//             RemoveSelectionVisual(currentSelectedVehicle);
//             currentSelectedVehicle.OnDeselected();
//             currentSelectedVehicle = null;
//             Debug.Log("Vehicle deselected.");
//         }
//     }
//     
//     // Generic method to apply selection visual
//     void ApplySelectionVisual(MonoBehaviour selectableObject)
//     {
//         Renderer renderer = selectableObject.GetComponent<Renderer>();
//         if (renderer != null)
//         {
//             // Store original material if not already stored
//             if (!originalMaterials.ContainsKey(selectableObject))
//             {
//                 originalMaterials[selectableObject] = renderer.material;
//             }
//             // Apply selected material
//             if (selectedMaterial != null)
//             {
//                 renderer.material = selectedMaterial;
//             }
//         }
//         
//         if (selectionIndicatorPrefab != null)
//         {
//             GameObject indicator = Instantiate(selectionIndicatorPrefab, selectableObject.transform);
//             indicator.name = "SelectionIndicator";
//             indicator.transform.localPosition = Vector3.zero + Vector3.up * 0.1f;
//         }
//     }
//     
//     // Generic method to remove selection visual
//     void RemoveSelectionVisual(MonoBehaviour selectableObject)
//     {
//         Renderer renderer = selectableObject.GetComponent<Renderer>();
//         if (renderer != null && originalMaterials.ContainsKey(selectableObject))
//         {
//             renderer.material = originalMaterials[selectableObject];
//             originalMaterials.Remove(selectableObject);
//         }
//         
//         Transform indicator = selectableObject.transform.Find("SelectionIndicator");
//         if (indicator != null)
//         {
//             Destroy(indicator.gameObject);
//         }
//     }
//     
//     void MoveSelectedUnitToPosition(Vector3 worldPosition)
//     {
//         if (currentSelectedUnit == null || unitPathfinder == null) return;
//         
//         // Now FindPath for units returns List<HexagonPathfinder.UnitPathStep>
//         List<UnitPathFinder.UnitPathStep> pathSteps = unitPathfinder.FindPath(currentSelectedUnit.transform.position, worldPosition);
//         
//         if (pathSteps.Count > 0)
//         {
//             // Pass the new pathSteps to MoveAlongPath
//             unitPathfinder.MoveAlongPath(currentSelectedUnit.gameObject, pathSteps, () => {
//                 Debug.Log($"{currentSelectedUnit.name} reached destination!");
//                 currentSelectedUnit.OnMovementComplete();
//             });
//             currentSelectedUnit.OnMovementStarted();
//         }
//         else
//         {
//             Debug.Log("No path found for unit to clicked position!");
//         }
//     }
//
//     void MoveSelectedVehicleToPosition(Vector3 targetPosition)
//     {
//         if (currentSelectedVehicle == null || vehiclePathfinder == null) return;
//
//         Vector3 vehicleWorldPosition = currentSelectedVehicle.transform.position;
//         Vector2Int currentVehicleGridPos = hexController.WorldToGridPosition(vehicleWorldPosition);
//
//         // Check if movement is possible and get movement data
//         VehiclePathFinderHybrid.VehicleMovementData movementData =
//             vehiclePathfinder.CalculateMovement(vehicleWorldPosition, targetPosition);
//
//         if (movementData.pathExists)
//         {
//             // Move directly to destination
//             vehiclePathfinder.MoveToDestination(currentSelectedVehicle, movementData,
//                 () => { Debug.Log("Vehicle reached destination!"); });
//         }
//     }
//
//     // Public method to deselect all (useful for other scripts)
//     public void DeselectAll()
//     {
//         DeselectCurrentUnit();
//         DeselectCurrentVehicle();
//     }
// }




//
// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq; // Added for .Select() and .Join()
//
// public class UnitSelectionController : MonoBehaviour
// {
//     [Header("Selection Settings")]
//     public Material selectedMaterial;  // Material to show when unit/vehicle is selected
//     public Material normalMaterial;    // Normal unit/vehicle material
//     
//     [Header("Selection Indicator")]
//     public GameObject selectionIndicatorPrefab; // Optional: a ring/circle to show around selected unit/vehicle
//
//     [Header("Raycast Settings")]
//     [Tooltip("Layers that the mouse raycast should interact with (e.g., Units, Vehicles, and HexagonColliders).")]
//     public LayerMask clickableLayers; // LayerMask for raycasting
//
//     private UnitController currentSelectedUnit;
//     private VehicleController currentSelectedVehicle; // New: To track selected vehicle
//
//     private UnitPathFinder unitPathfinder; // Renamed for clarity
//     private VehiclePathFinderHybrid vehiclePathfinder; // MODIFIED: Reference to VehiclePathFinderCatmullRom
//     private HexagonController hexController;
//     private Camera playerCamera;
//     
//     // Store original materials for each selectable object
//     private Dictionary<MonoBehaviour, Material> originalMaterials = new Dictionary<MonoBehaviour, Material>(); // Changed to MonoBehaviour
//
//     void Start()
//     {
//         playerCamera = Camera.main;
//         unitPathfinder = FindObjectOfType<UnitPathFinder>();
//         vehiclePathfinder = FindObjectOfType<VehiclePathFinderHybrid>(); // MODIFIED: Find the new vehicle pathfinder
//         hexController = FindObjectOfType<HexagonController>();
//         
//         if (unitPathfinder == null)
//         {
//             Debug.LogError("UnitSelectionManager: No HexPathfinder found in scene! Ensure it's on an active GameObject.");
//         }
//         if (vehiclePathfinder == null) // Check for vehicle pathfinder
//         {
//             Debug.LogError("UnitSelectionManager: No VehiclePathFinderCatmullRom found in scene! Ensure it's on an active GameObject."); // MODIFIED: Error message
//         }
//         if (hexController == null)
//         {
//             Debug.LogError("UnitSelectionManager: No HexagonController found in scene! Ensure it's on an active GameObject.");
//         }
//
//         // Initialize clickableLayers if it's not set in the Inspector (e.g., for safety)
//         if (clickableLayers.value == 0)
//         {
//             if (hexController != null && hexController.colliderLayer.value != 0)
//             {
//                 clickableLayers = hexController.colliderLayer;
//                 Debug.LogWarning("clickableLayers was not set. Defaulting to HexagonController's colliderLayer. Please also add Unit and Vehicle layers.");
//             }
//             else
//             {
//                 clickableLayers = LayerMask.GetMask("Default");
//                 Debug.LogWarning("clickableLayers not set and HexagonController's colliderLayer not found. Using 'Default' layer. Please configure in Inspector (Units, Vehicles, HexagonColliders).");
//             }
//         }
//     }
//     
//     void Update()
//     {
//         if (Input.GetMouseButtonDown(0)) // Left mouse click
//         {
//             HandleMouseClick();
//         }
//     }
//     
//     void HandleMouseClick()
//     {
//         Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
//         RaycastHit hit;
//         Debug.Log($"fuck 0");
//         
//         if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableLayers))
//         {
//             Debug.Log($"Raycast hit: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)} at {hit.point}");
//
//             UnitController clickedUnit = hit.collider.GetComponent<UnitController>();
//             VehicleController clickedVehicle = hit.collider.GetComponent<VehicleController>(); // Check for VehicleController
//
//             Debug.Log($"fuck a");
//             if (clickedUnit != null && clickedUnit.CanBeSelected)
//             {
//                 SelectObject(clickedUnit); // Select the unit
//             }
//             else if (clickedVehicle != null && clickedVehicle.CanBeSelected) // If a vehicle was clicked
//             {
//                 Debug.Log($"fuck b");
//                 SelectObject(clickedVehicle); // Select the vehicle
//             }
//             else if (currentSelectedUnit != null && currentSelectedUnit.CanMove)
//             {
//                 // Unit is selected and clicked on terrain
//                 MoveSelectedUnitToPosition(hit.point);
//             }
//             else if (currentSelectedVehicle != null && currentSelectedVehicle.CanMove)
//             {
//                 // Vehicle is selected and clicked on terrain
//                 MoveSelectedVehicleToPosition(hit.point);
//             }
//             else
//             {
//                 // Clicked on something else, or clicked on a non-selectable object, deselect if anything is currently selected
//                 DeselectAll();
//             }
//         }
//         else // If raycast didn't hit anything
//         {
//             Debug.Log("Raycast did not hit anything on clickable layers.");
//             DeselectAll(); // Deselect if nothing was hit
//         }
//     }
//     
//     // Generic method to select either a Unit or a Vehicle
//     public void SelectObject(MonoBehaviour selectableObject)
//     {
//         Debug.Log($"fuck 1");
//         
//         // Deselect current unit/vehicle if a different one is being selected
//         if (currentSelectedUnit != null && currentSelectedUnit != selectableObject)
//         {
//             DeselectCurrentUnit();
//         }
//         if (currentSelectedVehicle != null && currentSelectedVehicle != selectableObject)
//         {
//             DeselectCurrentVehicle();
//         }
//
//         if (selectableObject is UnitController unit && unit.CanBeSelected)
//         {
//             if (currentSelectedUnit != unit) // Only re-select if it's a different unit
//             {
//                 currentSelectedUnit = unit;
//                 currentSelectedVehicle = null; // Ensure only one type is selected at a time
//                 ApplySelectionVisual(unit);
//                 unit.OnSelected();
//                 Debug.Log($"Selected unit: {unit.name}");
//             }
//         }
//         else if (selectableObject is VehicleController vehicle && vehicle.CanBeSelected)
//         {
//             Debug.Log($"fuck 2");
//             
//             if (currentSelectedVehicle != vehicle) // Only re-select if it's a different vehicle
//             {
//                 currentSelectedVehicle = vehicle;
//                 currentSelectedUnit = null; // Ensure only one type is selected at a time
//                 ApplySelectionVisual(vehicle);
//                 vehicle.OnSelected();
//                 Debug.Log($"Selected vehicle: {vehicle.name}");
//             }
//         }
//     }
//
//     void DeselectCurrentUnit()
//     {
//         if (currentSelectedUnit != null)
//         {
//             RemoveSelectionVisual(currentSelectedUnit);
//             currentSelectedUnit.OnDeselected();
//             currentSelectedUnit = null;
//             Debug.Log("Unit deselected.");
//         }
//     }
//
//     void DeselectCurrentVehicle()
//     {
//         if (currentSelectedVehicle != null)
//         {
//             RemoveSelectionVisual(currentSelectedVehicle);
//             currentSelectedVehicle.OnDeselected();
//             currentSelectedVehicle = null;
//             Debug.Log("Vehicle deselected.");
//         }
//     }
//     
//     // Generic method to apply selection visual
//     void ApplySelectionVisual(MonoBehaviour selectableObject)
//     {
//         Renderer renderer = selectableObject.GetComponent<Renderer>();
//         if (renderer != null)
//         {
//             // Store original material if not already stored
//             if (!originalMaterials.ContainsKey(selectableObject))
//             {
//                 originalMaterials[selectableObject] = renderer.material;
//             }
//             // Apply selected material
//             if (selectedMaterial != null)
//             {
//                 renderer.material = selectedMaterial;
//             }
//         }
//         
//         if (selectionIndicatorPrefab != null)
//         {
//             GameObject indicator = Instantiate(selectionIndicatorPrefab, selectableObject.transform);
//             indicator.name = "SelectionIndicator";
//             indicator.transform.localPosition = Vector3.zero + Vector3.up * 0.1f;
//         }
//     }
//     
//     // Generic method to remove selection visual
//     void RemoveSelectionVisual(MonoBehaviour selectableObject)
//     {
//         Renderer renderer = selectableObject.GetComponent<Renderer>();
//         if (renderer != null && originalMaterials.ContainsKey(selectableObject))
//         {
//             renderer.material = originalMaterials[selectableObject];
//             originalMaterials.Remove(selectableObject);
//         }
//         
//         Transform indicator = selectableObject.transform.Find("SelectionIndicator");
//         if (indicator != null)
//         {
//             Destroy(indicator.gameObject);
//         }
//     }
//     
//     void MoveSelectedUnitToPosition(Vector3 worldPosition)
//     {
//         if (currentSelectedUnit == null || unitPathfinder == null) return;
//         
//         // Now FindPath for units returns List<HexagonPathfinder.UnitPathStep>
//         List<UnitPathFinder.UnitPathStep> pathSteps = unitPathfinder.FindPath(currentSelectedUnit.transform.position, worldPosition);
//         
//         if (pathSteps.Count > 0)
//         {
//             // Pass the new pathSteps to MoveAlongPath
//             unitPathfinder.MoveAlongPath(currentSelectedUnit.gameObject, pathSteps, () => {
//                 Debug.Log($"{currentSelectedUnit.name} reached destination!");
//                 currentSelectedUnit.OnMovementComplete();
//             });
//             currentSelectedUnit.OnMovementStarted();
//         }
//         else
//         {
//             Debug.Log("No path found for unit to clicked position!");
//         }
//     }
//
//     void MoveSelectedVehicleToPosition(Vector3 targetPosition)
//     {
//         if (currentSelectedVehicle == null || vehiclePathfinder == null) return;
//
//         Vector3 vehicleWorldPosition = currentSelectedVehicle.transform.position;
//         Vector2Int currentVehicleGridPos = hexController.WorldToGridPosition(vehicleWorldPosition);
//
// // Check if movement is possible and get movement data
//         VehiclePathFinderHybrid.VehicleMovementData movementData =
//             vehiclePathfinder.CalculateMovement(vehicleWorldPosition, targetPosition);
//
//         if (movementData.pathExists)
//         {
//             // Move directly to destination
//             vehiclePathfinder.MoveToDestination(currentSelectedVehicle, movementData,
//                 () => { Debug.Log("Vehicle reached destination!"); });
//         }
//     }
//
//     // Public method to deselect all (useful for other scripts)
//     public void DeselectAll()
//     {
//         DeselectCurrentUnit();
//         DeselectCurrentVehicle();
//     }
// }
