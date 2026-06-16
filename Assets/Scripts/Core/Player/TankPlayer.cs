using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Cinemachine;
using Unity.Collections;
using System;

public class TankPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private SpriteRenderer minimapIconRenderer;
    [field: SerializeField] public Mau Health { get; private set; }
    [field: SerializeField] public CoinWallet Wallet { get; private set; }
    [field: SerializeField] public ItemInventory Inventory { get; private set; }

    [Header("Settings")]
    [SerializeField] private int ownerPriority = 15;
    [SerializeField] private Color ownerColour;

    public NetworkVariable<Unity.Collections.FixedString32Bytes> PlayerName = new NetworkVariable<Unity.Collections.FixedString32Bytes>(new Unity.Collections.FixedString32Bytes(""));
    public NetworkVariable<int> TeamIndex = new NetworkVariable<int>(-1);
    public NetworkVariable<bool> IsBot = new NetworkVariable<bool>(false);

    public bool IsCurrentlyBot()
    {
        return IsBot.Value || GetComponent<BotTag>() != null;
    }

    public static event Action<TankPlayer> OnPlayerSpawned;
    public static event Action<TankPlayer> OnPlayerDespawned;

    public override void OnNetworkSpawn()
    {
        if (virtualCamera == null)
        {
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
        }

        if (Health == null)
        {
            Health = GetComponent<Mau>();
        }

        if (Wallet == null)
        {
            Wallet = GetComponent<CoinWallet>();
        }

        if (Inventory == null)
        {
            Inventory = GetComponent<ItemInventory>();
        }

        if (IsServer)
        {
            if (!IsCurrentlyBot())
            {
                UserData userData = null;
                if (HostSingleton.Instance != null &&
                    HostSingleton.Instance.GameManager != null &&
                    HostSingleton.Instance.GameManager.NetworkServer != null)
                {
                    userData =
                        HostSingleton.Instance.GameManager.NetworkServer.GetUserDataByClientId(OwnerClientId);
                }

                if (userData != null)
                {
                    PlayerName.Value = userData.userName;
                    TeamIndex.Value = userData.teamIndex;
                }
                else
                {
                    // Fallback so leaderboard never receives an empty player name.
                    string fallbackName = OwnerClientId == NetworkManager.ServerClientId
                        ? PlayerPrefs.GetString("PlayerName", $"Player {OwnerClientId}")
                        : $"Player {OwnerClientId}";
                    PlayerName.Value = fallbackName;

                    if (TeamIndex.Value == 0 && OwnerClientId != NetworkManager.ServerClientId)
                    {
                        TeamIndex.Value = -1;
                    }
                }
            }
            else
            {
                // Đây là BOT
                IsBot.Value = true; // Đánh dấu đây là Bot để các script của người chơi tự động tắt

                if (PlayerName.Value.ToString() == "")
                {
                    if (GetComponent<TankAgentUltra>() != null)
                    {
                        // ML-Agents Bots
                        string botName = "AI Bot " + Mathf.Abs(gameObject.GetInstanceID() % 1000);
                        PlayerName.Value = botName;
                        if (TeamIndex.Value == -1) TeamIndex.Value = gameObject.GetInstanceID();
                    }
                    else
                    {
                        // BotSpawner Bots
                        if (BotSpawner.Instance != null)
                            PlayerName.Value = BotSpawner.Instance.GetRandomBotName();
                        else
                            PlayerName.Value = "Bot " + Mathf.Abs(gameObject.GetInstanceID() % 100);
                            
                        if (TeamIndex.Value == -1) TeamIndex.Value = -1;
                    }
                }
            }

            OnPlayerSpawned?.Invoke(this);
        }

        if (IsOwner && !IsCurrentlyBot())
        {
            virtualCamera.Priority = ownerPriority;

            if (minimapIconRenderer != null)
            {
                minimapIconRenderer.color = ownerColour;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null && !NetworkManager.ShutdownInProgress)
        {
            OnPlayerDespawned?.Invoke(this);
        }
    }
}
