using Unity.Netcode.Components;
using UnityEngine;

public class ClientNetworkTransform : NetworkTransform
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CanCommitToTransform = IsOwner;
    }

    protected override void Update()
    {
        CanCommitToTransform = IsOwner;
        base.Update();

        // Chỉ commit nếu KHÔNG phải Host (tránh duplicate tick error)
        if (!IsHost && NetworkManager != null && NetworkManager.IsConnectedClient && CanCommitToTransform)
        {
            TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
        }
    }

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}