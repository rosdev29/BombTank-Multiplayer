using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LeaderBoardEntityDisplay : MonoBehaviour
{
    private const float StatsColumnWidth = 92f;

    [SerializeField] private TMP_Text displayText;
    [SerializeField] private Color myColour;

    private TMP_Text statsText;
    private string displayName;
    private ulong crownLookupId;
    private int displayRank = 1;

    public ulong ClientId { get; private set; }
    public int TeamIndex { get; private set; } = -1;
    public int Coins { get; private set; }
    public int PingMs { get; private set; } = -1;
    public string DisplayName => displayName;

    private void Awake()
    {
        EnsureStatsLabel();
    }

    public void Initialise(
        ulong clientId,
        FixedString32Bytes playerName,
        int coins,
        ulong networkObjectId = 0,
        int pingMs = -1)
    {
        ClientId = clientId;
        crownLookupId = networkObjectId != 0 ? networkObjectId : clientId;
        TeamIndex = -1;
        displayName = playerName.ToString();
        PingMs = pingMs;

        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            ApplyColour(myColour);
        }

        UpdateCoins(coins);
    }

    public void SetCrownLookupId(ulong networkObjectId)
    {
        if (networkObjectId != 0)
        {
            crownLookupId = networkObjectId;
        }
    }

    public void Initialise(int teamIndex, string teamName, int coins)
    {
        ClientId = 0;
        crownLookupId = 0;
        TeamIndex = teamIndex;
        displayName = teamName;
        PingMs = -1;
        UpdateCoins(coins);
    }

    public void SetRank(int rank)
    {
        displayRank = Mathf.Max(1, rank);
    }

    public void UpdateCoins(int coins)
    {
        Coins = coins;
        UpdateText();
    }

    public void UpdatePing(int pingMs)
    {
        PingMs = pingMs;
        UpdateText();
    }

    public void SetColour(Color colour)
    {
        ApplyColour(colour);
    }

    public void UpdateName(FixedString32Bytes playerName)
    {
        displayName = playerName.ToString();
        UpdateText();
    }

    public void UpdateText()
    {
        if (displayText == null) { return; }

        EnsureStatsLabel();

        string nameToShow = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName;

        if (NetworkManager.Singleton != null &&
            ClientId == NetworkManager.Singleton.LocalClientId)
        {
            nameToShow += " [YOU]";
        }

        bool hasCrown = crownLookupId != 0 &&
            BountySystem.Instance != null &&
            BountySystem.Instance.HasCrown(crownLookupId);
        string crownPrefix = hasCrown ? "<sprite name=\"BountyCrown\"> " : "";

        displayText.alignment = TextAlignmentOptions.MidlineLeft;
        displayText.overflowMode = TextOverflowModes.Ellipsis;
        displayText.text = $"{displayRank,2}. {crownPrefix}{nameToShow}";

        if (statsText != null)
        {
            statsText.text = PingMs >= 0 ? $"{PingMs}ms ({Coins})" : $"({Coins})";
        }
    }

    private void EnsureStatsLabel()
    {
        if (statsText != null || displayText == null) { return; }

        RectTransform nameRect = displayText.rectTransform;
        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = new Vector2(-StatsColumnWidth, 0f);

        GameObject statsGo = new GameObject("Stats", typeof(RectTransform));
        statsGo.transform.SetParent(transform, false);

        RectTransform statsRect = statsGo.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(1f, 0f);
        statsRect.anchorMax = new Vector2(1f, 1f);
        statsRect.pivot = new Vector2(1f, 0.5f);
        statsRect.sizeDelta = new Vector2(StatsColumnWidth, 0f);
        statsRect.anchoredPosition = Vector2.zero;

        statsText = statsGo.AddComponent<TextMeshProUGUI>();
        statsText.font = displayText.font;
        statsText.fontSharedMaterial = displayText.fontSharedMaterial;
        statsText.fontSize = displayText.fontSize;
        statsText.color = displayText.color;
        statsText.raycastTarget = false;
        statsText.alignment = TextAlignmentOptions.MidlineRight;
        statsText.overflowMode = TextOverflowModes.Overflow;
        statsText.enableWordWrapping = false;
    }

    private void ApplyColour(Color colour)
    {
        if (displayText != null)
        {
            displayText.color = colour;
        }

        if (statsText != null)
        {
            statsText.color = colour;
        }
    }
}
