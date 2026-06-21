using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class CoinSpawner : NetworkBehaviour
{
    [SerializeField] private TaiSinhCoin coinPrefab;
    [SerializeField] private int maxCoins = 50;
    [SerializeField] private int coinValue = 10;
    [SerializeField] private Vector2 xSpawnRange;
    [SerializeField] private Vector2 ySpawnRange;
    [SerializeField] private LayerMask layerMask;

    private Collider2D[] coinBuffer = new Collider2D[1]; 

    private float coinRadius;
    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }
        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        for (int i = 0; i < maxCoins; i++) 
        {
            SpawnCoin();
        }
    }

    private void SpawnCoin()
    {
        TaiSinhCoin coinInstance = Instantiate(
            coinPrefab, 
            GetSpawnPoint(),
            Quaternion.identity);
        coinInstance.SetValue(coinValue);
        coinInstance.GetComponent<NetworkObject>().Spawn();

        coinInstance.OnCollected += XuLyKhiNhatXu;
    }
    private void XuLyKhiNhatXu(TaiSinhCoin coin)
    {
        coin.transform.position = GetSpawnPoint();
        coin.Reset();
    }

    private Vector2 GetSpawnPoint()
    {
        float x = 0;
        float y = 0;
        int maxAttempts = 100;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            x = Random.Range(xSpawnRange.x, xSpawnRange.y);
            y = Random.Range(ySpawnRange.x, ySpawnRange.y);
            Vector2 spawnPoint = new Vector2(x, y);
            
            Collider2D[] colliders = Physics2D.OverlapCircleAll(spawnPoint, coinRadius);
            bool hitObstacle = false;
            foreach (Collider2D col in colliders)
            {
                if (!col.isTrigger)
                {
                    hitObstacle = true;
                    break;
                }
            }

            if (!hitObstacle)
            {
                return spawnPoint;
            }
        }
        
        return new Vector2(x, y);
    }
}
