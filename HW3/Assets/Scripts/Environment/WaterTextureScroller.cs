using UnityEngine;

// Scrolls the water texture to give the maze water a gentle flowing motion.
public class WaterTextureScroller : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.1f;
    [SerializeField, Min(1f)] private float textureTiling = 8f;

    private Material waterMaterialInstance;
    private Vector2 textureOffset;

    private void Awake()
    {
        Renderer waterRenderer = GetComponent<Renderer>();
        if (waterRenderer != null)
        {
            waterMaterialInstance = waterRenderer.material;
            waterMaterialInstance.mainTextureScale = Vector2.one * textureTiling;
        }
    }

    private void Update()
    {
        if (waterMaterialInstance == null) return;

        textureOffset.y += scrollSpeed * Time.deltaTime;
        waterMaterialInstance.mainTextureOffset = textureOffset;
    }
}
