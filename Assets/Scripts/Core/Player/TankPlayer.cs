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

    [Header("Settings")]
    [SerializeField] private int ownerPriority = 15;
    [SerializeField] private Color ownerColour;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<int> TeamIndex = new NetworkVariable<int>();

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

        if (IsServer)
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

            OnPlayerSpawned?.Invoke(this);
        }

        if (IsOwner)
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
