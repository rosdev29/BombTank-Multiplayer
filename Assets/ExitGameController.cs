using UnityEngine;
using UnityEngine.UI;

public class ExitGameController : MonoBehaviour
{
    public GameObject exitPopupPanel;

    private void Awake()
    {
        EnsureLeaveGameButtonVisual();
        EnsureExitPopupLayout();
    }

    private static void EnsureLeaveGameButtonVisual()
    {
        GameObject btnGo = GameObject.Find("LeaveGameButton");
        if (btnGo == null) { return; }

        Image img = btnGo.GetComponent<Image>();
        if (img != null)
        {
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }

        Transform duplicateArt = btnGo.transform.Find("Thoát");
        if (duplicateArt != null)
            duplicateArt.gameObject.SetActive(false);
    }

    private void EnsureExitPopupLayout()
    {
        if (exitPopupPanel == null) { return; }

        RectTransform panelRect = exitPopupPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;
        }

        Image dim = exitPopupPanel.GetComponent<Image>();
        if (dim == null)
            dim = exitPopupPanel.AddComponent<Image>();
        dim.enabled = true;
        dim.type = Image.Type.Simple;
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        Transform frame = exitPopupPanel.transform.Find("Frame_Background");
        if (frame == null) { return; }

        Image frameImg = frame.GetComponent<Image>();
        if (frameImg == null) { return; }

        frameImg.type = Image.Type.Simple;
        if (frameImg.sprite == null)
            frameImg.color = new Color(0.12f, 0.1f, 0.08f, 0.98f);
    }

    public void OpenPopup()
    {
        if (exitPopupPanel == null) { return; }
        exitPopupPanel.SetActive(true);
    }

    public void ClosePopup()
    {
        if (exitPopupPanel == null) { return; }
        exitPopupPanel.SetActive(false);
    }

    public void ConfirmExit()
    {
        if (exitPopupPanel != null)
            exitPopupPanel.SetActive(false);

        GameHUD gameHUD = GetComponent<GameHUD>();
        if (gameHUD == null)
            gameHUD = FindFirstObjectByType<GameHUD>();

        if (gameHUD != null)
        {
            gameHUD.LeaveGame();
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }
}
