using System.Collections.Generic;
using UnityEngine;

public struct MazeWallSegment
{
    public Vector3 position;
    public bool vertical;
    public float length;
}

public struct MazeFloorCellData
{
    public Vector2Int cell;
    public bool northOpen;
    public bool southOpen;
    public bool eastOpen;
    public bool westOpen;
}

// Builds the maze's walls, floor tiles, entrance and exit platforms, and slime water.
public class MazeEnvironmentBuilder : MonoBehaviour
{
    [Header("Wall Assets")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private GameObject wallTorchPrefab;
    [SerializeField, Min(1)] private int torchEveryWallSegments = 5;

    [Header("Floor and Water Assets")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject[] decorativePropPrefabs;
    [SerializeField] private Mesh floorMesh;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material waterMaterial;
    [SerializeField, Range(0.25f, 0.75f)] private float floorPathWidthRatio = 0.5f;

    private float wallHeightScale;
    private float wallBaseOffset;
    private float wallThickness;
    private float floorThickness;
    private float waterDepth;

    private readonly Dictionary<Vector2Int, GameObject> floorTiles = new Dictionary<Vector2Int, GameObject>();

    public Dictionary<Vector2Int, GameObject> BuildMaze(List<MazeWallSegment> walls,
        List<MazeFloorCellData> floorCells,
        int width, int height, float cellSize, float configuredWallThickness,
        float configuredWallHeight, float configuredWallBaseOffset, float configuredFloorThickness,
        float configuredWaterDepth, List<Vector2Int> exitCells, Transform generatedParent)
    {
        ClearGeneratedObjects(generatedParent);
        floorTiles.Clear();

        wallThickness = configuredWallThickness;
        wallHeightScale = configuredWallHeight;
        wallBaseOffset = configuredWallBaseOffset;
        floorThickness = configuredFloorThickness;
        waterDepth = configuredWaterDepth;

        int torchEligibleSegments = 0;
        int torchCount = 0;
        foreach (MazeWallSegment segment in walls)
        {
            PlaceWall(segment.position, segment.vertical, segment.length, cellSize, generatedParent);
            if (wallTorchPrefab == null || segment.length < cellSize * 0.75f ||
                IsOuterWall(segment, width, height, cellSize, generatedParent.position))
                continue;

            if (torchEligibleSegments++ % Mathf.Max(1, torchEveryWallSegments) == 0)
                PlaceWallTorch(segment, torchCount++, generatedParent);
        }

        DrawFloorAndWater(width, height, cellSize, floorCells, exitCells, generatedParent);
        Debug.Log("Built maze environment with " + walls.Count + " wall segments.", this);
        return floorTiles;
    }

    private void DrawFloorAndWater(int width, int height, float cellSize,
        List<MazeFloorCellData> pathCells, List<Vector2Int> exitCells, Transform generatedParent)
    {
        float spawnPadSize = cellSize * 1.5f;
        float exitPlatformSize = cellSize * 1.5f;
        Vector3 origin = generatedParent.position;
        float floorY = origin.y - floorThickness * 0.5f;

        if (floorMaterial != null)
        {
            float pathWidth = cellSize * floorPathWidthRatio;
            float armLength = Mathf.Max(0.01f, (cellSize - pathWidth) * 0.5f);
            float armOffset = pathWidth * 0.5f + armLength * 0.5f;

            foreach (MazeFloorCellData pathCell in pathCells)
            {
                int x = pathCell.cell.x;
                int y = pathCell.cell.y;
                float centerX = (x + 0.5f) * cellSize - width * cellSize * 0.5f;
                float centerZ = height * cellSize * 0.5f - (y + 0.5f) * cellSize;
                Vector3 center = origin + new Vector3(centerX, floorY, centerZ);
                GameObject cellFloor = new GameObject("Generated Maze Floor Cell");
                cellFloor.transform.SetParent(generatedParent, true);
                cellFloor.transform.position = center;
                floorTiles[pathCell.cell] = cellFloor;

                CreateFloorTile(center, new Vector2(pathWidth, pathWidth),
                    "Generated Maze Floor Tile", cellFloor.transform);

                if (pathCell.northOpen)
                    CreateFloorTile(center + Vector3.forward * armOffset,
                        new Vector2(pathWidth, armLength), "Generated Maze Floor Path", cellFloor.transform);
                if (pathCell.southOpen)
                    CreateFloorTile(center + Vector3.back * armOffset,
                        new Vector2(pathWidth, armLength), "Generated Maze Floor Path", cellFloor.transform);
                if (pathCell.eastOpen)
                    CreateFloorTile(center + Vector3.right * armOffset,
                        new Vector2(armLength, pathWidth), "Generated Maze Floor Path", cellFloor.transform);
                if (pathCell.westOpen)
                    CreateFloorTile(center + Vector3.left * armOffset,
                        new Vector2(armLength, pathWidth), "Generated Maze Floor Path", cellFloor.transform);
            }

            float spawnX = (width / 2f + 0.5f) * cellSize - width * cellSize * 0.5f;
            float southEdge = origin.z - height * cellSize * 0.5f;
            Vector3 entrancePadPosition = new Vector3(
                origin.x + spawnX,
                floorY,
                southEdge - spawnPadSize * 0.5f);
            CreateFloorTile(entrancePadPosition,
                new Vector2(spawnPadSize, spawnPadSize),
                "Generated Maze Entrance Platform", generatedParent);

            foreach (Vector2Int exitCell in exitCells)
            {
                float centerX = (exitCell.x + 0.5f) * cellSize - width * cellSize * 0.5f;
                float centerZ = height * cellSize * 0.5f - (exitCell.y + 0.5f) * cellSize;
                Vector3 outward = GetExitOutward(exitCell, width, height);
                Vector3 padPosition = origin + new Vector3(centerX, floorY, centerZ);
                padPosition += outward * (cellSize * 0.5f + exitPlatformSize * 0.5f);
                CreateFloorTile(padPosition,
                    new Vector2(exitPlatformSize, exitPlatformSize),
                    "Generated Maze Exit Platform", generatedParent);
            }
        }

        if (waterMaterial == null) return;

        float mazeWidth = width * cellSize;
        float mazeDepth = height * cellSize;
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.transform.SetParent(generatedParent, true);
        water.transform.position = origin + Vector3.down * waterDepth;
        water.name = "Generated Maze Slime Water";
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

    public void SpawnDecorations(List<MazeFloorCellData> pathCells, Vector2Int playerStartCell,
        Vector2Int keyExitCell, Vector2Int doorExitCell, List<Vector2Int> exitCells,
        float cellSize, float floorHeight, Transform generatedParent)
    {
        if (decorativePropPrefabs == null || decorativePropPrefabs.Length == 0) return;

        int decorationIndex = 0;
        foreach (MazeFloorCellData cell in pathCells)
        {
            int openPassages = 0;
            if (cell.northOpen) openPassages++;
            if (cell.southOpen) openPassages++;
            if (cell.eastOpen) openPassages++;
            if (cell.westOpen) openPassages++;

            int propsForCell;
            if (openPassages == 1)
                propsForCell = 3;
            else if (openPassages >= 3)
                propsForCell = 1;
            else continue;

            if (cell.cell == playerStartCell || cell.cell == keyExitCell || cell.cell == doorExitCell ||
                exitCells.Contains(cell.cell)) continue;

            List<Vector3> closedSides = new List<Vector3>();
            if (!cell.northOpen) closedSides.Add(Vector3.forward);
            if (!cell.southOpen) closedSides.Add(Vector3.back);
            if (!cell.eastOpen) closedSides.Add(Vector3.right);
            if (!cell.westOpen) closedSides.Add(Vector3.left);
            if (closedSides.Count == 0) continue;

            GameObject floorCell;
            if (!floorTiles.TryGetValue(cell.cell, out floorCell)) continue;
            if (HasTrapInCell(generatedParent, floorCell.transform.position, cellSize * 0.52f)) continue;

            for (int item = 0; item < propsForCell; item++)
            {
                GameObject prefab = decorativePropPrefabs[decorationIndex % decorativePropPrefabs.Length];
                if (prefab == null) continue;

                Vector3 side = closedSides[(decorationIndex + item) % closedSides.Count];
                Vector3 position = floorCell.transform.position + side * (cellSize * 0.16f);
                position.y = floorHeight;
                GameObject prop = Instantiate(prefab, position,
                    Quaternion.Euler(0f, (cell.cell.x * 37 + cell.cell.y * 53 + item * 71) % 360, 0f),
                    generatedParent);
                prop.name = "Generated Maze Prop " + (decorationIndex + 1);

                if (wallMaterial != null)
                {
                    foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterial = wallMaterial;
                }

                AlignPropToFloor(prop, floorHeight);
                decorationIndex++;
            }
        }
    }

    private bool HasTrapInCell(Transform generatedParent, Vector3 cellCenter, float radius)
    {
        float radiusSquared = radius * radius;
        for (int i = 0; i < generatedParent.childCount; i++)
        {
            Transform child = generatedParent.GetChild(i);
            if (!child.name.StartsWith("Generated Maze Trap -")) continue;

            Vector2 trapPosition = new Vector2(child.position.x, child.position.z);
            Vector2 centerPosition = new Vector2(cellCenter.x, cellCenter.z);
            if ((trapPosition - centerPosition).sqrMagnitude <= radiusSquared)
                return true;
        }

        return false;
    }

    private void AlignPropToFloor(GameObject prop, float floorHeight)
    {
        Renderer[] renderers = prop.GetComponentsInChildren<Renderer>(true);
        float lowestPoint = float.PositiveInfinity;
        foreach (Renderer renderer in renderers)
            lowestPoint = Mathf.Min(lowestPoint, renderer.bounds.min.y);

        if (!float.IsInfinity(lowestPoint))
            prop.transform.position += Vector3.up * (floorHeight - lowestPoint);
    }

    private GameObject CreateFloorTile(Vector3 position, Vector2 size, string objectName, Transform parent)
    {
        if (floorPrefab != null)
        {
            GameObject prefabTile = Instantiate(floorPrefab, position, Quaternion.identity, parent);
            prefabTile.name = objectName;
            BoxCollider prefabCollider = prefabTile.GetComponent<BoxCollider>();
            float yScale = 1f;
            if (prefabCollider != null)
                yScale = floorThickness / Mathf.Max(prefabCollider.size.y, 0.001f);

            prefabTile.transform.localScale = new Vector3(size.x * 0.5f, yScale, size.y * 0.5f);
            if (prefabCollider != null)
            {
                position.y += floorThickness * 0.5f -
                    (prefabCollider.center.y + prefabCollider.size.y * 0.5f) * yScale;
                prefabTile.transform.position = position;
            }

            if (floorMaterial != null)
            {
                foreach (Renderer renderer in prefabTile.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = floorMaterial;
            }

            return prefabTile;
        }

        GameObject tile = new GameObject(objectName);
        tile.transform.SetParent(parent, true);
        tile.transform.position = position;
        tile.transform.localScale = new Vector3(size.x * 0.5f, floorThickness * 0.5f, size.y * 0.5f);

        MeshFilter meshFilter = tile.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = floorMesh;
        MeshRenderer meshRenderer = tile.AddComponent<MeshRenderer>();
        if (floorMaterial != null) meshRenderer.sharedMaterial = floorMaterial;

        BoxCollider tileCollider = tile.AddComponent<BoxCollider>();
        tileCollider.size = Vector3.one * 2f;
        return tile;
    }

    private void PlaceWall(Vector3 segmentPosition, bool vertical, float segmentLength, float cellSize, Transform parent)
    {
        if (wallPrefab == null)
        {
            Debug.LogError("Assign a wall prefab to MazeEnvironmentBuilder.", this);
            return;
        }

        GameObject wall = Instantiate(wallPrefab, segmentPosition, Quaternion.identity, parent);
        MeshFilter meshFilter = wall.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("The assigned wall prefab must contain a mesh.", wall);
            if (Application.isPlaying) Destroy(wall);
            else DestroyImmediate(wall);
            return;
        }

        if (wallMaterial != null)
        {
            foreach (Renderer renderer in wall.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = wallMaterial;
        }

        Bounds meshBounds = meshFilter.sharedMesh.bounds;
        Vector3 baseSize = Vector3.Scale(meshBounds.size, meshFilter.transform.localScale);
        segmentLength = segmentLength > 0f ? segmentLength : cellSize;
        Vector3 targetSize = new Vector3(segmentLength, wallHeightScale * 2f, wallThickness);
        Vector3 scale = new Vector3(
            targetSize.x / Mathf.Max(baseSize.x, 0.001f),
            targetSize.y / Mathf.Max(baseSize.y, 0.001f),
            targetSize.z / Mathf.Max(baseSize.z, 0.001f));

        Quaternion rotation = vertical ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        Vector3 meshCenter = Vector3.Scale(meshBounds.center, meshFilter.transform.localScale);
        Vector3 desiredMeshCenter = segmentPosition + Vector3.up * (wallHeightScale + wallBaseOffset);
        wall.transform.rotation = rotation;
        wall.transform.localScale = scale;
        wall.transform.position = desiredMeshCenter - rotation * Vector3.Scale(meshCenter, scale);
        wall.name = "Generated Maze Wall";
    }

    private bool IsOuterWall(MazeWallSegment segment, int width, int height, float cellSize, Vector3 origin)
    {
        float halfMazeWidth = width * cellSize * 0.5f;
        float halfMazeDepth = height * cellSize * 0.5f;
        if (segment.vertical)
            return Mathf.Abs(Mathf.Abs(segment.position.x - origin.x) - halfMazeWidth) < 0.01f;

        return Mathf.Abs(Mathf.Abs(segment.position.z - origin.z) - halfMazeDepth) < 0.01f;
    }

    private void PlaceWallTorch(MazeWallSegment segment, int torchIndex, Transform parent)
    {
        float side = torchIndex % 2 == 0 ? 1f : -1f;
        Vector3 normal = (segment.vertical ? Vector3.right : Vector3.forward) * side;
        float yaw = segment.vertical ? (side > 0f ? 90f : -90f) : (side > 0f ? 0f : 180f);

        const float torchDepth = 0.2092f;
        Vector3 position = segment.position + normal * (wallThickness * 0.5f + torchDepth * 0.5f + 0.01f);
        position.y += wallHeightScale + wallBaseOffset + 0.35f;
        GameObject torch = Instantiate(wallTorchPrefab, position, Quaternion.Euler(0f, yaw, 0f), parent);
        torch.name = "Generated Maze Torch " + (torchIndex + 1);

        if (wallMaterial != null)
        {
            foreach (Renderer renderer in torch.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = wallMaterial;
        }

        GameObject lightObject = new GameObject("Torch Light");
        lightObject.transform.SetParent(torch.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.3f, 0.16f);
        Light torchLight = lightObject.AddComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.color = new Color(1f, 0.58f, 0.24f);
        torchLight.intensity = 2f;
        torchLight.range = 5f;
        torchLight.shadows = LightShadows.None;
    }

    private Vector3 GetExitOutward(Vector2Int cell, int width, int height)
    {
        if (cell.y == 0) return Vector3.forward;
        if (cell.y == height - 1) return Vector3.back;
        if (cell.x == 0) return Vector3.left;
        return Vector3.right;
    }

    private void ClearGeneratedObjects(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (!child.name.StartsWith("Generated Maze") && child.name != "MazeWall") continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
