using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemSpawner : NetworkBehaviour
{
    [Header("Prefabs (Gán 3 prefab item vào đây)")]
    public ItemPickup buffCoinPrefab;
    public ItemPickup trapPrefab;
    public ItemPickup doubleBarrelPrefab;

    [Header("Settings")]
    [SerializeField] private int maxItemsOnMap = 20; // Đã tăng từ 15 lên 40
    [SerializeField] private float respawnTime = 5f;  // Rút ngắn thời gian hồi từ 5s xuống 1s
    [SerializeField] private Vector2 xSpawnRange = new Vector2(-20, 20);
    [SerializeField] private Vector2 ySpawnRange = new Vector2(-20, 20);
    [SerializeField] private LayerMask layerMask;

    private Collider2D[] itemBuffer = new Collider2D[1];
    private float itemRadius = 1f; // Mặc định, có thể lấy từ prefab
    private int currentItemCount = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        if (buffCoinPrefab != null)
        {
            itemRadius = buffCoinPrefab.GetComponent<CircleCollider2D>().radius;
        }

        for (int i = 0; i < maxItemsOnMap; i++)
        {
            SpawnRandomItem();
        }
    }

    private void SpawnRandomItem()
    {
        if (!IsServer || currentItemCount >= maxItemsOnMap) return;

        ItemPickup prefabToSpawn = GetRandomPrefab();
        if (prefabToSpawn == null) return;

        ItemPickup itemInstance = Instantiate(prefabToSpawn, GetSpawnPoint(), Quaternion.identity);
        itemInstance.GetComponent<NetworkObject>().Spawn();
        currentItemCount++;

        // Khi item bị nhặt, ta sẽ bắt sự kiện để spawn lại
        itemInstance.OnCollected += HandleItemCollected;
    }

    private void HandleItemCollected(ItemPickup item)
    {
        currentItemCount--;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);
        SpawnRandomItem();
    }

    private ItemPickup GetRandomPrefab()
    {
        int rand = Random.Range(0, 3);
        switch (rand)
        {
            case 0: return buffCoinPrefab;
            case 1: return trapPrefab;
            case 2: return doubleBarrelPrefab;
        }
        return buffCoinPrefab;
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
            int numColliders = Physics2D.OverlapCircleNonAlloc(spawnPoint, itemRadius, itemBuffer, layerMask);
            if (numColliders == 0)
            {
                return spawnPoint;
            }
        }
        
        return new Vector2(x, y);
    }
}
