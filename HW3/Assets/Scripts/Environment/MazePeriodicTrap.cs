using System.Collections;
using UnityEngine;

public enum MazeTrapType
{
    Spikes,
    TrapDoor
}

// Runs a repeating coroutine that opens a short danger window for each trap.
public class MazePeriodicTrap : MonoBehaviour
{
    private MazeTrapType trapType;
    private GameObject floorTile;
    private float safeDuration;
    private float dangerDuration;
    private float animationLeadTime;
    private Vector3 trapScale;
    private Animator trapAnimator;
    private BoxCollider dangerTrigger;
    private Renderer[] floorRenderers;
    private Collider[] floorColliders;
    private bool initialized;
    private Coroutine cycleRoutine;

    public void Initialize(MazeTrapType type, GameObject tile, float cellSize, float floorHeight,
        float safeTime, float dangerTime, float leadTime)
    {
        trapType = type;
        floorTile = tile;
        safeDuration = safeTime;
        dangerDuration = dangerTime;
        animationLeadTime = leadTime;
        trapScale = transform.lossyScale;
        trapScale.x = Mathf.Max(Mathf.Abs(trapScale.x), 0.01f);
        trapScale.y = Mathf.Max(Mathf.Abs(trapScale.y), 0.01f);
        trapScale.z = Mathf.Max(Mathf.Abs(trapScale.z), 0.01f);
        trapAnimator = GetComponentInChildren<Animator>(true);

        dangerTrigger = GetComponent<BoxCollider>();
        if (dangerTrigger == null) dangerTrigger = gameObject.AddComponent<BoxCollider>();
        dangerTrigger.isTrigger = true;
        dangerTrigger.size = new Vector3(
            cellSize * 0.9f / trapScale.x,
            2.2f / trapScale.y,
            cellSize * 0.9f / trapScale.z);

        float hazardCenterHeight = 0.9f;
        if (trapType == MazeTrapType.TrapDoor)
            hazardCenterHeight = 0.75f;

        float prefabRootHeight = transform.position.y - floorHeight;
        dangerTrigger.center = new Vector3(0f,
            (hazardCenterHeight - prefabRootHeight) / trapScale.y, 0f);
        dangerTrigger.enabled = false;

        Rigidbody trapBody = GetComponent<Rigidbody>();
        if (trapBody == null) trapBody = gameObject.AddComponent<Rigidbody>();
        trapBody.isKinematic = true;
        trapBody.useGravity = false;

        if (floorTile != null)
        {
            floorRenderers = floorTile.GetComponentsInChildren<Renderer>(true);
            floorColliders = floorTile.GetComponentsInChildren<Collider>(true);
        }

        initialized = true;
    }

    private void OnEnable()
    {
        if (initialized && cycleRoutine == null)
            cycleRoutine = StartCoroutine(RunTrapCycle());
    }

    private void OnDisable()
    {
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }
    }

    private IEnumerator RunTrapCycle()
    {
        float stagger = Mathf.Abs(GetInstanceID() % 10) * 0.12f;
        yield return new WaitForSeconds(stagger);

        while (!MazeRoundUI.IsRoundEnding)
        {
            yield return new WaitForSeconds(safeDuration);
            if (MazeRoundUI.IsRoundEnding) break;

            SetAnimatorTrigger("open");
            yield return new WaitForSeconds(animationLeadTime);

            if (trapType == MazeTrapType.TrapDoor)
                SetFloorEnabled(false);

            dangerTrigger.enabled = true;
            yield return new WaitForSeconds(dangerDuration);
            dangerTrigger.enabled = false;
            SetAnimatorTrigger("close");

            if (trapType == MazeTrapType.TrapDoor)
            {
                yield return new WaitForSeconds(animationLeadTime);
                SetFloorEnabled(true);
            }
            else
            {
                yield return new WaitForSeconds(animationLeadTime);
            }
        }
    }

    private void SetAnimatorTrigger(string triggerName)
    {
        if (trapAnimator == null) return;

        foreach (AnimatorControllerParameter parameter in trapAnimator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                trapAnimator.SetTrigger(triggerName);
                return;
            }
        }
    }

    private void SetFloorEnabled(bool enabled)
    {
        if (floorRenderers != null)
        {
            foreach (Renderer floorRenderer in floorRenderers)
                if (floorRenderer != null) floorRenderer.enabled = enabled;
        }

        if (floorColliders != null)
        {
            foreach (Collider floorCollider in floorColliders)
                if (floorCollider != null) floorCollider.enabled = enabled;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!dangerTrigger.enabled || MazeRoundUI.IsRoundEnding) return;

        Transform player = other.transform;
        while (player != null && !player.CompareTag("Player"))
            player = player.parent;

        if (player != null)
            MazeRoundUI.KillPlayer(player, GetDeathMessage());
    }

    private string GetDeathMessage()
    {
        if (trapType == MazeTrapType.Spikes)
            return "You were impaled by the spikes.";
        return "The trap door swallowed you.";
    }
}
