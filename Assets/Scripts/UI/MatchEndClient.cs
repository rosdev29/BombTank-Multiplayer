using UnityEngine;

/// <summary>
/// Temporary end-of-match overlay until Thái's end screen is ready.
/// </summary>
public class MatchEndClient : MonoBehaviour
{
    private static MatchEndClient instance;
    private bool showOverlay;

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
    }

    private void OnGUI()
    {
        // Đọc giá trị để fix C# warning
        if (!showOverlay) { return; }

        // Train Mode: An UI ket thuc
        /*

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        Rect rect = new Rect(0f, Screen.height * 0.42f, Screen.width, 60f);
        GUI.Label(rect, "Het gio! Tran ket thuc.\n(Man ket thuc cua Thai se noi sau)", style);
        */
    }
}
