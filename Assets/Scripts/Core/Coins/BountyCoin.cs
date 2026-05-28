using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BountyCoin : Coin
{
    public override int Collect()
    {
        Debug.Log("COIN COLLECT FUNCTION RUNNING");

        if (!IsServer)
        {
            Show(false);
            return 0;
        }

        if (alreadyCollected) { return 0; }

        alreadyCollected = true;

        Debug.Log("PLAY COIN SOUND");

        AudioManager.Instance.PlaySFX(AudioManager.Instance.coinPickup);

        Destroy(gameObject);

        return coinValue;
    }
}