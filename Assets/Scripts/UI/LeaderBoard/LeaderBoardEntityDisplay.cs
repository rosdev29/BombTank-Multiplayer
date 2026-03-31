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

    public ulong ClientId { get; private set; }
    public int TeamIndex { get; private set; } = -1;
    public int Coins { get; private set; }

    public void Initialise(ulong clientId, FixedString32Bytes playerName, int coins)
    {
        ClientId = clientId;
        TeamIndex = -1;
        displayName = playerName.ToString();

        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            displayText.color = myColour;
        }

        UpdateCoins(coins);
    }

    public void Initialise(int teamIndex, string teamName, int coins)
    {
        ClientId = 0;
        TeamIndex = teamIndex;
        displayName = teamName;
        UpdateCoins(coins);
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

    public void UpdateText()
    {
        if (displayText == null) { return; }
        string nameToShow = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName;
        displayText.text = $"{transform.GetSiblingIndex() + 1}. {nameToShow} ({Coins})";
    }
}
