using UnityEngine;

/// <summary>
/// Displays synced match timer at top-center. No scene wiring required.
/// </summary>
public class MatchTimerClient : MonoBehaviour
{
    private static MatchTimerClient instance;
    private static MatchTimerManager timerSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null) { return; }
        GameObject go = new GameObject("MatchTimerClient");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MatchTimerClient>();
    }

    public static void Register(MatchTimerManager manager)
    {
        if (instance == null)
        {
            EnsureInstance();
        }

        timerSource = manager;
    }

    public static void Unregister(MatchTimerManager manager)
    {
        if (timerSource == manager)
        {
            timerSource = null;
        }
    }

    private void OnGUI()
    {
        if (timerSource == null || !timerSource.IsSpawned) { return; }

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, timerSource.TimeRemainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string text = $"{minutes:00}:{seconds:00}";

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        Rect rect = new Rect(0f, 8f, Screen.width, 40f);
        GUI.Label(rect, text, style);
    }
}
