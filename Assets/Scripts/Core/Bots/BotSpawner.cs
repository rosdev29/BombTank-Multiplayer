using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class BotSpawner : MonoBehaviour
{
    private const int TargetTotalTanks = 3;

    private List<TankPlayer> activeBots = new List<TankPlayer>();
    private int realPlayerCount = 0;

    private static readonly string[] BotNames = new string[]
    {
        "XeHutHamCau", "GaAnThoc", "ThichDiDao", "BanXongChay", "TrumBom",
        "MayXucDat", "SieuNhanGao", "ChoiChoVui", "NemDaGiauTay", "XeTangGia",
        "TrumCuoi", "KiepDoDen", "ThanhDoMin", "BaoThu", "VuaLiDon"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        GameObject go = new GameObject("BotSpawner");
        DontDestroyOnLoad(go);
        go.AddComponent<BotSpawner>();
    }

    private NetworkManager cachedNetworkManager;

    private void Update()
    {
        if (NetworkManager.Singleton != cachedNetworkManager)
        {
            if (cachedNetworkManager != null)
            {
                cachedNetworkManager.OnServerStarted -= HandleServerStarted;
            }

            cachedNetworkManager = NetworkManager.Singleton;

            if (cachedNetworkManager != null)
            {
                cachedNetworkManager.OnServerStarted += HandleServerStarted;
            }
        }
    }

    private void Start()
    {
        TankPlayer.OnPlayerSpawned += HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned += HandlePlayerDespawned;
    }

    private void OnDestroy()
    {
        TankPlayer.OnPlayerSpawned -= HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned -= HandlePlayerDespawned;

        if (cachedNetworkManager != null)
        {
            cachedNetworkManager.OnServerStarted -= HandleServerStarted;
        }
    }

    private void HandleServerStarted()
    {
        activeBots.Clear();
        realPlayerCount = 0;
    }

    private void HandlePlayerSpawned(TankPlayer player)
    {
        if (player.IsBot.Value || player.GetComponent<BotTag>() != null)
        {
            if (!activeBots.Contains(player))
            {
                activeBots.Add(player);
            }
        }
        else
        {
            realPlayerCount++;
            UpdateBots();
        }
    }

    private void HandlePlayerDespawned(TankPlayer player)
    {
        if (player.IsBot.Value || player.GetComponent<BotTag>() != null)
        {
            activeBots.Remove(player);
            UpdateBots();
        }
        else
        {
            realPlayerCount--;
            UpdateBots();
        }
    }

    private void UpdateBots()
    {
        if (!Application.isPlaying) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer) return;
        if (!NetworkManager.Singleton.IsListening) return;
        if (NetworkManager.Singleton.ShutdownInProgress) return;
        if (NetworkManager.Singleton.ConnectedClients == null || NetworkManager.Singleton.ConnectedClients.Count == 0) return;

        int targetBotCount = Mathf.Max(0, TargetTotalTanks - realPlayerCount);

        int safetyCounter = 0;
        // Need more bots
        while (activeBots.Count < targetBotCount && safetyCounter < 20)
        {
            SpawnBot();
            safetyCounter++;
        }

        // Need fewer bots
        while (activeBots.Count > targetBotCount)
        {
            TankPlayer botToRemove = activeBots[activeBots.Count - 1];
            activeBots.RemoveAt(activeBots.Count - 1);
            if (botToRemove != null && botToRemove.NetworkObject != null && botToRemove.NetworkObject.IsSpawned)
            {
                botToRemove.NetworkObject.Despawn();
            }
        }
    }

    private void SpawnBot()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.NetworkConfig == null) return;
        var prefabObj = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        if (prefabObj == null) return;

        Vector3 spawnPosition = GetRandomSpawnPosition();

        NetworkObject botInstance = Instantiate(prefabObj, spawnPosition, Quaternion.identity).GetComponent<NetworkObject>();

        botInstance.gameObject.AddComponent<BotTag>();

        if (botInstance.GetComponent<BotSense>() == null)
            botInstance.gameObject.AddComponent<BotSense>();

        if (botInstance.GetComponent<BotMover>() == null)
            botInstance.gameObject.AddComponent<BotMover>();

        if (botInstance.GetComponent<BotTurretController>() == null)
            botInstance.gameObject.AddComponent<BotTurretController>();

        if (botInstance.GetComponent<BotShooter>() == null)
            botInstance.gameObject.AddComponent<BotShooter>();

        if (botInstance.GetComponent<BotBrain>() == null)
            botInstance.gameObject.AddComponent<BotBrain>();

        // LayerMask được BotBrain tự set trong OnNetworkSpawn qua KhoiTao() — không cần override ở đây.

        TankPlayer tankPlayer = botInstance.GetComponent<TankPlayer>();

        botInstance.Spawn(true);

        // Đặt giá trị thông qua .Value SAU KHI Spawn
        if (tankPlayer != null)
        {
            tankPlayer.IsBot.Value = true;
            tankPlayer.PlayerName.Value = new Unity.Collections.FixedString32Bytes(GetRandomBotName());
            tankPlayer.TeamIndex.Value = -1;
        }
    }


    private string GetRandomBotName()
    {
        // Pick a random name that isn't already used by an active bot
        var usedNames = activeBots.Select(b => b.PlayerName.Value.ToString()).ToHashSet();
        var availableNames = BotNames.Where(n => !usedNames.Contains(n)).ToList();

        if (availableNames.Count > 0)
        {
            return availableNames[Random.Range(0, availableNames.Count)];
        }

        return BotNames[Random.Range(0, BotNames.Length)];
    }

    private Vector3 GetRandomSpawnPosition()
    {
        SpawnPoint[] spawnPoints = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return SpawnPoint.GetRandomSpawnPos();
        }

        TankPlayer[] allTanks = Object.FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        var availablePoints = spawnPoints.ToList();

        // Trộn danh sách để chọn ngẫu nhiên
        for (int i = 0; i < availablePoints.Count; i++)
        {
            int r = Random.Range(i, availablePoints.Count);
            (availablePoints[i], availablePoints[r]) = (availablePoints[r], availablePoints[i]);
        }

        // Tìm một SpawnPoint không có xe nào đứng quá gần
        foreach (var sp in availablePoints)
        {
            bool isOccupied = false;
            foreach (var tank in allTanks)
            {
                if (Vector3.Distance(sp.transform.position, tank.transform.position) < 2.5f)
                {
                    isOccupied = true;
                    break;
                }
            }
            if (!isOccupied) return sp.transform.position;
        }

        // Nếu tất cả đều kín chỗ (vd: map ít điểm spawn hơn số bot), chọn ngẫu nhiên 1 điểm và nhích ra một chút
        Vector3 fallback = spawnPoints[Random.Range(0, spawnPoints.Length)].transform.position;
        fallback += new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f);
        return fallback;
    }
}