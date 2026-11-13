using System.Collections.Generic;
using UnityEngine;

// Information the maze generator passes to the trap spawner for each floor cell.
public struct MazeTrapSpawnSite
{
    public Vector2Int cell;
    public Vector3 floorPosition;
    public GameObject floorTile;
    public int openPassages;
    public bool isStraightCorridor;
    public bool northOpen;
    public bool southOpen;
    public bool eastOpen;
    public bool westOpen;
}

// Selects safe maze cells and creates the different trap prefabs there.
public class MazeTrapSpawner : MonoBehaviour
{
    [Header("Trap Prefabs")]
    [SerializeField] private GameObject floorFireTrapPrefab;
    [SerializeField] private GameObject floorFireVfxPrefab;
    [SerializeField] private GameObject arrowTrapPrefab;
    [SerializeField] private GameObject arrowProjectilePrefab;
    [SerializeField] private Material trapMaterial;

    [Header("Trap Count")]
    [SerializeField, Min(1)] private int trapsPerType = 5;
    [SerializeField, Min(1)] private int preferredCellSpacing = 4;

    [Header("Trap Timing")]
    [SerializeField, Min(0.1f)] private float safeDuration = 2f;
    [SerializeField, Min(0.1f)] private float dangerDuration = 1.25f;
    [SerializeField, Min(0.1f)] private float animationLeadTime = 0.75f;

    public void SpawnTraps(List<MazeTrapSpawnSite> openCells, List<MazeWallSegment> wallSegments,
        Vector2Int playerStartCell,
        Vector2Int keyExitCell, Vector2Int doorExitCell, float cellSize, float wallThickness,
        float floorHeight,
        int randomSeed, Transform generatedParent)
    {
        if (floorFireTrapPrefab == null || floorFireVfxPrefab == null ||
            arrowTrapPrefab == null || arrowProjectilePrefab == null)
        {
            Debug.LogWarning("Assign the floor fire trap/VFX and arrow trap/projectile prefabs to MazeTrapSpawner.", this);
            return;
        }

        int requestedCount = trapsPerType * 2;
        List<MazeTrapSpawnSite> selectedSites = SelectTrapSites(openCells, playerStartCell,
            keyExitCell, doorExitCell, requestedCount, randomSeed);

        if (selectedSites.Count < requestedCount)
            Debug.LogWarning("MazeTrapSpawner found fewer safe locations than the requested trap count.", this);

        float corridorWidth = cellSize * 0.5f;
        for (int i = 0; i < selectedSites.Count; i++)
        {
            MazeTrapType type = (MazeTrapType)(i % 2);
            Vector3 scale = GetTrapScale(type, corridorWidth);
            string trapName = "Generated Maze Trap - " + GetTrapName(type) + " " + (i + 1);
            Vector3 arrowDirection = type == MazeTrapType.Arrow
                ? GetArrowDirection(selectedSites[i])
                : Vector3.zero;
            CreateTrap(GetPrefab(type), selectedSites[i], wallSegments, trapName, type, scale,
                corridorWidth, cellSize, wallThickness, arrowDirection, floorHeight, generatedParent);
        }
    }

    private List<MazeTrapSpawnSite> SelectTrapSites(List<MazeTrapSpawnSite> openCells,
        Vector2Int playerStartCell, Vector2Int keyExitCell, Vector2Int doorExitCell,
        int requestedCount, int randomSeed)
    {
        List<MazeTrapSpawnSite> straightCorridors = new List<MazeTrapSpawnSite>();
        List<MazeTrapSpawnSite> turningCorridors = new List<MazeTrapSpawnSite>();
        List<MazeTrapSpawnSite> otherSafeCells = new List<MazeTrapSpawnSite>();

        foreach (MazeTrapSpawnSite site in openCells)
        {
            if (ManhattanDistance(site.cell, playerStartCell) < 3) continue;
            if (ManhattanDistance(site.cell, keyExitCell) < 2) continue;
            if (ManhattanDistance(site.cell, doorExitCell) < 2) continue;

            if (site.openPassages == 2)
            {
                if (site.isStraightCorridor) straightCorridors.Add(site);
                else turningCorridors.Add(site);
            }
            else
            {
                otherSafeCells.Add(site);
            }
        }

        System.Random random = new System.Random(randomSeed + 1337);
        Shuffle(straightCorridors, random);
        Shuffle(turningCorridors, random);

        List<MazeTrapSpawnSite> selected = SelectSpacedSites(straightCorridors, requestedCount);
        if (selected.Count < requestedCount)
        {
            selected.AddRange(SelectSpacedSites(turningCorridors, requestedCount - selected.Count, selected));
        }
        if (selected.Count < requestedCount)
            selected.AddRange(SelectSpacedSites(otherSafeCells, requestedCount - selected.Count, selected));

        return selected;
    }

