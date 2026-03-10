using System;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Networking.Transport.Relay;

public class HostGameManager
{
    private Allocation allocation;
    private RelayServerData relayServerData;
    private bool hasRelayServerData;
    private const int MaxConnections = 20;

    public RelayServerData RelayServerData => relayServerData;
    public bool HasRelayServerData => hasRelayServerData;

    public async Task StartHostAsync()
    {
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
            relayServerData = allocation.ToRelayServerData("udp");
            hasRelayServerData = true;
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }
}
