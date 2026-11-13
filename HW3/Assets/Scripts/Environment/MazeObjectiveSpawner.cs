using UnityEngine;

// Places an open jail cell with the key at one exit and a locked wooden door at the other.
public class MazeObjectiveSpawner : MonoBehaviour
{
    [Header("Key")]
    [SerializeField] private Mesh keyMesh;
    [SerializeField] private Material keyMaterial;

    [Header("Exit Structure Prefabs")]
    [SerializeField] private GameObject jailColumnPrefab;
    [SerializeField] private GameObject jailWallPrefab;
    [SerializeField] private GameObject jailDoorwayPrefab;
    [SerializeField] private GameObject jailDoorPrefab;
    [SerializeField] private GameObject woodenDoorPrefab;
    [SerializeField] private GameObject flatWallPrefab;
    [SerializeField] private GameObject flatDoorwayPrefab;
    [SerializeField] private Material structureMaterial;

    public void SpawnObjectives(Vector2Int keyExitCell, Vector2Int doorExitCell,
        int width, int height, float cellSize, Vector3 origin, Transform generatedParent)
    {
        if (keyExitCell == doorExitCell)
        {
            Debug.LogWarning("MazeObjectiveSpawner needs two different exit cells.", this);
            return;
        }

        if (jailColumnPrefab == null || jailWallPrefab == null || jailDoorwayPrefab == null ||
            jailDoorPrefab == null || woodenDoorPrefab == null || flatWallPrefab == null ||
            flatDoorwayPrefab == null || structureMaterial == null)
        {
            Debug.LogWarning("Assign the jail, flat wall/doorway, wooden door, and structure material assets.", this);
            return;
        }

        Vector3 platformOrigin = origin;
        float platformSize = cellSize * 1.5f;
        Vector3 jailOutward = GetExitOutward(keyExitCell, width, height);
        Vector3 jailCenter = ExitPlatformCenter(keyExitCell, width, height, cellSize, platformOrigin, platformSize);
        BuildPlatformEnclosure(jailCenter, jailOutward, platformSize, cellSize, platformOrigin.y,
            jailWallPrefab, flatDoorwayPrefab, "Generated Maze Key Platform", generatedParent);
        if (keyMesh != null && keyMaterial != null)
            CreateKey(jailCenter - jailOutward * 0.45f + Vector3.up * 0.65f, generatedParent);
        else
            Debug.LogWarning("Assign the key mesh and material to MazeObjectiveSpawner.", this);

        Vector3 doorOutward = GetExitOutward(doorExitCell, width, height);
        Vector3 doorPlatformCenter = ExitPlatformCenter(doorExitCell, width, height,
            cellSize, platformOrigin, platformSize);
        BuildPlatformEnclosure(doorPlatformCenter, doorOutward, platformSize, cellSize, platformOrigin.y,
            flatWallPrefab, flatDoorwayPrefab, "Generated Maze Door Platform", generatedParent);
        Vector3 doorPosition = doorPlatformCenter - doorOutward * (platformSize * 0.5f);
        CreateWoodenExitDoor(doorPosition, doorOutward, cellSize, platformOrigin.y, generatedParent);

        float spawnPadSize = cellSize * 1.5f;
        Vector2Int playerEntranceCell = new Vector2Int(width / 2, height - 1);
        Vector3 entranceCellCenter = CellCenter(playerEntranceCell, width, height,
            cellSize, platformOrigin);
        float mazeSouthEdge = platformOrigin.z - height * cellSize * 0.5f;
        Vector3 startCenter = new Vector3(entranceCellCenter.x, platformOrigin.y,
            mazeSouthEdge - spawnPadSize * 0.5f);
        Vector3 startOutward = Vector3.back;
        BuildPlatformEnclosure(startCenter, startOutward, spawnPadSize, cellSize, platformOrigin.y,
            flatWallPrefab, flatDoorwayPrefab, "Generated Maze Starting Platform", generatedParent);
    }

