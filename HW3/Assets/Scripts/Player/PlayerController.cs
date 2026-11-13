using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public Rigidbody rigidBody;
    public float forceApplied = 5f;
    [SerializeField] private Transform movementCamera;

    private void Awake()
    {
        if (movementCamera == null && Camera.main != null)
            movementCamera = Camera.main.transform;
    }

    private void FixedUpdate()
    {
        Vector2 userInput = Vector2.zero;

        if (Keyboard.current == null) return;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            userInput.y += 1;
        }

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            userInput.x -= 1;
        }

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            userInput.y -= 1;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            userInput.x += 1;
        }

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (movementCamera != null)
        {
            forward = Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(movementCamera.right, Vector3.up).normalized;
        }

        Vector3 force = (right * userInput.x + forward * userInput.y).normalized;

        if (rigidBody != null)
            rigidBody.AddForce(force * forceApplied);
    }
}
