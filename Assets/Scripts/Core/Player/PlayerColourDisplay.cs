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

    private Coroutine effectCoroutine;
    private Color originalColor;
    private List<SpriteRenderer> dynamicSprites = new List<SpriteRenderer>();

    private void Start()
    {
        HandleTeamChanged(-1, player.TeamIndex.Value);

        player.TeamIndex.OnValueChanged += HandleTeamChanged;
    }

    private void OnDestroy()
    {
        player.TeamIndex.OnValueChanged -= HandleTeamChanged;
    }

    public void AddDynamicSprite(SpriteRenderer sr)
    {
        if (!dynamicSprites.Contains(sr))
        {
            dynamicSprites.Add(sr);
            sr.color = originalColor;
        }
    }

    public void RemoveDynamicSprite(SpriteRenderer sr)
    {
        if (dynamicSprites.Contains(sr))
        {
            dynamicSprites.Remove(sr);
        }
    }

    private void HandleTeamChanged(int oldTeamIndex, int newTeamIndex)
    {
        Color teamColour;

        if (player != null && player.IsCurrentlyBot())
        {
            int fallbackIndex = 1 + (int)(player.NetworkObjectId % (ulong)(fallbackColours.Length - 1));
            teamColour = fallbackColours.Length > 1 ? fallbackColours[fallbackIndex] : Color.red;
        }
        else
        {
            teamColour = fallbackColours.Length > 0 ? fallbackColours[0] : new Color(0.20f, 0.65f, 1.00f, 1.00f);
        }

        foreach (SpriteRenderer sprite in playerSprites)
        {
            if (sprite != null) sprite.color = teamColour;
        }
        foreach (SpriteRenderer sprite in dynamicSprites)
        {
            if (sprite != null) sprite.color = teamColour;
        }

        if (effectCoroutine == null)
        {
            originalColor = teamColour;
        }
    }

    public void PlayEffect(Color effectColor, float duration, bool flash = false)
    {
        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
            foreach (SpriteRenderer sprite in playerSprites)
            {
                if (sprite != null) sprite.color = originalColor;
            }
            foreach (SpriteRenderer sprite in dynamicSprites)
            {
                if (sprite != null) sprite.color = originalColor;
            }
        }
        else
        {
            if (playerSprites.Length > 0 && playerSprites[0] != null)
                originalColor = playerSprites[0].color;
        }

        effectCoroutine = StartCoroutine(EffectRoutine(effectColor, duration, flash));
    }

    private IEnumerator EffectRoutine(Color effectColor, float duration, bool flash)
    {
        float timer = 0f;
        while (timer < duration)
        {
            Color current = flash ? (Mathf.PingPong(Time.time * 8f, 1f) > 0.5f ? effectColor : originalColor) : effectColor;
            
            foreach (SpriteRenderer sprite in playerSprites)
            {
                if (sprite != null) sprite.color = current;
            }
            foreach (SpriteRenderer sprite in dynamicSprites)
            {
                if (sprite != null) sprite.color = current;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        foreach (SpriteRenderer sprite in playerSprites)
        {
            if (sprite != null) sprite.color = originalColor;
        }
        foreach (SpriteRenderer sprite in dynamicSprites)
        {
            if (sprite != null) sprite.color = originalColor;
        }
        effectCoroutine = null;
    }
}
