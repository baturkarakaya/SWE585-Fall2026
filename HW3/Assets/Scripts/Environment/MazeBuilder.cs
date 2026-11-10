using System.Collections.Generic;
using UnityEngine;

// Creates a maze using the Aldous-Broder method.
public class MazeBuilder : MonoBehaviour
{
    [Header("Maze Settings")]
    [SerializeField] private int width = 21;
    [SerializeField] private int height = 21;
    [SerializeField, Range(0f, 0.5f)] private float extraPathProbability = 0.08f;
    [SerializeField] private int randomSeed = 585;
    [SerializeField, Min(1)] private int exitCount = 2;
    [SerializeField, Min(0f)] private float playerStartDistanceFromGate = 3f;

    [Header("Prefab Settings")]
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private float wallThickness = 1.5f;
    [SerializeField, Range(0.25f, 1.5f)] private float wallHeightScale = 1.5f;
    [SerializeField] private float wallBaseOffset = -0.25f;
    [SerializeField, Min(0.01f)] private float floorThickness = 0.15f;
    [SerializeField] private float waterDepth = 1.5f;
    [SerializeField] private bool buildOnStart = true;

    private sealed class Cell
    {
        public bool north = true;
        public bool south = true;
        public bool east = true;
        public bool west = true;
        public bool visited;
    }

    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Mesh floorMesh;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material waterMaterial;
    private Cell[,] cells;
    private readonly List<GameObject> generatedWalls = new List<GameObject>();
    private readonly List<GameObject> generatedFloors = new List<GameObject>();
    private readonly List<Vector2Int> mazeExitCells = new List<Vector2Int>();

    // These properties return the grid cells used for the player, key, and door.
    public Vector2Int PlayerStartCell
    {
        get
        {
            return new Vector2Int(width / 2, height - 2);
        }
    }

    public Vector2Int KeyCell
    {
        get
        {
            return new Vector2Int(1, 1);
        }
    }

    public Vector2Int DoorCell
    {
        get
        {
            if (mazeExitCells.Count > 0)
            {
                Vector2Int farthestExit = mazeExitCells[0];
                int greatestDistance = -1;
                foreach (Vector2Int exitCell in mazeExitCells)
                {
                    int distance = Mathf.Abs(exitCell.x - KeyCell.x) + Mathf.Abs(exitCell.y - KeyCell.y);
                    if (distance > greatestDistance)
                    {
                        greatestDistance = distance;
                        farthestExit = exitCell;
                    }
                }

                return farthestExit;
            }

            return new Vector2Int(width - 2, height - 1);
        }
    }

    private void Start()
    {
        if (buildOnStart) GenerateMaze();
    }

    [ContextMenu("Generate Maze")]
    public void GenerateMaze()
    {
        if (wallPrefab == null)
        {
            Debug.LogError("Assign a wall prefab to MazeBuilder in the Inspector.", this);
            return;
        }

        width = Mathf.Max(3, width);
        height = Mathf.Max(3, height);
        var random = new System.Random(randomSeed);
        cells = new Cell[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) cells[x, y] = new Cell();

        // Start near the south entrance, then visit every cell by walking randomly.
        Vector2Int current = new Vector2Int(width / 2, height - 2);
        cells[current.x, current.y].visited = true;
        int unvisited = width * height - 1;
        Vector2Int[] shifts = { new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

        while (unvisited > 0)
        {
            var legal = new List<Vector2Int>(4);
            foreach (Vector2Int shift in shifts)
            {
                Vector2Int next = current + shift;
                if (Inside(next)) legal.Add(shift);
            }

            Vector2Int direction = legal[random.Next(legal.Count)];
            Vector2Int neighbor = current + direction;
            if (!cells[neighbor.x, neighbor.y].visited)
            {
                RemoveSharedWall(current, neighbor);
                cells[neighbor.x, neighbor.y].visited = true;
                unvisited--;
            }
            current = neighbor;
        }

        // Remove a few more walls so the maze has alternate routes.
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (x + 1 < width && cells[x, y].east && random.NextDouble() < extraPathProbability)
                RemoveSharedWall(new Vector2Int(x, y), new Vector2Int(x + 1, y));
            if (y + 1 < height && cells[x, y].south && random.NextDouble() < extraPathProbability)
                RemoveSharedWall(new Vector2Int(x, y), new Vector2Int(x, y + 1));
        }

        OpenPlayerEntrance();
        PlaceMazeExits();

        DrawMaze();
        PlacePlayerAtStart();
        Debug.Log($"Generated {width}x{height} Aldous-Broder maze ({generatedWalls.Count} wall segments).", this);
    }

