using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Range(0f, 1f)] public float coinPickupVolume = 0.04f;
    [Range(0f, 1f)] public float clickVolume = 1f;

    [Header("Music Clips")]
    public AudioClip menuMusic;

    public static AudioManager GetInstance()
    {
        if (Instance != null) { return Instance; }

        Instance = FindAnyObjectByType<AudioManager>();
        if (Instance != null) { return Instance; }

        return EnsureExists();
    }

    public static AudioManager EnsureExists()
    {
        if (Instance != null) { return Instance; }

        var go = new GameObject("AudioManager");
        Instance = go.AddComponent<AudioManager>();
        return Instance;
    }

    private void Awake()
    {
        EnsureAudioSources();
        EnsureClipsFromResources();

        if (Instance != null && Instance != this)
        {
            // Runtime fallback từ EnsureExists() — thay bằng instance scene (có clip gán sẵn).
            if (Instance.gunShot == null && gunShot != null)
                Destroy(Instance.gameObject);
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplySavedVolumes();
        PreloadSfxClips();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == ClientSessionOverlay.MenuSceneName)
            PlayMusic(menuMusic);

        if (scene.name == "Game")
        {
            EnsureExists();
            EnsureClipsFromResources();
            if (musicSource != null)
                musicSource.Stop();
            ApplySavedVolumes();
        }

        AttachClickSoundToAllButtons();
    }

    private void Start()
    {
        ApplySavedVolumes();
        if (SceneManager.GetActiveScene().name == ClientSessionOverlay.MenuSceneName)
            PlayMusic(menuMusic);
        AttachClickSoundToAllButtons();
    }

    private void EnsureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (musicSource == null)
        {
            musicSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        musicSource.spatialBlend = 0f;
        sfxSource.spatialBlend = 0f;
    }

    private void EnsureClipsFromResources()
    {
        if (gunShot == null)     gunShot = Resources.Load<AudioClip>("SFX/tankshot");
        if (coinPickup == null)  coinPickup = Resources.Load<AudioClip>("SFX/pick_coin");
        if (clickSound == null)  clickSound = Resources.Load<AudioClip>("SFX/click-ui");
        PreloadSfxClips();
    }

    private void ApplySavedVolumes()
    {
        if (musicSource != null)
            musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        if (sfxSource != null)
            sfxSource.volume = PlayerPrefs.GetFloat("SfxVolume", 1f);
    }

    public void PlayGunshot() => PlaySFX(gunShot, gunShotVolume);
    public void PlayCoinPickup() => PlaySFX(coinPickup, coinPickupVolume);

    private void PreloadSfxClips()
    {
        if (clickSound != null && clickSound.loadState == AudioDataLoadState.Unloaded)
            clickSound.LoadAudioData();
    }

    private void AttachClickSoundToAllButtons()
    {
        Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button btn in allButtons)
        {
            if (btn == null || btn.gameObject.scene.name == null) { continue; }

            // Gỡ cách cũ (onClick chạy sau handler nút → cảm giác delay).
            btn.onClick.RemoveListener(PlayClick);

            if (btn.GetComponent<ButtonClickSfx>() == null)
                btn.gameObject.AddComponent<ButtonClickSfx>();
        }
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) { return; }
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayClick() => PlaySFX(clickSound, clickVolume);

    public void PlayMusic(AudioClip musicClip)
    {
        if (musicClip == null || musicSource == null) { return; }
        if (musicSource.clip == musicClip && musicSource.isPlaying) { return; }

        musicSource.clip = musicClip;
        musicSource.Play();
    }

    public void PlayWinMusic() => PlayMusic(winMusic);
    public void PlayLoseMusic() => PlayMusic(loseMusic);
    public void StopMusic() => musicSource.Stop();
}
