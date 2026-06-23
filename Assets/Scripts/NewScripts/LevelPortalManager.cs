using System.Collections.Generic;
using UnityEngine;

public class LevelPortalManager : MonoBehaviour
{
    // Key = Parent Grid + Hex Coords
    // Value = The specific settings or Seed for that child grid
    private Dictionary<string, int> portalSeeds = new Dictionary<string, int>();

    public void RegisterPortal(SimpleHexGridBase grid, Vector2Int coords, int seed)
    {
        string key = $"{grid.name}_{coords.x}_{coords.y}";
        portalSeeds[key] = seed;
    }

    public int GetSeedForPortal(SimpleHexGridBase grid, Vector2Int coords)
    {
        string key = $"{grid.name}_{coords.x}_{coords.y}";
        return portalSeeds.ContainsKey(key) ? portalSeeds[key] : Random.Range(0, 999999);
    }
}