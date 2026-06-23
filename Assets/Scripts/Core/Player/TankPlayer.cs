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

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<int> TeamIndex = new NetworkVariable<int>();
    public NetworkVariable<bool> IsBot = new NetworkVariable<bool>(false);

    public bool IsCurrentlyBot()
    {
        return IsBot.Value || GetComponent<BotTag>() != null;
    }

    public static event Action<TankPlayer> OnPlayerSpawned;
    public static event Action<TankPlayer> OnPlayerDespawned;

    // Danh sách tất cả TankPlayer đang tồn tại trên Server (cả bot lẫn người thật).
    public static IReadOnlyList<TankPlayer> AllTankPlayers => _allTankPlayers;
    private static readonly List<TankPlayer> _allTankPlayers = new List<TankPlayer>();

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
                    string playerPrefsName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "");

                    if (OwnerClientId == NetworkManager.ServerClientId &&
                        !string.IsNullOrWhiteSpace(playerPrefsName))
                    {
                        PlayerName.Value = playerPrefsName;
                    }
                    else if (!string.IsNullOrWhiteSpace(userData.userName))
                    {
                        PlayerName.Value = userData.userName;
                    }
                    else
                    {
                        PlayerName.Value = $"Player {OwnerClientId}";
                    }

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
            PlayerName.OnValueChanged += HandlePlayerNameChanged;
            OnPlayerSpawned?.Invoke(this);
        }

        // Tự đăng ký vào danh sách toàn cục người chơi (server-side)
        if (IsServer && !_allTankPlayers.Contains(this))
        {
            _allTankPlayers.Add(this);
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

    private void HandlePlayerNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        if (!IsServer) { return; }

        OnPlayerSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= HandlePlayerNameChanged;

        if (IsServer && NetworkManager != null && !NetworkManager.ShutdownInProgress)
        {
            OnPlayerDespawned?.Invoke(this);
            _allTankPlayers.Remove(this);
        }
    }
}
