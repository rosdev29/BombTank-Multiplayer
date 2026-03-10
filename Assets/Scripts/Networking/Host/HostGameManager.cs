using System;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Networking.Transport.Relay;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine.SceneManagement;
public class HostGameManager
{
    private Allocation allocation;
    private string joinCode;
    private RelayServerData relayServerData;
    private bool hasRelayServerData;
    private const int MaxConnections = 20;
    private const string GameScenceName = "Game";

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
            return;
        }
        try
        {
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log(joinCode);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null && hasRelayServerData)
            transport.SetRelayServerData(relayServerData);

        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(GameScenceName, LoadSceneMode.Single);
    }
}
