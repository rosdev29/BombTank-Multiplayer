using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Cinemachine;
using Unity.Collections;

public class TankPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    [Header("Settings")]
    [SerializeField] private int ownerPriority = 15;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();

    public override void OnNetworkSpawn()
    {
        if (virtualCamera == null)
        {
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
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
        }

        if (IsOwner)
        {
            virtualCamera.Priority = ownerPriority;
        }
    }
}