    private void OpenPlayerEntrance()
    {
        Vector2Int entranceCell = new Vector2Int(width / 2, height - 1);
        RemoveSharedWall(PlayerStartCell, entranceCell);
        cells[entranceCell.x, entranceCell.y].south = false;
    }

    private void PlaceMazeExits()
    {
        mazeExitCells.Clear();
        List<Vector2Int> borderCells = GetBorderCells();
        Vector2Int entranceCell = new Vector2Int(width / 2, height - 1);
        int entranceIndex = borderCells.IndexOf(entranceCell);

        // Spread the exits evenly around the border, counting the player
        // entrance as one of the openings when calculating the spacing.
        int actualExitCount = Mathf.Clamp(exitCount, 1, borderCells.Count - 1);
        int totalOpenings = actualExitCount + 1;
        for (int i = 1; i <= actualExitCount; i++)
        {
            int offset = Mathf.RoundToInt(i * borderCells.Count / (float)totalOpenings);
            int borderIndex = (entranceIndex + offset) % borderCells.Count;
            Vector2Int exitCell = borderCells[borderIndex];
            OpenOuterWall(exitCell);
            mazeExitCells.Add(exitCell);
        }
    }

    private List<Vector2Int> GetBorderCells()
    {
        List<Vector2Int> borderCells = new List<Vector2Int>();

        for (int x = 0; x < width; x++) borderCells.Add(new Vector2Int(x, 0));
        for (int y = 1; y < height; y++) borderCells.Add(new Vector2Int(width - 1, y));
        for (int x = width - 2; x >= 0; x--) borderCells.Add(new Vector2Int(x, height - 1));
        for (int y = height - 2; y > 0; y--) borderCells.Add(new Vector2Int(0, y));

        return borderCells;
    }

    private void OpenOuterWall(Vector2Int cellPosition)
    {
        Cell cell = cells[cellPosition.x, cellPosition.y];
        if (cellPosition.y == 0) cell.north = false;
        else if (cellPosition.y == height - 1) cell.south = false;
        else if (cellPosition.x == 0) cell.west = false;
        else if (cellPosition.x == width - 1) cell.east = false;
    }

