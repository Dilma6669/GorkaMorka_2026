using UnityEngine;

public class CullableObject : MonoBehaviour
{
    public void SetVisibility(bool isVisible)
    {
        gameObject.SetActive(isVisible);
    }
}