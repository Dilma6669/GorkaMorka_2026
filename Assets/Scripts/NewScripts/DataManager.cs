using System;
using System.Collections.Generic;
using UnityEngine;

public class DataManager
{
    // The central registry of all entity data in the game
    public static Dictionary<string, EntityData> globalDataRegistry = new Dictionary<string, EntityData>();

    public static string RegisterData(EntityData data)
    {
        string newGUID = data.entityName + "_Data_" + Guid.NewGuid();
        Debug.Log($"Adding Entity data to globalDataRegistry: {newGUID}");
        data.entityGUID = newGUID;
        globalDataRegistry[newGUID] = data;
        return newGUID;
    }

    public static void UpdateData(string guid, EntityData data)
    {
        globalDataRegistry[guid] = data;
    }

    public static void UnregisterData(string guid)
    {
        if (globalDataRegistry.ContainsKey(guid))
        {
            globalDataRegistry.Remove(guid);
        }
    }

    // This handles the generic retrieval. 
    // You can call this and then check the type using 'is' or 'as'.
    public static bool TryGetData<T>(string guid, out T data) where T : EntityData
    {
        if (globalDataRegistry.TryGetValue(guid, out EntityData baseData))
        {
            data = baseData as T;
            return data != null;
        }
        data = null;
        return false;
    }
}