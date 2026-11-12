using UnityEngine;

// Places the physics key and the two castle exits.
public class MazeObjectiveSpawner : MonoBehaviour
{
    [Header("Key")]
    [SerializeField] private Mesh keyMesh;
    [SerializeField] private Material keyMaterial;

    [Header("Castle Prefabs")]
    [SerializeField] private GameObject blueCastlePrefab;
    [SerializeField] private Material blueCastleMaterial;
    [SerializeField] private GameObject redCastlePrefab;
    [SerializeField] private Material redCastleMaterial;
    [SerializeField, Range(0.25f, 2f)] private float castleScale = 1f;

    public void SpawnObjectives(Vector2Int keyExitCell, Vector2Int doorExitCell,
        int width, int height, float cellSize, Vector3 origin, Transform generatedParent)
    {
        if (keyMesh != null && keyMaterial != null)
            CreateKey(keyExitCell, width, height, cellSize, origin, generatedParent);
        else
            Debug.LogWarning("Assign the key mesh and material to MazeObjectiveSpawner.", this);

        if (blueCastlePrefab == null || blueCastleMaterial == null ||
            redCastlePrefab == null || redCastleMaterial == null || keyExitCell == doorExitCell)
        {
            Debug.LogWarning("Assign both castle prefabs and materials, and set at least two exits.", this);
            return;
        }

        CreateCastle(doorExitCell, width, height, cellSize, origin, generatedParent,
            blueCastlePrefab, blueCastleMaterial, "Generated Maze Blue Castle");
        CreateCastle(keyExitCell, width, height, cellSize, origin, generatedParent,
            redCastlePrefab, redCastleMaterial, "Generated Maze Red Castle");
    }

    private void CreateKey(Vector2Int exitCell, int width, int height, float cellSize,
        Vector3 origin, Transform generatedParent)
    {
        GameObject key = new GameObject("Generated Maze Key");
        key.transform.SetParent(generatedParent, true);
        key.transform.localScale = Vector3.one * 0.45f;

        Vector3 outward = GetExitOutward(exitCell, width, height);
        Vector3 sideOffset = Mathf.Abs(outward.z) > 0.5f ? Vector3.right : Vector3.forward;
        key.transform.position = CellCenter(exitCell, width, height, cellSize, origin) +
            outward * (cellSize * 0.5f + cellSize) +
            sideOffset * (cellSize * 1.1f) + Vector3.up;

        MeshFilter meshFilter = key.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = keyMesh;
        MeshRenderer keyRenderer = key.AddComponent<MeshRenderer>();
        keyRenderer.sharedMaterial = keyMaterial;

        SphereCollider keyCollider = key.AddComponent<SphereCollider>();
        keyCollider.radius = 0.75f;
        Rigidbody body = key.AddComponent<Rigidbody>();
        body.mass = 0.6f;
        body.linearDamping = 0.15f;
        body.angularDamping = 0.25f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        key.AddComponent<MazeKey>();

        CreateKeyPiece(key.transform, new Vector3(0.9f, 0f, 0f), new Vector3(1.4f, 0.16f, 0.18f));
        CreateKeyPiece(key.transform, new Vector3(1.25f, 0f, 0.12f), new Vector3(0.18f, 0.16f, 0.38f));
        CreateKeyPiece(key.transform, new Vector3(0.8f, 0f, 0.12f), new Vector3(0.18f, 0.16f, 0.3f));
    }

    private void CreateKeyPiece(Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = "Key Detail";
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        piece.GetComponent<Renderer>().sharedMaterial = keyMaterial;
    }

    private void CreateCastle(Vector2Int exitCell, int width, int height, float cellSize,
        Vector3 origin, Transform generatedParent, GameObject prefab, Material material, string objectName)
    {
        Vector3 outward = GetExitOutward(exitCell, width, height);
        GameObject castle = Instantiate(prefab, generatedParent);
        castle.name = objectName;
        castle.transform.localScale = Vector3.one * castleScale;
        castle.transform.rotation = Quaternion.Euler(0f, ExitYaw(exitCell, height), 0f);
        castle.transform.position = CellCenter(exitCell, width, height, cellSize, origin) +
            outward * (cellSize * 0.5f + cellSize * 2f);

        foreach (Renderer renderer in castle.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;

        AddCastleColliders(castle, objectName == "Generated Maze Blue Castle");
    }

    private void AddCastleColliders(GameObject castle, bool isBlueCastle)
    {
        foreach (MeshFilter meshFilter in castle.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null) continue;

            if (meshFilter.name == "DoorMiddle")
            {
                Collider lockCollider = meshFilter.GetComponent<Collider>();
                if (lockCollider != null) lockCollider.enabled = false;
                continue;
            }

            bool isDoorLeaf = meshFilter.name.StartsWith("DoorL") || meshFilter.name.StartsWith("DoorR");
            if (isDoorLeaf)
            {
                BoxCollider doorCollider = meshFilter.GetComponent<BoxCollider>();
                if (doorCollider == null) doorCollider = meshFilter.gameObject.AddComponent<BoxCollider>();
                doorCollider.center = meshFilter.sharedMesh.bounds.center;
                doorCollider.size = meshFilter.sharedMesh.bounds.size;

                if (isBlueCastle && meshFilter.GetComponent<MazeDoor>() == null)
                    meshFilter.gameObject.AddComponent<MazeDoor>();
            }
            else
            {
                Collider existingCollider = meshFilter.GetComponent<Collider>();
                if (existingCollider is MeshCollider) continue;
                if (existingCollider != null) existingCollider.enabled = false;

                MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
            }
        }
    }

    private Vector3 CellCenter(Vector2Int cell, int width, int height, float cellSize, Vector3 origin)
    {
        float x = (cell.x + 0.5f) * cellSize - width * cellSize * 0.5f;
        float z = height * cellSize * 0.5f - (cell.y + 0.5f) * cellSize;
        return origin + new Vector3(x, 0f, z);
    }

    private Vector3 GetExitOutward(Vector2Int cell, int width, int height)
    {
        if (cell.y == 0) return Vector3.forward;
        if (cell.y == height - 1) return Vector3.back;
        if (cell.x == 0) return Vector3.left;
        return Vector3.right;
    }

    private float ExitYaw(Vector2Int cell, int height)
    {
        if (cell.y == 0) return 180f;
        if (cell.y == height - 1) return 0f;
        if (cell.x == 0) return 90f;
        return -90f;
    }
}