    private void BuildPlatformEnclosure(Vector3 center, Vector3 outward, float platformSize,
        float cellSize, float floorHeight, GameObject wallPrefab, GameObject doorwayPrefab, string objectName,
        Transform generatedParent)
    {
        const float wallHeight = 5f;
        const float wallDepth = 0.5f;
        float halfSize = platformSize * 0.5f;
        // Keep the room entrance the same width as the maze corridor so its jambs
        // start at the corridor wall ends instead of overlapping them.
        float openingWidth = cellSize * 0.5f;
        Vector3 side = Mathf.Abs(outward.z) > 0.5f ? Vector3.right : Vector3.forward;

        PlaceWallRun(center + outward * halfSize, side, outward, platformSize,
            floorHeight, wallHeight, wallDepth, wallPrefab, objectName + " Back Wall", generatedParent);
        PlaceWallRun(center + side * halfSize, outward, side, platformSize,
            floorHeight, wallHeight, wallDepth, wallPrefab, objectName + " Side Wall", generatedParent);
        PlaceWallRun(center - side * halfSize, outward, -side, platformSize,
            floorHeight, wallHeight, wallDepth, wallPrefab, objectName + " Side Wall", generatedParent);

        Vector3 entranceCenter = center - outward * halfSize;
        float flankLength = (platformSize - openingWidth) * 0.5f;
        PlaceWallRun(entranceCenter - side * (openingWidth * 0.5f + flankLength * 0.5f),
            side, -outward, flankLength, floorHeight, wallHeight, wallDepth,
            wallPrefab, objectName + " Entrance Wall", generatedParent);
        PlaceWallRun(entranceCenter + side * (openingWidth * 0.5f + flankLength * 0.5f),
            side, -outward, flankLength, floorHeight, wallHeight, wallDepth,
            wallPrefab, objectName + " Entrance Wall", generatedParent);
        if (doorwayPrefab == null) return;

        Vector3 doorwayPosition = entranceCenter;
        doorwayPosition.y = floorHeight;
        GameObject doorway = CreateStructure(doorwayPrefab, doorwayPosition,
            Quaternion.LookRotation(-outward, Vector3.up),
            new Vector3(openingWidth * 0.5f, wallHeight * 0.5f, wallDepth / 0.25f),
            objectName + " Doorway", generatedParent);
        AlignStructureToFloor(doorway, floorHeight);
    }

    private void PlaceWallRun(Vector3 center, Vector3 tangent, Vector3 normal, float length,
        float floorHeight, float wallHeight, float wallDepth, GameObject wallPrefab, string objectName,
        Transform generatedParent)
    {
        const float moduleLength = 2f;
        int moduleCount = Mathf.CeilToInt(length / moduleLength);
        float start = -length * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);

