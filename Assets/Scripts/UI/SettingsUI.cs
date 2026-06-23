using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public GameObject settingsPanel;
    [SerializeField] private GameObject[] menuElementsToHide;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private TutorialUI tutorialUI;
    private GameObject applySuccessToast;
    private Coroutine hideToastCoroutine;
    private float appliedMusicVolume = 1f;
    private float appliedSfxVolume = 1f;
    private float pendingMusicVolume = 1f;
    private float pendingSfxVolume = 1f;

    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SfxVolume";

    private void Awake()
    {
        tutorialUI = GetComponent<TutorialUI>();

        appliedMusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        appliedSfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        pendingMusicVolume = appliedMusicVolume;
        pendingSfxVolume = appliedSfxVolume;
        ApplyVolumesToAudio(appliedMusicVolume, appliedSfxVolume);

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
    }

    public void OpenSettings()
    {
        pendingMusicVolume = appliedMusicVolume;
        pendingSfxVolume = appliedSfxVolume;
        SyncSliders();
        SetMenuElementsActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        tutorialUI?.CloseTutorial();
        settingsPanel.SetActive(false);
        SetMenuElementsActive(true);
    }

    public void ApplySettings()
    {
        appliedMusicVolume = pendingMusicVolume;
        appliedSfxVolume = pendingSfxVolume;
        PlayerPrefs.SetFloat(MusicVolumeKey, appliedMusicVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, appliedSfxVolume);
        PlayerPrefs.Save();
        ApplyVolumesToAudio(appliedMusicVolume, appliedSfxVolume);
        ShowApplySuccessToast();
    }

    public void CancelSettings()
    {
        pendingMusicVolume = appliedMusicVolume;
        pendingSfxVolume = appliedSfxVolume;
        SyncSliders();
        ApplyVolumesToAudio(appliedMusicVolume, appliedSfxVolume);
    }

    public void GoBack()
    {
        if (tutorialUI != null && tutorialUI.IsOpen)
        {
            tutorialUI.CloseTutorial();
            return;
        }

        CloseSettings();
    }

    public void OnMusicVolumeChanged(float value)
    {
        pendingMusicVolume = value;
        ApplyVolumesToAudio(pendingMusicVolume, pendingSfxVolume);
    }

    public void OnSfxVolumeChanged(float value)
    {
        pendingSfxVolume = value;
        ApplyVolumesToAudio(pendingMusicVolume, pendingSfxVolume);
    }

    private void SyncSliders()
    {
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(pendingMusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(pendingSfxVolume);
        }
    }

    private static void ApplyVolumesToAudio(float musicVolume, float sfxVolume)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) { return; }

        if (audio.musicSource != null)
        {
            audio.musicSource.volume = musicVolume;
        }

        if (audio.sfxSource != null)
        {
            audio.sfxSource.volume = sfxVolume;
        }
    }

    private void SetMenuElementsActive(bool active)
    {
        if (menuElementsToHide == null) { return; }

        foreach (GameObject element in menuElementsToHide)
        {
            if (element != null)
            {
                element.SetActive(active);
            }
        }
    }

    private void ShowApplySuccessToast()
    {
        EnsureApplySuccessToast();

        if (applySuccessToast == null) { return; }

        applySuccessToast.SetActive(true);

        if (hideToastCoroutine != null)
        {
            StopCoroutine(hideToastCoroutine);
        }

        hideToastCoroutine = StartCoroutine(HideApplySuccessToastAfterDelay(3f));
    }

    private void EnsureApplySuccessToast()
    {
        if (applySuccessToast != null || settingsPanel == null) { return; }

        applySuccessToast = new GameObject("ApplySuccessToast", typeof(RectTransform));
        RectTransform toastRect = applySuccessToast.GetComponent<RectTransform>();
        toastRect.SetParent(settingsPanel.transform, false);
        toastRect.anchorMin = new Vector2(0.5f, 0.5f);
        toastRect.anchorMax = new Vector2(0.5f, 0.5f);
        toastRect.pivot = new Vector2(0.5f, 0.5f);
        toastRect.anchoredPosition = new Vector2(0f, 120f);
        toastRect.sizeDelta = new Vector2(560f, 90f);

        Image background = applySuccessToast.AddComponent<Image>();
        background.color = new Color(0.08f, 0.45f, 0.18f, 0.92f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(toastRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI message = textObject.AddComponent<TextMeshProUGUI>();
        message.font = TMP_Settings.defaultFontAsset;
        message.text = "Đã áp dụng thành công!";
        message.alignment = TextAlignmentOptions.Center;
        message.fontSize = 30f;
        message.color = Color.white;
        message.raycastTarget = false;

        applySuccessToast.SetActive(false);
    }

    private IEnumerator HideApplySuccessToastAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        if (applySuccessToast != null)
        {
            applySuccessToast.SetActive(false);
        }

        hideToastCoroutine = null;
    }
}
