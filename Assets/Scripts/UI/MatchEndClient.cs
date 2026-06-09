using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


public class MatchEndClient : MonoBehaviour
{
    private static MatchEndClient instance;
    private bool showOverlay;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle boxStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null) { return; }

        GameObject go = new GameObject("MatchEndClient");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MatchEndClient>();
    }

    public static void ShowEndOverlay()
    {
        if (instance == null)
        {
            EnsureInstance();
        }

        instance.showOverlay = true;
        Time.timeScale = 0f;
    }

    private void InitStyles()
    {
        if (titleStyle != null) { return; }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { textColor = Color.white }
        };
    }

    private void OnGUI()
    {
        if (!showOverlay) { return; }

        InitStyles();

        float width = 650f;
        float height = 560f;
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "", boxStyle);

        GUI.Label(new Rect(x, y + 25f, width, 50f), "MATCH END", titleStyle);

        GUI.Label(new Rect(x + 45f, y + 95f, width - 90f, 35f), "RANKINGS", labelStyle);

        string rankingText = GetRankingText();
        GUI.Label(new Rect(x + 55f, y + 150f, width - 110f, 220f), rankingText, labelStyle);

        if (GUI.Button(new Rect(x + (width - 170f) / 2f, y + 470f, 170f, 55f), "HOME", buttonStyle))
        {
            GoHome();
        }
    }

    private string GetRankingText()
    {
        LeaderBoardEntityDisplay[] displays =
            FindObjectsByType<LeaderBoardEntityDisplay>(FindObjectsSortMode.None);

        if (displays == null || displays.Length == 0)
        {
            return "Khong co du lieu diem.";
        }

        System.Array.Sort(displays, (a, b) => b.Coins.CompareTo(a.Coins));

        string result = "";

        for (int i = 0; i < displays.Length; i++)
        {
            string playerName = displays[i].DisplayName;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = "Unknown";
            }

            result += $"{i + 1}. {playerName} ({displays[i].Coins})\n";
        }

        return result;
    }

    private void GoHome()
    {
        showOverlay = false;
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(0);
    }

    private void PlayAgain()
    {
        showOverlay = false;
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(1);
    }
}