    private void Shuffle(List<MazeTrapSpawnSite> sites, System.Random random)
    {
        for (int i = sites.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            MazeTrapSpawnSite temporary = sites[i];
            sites[i] = sites[swapIndex];
            sites[swapIndex] = temporary;
        }
    }

    private List<MazeTrapSpawnSite> SelectSpacedSites(List<MazeTrapSpawnSite> candidates,
        int requestedCount, List<MazeTrapSpawnSite> alreadySelected = null)
    {
        List<MazeTrapSpawnSite> selected = new List<MazeTrapSpawnSite>();
        int startingSpacing = Mathf.Max(2, preferredCellSpacing);

        for (int spacing = startingSpacing; spacing >= 1 && selected.Count < requestedCount; spacing--)
        {
            foreach (MazeTrapSpawnSite candidate in candidates)
            {
                if (selected.Count >= requestedCount) break;

                bool farEnough = true;
                if (alreadySelected != null)
                {
                    foreach (MazeTrapSpawnSite existing in alreadySelected)
                    {
                        if (ManhattanDistance(candidate.cell, existing.cell) < spacing)
                        {
                            farEnough = false;
                            break;
                        }
                    }
                }

                if (farEnough)
                {
                    foreach (MazeTrapSpawnSite existing in selected)
                    {
                        if (ManhattanDistance(candidate.cell, existing.cell) < spacing)
                        {
                            farEnough = false;
                            break;
                        }
                    }
                }

                if (farEnough) selected.Add(candidate);
            }
        }

        return selected;
    }

