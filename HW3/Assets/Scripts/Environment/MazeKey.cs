using UnityEngine;
using UnityEngine.InputSystem;

// Lets the player pick up and carry the physics-based key, or drop it again.
public class MazeKey : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float interactionRange = 2.5f;
    [SerializeField] private Vector3 carryOffset = new Vector3(0f, 0.9f, 0.9f);

    private Rigidbody body;
    private GameObject player;
    private bool isHeld;

    public bool IsHeld
    {
        get { return isHeld; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (MazeRoundUI.IsRoundEnding) return;

        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        if (isHeld)
        {
            Drop();
        }
        else if (player != null && Vector3.Distance(player.transform.position, transform.position) <= interactionRange)
        {
            PickUp();
        }
    }

    private void FixedUpdate()
    {
        if (!isHeld || player == null || body == null) return;

        Vector3 targetPosition = player.transform.TransformPoint(carryOffset);
        body.MovePosition(targetPosition);
        body.MoveRotation(player.transform.rotation);
    }

    private void PickUp()
    {
        if (body == null) return;

        isHeld = true;
        SetPlayerCollisionIgnored(true);
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
    }

    private void Drop()
    {
        if (body == null) return;

        isHeld = false;
        if (player != null)
            body.position = player.transform.TransformPoint(carryOffset + new Vector3(0f, 0f, 0.35f));
        SetPlayerCollisionIgnored(false);
        body.constraints = RigidbodyConstraints.None;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
    }

    private void SetPlayerCollisionIgnored(bool ignored)
    {
        if (player == null) return;

        Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
        Collider[] keyColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider playerCollider in playerColliders)
        foreach (Collider keyCollider in keyColliders)
        {
            if (playerCollider != null && keyCollider != null)
                Physics.IgnoreCollision(playerCollider, keyCollider, ignored);
        }
    }

    private void OnGUI()
    {
        if (MazeRoundUI.IsRoundEnding) return;
        if (player == null) return;
        if (!isHeld && Vector3.Distance(player.transform.position, transform.position) > interactionRange) return;

        GUIStyle promptStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Screen.width / 48, 28, 42),
            fontStyle = FontStyle.Bold
        };
        string prompt = isHeld ? "E  Drop key" : "E  Pick up key";
        GUI.Box(new Rect(Screen.width * 0.5f - 210f, Screen.height - 115f, 420f, 76f), prompt, promptStyle);
    }
}
