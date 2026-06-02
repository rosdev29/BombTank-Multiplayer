using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    [SerializeField] private GameObject huongDan;

    public void OpenTutorial()
    {
        huongDan.SetActive(true);
    }

    public void CloseTutorial()
    {
        huongDan.SetActive(false);
    }
}