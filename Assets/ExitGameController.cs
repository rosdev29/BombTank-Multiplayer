using UnityEngine;

public class ExitGameController : MonoBehaviour
{
    public GameObject exitPopupPanel;

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
