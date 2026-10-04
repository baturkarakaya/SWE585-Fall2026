using UnityEngine;
using UnityEngine.InputSystem;

public class PrefabSpawner : MonoBehaviour
{
    public GameObject prefab;
    public Transform spawnPoint;

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        }
    }
}