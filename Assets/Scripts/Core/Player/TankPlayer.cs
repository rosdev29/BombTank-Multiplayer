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
    [field: SerializeField] public Mau Health { get; private set; }

    [Header("Settings")]
    [SerializeField] private int ownerPriority = 15;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();

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
            }

            OnPlayerSpawned?.Invoke(this);
        }

        if (IsOwner)
        {
            virtualCamera.Priority = ownerPriority;
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
