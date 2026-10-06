using UnityEngine;
using UnityEngine.InputSystem;

public class SoundController : MonoBehaviour
{
    public AudioSource audioSource;

    void Update()
    {
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
            else
            {
                audioSource.UnPause();
            }
        }
    }
}
