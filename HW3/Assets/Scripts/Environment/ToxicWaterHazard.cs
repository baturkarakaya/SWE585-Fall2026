using System.Collections;
using UnityEngine;

// Stops the player and sinks them into the slime when they fall off the maze.
public class ToxicWaterHazard : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float sinkDistance = 2.5f;
    [SerializeField, Min(0.1f)] private float sinkDuration = 1.2f;

    private bool playerCaught;

    private void OnTriggerEnter(Collider other)
    {
        Transform player = other.transform;
        while (player != null && !player.CompareTag("Player"))
            player = player.parent;

        if (player == null || playerCaught) return;
        playerCaught = true;

        foreach (CharacterController controller in player.GetComponentsInChildren<CharacterController>())
            controller.enabled = false;

        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>())
        {
            string componentName = behaviour.GetType().Name;
            if (componentName == "PlayerController" || componentName == "ThirdPersonController" ||
                componentName == "FirstPersonController")
                behaviour.enabled = false;
        }

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        StartCoroutine(SinkPlayer(player));
    }

    private IEnumerator SinkPlayer(Transform player)
    {
        Vector3 startPosition = player.position;
        Vector3 endPosition = startPosition + Vector3.down * sinkDistance;
        float elapsed = 0f;

        while (elapsed < sinkDuration && player != null)
        {
            elapsed += Time.deltaTime;
            player.position = Vector3.Lerp(startPosition, endPosition, elapsed / sinkDuration);
            yield return null;
        }
    }
}
