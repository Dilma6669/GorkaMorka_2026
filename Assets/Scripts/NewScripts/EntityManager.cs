using System;
using System.Collections.Generic;
using UnityEngine;


public class EntityManager
{
    private static Dictionary<string, Entity> entities = new Dictionary<string, Entity>();

    public static string RegisterEntity(Entity entity)
    {
        // 2. Generate a real unique ID
        string newGUID = Guid.NewGuid().ToString();
        entities[newGUID] = entity;
        return newGUID; // Return it so the Entity knows its own ID
    }

    public static void UnregisterEntity(string entityGUID)
    {
        if (entities.ContainsKey(entityGUID))
        {
            entities.Remove(entityGUID);
        }
    }

    // 3. Proper Dictionary Look-up
    public static bool TryGetEntity(string entityGUID, out Entity entity)
    {
        return entities.TryGetValue(entityGUID, out entity);
    }

    // Accessor to check existence
    public static bool ContainsEntity(string entityGUID)
    {
        return entities.ContainsKey(entityGUID);
    }
}