using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-only kill feed UI (left side, vertically centered). No scene wiring required.
/// </summary>
public class KillFeedClient : MonoBehaviour
{
    private const float EntryLifetimeSeconds = 4f;
    private const float LeftMargin = 28f;
    private const float LineHeight = 34f;
    private const int FontSize = 22;
    private static readonly Color RealPlayerColour = new Color(1f, 0.92f, 0.2f, 1f);
    private static readonly Color BotColour = Color.white;
    private static readonly Color ShadowColour = new Color(0f, 0f, 0f, 0.75f);

    private static KillFeedClient instance;
    private readonly List<KillFeedEntry> entries = new List<KillFeedEntry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null) { return; }
        GameObject go = new GameObject("KillFeedClient");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<KillFeedClient>();
    }

    public static void AddEntry(string killerName, bool killerIsBot, string victimName, bool victimIsBot)
    {
        if (instance == null)
        {
            EnsureInstance();
        }

        instance?.entries.Add(new KillFeedEntry
        {
            killerName = string.IsNullOrWhiteSpace(killerName) ? "Unknown" : killerName,
            killerIsBot = killerIsBot,
            victimName = string.IsNullOrWhiteSpace(victimName) ? "Unknown" : victimName,
            victimIsBot = victimIsBot,
            expiresAt = Time.unscaledTime + EntryLifetimeSeconds
        });
    }

    private void Update()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime >= entries[i].expiresAt)
            {
                entries.RemoveAt(i);
            }
        }
    }

    private void OnGUI()
    {
        if (entries.Count == 0) { return; }

        float blockHeight = entries.Count * LineHeight;
        float startY = (Screen.height - blockHeight) * 0.5f;

        GUIStyle killerStyle = CreateRowStyle();
        GUIStyle arrowStyle = CreateRowStyle();
        arrowStyle.normal.textColor = Color.white;
        GUIStyle victimStyle = CreateRowStyle();
        GUIStyle shadowStyle = CreateRowStyle();
        shadowStyle.normal.textColor = ShadowColour;

        for (int i = 0; i < entries.Count; i++)
        {
            KillFeedEntry entry = entries[i];
            killerStyle.normal.textColor = entry.killerIsBot ? BotColour : RealPlayerColour;
            victimStyle.normal.textColor = entry.victimIsBot ? BotColour : RealPlayerColour;

            float rowY = startY + i * LineHeight;
            float killerWidth = killerStyle.CalcSize(new GUIContent(entry.killerName)).x;
            float arrowWidth = arrowStyle.CalcSize(new GUIContent("→")).x;
            float victimWidth = victimStyle.CalcSize(new GUIContent(entry.victimName)).x;
            float cursorX = LeftMargin;

            DrawLabelWithShadow(new Rect(cursorX, rowY, killerWidth + 8f, LineHeight), entry.killerName, killerStyle, shadowStyle);
            cursorX += killerWidth + 10f;

            DrawLabelWithShadow(new Rect(cursorX, rowY, arrowWidth + 8f, LineHeight), "→", arrowStyle, shadowStyle);
            cursorX += arrowWidth + 10f;

            DrawLabelWithShadow(new Rect(cursorX, rowY, victimWidth + 8f, LineHeight), entry.victimName, victimStyle, shadowStyle);
        }
    }

    private static GUIStyle CreateRowStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = FontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
    }

    private static void DrawLabelWithShadow(Rect rect, string text, GUIStyle style, GUIStyle shadowStyle)
    {
        Rect shadowRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
        GUI.Label(shadowRect, text, shadowStyle);
        GUI.Label(rect, text, style);
    }

    private struct KillFeedEntry
    {
        public string killerName;
        public bool killerIsBot;
        public string victimName;
        public bool victimIsBot;
        public float expiresAt;
    }
}
