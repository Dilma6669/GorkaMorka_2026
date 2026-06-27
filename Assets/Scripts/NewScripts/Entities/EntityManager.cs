using System;
using System.Collections.Generic;
using UnityEngine;


public class EntityManager
{
    private static Dictionary<string, Entity> globalEntitiesRegistry = new Dictionary<string, Entity>();
    

    public static void RegisterEntity(string dataGUID, Entity entity)
    {
        globalEntitiesRegistry[dataGUID] = entity;
    }

    public static void UnregisterEntity(string entityGUID)
    {
        if (globalEntitiesRegistry.ContainsKey(entityGUID))
        {
            globalEntitiesRegistry.Remove(entityGUID);
        }
    }

    // 3. Proper Dictionary Look-up
    public static bool TryGetEntity(string entityGUID, out Entity entity)
    {
        return globalEntitiesRegistry.TryGetValue(entityGUID, out entity);
    }

    // Accessor to check existence
    public static bool ContainsEntity(string entityGUID)
    {
        return globalEntitiesRegistry.ContainsKey(entityGUID);
    }
    
    public static Dictionary<string, Entity> GetAllEntities()
    {
        return globalEntitiesRegistry;
    }

    public static void ClearAll()
    {
        globalEntitiesRegistry.Clear();
    }
}