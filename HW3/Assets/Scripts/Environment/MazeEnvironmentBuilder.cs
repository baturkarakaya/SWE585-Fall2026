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

    [Header("Floor and Water Assets")]
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

        foreach (MazeWallSegment segment in walls)
            PlaceWall(segment.position, segment.vertical, segment.length, cellSize, generatedParent);

        DrawFloorAndWater(width, height, cellSize, floorCells, exitCells, generatedParent);
        Debug.Log("Built maze environment with " + walls.Count + " wall segments.", this);
        return floorTiles;
    }

    private void DrawFloorAndWater(int width, int height, float cellSize,
        List<MazeFloorCellData> pathCells, List<Vector2Int> exitCells, Transform generatedParent)
    {
        float spawnPadSize = cellSize * 1.5f;
        float exitPlatformSize = cellSize * 6f;
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
            CreateFloorTile(entrancePadPosition, new Vector2(spawnPadSize, spawnPadSize),
                "Generated Maze Entrance Platform", generatedParent);

            foreach (Vector2Int exitCell in exitCells)
            {
                float centerX = (exitCell.x + 0.5f) * cellSize - width * cellSize * 0.5f;
                float centerZ = height * cellSize * 0.5f - (exitCell.y + 0.5f) * cellSize;
                Vector3 outward = GetExitOutward(exitCell, width, height);
                Vector3 padPosition = origin + new Vector3(centerX, floorY, centerZ);
                padPosition += outward * (cellSize * 0.5f + exitPlatformSize * 0.5f);
                CreateFloorTile(padPosition, new Vector2(exitPlatformSize, exitPlatformSize),
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

    private GameObject CreateFloorTile(Vector3 position, Vector2 size, string objectName, Transform parent)
    {
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