        for (int i = 0; i < moduleCount; i++)
        {
            float segmentLength = Mathf.Min(moduleLength, length - i * moduleLength);
            if (segmentLength <= 0.01f) continue;

            Vector3 position = center + tangent * (start + i * moduleLength + segmentLength * 0.5f);
            position.y = floorHeight + wallHeight * 0.5f;
            GameObject wall = CreateStructure(wallPrefab, position, rotation,
                new Vector3(segmentLength * 0.5f, wallHeight * 0.5f, wallDepth / 0.25046f),
                objectName, generatedParent);
            AlignStructureToFloor(wall, floorHeight);
        }
    }

    private void CreateOpenStartingGate(Vector3 center, Vector3 outward, float platformSize,
        float cellSize, float floorHeight, Transform generatedParent)
    {
        float structureScale = cellSize / 4.5f;
        Vector3 side = Mathf.Abs(outward.z) > 0.5f ? Vector3.right : Vector3.forward;
        Vector3 entranceCenter = center - outward * (platformSize * 0.5f);
        Vector3 openPosition = entranceCenter + side * (cellSize * 0.12f) - outward * 0.7f;
        openPosition.y = floorHeight + structureScale;

        GameObject gate = CreateStructure(jailDoorPrefab, openPosition,
            Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(0f, 90f, 0f),
            structureScale * 2f, "Generated Maze Starting Gate (Open)", generatedParent);
        AlignStructureToFloor(gate, floorHeight);
    }

    private void CreateOpenJail(Vector3 center, Vector3 outward, float cellSize,
        float floorHeight, Transform generatedParent)
    {
        center.y = floorHeight;
        float structureScale = cellSize / 4.5f;
        float halfSize = structureScale;
        Vector3 side = Mathf.Abs(outward.z) > 0.5f ? Vector3.right : Vector3.forward;
        Quaternion backRotation = Quaternion.LookRotation(outward, Vector3.up);
        Quaternion sideRotation = Quaternion.LookRotation(side, Vector3.up);
        float centerHeight = structureScale;

        CreateStructure(jailWallPrefab, center + outward * halfSize + Vector3.up * centerHeight,
            backRotation, structureScale, "Generated Maze Jail Back Wall", generatedParent);
        CreateStructure(jailWallPrefab, center + side * halfSize + Vector3.up * centerHeight,
            sideRotation, structureScale, "Generated Maze Jail Side Wall", generatedParent);
        CreateStructure(jailWallPrefab, center - side * halfSize + Vector3.up * centerHeight,
            sideRotation, structureScale, "Generated Maze Jail Side Wall", generatedParent);

        for (int sideSign = -1; sideSign <= 1; sideSign += 2)
        for (int depthSign = -1; depthSign <= 1; depthSign += 2)
        {
            Vector3 postPosition = center + side * (sideSign * halfSize) +
                outward * (depthSign * halfSize) + Vector3.up * centerHeight;
            CreateStructure(jailColumnPrefab, postPosition, Quaternion.identity, structureScale,
                "Generated Maze Jail Column", generatedParent);
        }
    }

    private void CreateWoodenExitDoor(Vector3 position, Vector3 outward,
        float cellSize, float floorHeight, Transform generatedParent)
    {
        Quaternion rotation = Quaternion.LookRotation(-outward, Vector3.up);
        BoxCollider sourceCollider = woodenDoorPrefab.GetComponent<BoxCollider>();
        Vector3 sourceSize = sourceCollider == null
            ? new Vector3(1.1f, 1.65f, 0.37f)
            : sourceCollider.size;
        Vector3 doorScale = new Vector3(
            cellSize * 0.5f * 0.98f / Mathf.Max(sourceSize.x, 0.001f),
            6f / Mathf.Max(sourceSize.y, 0.001f),
            0.5f / Mathf.Max(sourceSize.z, 0.001f));
        GameObject door = CreateStructure(woodenDoorPrefab, position, rotation, doorScale,
            "Generated Maze Wooden Door", generatedParent);
        AlignStructureToFloor(door, floorHeight);

        if (door.GetComponent<BoxCollider>() == null)
        {
            BoxCollider collider = door.AddComponent<BoxCollider>();
            MeshFilter filter = door.GetComponentInChildren<MeshFilter>();
            if (filter != null)
            {
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
        }

        if (door.GetComponent<MazeDoor>() == null)
            door.AddComponent<MazeDoor>();

    }

    private GameObject CreateStructure(GameObject prefab, Vector3 position, Quaternion rotation,
        float scale, string objectName, Transform generatedParent)
    {
        return CreateStructure(prefab, position, rotation, Vector3.one * scale, objectName, generatedParent);
    }

    private GameObject CreateStructure(GameObject prefab, Vector3 position, Quaternion rotation,
        Vector3 scale, string objectName, Transform generatedParent)
    {
        GameObject structure = Instantiate(prefab, position, rotation, generatedParent);
        structure.name = objectName;
        structure.transform.localScale = scale;

        foreach (Renderer renderer in structure.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = structureMaterial;

        return structure;
    }

    private void AlignStructureToFloor(GameObject structure, float floorHeight)
    {
        float lowestPoint = float.PositiveInfinity;
        foreach (Renderer renderer in structure.GetComponentsInChildren<Renderer>(true))
            lowestPoint = Mathf.Min(lowestPoint, renderer.bounds.min.y);

        if (!float.IsInfinity(lowestPoint))
            structure.transform.position += Vector3.up * (floorHeight - lowestPoint);
    }

    private void CreateKey(Vector3 position, Transform generatedParent)
    {
        GameObject key = new GameObject("Generated Maze Key");
        key.transform.SetParent(generatedParent, true);
        key.transform.position = position;
        key.transform.localScale = Vector3.one * 0.45f;

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

    private Vector3 ExitPlatformCenter(Vector2Int cell, int width, int height,
        float cellSize, Vector3 origin, float platformSize)
    {
        Vector3 outward = GetExitOutward(cell, width, height);
        return CellCenter(cell, width, height, cellSize, origin) +
            outward * (cellSize * 0.5f + platformSize * 0.5f);
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
}
