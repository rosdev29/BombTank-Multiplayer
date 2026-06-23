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
    public NetworkVariable<int> LifetimeCoins = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;
    }

    public void SpendCoins(int chiPhiBan)
    {
        if (chiPhiBan <= 0) { return; }

        TotalCoins.Value = Mathf.Max(0, TotalCoins.Value - chiPhiBan);
    }

    public bool TrySpendCoins(int chiPhiBan)
    {
        if (chiPhiBan <= 0) { return true; }
        if (TotalCoins.Value < chiPhiBan) { return false; }

        TotalCoins.Value -= chiPhiBan;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.TryGetComponent<Coin>(out Coin coin)) { return; }

        if (LaNguoiChoiLocal())
        {
            AudioManager.Instance?.PlaySFX(
                AudioManager.Instance.coinPickup,
                AudioManager.Instance.coinPickupVolume
            );
        }

        int coinValue = coin.Collect();

        if (!IsServer) { return; }

        // Kiểm tra xem có đang kích hoạt BuffCoin không (x3 điểm)
        if (TryGetComponent<ItemInventory>(out ItemInventory inventory) && inventory.IsCoinBuffActive.Value)
        {
            coinValue *= 3;
        }

        TotalCoins.Value += coinValue;
        LifetimeCoins.Value += coinValue;
    }

    /// <summary>
    /// Rơi 50% coin ra map, xóa ví. Trả về 50% số coin đã rơi để hồi sinh.
    /// </summary>
    public int ProcessDeathCoinDrop()
    {
        if (!IsServer) { return 0; }

        int total = TotalCoins.Value;
        if (total <= 0)
        {
            LifetimeCoins.Value = LifetimeCoins.Value / 2;
            TotalCoins.Value = 0;
            return 0;
        }

        int dropAmount = Mathf.FloorToInt(total * (bountyPercentage / 100f));
        dropAmount = Mathf.Clamp(dropAmount, 0, total);

        if (dropAmount > 0)
        {
            SpawnBountyCoins(dropAmount);
        }

        LifetimeCoins.Value = LifetimeCoins.Value / 2;
        TotalCoins.Value = 0;
        return dropAmount / 2;
    }

    private void SpawnBountyCoins(int bountyValue)
    {
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

    private bool LaNguoiChoiLocal()
    {
        TankPlayer tank = GetComponent<TankPlayer>();
        return tank != null && tank.IsOwner && !tank.IsCurrentlyBot();
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