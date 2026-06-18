using TMPro;
using UnityEngine;

public static class BountyNotificationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateNotification()
    {
        if (Object.FindFirstObjectByType<BountyKillNotification>() != null)
            return;

        GameObject canvasGO = new GameObject("BountyCanvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject notificationGO =
            new GameObject("BountyKillNotification");

        notificationGO.transform.SetParent(canvasGO.transform, false);

        TMP_Text text =
            notificationGO.AddComponent<TextMeshProUGUI>();

        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 36;

        RectTransform rt =
            notificationGO.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);

        rt.pivot = new Vector2(0.5f, 1f);

        rt.anchoredPosition = new Vector2(0f, -80f);

        rt.sizeDelta = new Vector2(1000f, 120f);

        BountyKillNotification notification =
            notificationGO.AddComponent<BountyKillNotification>();

        Object.DontDestroyOnLoad(canvasGO);

        Debug.Log("[Bounty] Notification UI created.");
    }
}