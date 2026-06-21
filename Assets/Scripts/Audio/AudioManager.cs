using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip gunShot;
    public AudioClip coinPickup;
    public AudioClip clickSound;

    [Header("Win/Lose Music")]
    public AudioClip winMusic;
    public AudioClip loseMusic;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float gunShotVolume = 0.7f;
    [Range(0f, 1f)] public float coinPickupVolume = 0.075f;
    [Range(0f, 1f)] public float clickVolume = 1f;

    [Header("Music Clips")]
    public AudioClip menuMusic;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Kiểm tra tên scene để tự động phát nhạc menu
        if (scene.name == ClientSessionOverlay.MenuSceneName)
        {
            PlayMusic(menuMusic);
        }
    }

    private void Start()
    {
        // Load cài đặt âm lượng từ PlayerPrefs
        musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        sfxSource.volume = PlayerPrefs.GetFloat("SfxVolume", 1f);
        PlayMusic(menuMusic);
    }

    // --- Các hàm điều khiển Âm thanh ---

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayClick() => PlaySFX(clickSound, clickVolume);

    public void PlayMusic(AudioClip musicClip)
    {
        if (musicClip == null || musicSource == null) return;
        if (musicSource.clip == musicClip && musicSource.isPlaying) return;

        musicSource.clip = musicClip;
        musicSource.Play();
    }

    // Hàm gọi khi thắng
    public void PlayWinMusic() => PlayMusic(winMusic);

    // Hàm gọi khi thua
    public void PlayLoseMusic() => PlayMusic(loseMusic);

    public void StopMusic() => musicSource.Stop();
}