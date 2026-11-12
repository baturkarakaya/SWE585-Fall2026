using UnityEngine;
using UnityEngine.SceneManagement;

// Shows a full-screen result panel and lets the player restart the round.
public class MazeRoundUI : MonoBehaviour
{
    private static MazeRoundUI instance;
    private static bool roundEnding;

    private bool showPanel;
    private bool playerWon;
    private string deathMessage = "You are dead. The slime got you.";

    public static bool IsRoundEnding
    {
        get { return roundEnding; }
    }

    private void Awake()
    {
        instance = this;
        roundEnding = false;
        showPanel = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void BeginRoundEnd()
    {
        roundEnding = true;
    }

    public static void ShowWin()
    {
        ShowResult(true);
    }

    public static void ShowDeath()
    {
        ShowDeath("You are dead. The slime got you.");
    }

    public static void ShowDeath(string message)
    {
        if (instance == null) instance = FindObjectOfType<MazeRoundUI>();
        if (instance != null) instance.deathMessage = message;
        ShowResult(false);
    }

    public static void KillPlayer(Transform player, string message)
    {
        if (player == null || roundEnding) return;

        BeginRoundEnd();
        foreach (CharacterController controller in player.GetComponentsInChildren<CharacterController>())
            controller.enabled = false;

        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>())
        {
            string componentName = behaviour.GetType().Name;
            if (componentName == "PlayerController" || componentName == "ThirdPersonController" ||
                componentName == "FirstPersonController")
                behaviour.enabled = false;
        }

        ShowDeath(message);
    }

    private static void ShowResult(bool won)
    {
        if (instance == null) instance = FindObjectOfType<MazeRoundUI>();
        if (instance == null)
        {
            Debug.LogError("MazeRoundUI is missing from the scene.");
            return;
        }

        instance.playerWon = won;
        instance.showPanel = true;
        roundEnding = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        if (!showPanel) return;

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.84f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        string title = playerWon ? "YOU ESCAPED" : "WASTED";
        string message = playerWon ? "The key opened the wooden door." : deathMessage;
        Color titleColor = playerWon ? new Color(0.4f, 1f, 0.55f) : new Color(1f, 0.15f, 0.15f);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.fontSize = Mathf.Clamp(Screen.width / 11, 56, 112);
        titleStyle.normal.textColor = titleColor;
        GUI.Label(new Rect(0f, Screen.height * 0.27f, Screen.width, Screen.height * 0.2f), title, titleStyle);

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.alignment = TextAnchor.MiddleCenter;
        messageStyle.fontSize = Mathf.Clamp(Screen.width / 38, 24, 40);
        messageStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.48f, Screen.width, 70f), message, messageStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.fontSize = Mathf.Clamp(Screen.width / 45, 26, 38);
        Rect buttonRect = new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.64f, 380f, 82f);
        if (GUI.Button(buttonRect, "PLAY AGAIN", buttonStyle))
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
