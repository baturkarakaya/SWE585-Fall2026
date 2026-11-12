using System.Collections.Generic;
using UnityEngine;

// Generates the maze layout and passes it to the scene-building components.
[RequireComponent(typeof(MazeEnvironmentBuilder))]
[RequireComponent(typeof(MazeObjectiveSpawner))]
[RequireComponent(typeof(MazeTrapSpawner))]
public class MazeBuilder : MonoBehaviour
{
    [Header("Maze Settings")]
    [SerializeField] private int width = 21;
    [SerializeField] private int height = 21;
    [SerializeField, Range(0f, 0.5f)] private float extraPathProbability = 0.08f;
    [SerializeField] private int randomSeed = 585;
    [SerializeField, Min(1)] private int exitCount = 2;
    [SerializeField, Min(0f)] private float playerStartDistanceFromGate = 3f;

    [Header("Layout Settings")]
    [SerializeField, Min(0.1f)] private float cellSize = 2f;
    [SerializeField, Min(0.1f)] private float wallThickness = 0.75f;
    [SerializeField, Range(0.25f, 1.5f)] private float wallHeightScale = 1.5f;
    [SerializeField] private float wallBaseOffset = -0.25f;
    [SerializeField, Min(0.01f)] private float floorThickness = 0.15f;
    [SerializeField, Min(0.1f)] private float waterDepth = 1.5f;
    [SerializeField] private bool buildOnStart = true;

    private sealed class Cell
    {
        public bool north = true;
        public bool south = true;
        public bool east = true;
        public bool west = true;
        public bool visited;
    }

    private Cell[,] cells;
    private readonly List<Vector2Int> mazeExitCells = new List<Vector2Int>();

    public Vector2Int PlayerStartCell
    {
        get { return new Vector2Int(width / 2, height - 2); }
    }

    public Vector2Int KeyCell
    {
        get
        {
            foreach (Vector2Int exitCell in mazeExitCells)
            {
                if (exitCell != DoorCell) return exitCell;
            }

            return new Vector2Int(1, 1);
        }
    }

    public Vector2Int DoorCell
    {
        get
        {
            if (mazeExitCells.Count == 0) return new Vector2Int(width - 2, height - 1);

            Vector2Int farthestExit = mazeExitCells[0];
            int greatestDistance = -1;
            Vector2Int selectionAnchor = new Vector2Int(1, 1);
            foreach (Vector2Int exitCell in mazeExitCells)
            {
                int distance = ManhattanDistance(exitCell, selectionAnchor);
                if (distance > greatestDistance)
                {
                    greatestDistance = distance;
                    farthestExit = exitCell;
                }
            }

            return farthestExit;
        }
    }

    private void Start()
    {
        if (!buildOnStart) return;

        if (GetComponent<MazeRoundUI>() == null)
            gameObject.AddComponent<MazeRoundUI>();

        GenerateMaze();
    }

    [ContextMenu("Generate Maze")]
    public void GenerateMaze()
    {
        MazeEnvironmentBuilder environmentBuilder = GetComponent<MazeEnvironmentBuilder>();
        MazeObjectiveSpawner objectiveSpawner = GetComponent<MazeObjectiveSpawner>();
        MazeTrapSpawner trapSpawner = GetComponent<MazeTrapSpawner>();
        if (environmentBuilder == null || objectiveSpawner == null || trapSpawner == null)
        {
            Debug.LogError("MazeBuilder requires its environment, objective, and trap components.", this);
            return;
        }

        width = Mathf.Max(3, width);
        height = Mathf.Max(3, height);
        GenerateCellLayout();
        OpenPlayerEntrance();
        PlaceMazeExits();

        bool[,] walkableGrid = BuildWalkableGrid();
        List<MazeWallSegment> walls = CollectWallSegments(walkableGrid);
        List<MazeFloorCellData> floorCells = CollectFloorCells();
        Dictionary<Vector2Int, GameObject> floorTiles = environmentBuilder.BuildMaze(
            walls, floorCells, width, height, cellSize, wallThickness, wallHeightScale, wallBaseOffset,
            floorThickness, waterDepth, mazeExitCells, transform);

        List<MazeTrapSpawnSite> trapSites = CreateTrapSpawnSites(floorTiles);
        trapSpawner.SpawnTraps(trapSites, PlayerStartCell, KeyCell, DoorCell, cellSize,
            transform.position.y, randomSeed, transform);

        objectiveSpawner.SpawnObjectives(KeyCell, DoorCell, width, height, cellSize, transform.position, transform);
        PlacePlayerAtStart();

        Debug.Log($"Generated {width}x{height} Aldous-Broder maze ({walls.Count} wall segments, {trapSites.Count} possible trap cells).", this);
    }

