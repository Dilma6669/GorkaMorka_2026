using System;
using UnityEngine;
using System.Collections.Generic;

public class CullingManager : MonoBehaviour
{
    public float cullDistance = 50f; // Distance to hide objects
    private static List<CullableObject> allObjects = new List<CullableObject>();

    private void Start()
    {
        allObjects = new List<CullableObject>();
    }
    
    // Call this after you spawn your trees/rocks
    public static void RegisterObject(CullableObject cullableObject)
    {
        allObjects.Add(cullableObject);
    }

    void Update()
    {
        if (allObjects == null || allObjects.Count == 0)
            return;
        
        if (Time.frameCount % 10 == 0)
        {
            // Use the camera's position instead
            Vector3 camPos = Camera.main.transform.position; 
            float sqrDist = cullDistance * cullDistance;

            foreach (var terrainObject in allObjects)
            {
                float dist = (terrainObject.transform.position - camPos).sqrMagnitude;
                terrainObject.SetVisibility(dist < sqrDist);
            }
        }
    }

// Inside CullingManager
    public static void ClearAll() 
    {
        allObjects.Clear();
    }

    private void OnDestroy()
    {
        allObjects.Clear();
    }
}