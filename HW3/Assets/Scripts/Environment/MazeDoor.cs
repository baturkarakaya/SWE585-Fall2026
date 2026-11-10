using System.Collections;
using UnityEngine;

// Opens the exit and ends the round when the key physically hits the door.
public class MazeDoor : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float openDuration = 0.8f;
    [SerializeField] private float openAngle = 80f;

    private bool roundWon;

    private void OnCollisionEnter(Collision collision)
    {
        if (roundWon || collision.collider.GetComponentInParent<MazeKey>() == null) return;

        roundWon = true;
        MazeRoundUI.BeginRoundEnd();
        StartCoroutine(OpenDoorAndFinishRound());
    }

    private IEnumerator OpenDoorAndFinishRound()
    {
        Quaternion closedRotation = transform.rotation;
        Quaternion openRotation = Quaternion.AngleAxis(openAngle, Vector3.up) * closedRotation;
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(closedRotation, openRotation, elapsed / openDuration);
            yield return null;
        }

        transform.rotation = openRotation;
        MazeRoundUI.ShowWin();
    }
}