    private void PlacePlayerAtStart()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("No object with the Player tag was found.", this);
            return;
        }

        // Place the player just outside the south entrance, with the collider
        // kept clear of the maze edge.
        float x = (width / 2f + 0.5f) * cellSize - width * cellSize * 0.5f;
        float southEdge = transform.position.z - height * cellSize * 0.5f;
        Collider playerCollider = player.GetComponentInChildren<Collider>();
        float colliderDepth = playerCollider == null ? 0.5f : playerCollider.bounds.extents.z;
        Vector3 colliderCenterOffset = playerCollider == null
            ? Vector3.zero
            : playerCollider.bounds.center - player.transform.position;
        Vector3 desiredColliderCenter = new Vector3(
            transform.position.x + x,
            playerCollider == null ? player.transform.position.y : playerCollider.bounds.center.y,
            southEdge - colliderDepth - playerStartDistanceFromGate);
        Vector3 startPosition = desiredColliderCenter - colliderCenterOffset;

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = startPosition;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        else
        {
            player.transform.position = startPosition;
        }
    }

    private bool Inside(Vector2Int p)
    {
        return p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;
    }

    private void RemoveSharedWall(Vector2Int a, Vector2Int b)
    {
        int dx = b.x - a.x, dy = b.y - a.y;
        if (dx == 1) { cells[a.x, a.y].east = false; cells[b.x, b.y].west = false; }
        else if (dx == -1) { cells[a.x, a.y].west = false; cells[b.x, b.y].east = false; }
        else if (dy == 1) { cells[a.x, a.y].south = false; cells[b.x, b.y].north = false; }
        else if (dy == -1) { cells[a.x, a.y].north = false; cells[b.x, b.y].south = false; }
    }

    private void DrawMaze()
    {
        ClearGeneratedObjects();
        DrawWalls();
        DrawFloorAndWater();
    }

    private void DrawWalls()
    {
        float halfWidth = width * cellSize * 0.5f;
        float halfHeight = height * cellSize * 0.5f;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Cell cell = cells[x, y];
            float centerX = (x + 0.5f) * cellSize - halfWidth;
            float centerZ = halfHeight - (y + 0.5f) * cellSize;
            // Draw the north and west walls once. Add the south/east outer walls at the edge.
            if (cell.north) PlaceWall(centerX, centerZ + cellSize * 0.5f, false);
            if (cell.west) PlaceWall(centerX - cellSize * 0.5f, centerZ, true);
            if (y == height - 1 && cell.south) PlaceWall(centerX, centerZ - cellSize * 0.5f, false);
            if (x == width - 1 && cell.east) PlaceWall(centerX + cellSize * 0.5f, centerZ, true);
        }
    }

    private void DrawFloorAndWater()
    {
        float spawnPadSize = cellSize * 1.5f;
        float exitPlatformSize = cellSize * 4f;
        float floorY = transform.position.y - floorThickness * 0.5f;
        if (floorMaterial != null)
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float centerX = (x + 0.5f) * cellSize - width * cellSize * 0.5f;
                float centerZ = height * cellSize * 0.5f - (y + 0.5f) * cellSize;
                CreateFloorTile(
                    transform.position + new Vector3(centerX, floorY, centerZ),
                    new Vector2(cellSize + 0.02f, cellSize + 0.02f),
                    "Generated Maze Floor");
            }

            // Put a wider landing pad directly under the player spawn point.
            float spawnX = (width / 2f + 0.5f) * cellSize - width * cellSize * 0.5f;
            float southEdge = transform.position.z - height * cellSize * 0.5f;
            Vector3 entrancePadPosition = new Vector3(
                transform.position.x + spawnX,
                floorY,
                southEdge - spawnPadSize * 0.5f);
            CreateFloorTile(entrancePadPosition, new Vector2(spawnPadSize, spawnPadSize), "Generated Maze Entrance Platform");

            // Add a landing pad just beyond each maze exit.
            foreach (Vector2Int exitCell in mazeExitCells)
            {
                float centerX = (exitCell.x + 0.5f) * cellSize - width * cellSize * 0.5f;
                float centerZ = height * cellSize * 0.5f - (exitCell.y + 0.5f) * cellSize;
                Vector3 outward;
                if (exitCell.y == 0) outward = Vector3.forward;
                else if (exitCell.y == height - 1) outward = Vector3.back;
                else if (exitCell.x == 0) outward = Vector3.left;
                else outward = Vector3.right;

                Vector3 padPosition = transform.position + new Vector3(centerX, floorY, centerZ);
                padPosition += outward * (cellSize * 0.5f + exitPlatformSize * 0.5f);
                CreateFloorTile(padPosition, new Vector2(exitPlatformSize, exitPlatformSize), "Generated Maze Exit Platform");
            }
        }

        if (waterMaterial != null)
        {
            float mazeWidth = width * cellSize;
            float mazeDepth = height * cellSize;
            Vector3 waterPosition = transform.position + Vector3.down * waterDepth;
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.transform.SetParent(transform, true);
            water.transform.position = waterPosition;
            water.name = "Generated Maze Slime Water";
            // Unity's plane mesh is 10 by 10 units. The material texture comes from the dungeon pack.
            water.transform.localScale = new Vector3(
                (mazeWidth + exitPlatformSize * 2f) / 10f,
                1f,
                (mazeDepth + exitPlatformSize * 2f) / 10f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Collider waterCollider = water.GetComponent<Collider>();
            if (waterCollider != null) waterCollider.enabled = false;
            water.AddComponent<WaterTextureScroller>();
            BoxCollider hazardTrigger = water.AddComponent<BoxCollider>();
            hazardTrigger.isTrigger = true;
            hazardTrigger.size = new Vector3(10f, 1f, 10f);
            water.AddComponent<ToxicWaterHazard>();
        }
    }

    private void CreateFloorTile(Vector3 position, Vector2 size, string objectName)
    {
        GameObject tile = new GameObject(objectName);
        tile.name = objectName;
        tile.transform.SetParent(transform, true);
        tile.transform.position = position;
        // The dungeon floor mesh uses the same atlas UV layout as its wall pieces.
        tile.transform.localScale = new Vector3(size.x * 0.5f, floorThickness * 0.5f, size.y * 0.5f);

        MeshFilter tileMeshFilter = tile.AddComponent<MeshFilter>();
        tileMeshFilter.sharedMesh = floorMesh;
        MeshRenderer tileRenderer = tile.AddComponent<MeshRenderer>();
        if (tileRenderer != null && floorMaterial != null)
            tileRenderer.sharedMaterial = floorMaterial;

        BoxCollider tileCollider = tile.AddComponent<BoxCollider>();
        tileCollider.size = Vector3.one * 2f;

        generatedFloors.Add(tile);
    }

    private void PlaceWall(float x, float z, bool vertical)
    {
        Vector3 segmentPosition = transform.position + new Vector3(x, 0f, z);
        GameObject wall = Instantiate(wallPrefab, segmentPosition, Quaternion.identity, transform);

        MeshFilter meshFilter = wall.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("The assigned wall prefab must contain a mesh.", wall);
            Destroy(wall);
            return;
        }

        if (wallMaterial != null)
        {
            foreach (Renderer renderer in wall.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = wallMaterial;
        }

        Bounds meshBounds = meshFilter.sharedMesh.bounds;
        Vector3 baseSize = Vector3.Scale(meshBounds.size, meshFilter.transform.localScale);
        Vector3 targetSize = new Vector3(cellSize, wallHeightScale * 2f, wallThickness);
        Vector3 scale = new Vector3(
            targetSize.x / Mathf.Max(baseSize.x, 0.001f),
            targetSize.y / Mathf.Max(baseSize.y, 0.001f),
            targetSize.z / Mathf.Max(baseSize.z, 0.001f));
        wall.transform.localScale = scale;

        Quaternion rotation = vertical ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        Vector3 meshCenter = Vector3.Scale(meshBounds.center, meshFilter.transform.localScale);
        Vector3 desiredMeshCenter = segmentPosition + Vector3.up * (wallHeightScale + wallBaseOffset);
        wall.transform.rotation = rotation;
        wall.transform.position = desiredMeshCenter - rotation * Vector3.Scale(meshCenter, scale);
        wall.name = "Generated Maze Wall";
        generatedWalls.Add(wall);
    }

    private void ClearGeneratedObjects()
    {
        // Find generated objects saved as children too, so rebuilding does not duplicate them.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "Generated Maze Wall" && child.name != "MazeWall" &&
                child.name != "Generated Maze Floor" && child.name != "Generated Maze Entrance Platform" &&
                child.name != "Generated Maze Exit Platform" && child.name != "Generated Maze Slime Water") continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        // Also clear any objects still referenced by the current runtime list.
        for (int i = generatedWalls.Count - 1; i >= 0; i--)
        {
            if (generatedWalls[i] == null) continue;
            if (Application.isPlaying) Destroy(generatedWalls[i]);
            else DestroyImmediate(generatedWalls[i]);
        }

        generatedWalls.Clear();
        generatedFloors.Clear();
    }
}
