using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    [SerializeField] private GameObject tutorialPanel;

    private void Start()
    {
        // Kiểm tra tránh lỗi null
        if (tutorialPanel != null)
        {
            // Ẩn tutorial khi mới vào game
            tutorialPanel.SetActive(false);
        }
    }

    public void OpenTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
        }
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }
}