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

    // Các thông số được cấu hình cứng trong code để tránh bị Inspector ghi đè
    private int maxItemsOnMap = 10;
    private float respawnTime = 10f;
    private Vector2 xSpawnRange = new Vector2(-50, 50);
    private Vector2 ySpawnRange = new Vector2(-50, 50);
    
    [Header("Settings")]
    [SerializeField] private LayerMask layerMask;

    private Collider2D[] itemBuffer = new Collider2D[1];
    private float itemRadius = 1f; // Mặc định, có thể lấy từ prefab
    private int currentItemCount = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        // Khởi tạo Random với seed theo thời gian thực để mỗi trận luôn khác nhau hoàn toàn
        Random.InitState(System.Environment.TickCount);

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
            
            // Lấy tất cả colliders tại vị trí này, cộng thêm bán kính để item giãn cách ra
            Collider2D[] colliders = Physics2D.OverlapCircleAll(spawnPoint, itemRadius + 1.5f);
            bool hitObstacle = false;
            foreach (Collider2D col in colliders)
            {
                // Nếu chạm phải collider cứng (tường, đá...)
                if (!col.isTrigger)
                {
                    hitObstacle = true;
                    break;
                }
                // Nếu khu vực này đã có một Item khác, bỏ qua để không spawn đè/quá sát nhau
                if (col.GetComponent<ItemPickup>() != null)
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
