using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ConnectionButtons : MonoBehaviour
{
    /// <summary>
    /// Host với Relay (Unity 6 / com.unity.services.multiplayer): tạo allocation, chuyển sang RelayServerData, set transport rồi StartHost.
    /// </summary>
    public async void StartHost()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) return;
        if (HostSingleton.Instance?.GameManager == null) return;

        HostGameManager hostManager = HostSingleton.Instance.GameManager;
        await hostManager.StartHostAsync();
        // StartHostAsync() đã set RelayServerData và gọi StartHost() bên trong, không cần gọi lại ở đây.
    }

    IEnumerator DelayedStartClient()
    {
        yield return new WaitForSeconds(0.5f);
        if (NetworkManager.Singleton == null) yield break;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) yield break;
        NetworkManager.Singleton.StartClient();
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) return;
        StartCoroutine(DelayedStartClient());
    }
}
