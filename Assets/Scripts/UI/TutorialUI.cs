using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    [SerializeField] private GameObject tutorialPanel;

    private void Start()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
        else
        {
            Debug.LogError($"[TutorialUI] Bạn chưa gán 'Tutorial Panel' vào script trên object: {gameObject.name}");
        }
    }

    public void OpenTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            Debug.Log("[TutorialUI] Panel đã được mở!");
        }
        else
        {
            Debug.LogError("[TutorialUI] Không thể mở vì chưa gán Panel!");
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