using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Coin : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    protected int coinValue;
    protected bool alreadyCollected;

    public abstract int Collect();
    public void SetValue(int value)
    {
        coinValue = value;
    }

    protected void Show(bool show)
    {
        spriteRenderer.enabled = show;
    }
}
