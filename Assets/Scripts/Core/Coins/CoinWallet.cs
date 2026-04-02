using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CoinWallet : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Mau health;
    [SerializeField] private BountyCoin coinPrefab;

    [Header("Settings")]
    [SerializeField] private float coinSpread = 3f;
    [SerializeField] private float bountyPercentage = 50f;
    [SerializeField] private int bountyCoinCount = 10;
    [SerializeField] private int minBountyCoinValue = 5;
    [SerializeField] private LayerMask layerMask;

    private Collider2D[] coinBuffer = new Collider2D[1];
    private float coinRadius;

    public NetworkVariable<int> TotalCoins = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        health.KhiChet += HandleDie;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }

        health.KhiChet -= HandleDie;
    }

    public void SpendCoins(int chiPhiBan)
    {
        TotalCoins.Value -= chiPhiBan;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if(!col.TryGetComponent<Coin>(out Coin coin)) { return; }

        int coinValue = coin.Collect();

        if (!IsServer) { return; }

        TotalCoins.Value += coinValue;
    }

    private void HandleDie(Mau health)
    {
        int bountyValue = (int)(TotalCoins.Value * (bountyPercentage / 100f));
        if (bountyValue <= 0) { return; }

        int spawnCount = Mathf.Max(1, bountyCoinCount);
        spawnCount = Mathf.Min(spawnCount, bountyValue);

        if (minBountyCoinValue > 1)
        {
            int maxByMin = Mathf.Max(1, bountyValue / minBountyCoinValue);
            spawnCount = Mathf.Min(spawnCount, maxByMin);
        }

        int baseCoinValue = bountyValue / spawnCount;
        int remainder = bountyValue % spawnCount;

        for (int i = 0; i < spawnCount; i++)
        {
            BountyCoin coinInstance = Instantiate(coinPrefab, GetSpawnPoint(), Quaternion.identity);
            int coinValue = baseCoinValue + (i < remainder ? 1 : 0);
            coinInstance.SetValue(coinValue);
            coinInstance.NetworkObject.Spawn();
        }
    }

    private Vector2 GetSpawnPoint()
    {
        while (true)
        {
            Vector2 spawnPoint = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * coinSpread;
            int numColliders = Physics2D.OverlapCircleNonAlloc(spawnPoint, coinRadius, coinBuffer, layerMask);
            if (numColliders == 0)
            {
                return spawnPoint;
            }
        }
    }
}
