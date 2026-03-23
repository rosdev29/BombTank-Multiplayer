using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;
    [SerializeField] private NetworkObject playerPrefab;

    private async void Start()
    {
        DontDestroyOnLoad(gameObject);

        await LaunchClientHostMode();
    }

    private async Task LaunchClientHostMode()
    {
        HostSingleton hostSingleton = Instantiate(hostPrefab);
        hostSingleton.CreateHost(playerPrefab);

        ClientSingleton clientSingleton = Instantiate(clientPrefab);
        bool authenticated = await clientSingleton.CreateClient();
        if (authenticated)
        {
            clientSingleton.GameManager.GoToMenu();
        }
    }
}
