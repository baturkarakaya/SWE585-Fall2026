using System.Collections;
using UnityEngine;

// Opens the exit and ends the round when the key physically hits the door.
public class MazeDoor : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float openDuration = 0.8f;
    [SerializeField] private float openAngle = 90f;

    private bool roundWon;
    private Transform hingePivot;

    private void Awake()
    {
        BoxCollider doorCollider = GetComponent<BoxCollider>();
        Vector3 localHinge = doorCollider == null
            ? Vector3.left * 0.5f
            : doorCollider.center + Vector3.left * (doorCollider.size.x * 0.5f);
        Vector3 hingePosition = transform.TransformPoint(localHinge);
        Transform originalParent = transform.parent;

        GameObject hinge = new GameObject(gameObject.name + " Hinge");
        hinge.transform.SetParent(originalParent, true);
        hinge.transform.SetPositionAndRotation(hingePosition, transform.rotation);
        transform.SetParent(hinge.transform, true);
        hingePivot = hinge.transform;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (roundWon || collision.collider.GetComponentInParent<MazeKey>() == null) return;

        roundWon = true;
        MazeRoundUI.BeginRoundEnd();
        StartCoroutine(OpenDoorAndFinishRound());
    }

    private IEnumerator OpenDoorAndFinishRound()
    {
        Quaternion closedRotation = hingePivot.rotation;
        Quaternion swing = Quaternion.AngleAxis(openAngle, Vector3.up);
        Quaternion openRotation = swing * closedRotation;
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            hingePivot.rotation = Quaternion.Slerp(closedRotation, openRotation, elapsed / openDuration);
            yield return null;
        }

        hingePivot.rotation = openRotation;
        MazeRoundUI.ShowWin();
    }
}
