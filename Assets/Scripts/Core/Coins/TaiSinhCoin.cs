using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaiSinhCoin : Coin
{
    public event Action<TaiSinhCoin> OnCollected;

    private Vector3 previousPosittion;

    private void Update()
    {
        if (previousPosittion != transform.position)
        {
            Show(true);
        }

        previousPosittion = transform.position;
    }

    public override int Collect()
    {
        if (!IsServer)
        {
            Show(false);
            return 0;
        }

        if (alreadyCollected) { return 0; }
        alreadyCollected = true;
        OnCollected?.Invoke(this);
        return coinValue;
    }
        public void Reset()
    {
        alreadyCollected = false;
    }
}
