using UnityEngine;

public class ApplyForce : MonoBehaviour
{
    public Rigidbody rigidBody;
    public float forceApplied = 1f;

    void Update()
    {
        rigidBody.AddForce(Vector3.forward * forceApplied);
    }
}