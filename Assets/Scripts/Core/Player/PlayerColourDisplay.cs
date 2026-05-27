using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerColourDisplay : MonoBehaviour
{
    [SerializeField] private TeamColourLookup teamColourLookup;
    [SerializeField] private TankPlayer player;
    [SerializeField] private SpriteRenderer[] playerSprites;
    [SerializeField] private Color[] fallbackColours =
    {
        new Color(0.20f, 0.65f, 1.00f, 1.00f), // blue
        new Color(0.95f, 0.30f, 0.30f, 1.00f), // red
        new Color(0.35f, 0.85f, 0.35f, 1.00f), // green
        new Color(0.95f, 0.80f, 0.20f, 1.00f), // yellow
        new Color(0.75f, 0.40f, 0.95f, 1.00f), // purple
        new Color(1.00f, 0.55f, 0.20f, 1.00f), // orange
    };

    private void Start()
    {
        HandleTeamChanged(-1, player.TeamIndex.Value);

        player.TeamIndex.OnValueChanged += HandleTeamChanged;
    }

    private void OnDestroy()
    {
        player.TeamIndex.OnValueChanged -= HandleTeamChanged;
    }

    private void HandleTeamChanged(int oldTeamIndex, int newTeamIndex)
    {
        Color teamColour;

        if (player != null && player.IsCurrentlyBot())
        {
            // Bot chọn màu ngẫu nhiên nhưng tránh màu đầu tiên (màu của người chơi)
            int fallbackIndex = 1 + (int)(player.NetworkObjectId % (ulong)(fallbackColours.Length - 1));
            teamColour = fallbackColours.Length > 1 ? fallbackColours[fallbackIndex] : Color.red;
        }
        else
        {
            // Người chơi thật luôn mặc định màu xanh dương (phần tử đầu tiên)
            teamColour = fallbackColours.Length > 0 ? fallbackColours[0] : new Color(0.20f, 0.65f, 1.00f, 1.00f);
        }

        foreach (SpriteRenderer sprite in playerSprites)
        {
            sprite.color = teamColour;
        }
    }
}
