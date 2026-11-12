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
}

// Selects safe maze cells and creates the different trap prefabs there.
public class MazeTrapSpawner : MonoBehaviour
{
    [Header("Trap Prefabs")]
    [SerializeField] private GameObject spikeTrapPrefab;
    [SerializeField] private GameObject trapDoorPrefab;
    [SerializeField] private Material trapMaterial;

    [Header("Trap Count")]
    [SerializeField, Min(1)] private int trapsPerType = 2;
    [SerializeField, Min(1)] private int preferredCellSpacing = 4;

    [Header("Trap Timing")]
    [SerializeField, Min(0.1f)] private float safeDuration = 2.5f;
    [SerializeField, Min(0.1f)] private float dangerDuration = 1.25f;
    [SerializeField, Min(0.1f)] private float animationLeadTime = 0.75f;

    public void SpawnTraps(List<MazeTrapSpawnSite> openCells, Vector2Int playerStartCell,
        Vector2Int keyExitCell, Vector2Int doorExitCell, float cellSize, float floorHeight,
        int randomSeed, Transform generatedParent)
    {
        if (spikeTrapPrefab == null || trapDoorPrefab == null)
        {
            Debug.LogWarning("Assign the spike and trap door prefabs to MazeTrapSpawner.", this);
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
            CreateTrap(GetPrefab(type), selectedSites[i], trapName, type, scale, corridorWidth,
                floorHeight, generatedParent);
        }
    }

    private List<MazeTrapSpawnSite> SelectTrapSites(List<MazeTrapSpawnSite> openCells,
        Vector2Int playerStartCell, Vector2Int keyExitCell, Vector2Int doorExitCell,
        int requestedCount, int randomSeed)
    {
        List<MazeTrapSpawnSite> straightCorridors = new List<MazeTrapSpawnSite>();
        List<MazeTrapSpawnSite> turningCorridors = new List<MazeTrapSpawnSite>();

        foreach (MazeTrapSpawnSite site in openCells)
        {
            // Two open sides identify a corridor. Junctions and dead ends are skipped.
            if (site.openPassages != 2) continue;
            if (ManhattanDistance(site.cell, playerStartCell) < 3) continue;
            if (ManhattanDistance(site.cell, keyExitCell) < 2) continue;
            if (ManhattanDistance(site.cell, doorExitCell) < 2) continue;

            if (site.isStraightCorridor) straightCorridors.Add(site);
            else turningCorridors.Add(site);
        }

        System.Random random = new System.Random(randomSeed + 1337);
        Shuffle(straightCorridors, random);
        Shuffle(turningCorridors, random);

        List<MazeTrapSpawnSite> selected = SelectSpacedSites(straightCorridors, requestedCount);
        if (selected.Count < requestedCount)
        {
            // Bends are a fallback only; traps are never placed at intersections.
            selected.AddRange(SelectSpacedSites(turningCorridors, requestedCount - selected.Count, selected));
        }

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

        for (int spacing = startingSpacing; spacing >= 2 && selected.Count < requestedCount; spacing--)
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

    private void CreateTrap(GameObject prefab, MazeTrapSpawnSite site, string trapName,
        MazeTrapType type, Vector3 scale, float cellSize, float floorHeight, Transform generatedParent)
    {
        Vector3 position = site.floorPosition;
        if (type == MazeTrapType.Spikes)
            position.y = floorHeight + 2.1f * scale.y;
        else
            position.y = floorHeight + 0.02f;

        GameObject staging = new GameObject("Maze Trap Setup");
        staging.SetActive(false);
        GameObject trap = Instantiate(prefab, position, Quaternion.identity, staging.transform);
        trap.name = trapName;
        trap.transform.localScale = scale;

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

        // The pack's demo scripts have their own timing. This component owns trap timing instead.
        foreach (MonoBehaviour behaviour in trap.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string behaviourName = behaviour.GetType().Name;
            if (behaviourName == "SpikeTrapDemo" || behaviourName == "TrapDoorDemo")
                behaviour.enabled = false;
        }

        if (type == MazeTrapType.Spikes || type == MazeTrapType.TrapDoor)
            AlignVisualBottomWithFloor(trap, floorHeight);

        MazePeriodicTrap periodicTrap = trap.AddComponent<MazePeriodicTrap>();
        periodicTrap.Initialize(type, site.floorTile, cellSize, floorHeight,
            safeDuration, dangerDuration, animationLeadTime);

        trap.transform.SetParent(generatedParent, true);
        if (Application.isPlaying) Destroy(staging);
        else DestroyImmediate(staging);
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
        if (type == MazeTrapType.Spikes)
            return Vector3.one * (0.65f * cellScale);
        return Vector3.one * (0.5f * cellScale);
    }

    private GameObject GetPrefab(MazeTrapType type)
    {
        if (type == MazeTrapType.Spikes) return spikeTrapPrefab;
        return trapDoorPrefab;
    }

    private string GetTrapName(MazeTrapType type)
    {
        if (type == MazeTrapType.Spikes) return "Spikes";
        return "Trap Door";
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