    private void GenerateCellLayout()
    {
        System.Random random = new System.Random(randomSeed);
        cells = new Cell[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            cells[x, y] = new Cell();

        Vector2Int current = new Vector2Int(width / 2, height - 2);
        cells[current.x, current.y].visited = true;
        int unvisited = width * height - 1;
        Vector2Int[] shifts =
        {
            new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(1, 0), new Vector2Int(-1, 0)
        };

        while (unvisited > 0)
        {
            List<Vector2Int> legalShifts = new List<Vector2Int>(4);
            foreach (Vector2Int shift in shifts)
            {
                if (Inside(current + shift)) legalShifts.Add(shift);
            }

            Vector2Int next = current + legalShifts[random.Next(legalShifts.Count)];
            if (!cells[next.x, next.y].visited)
            {
                RemoveSharedWall(current, next);
                cells[next.x, next.y].visited = true;
                unvisited--;
            }

            current = next;
        }

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (x + 1 < width && cells[x, y].east && random.NextDouble() < extraPathProbability)
                RemoveSharedWall(new Vector2Int(x, y), new Vector2Int(x + 1, y));
            if (y + 1 < height && cells[x, y].south && random.NextDouble() < extraPathProbability)
                RemoveSharedWall(new Vector2Int(x, y), new Vector2Int(x, y + 1));
        }
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

        int actualExitCount = Mathf.Clamp(exitCount, 1, borderCells.Count - 1);
        int totalOpenings = actualExitCount + 1;
        for (int i = 1; i <= actualExitCount; i++)
        {
            int offset = Mathf.RoundToInt(i * borderCells.Count / (float)totalOpenings);
            Vector2Int exitCell = borderCells[(entranceIndex + offset) % borderCells.Count];
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

    private bool[,] BuildWalkableGrid()
    {
        int gridWidth = width * 2 + 1;
        int gridHeight = height * 2 + 1;
        bool[,] walkable = new bool[gridWidth, gridHeight];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Cell cell = cells[x, y];
            int gridX = x * 2 + 1;
            int gridY = y * 2 + 1;
            walkable[gridX, gridY] = true;

            if (!cell.north) walkable[gridX, gridY - 1] = true;
            if (!cell.south) walkable[gridX, gridY + 1] = true;
            if (!cell.east) walkable[gridX + 1, gridY] = true;
            if (!cell.west) walkable[gridX - 1, gridY] = true;
        }

        return walkable;
    }

    private List<MazeWallSegment> CollectWallSegments(bool[,] walkable)
    {
        List<MazeWallSegment> walls = new List<MazeWallSegment>();
        int gridWidth = walkable.GetLength(0);
        int gridHeight = walkable.GetLength(1);
        float gridCellSize = cellSize * 0.5f;
        int maximumSegmentsPerWall = Mathf.Max(1, Mathf.RoundToInt(cellSize / gridCellSize));
        bool[,] horizontalWalls = new bool[gridWidth, gridHeight + 1];
        bool[,] verticalWalls = new bool[gridWidth + 1, gridHeight];

        for (int y = 0; y < gridHeight; y++)
        for (int x = 0; x < gridWidth; x++)
        {
            if (!walkable[x, y]) continue;

            if (y > 0 && !walkable[x, y - 1])
                horizontalWalls[x, y] = true;
            if (y < gridHeight - 1 && !walkable[x, y + 1])
                horizontalWalls[x, y + 1] = true;
            if (x < gridWidth - 1 && !walkable[x + 1, y])
                verticalWalls[x + 1, y] = true;
            if (x > 0 && !walkable[x - 1, y])
                verticalWalls[x, y] = true;
        }

        for (int lineY = 0; lineY <= gridHeight; lineY++)
        {
            int x = 0;
            while (x < gridWidth)
            {
                if (!horizontalWalls[x, lineY])
                {
                    x++;
                    continue;
                }

                int firstX = x;
                while (x < gridWidth && horizontalWalls[x, lineY] &&
                    x - firstX < maximumSegmentsPerWall) x++;
                int segmentCount = x - firstX;
                float centerX = (firstX + segmentCount * 0.5f) * gridCellSize -
                    gridWidth * gridCellSize * 0.5f;
                float centerZ = gridHeight * gridCellSize * 0.5f - lineY * gridCellSize;
                Vector3 position = transform.position + new Vector3(centerX, 0f, centerZ);
                AddWallSegment(walls, position, false, segmentCount * gridCellSize);
            }
        }

        for (int lineX = 0; lineX <= gridWidth; lineX++)
        {
            int y = 0;
            while (y < gridHeight)
            {
                if (!verticalWalls[lineX, y])
                {
                    y++;
                    continue;
                }

                int firstY = y;
                while (y < gridHeight && verticalWalls[lineX, y] &&
                    y - firstY < maximumSegmentsPerWall) y++;
                int segmentCount = y - firstY;
                float centerX = lineX * gridCellSize - gridWidth * gridCellSize * 0.5f;
                float centerZ = gridHeight * gridCellSize * 0.5f -
                    (firstY + segmentCount * 0.5f) * gridCellSize;
                Vector3 position = transform.position + new Vector3(centerX, 0f, centerZ);
                AddWallSegment(walls, position, true, segmentCount * gridCellSize);
            }
        }

        return walls;
    }

    private List<MazeFloorCellData> CollectFloorCells()
    {
        List<MazeFloorCellData> floorCells = new List<MazeFloorCellData>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Cell cell = cells[x, y];
            MazeFloorCellData floorCell = new MazeFloorCellData();
            floorCell.cell = new Vector2Int(x, y);
            floorCell.northOpen = !cell.north;
            floorCell.southOpen = !cell.south;
            floorCell.eastOpen = !cell.east;
            floorCell.westOpen = !cell.west;
            floorCells.Add(floorCell);
        }

        return floorCells;
    }

