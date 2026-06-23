using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


public class MatchEndClient : MonoBehaviour
{
    private static MatchEndClient instance;
    private bool showOverlay;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle rankingStyle;
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

        DeathSpectatorClient.Dismiss();
        instance.showOverlay = true;
        GameplayInputGate.SetBlocked(true);
    }

    public static void HideOverlay()
    {
        if (instance == null) { return; }

        instance.showOverlay = false;
        GameplayInputGate.SetBlocked(false);
        Time.timeScale = 1f;
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

        rankingStyle = new GUIStyle(labelStyle)
        {
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Overflow,
            wordWrap = true
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

        GUI.Label(new Rect(x, y + 25f, width, 50f), "TRẬN ĐẤU KẾT THÚC", titleStyle);

        GUI.Label(new Rect(x + 45f, y + 95f, width - 90f, 35f), "BẢNG XẾP HẠNG", labelStyle);

        string rankingText = GetRankingText();
        GUI.Label(new Rect(x + 55f, y + 135f, width - 110f, 300f), rankingText, rankingStyle);

        if (GUI.Button(new Rect(x + (width - 170f) / 2f, y + 470f, 170f, 55f), "TRANG CHỦ", buttonStyle))
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
        ClientSessionOverlay.ReturnToMenu();
    }

    private void PlayAgain()
    {
        HideOverlay();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("Game");
    }
}