using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class SoundController : MonoBehaviour
{
    [SerializeField] private AudioClip backgroundMusic;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = backgroundMusic;
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        if (backgroundMusic != null) audioSource.Play();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.mKey.wasPressedThisFrame) return;
        if (audioSource.isPlaying) audioSource.Pause();
        else audioSource.UnPause();
    }
}
