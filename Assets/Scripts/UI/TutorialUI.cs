using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private RectTransform settingsPanelTransform;
    [SerializeField] private RectTransform[] footerButtons;

    private RectTransform footerOverlay;
    private bool footerRaised;

    public bool IsOpen => tutorialPanel != null && tutorialPanel.activeSelf;

    private void Start()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }

    public void OpenTutorial()
    {
        if (tutorialPanel == null) { return; }

        tutorialPanel.SetActive(true);
        AllowClicksThroughTutorialOverlay();
        RaiseFooterButtons();
    }

    public void CloseTutorial()
    {
        RestoreFooterButtons();

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }

    private void AllowClicksThroughTutorialOverlay()
    {
        Graphic[] graphics = tutorialPanel.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic.GetComponent<Button>() != null) { continue; }
            graphic.raycastTarget = false;
        }
    }

    private void EnsureFooterOverlay()
    {
        if (footerOverlay != null) { return; }

        Transform canvas = tutorialPanel.transform.parent;
        GameObject overlayObject = new GameObject("SettingsFooterOverlay", typeof(RectTransform));
        footerOverlay = overlayObject.GetComponent<RectTransform>();
        footerOverlay.SetParent(canvas, false);
        footerOverlay.anchorMin = Vector2.zero;
        footerOverlay.anchorMax = Vector2.one;
        footerOverlay.offsetMin = Vector2.zero;
        footerOverlay.offsetMax = Vector2.zero;
    }

    private void RaiseFooterButtons()
    {
        if (footerButtons == null || footerButtons.Length == 0) { return; }

        EnsureFooterOverlay();

        foreach (RectTransform button in footerButtons)
        {
            if (button == null) { continue; }
            button.SetParent(footerOverlay, true);
        }

        footerOverlay.SetAsLastSibling();
        footerRaised = true;
    }

    private void RestoreFooterButtons()
    {
        if (!footerRaised || settingsPanelTransform == null) { return; }

        foreach (RectTransform button in footerButtons)
        {
            if (button == null) { continue; }
            button.SetParent(settingsPanelTransform, true);
        }

        footerRaised = false;
    }
}
