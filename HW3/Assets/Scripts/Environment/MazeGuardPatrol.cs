using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Randomly patrols only through connected maze passages using a coroutine.
public class MazeGuardPatrol : MonoBehaviour
{
    private Dictionary<Vector2Int, List<Vector2Int>> passages;
    private Dictionary<Vector2Int, Vector3> cellPositions;
    private Vector2Int currentCell;
    private float floorY;
    private float moveSpeed;
    private float pauseAtCell;
    private System.Random random;

    public void Initialize(Dictionary<Vector2Int, List<Vector2Int>> mazePassages,
        Dictionary<Vector2Int, Vector3> worldPositions, Vector2Int startCell,
        float walkY, float speed, float pause, int seed, float cellSize)
    {
        passages = mazePassages;
        cellPositions = worldPositions;
        currentCell = startCell;
        floorY = walkY;
        moveSpeed = speed;
        pauseAtCell = pause;
        random = new System.Random(seed + GetInstanceID());

        CapsuleCollider oldTrigger = GetComponent<CapsuleCollider>();
        if (oldTrigger != null) oldTrigger.enabled = false;

        BoxCollider contactTrigger = GetComponent<BoxCollider>();
        if (contactTrigger == null) contactTrigger = gameObject.AddComponent<BoxCollider>();
        contactTrigger.isTrigger = true;
        contactTrigger.center = new Vector3(0f, 1f, 0f);
        Vector3 guardScale = transform.lossyScale;
        contactTrigger.size = new Vector3(
            cellSize * 0.75f / Mathf.Max(Mathf.Abs(guardScale.x), 0.01f),
            2f,
            cellSize * 0.75f / Mathf.Max(Mathf.Abs(guardScale.z), 0.01f));

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        if (Application.isPlaying)
            StartCoroutine(Patrol());
    }

    private IEnumerator Patrol()
    {
        Vector2Int previousCell = new Vector2Int(int.MinValue, int.MinValue);

        while (true)
        {
            List<Vector2Int> neighbors;
            if (!passages.TryGetValue(currentCell, out neighbors) || neighbors.Count == 0)
            {
                yield return null;
                continue;
            }

            List<Vector2Int> choices = new List<Vector2Int>();
            foreach (Vector2Int neighbor in neighbors)
                if (neighbor != previousCell || neighbors.Count == 1)
                    choices.Add(neighbor);

            Vector2Int nextCell = choices[random.Next(choices.Count)];
            Vector3 target = CellPosition(nextCell);
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            while ((transform.position - target).sqrMagnitude > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target,
                    moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = target;
            previousCell = currentCell;
            currentCell = nextCell;
            if (pauseAtCell > 0f) yield return new WaitForSeconds(pauseAtCell);
        }
    }

    private Vector3 CellPosition(Vector2Int cell)
    {
        Vector3 position = cellPositions[cell];
        position.y = floorY;
        return position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (MazeRoundUI.IsRoundEnding) return;

        Transform player = other.transform;
        while (player != null && !player.CompareTag("Player"))
            player = player.parent;

        if (player != null)
            MazeRoundUI.KillPlayer(player, "The Minotaur caught you.");
    }

}
