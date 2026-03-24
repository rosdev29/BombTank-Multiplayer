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

    private FixedString32Bytes displayName;

    public ulong ClientId { get; private set; }
    public int TeamIndex { get; private set; }
    public int Coins { get; private set; }

    public void Initialise(ulong clientId, FixedString32Bytes displayName, int coins)
    {
        ClientId = clientId;
        this.displayName = displayName;

        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Keep local player visible even if LeaderBoard owner colour is not configured.
            displayText.color = myColour;
        }

        UpdateCoins(coins);
    }

    public void Initialise(int teamIndex, FixedString32Bytes displayName, int coins)
    {
        TeamIndex = teamIndex;
        this.displayName = displayName;
        UpdateCoins(coins);
    }

    public void UpdateCoins(int coins)
    {
        Coins = coins;
        UpdateText();
    }

    public void UpdateText()
    {
        displayText.text = $"{transform.GetSiblingIndex() + 1}. {displayName} ({Coins})";
    }

    public void SetColour(Color colour)
    {
        displayText.color = colour;
    }
}
