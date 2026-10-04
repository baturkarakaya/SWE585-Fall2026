using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputs : MonoBehaviour
{
    public Rigidbody rigidBody;
    public float forceApplied = 15f;

    void FixedUpdate()
    {
        Vector2 userInput = Vector2.zero;

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

        Vector3 force = new Vector3(userInput.x, 0, userInput.y);

        rigidBody.AddForce(force * forceApplied);
    }
}