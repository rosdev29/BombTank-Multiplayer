using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LeaderBoardEntityDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private Color myColour;

    private string displayName;
    private ulong crownLookupId;

    private int displayRank = 1;

    public ulong ClientId { get; private set; }
    public int TeamIndex { get; private set; } = -1;
    public int Coins { get; private set; }
    public string DisplayName => displayName;

    public void Initialise(ulong clientId, FixedString32Bytes playerName, int coins, ulong networkObjectId = 0)
    {
        ClientId = clientId;
        crownLookupId = networkObjectId != 0 ? networkObjectId : clientId;
        TeamIndex = -1;
        displayName = playerName.ToString();

        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            displayText.color = myColour;
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

    public void SetColour(Color colour)
    {
        if (displayText == null) { return; }
        displayText.color = colour;
    }

    public void UpdateName(FixedString32Bytes playerName)
    {
        displayName = playerName.ToString();
        UpdateText();
    }


    public void UpdateText()
    {
        if (displayText == null) { return; }
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
        displayText.text = $"{displayRank,2}. {crownPrefix}{nameToShow} ({Coins})";
    }
}


