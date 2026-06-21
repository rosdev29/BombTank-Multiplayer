using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resets persistent client overlays/input when leaving a match.
/// </summary>
public static class ClientSessionOverlay
{
    public const string MenuSceneName = "Menu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            ResetForMenu();
        }
        else if (scene.name == "Game")
        {
            MatchEndClient.HideOverlay();
            DeathSpectatorClient.Dismiss();
            GameplayInputGate.SetBlocked(false);
            Time.timeScale = 1f;
        }
    }

    public static void ResetForMenu()
    {
        MatchEndBridge.Reset();
        MatchEndClient.HideOverlay();
        DeathSpectatorClient.Dismiss();
        GameplayInputGate.SetBlocked(false);
        Time.timeScale = 1f;
    }

    public static void ReturnToMenu()
    {
        ResetForMenu();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(MenuSceneName);
    }
}
