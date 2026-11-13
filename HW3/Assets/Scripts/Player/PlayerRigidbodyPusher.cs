using UnityEngine;

// Allows the CharacterController player to push dynamic Rigidbody objects.
[RequireComponent(typeof(CharacterController))]
public class PlayerRigidbodyPusher : MonoBehaviour
{
    [SerializeField, Min(0f)] private float pushPower = 5f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        MazeKey mazeKey = hit.collider.GetComponentInParent<MazeKey>();
        if (mazeKey != null && mazeKey.IsHeld) return;

        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;

        Vector3 pushDirection = hit.moveDirection;
        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude < 0.001f) return;

        body.AddForce(pushDirection.normalized * pushPower, ForceMode.Force);
    }
}
