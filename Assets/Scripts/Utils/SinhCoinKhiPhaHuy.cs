using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SinhCoinKhiPhaHuy : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    private static bool isQuitting;
    private static bool isSceneTransitionInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitSceneLifecycleGuards()
    {
        isQuitting = false;
        isSceneTransitionInProgress = false;

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting -= OnApplicationQuitting;
        Application.quitting += OnApplicationQuitting;
    }

    private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        isSceneTransitionInProgress = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isSceneTransitionInProgress = false;
    }

    private static void OnApplicationQuitting()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying || isQuitting || isSceneTransitionInProgress || prefab == null)
        {
            return;
        }

        Instantiate(prefab, transform.position, Quaternion.identity);
    }
}
