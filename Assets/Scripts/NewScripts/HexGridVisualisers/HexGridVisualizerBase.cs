using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic; // Required for List and Dictionary

// Phase 2.1 (Revised for GameObject Hexes and Visual Control): HexGridVisualizer Class
// Purpose: Instantiates visual hexagon GameObjects for a SimpleHexGrid.
// Now also manages references to individual HexVisualTiles for color manipulation.
public abstract class HexGridVisualizerBase : MonoBehaviour
{
    public GameObject HexagonsContainer;

    [Tooltip("The SimpleHexGrid data source this visualizer will represent.")]
    protected SimpleHexGridBase _targetGridBase;

    [Tooltip("The desired vertical scale (thickness) of the visual hexagon meshes.")]
    public float hexVisualHeight = 0.1f; // New parameter for controlling thickness

    protected virtual void Awake()
    {
        _targetGridBase = GetComponent<SimpleHexGridBase>();
        if (_targetGridBase != null)
        {
            _targetGridBase.OnGridReady += GenerateVisualGrid;
        }
    }

    /// <summary>
    /// Instantiates visual hex GameObjects for the target SimpleHexGrid.
    /// Now initializes and stores HexVisualTile components.
    /// </summary>
    public abstract void GenerateVisualGrid(SimpleHexGridBase hexGridBase);
    
    
    void OnDestroy()
    {
        _targetGridBase.OnGridReady -= GenerateVisualGrid;

    }
}