    private void CreateTrap(GameObject prefab, MazeTrapSpawnSite site,
        List<MazeWallSegment> wallSegments, string trapName,
        MazeTrapType type, Vector3 scale, float corridorWidth, float mazeCellSize,
        float wallThickness,
        Vector3 arrowDirection,
        float floorHeight, Transform generatedParent)
    {
        Vector3 position = site.floorPosition;
        if (type == MazeTrapType.FloorFire)
            position.y = floorHeight;
        else
        {
            position = FindArrowMountPosition(site, wallSegments, arrowDirection,
                wallThickness, scale, mazeCellSize, floorHeight);
        }

        GameObject staging = new GameObject("Maze Trap Setup");
        staging.SetActive(false);
        GameObject trap = Instantiate(prefab, position, Quaternion.identity, staging.transform);
        trap.name = trapName;
        trap.transform.localScale = scale;
        if (type == MazeTrapType.Arrow)
            trap.transform.rotation = Quaternion.LookRotation(arrowDirection, Vector3.up);

        // Replace the pack's built-in Standard-shader material with a URP-compatible one.
        if (trapMaterial != null)
        {
            foreach (Renderer renderer in trap.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = trapMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        if (type == MazeTrapType.FloorFire)
            AlignVisualBottomWithFloor(trap, floorHeight);

        MazePeriodicTrap periodicTrap = trap.AddComponent<MazePeriodicTrap>();
        periodicTrap.Initialize(type, corridorWidth, floorHeight, arrowDirection,
            // Include the launch offset and arrow length so its tip remains inside this cell.
            Mathf.Max(0.5f, mazeCellSize - 1.6f),
            arrowProjectilePrefab, floorFireVfxPrefab, trapMaterial,
            safeDuration, dangerDuration, animationLeadTime);

        trap.transform.SetParent(generatedParent, true);
        if (Application.isPlaying) Destroy(staging);
        else DestroyImmediate(staging);
    }

    private Vector3 FindArrowMountPosition(MazeTrapSpawnSite site,
        List<MazeWallSegment> wallSegments, Vector3 shotDirection, float wallThickness,
        Vector3 trapScale, float mazeCellSize, float floorHeight)
    {
        bool lookForVerticalWall = Mathf.Abs(shotDirection.x) > 0.5f;
        Vector3 towardWall = -shotDirection;
        MazeWallSegment? closestWall = null;
        float closestDistance = float.PositiveInfinity;

        foreach (MazeWallSegment wall in wallSegments)
        {
            if (wall.vertical != lookForVerticalWall) continue;

            float alongWallDistance = lookForVerticalWall
                ? Mathf.Abs(wall.position.z - site.floorPosition.z)
                : Mathf.Abs(wall.position.x - site.floorPosition.x);
            if (alongWallDistance > wall.length * 0.5f + 0.1f) continue;

            float distance = Vector3.Dot(wall.position - site.floorPosition, towardWall);
            if (distance <= 0f || distance >= closestDistance) continue;

            closestDistance = distance;
            closestWall = wall;
        }

        if (!closestWall.HasValue)
        {
            // Keep a safe fallback in case a custom maze layout has no wall segment on that side.
            Vector3 fallback = site.floorPosition - shotDirection * (mazeCellSize * 0.5f - 0.55f);
            fallback.y = floorHeight + 1.05f;
            return fallback;
        }

        BoxCollider launcherCollider = arrowTrapPrefab == null
            ? null
            : arrowTrapPrefab.GetComponent<BoxCollider>();
        float launcherDepth = launcherCollider == null
            ? 0.18f
            : launcherCollider.size.z * Mathf.Abs(trapScale.z);

        Vector3 mountPosition = closestWall.Value.position + shotDirection *
            (wallThickness * 0.5f + launcherDepth * 0.5f);
        mountPosition.y = floorHeight + 1.05f;
        return mountPosition;
    }

    private void AlignVisualBottomWithFloor(GameObject trap, float floorHeight)
    {
        float lowestPoint = float.PositiveInfinity;
        MeshFilter[] meshFilters = trap.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null) continue;
            Bounds bounds = meshFilter.sharedMesh.bounds;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 localPoint = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                float pointY = meshFilter.transform.TransformPoint(localPoint).y;
                if (pointY < lowestPoint) lowestPoint = pointY;
            }
        }

        if (!float.IsInfinity(lowestPoint))
            trap.transform.position += Vector3.up * (floorHeight - lowestPoint);
    }

    private Vector3 GetTrapScale(MazeTrapType type, float cellSize)
    {
        float cellScale = cellSize / 2f;
        if (type == MazeTrapType.FloorFire)
            return Vector3.one * (0.7f * cellScale);
        return Vector3.one * (1.4f / 0.8276f);
    }

    private Vector3 GetArrowDirection(MazeTrapSpawnSite site)
    {
        int sideParity = (site.cell.x + site.cell.y) & 1;
        if (site.eastOpen != site.westOpen)
            return site.eastOpen ? Vector3.right : Vector3.left;
        if (site.northOpen != site.southOpen)
            return site.northOpen ? Vector3.forward : Vector3.back;

        // A straight corridor has both directions open along its length, so
        // fire across it from alternating side walls.
        if (site.northOpen && site.southOpen)
            return sideParity == 0 ? Vector3.right : Vector3.left;
        return sideParity == 0 ? Vector3.forward : Vector3.back;
    }

    private GameObject GetPrefab(MazeTrapType type)
    {
        if (type == MazeTrapType.FloorFire) return floorFireTrapPrefab;
        return arrowTrapPrefab;
    }

    private string GetTrapName(MazeTrapType type)
    {
        if (type == MazeTrapType.FloorFire) return "Floor Fire";
        return "Arrow";
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
