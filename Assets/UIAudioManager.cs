using UnityEngine;

public class UIAudioManager : MonoBehaviour
{
    public static UIAudioManager Instance;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickSound;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlayClick()
    {
        if (audioSource != null && clickSound != null)
        {
            // Tắt chế độ Mute nếu vô tình bị bật
            audioSource.mute = false;
            audioSource.volume = 1.0f;

            // Dùng PlayOneShot là cách ổn định nhất cho UI
            audioSource.PlayOneShot(clickSound);

            Debug.Log("Đã phát âm thanh click thành công!");
        }
        else
        {
            Debug.LogError("Kiểm tra lại AudioSource hoặc AudioClip trong UIAudioManager!");
        }
    }
}