    private void AddWallSegment(List<MazeWallSegment> walls, Vector3 position,
        bool vertical, float length)
    {
        MazeWallSegment segment = new MazeWallSegment();
        segment.position = position;
        segment.vertical = vertical;
        segment.length = length;
        walls.Add(segment);
    }

    private List<MazeTrapSpawnSite> CreateTrapSpawnSites(Dictionary<Vector2Int, GameObject> floorTiles)
    {
        List<MazeTrapSpawnSite> sites = new List<MazeTrapSpawnSite>();
        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
        {
            Vector2Int position = new Vector2Int(x, y);
            GameObject floorTile;
            if (!floorTiles.TryGetValue(position, out floorTile)) continue;

            Cell cell = cells[x, y];
            int openPassages = 0;
            if (!cell.north) openPassages++;
            if (!cell.south) openPassages++;
            if (!cell.east) openPassages++;
            if (!cell.west) openPassages++;

            MazeTrapSpawnSite site = new MazeTrapSpawnSite();
            site.cell = position;
            site.floorPosition = CellCenter(position);
            site.floorTile = floorTile;
            site.openPassages = openPassages;
            site.isStraightCorridor =
                (!cell.north && !cell.south && cell.east && cell.west) ||
                (!cell.east && !cell.west && cell.north && cell.south);
            sites.Add(site);
        }

        return sites;
    }

    private void PlacePlayerAtStart()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("No object with the Player tag was found.", this);
            return;
        }

        if (player.GetComponent<PlayerRigidbodyPusher>() == null)
            player.AddComponent<PlayerRigidbodyPusher>();

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

    private Vector3 CellCenter(Vector2Int cell)
    {
        float x = (cell.x + 0.5f) * cellSize - width * cellSize * 0.5f;
        float z = height * cellSize * 0.5f - (cell.y + 0.5f) * cellSize;
        return transform.position + new Vector3(x, 0f, z);
    }

    private bool Inside(Vector2Int position)
    {
        return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
    }

    private void RemoveSharedWall(Vector2Int a, Vector2Int b)
    {
        int dx = b.x - a.x;
        int dy = b.y - a.y;
        if (dx == 1) { cells[a.x, a.y].east = false; cells[b.x, b.y].west = false; }
        else if (dx == -1) { cells[a.x, a.y].west = false; cells[b.x, b.y].east = false; }
        else if (dy == 1) { cells[a.x, a.y].south = false; cells[b.x, b.y].north = false; }
        else if (dy == -1) { cells[a.x, a.y].north = false; cells[b.x, b.y].south = false; }
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
