     using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ConnectionButtons : MonoBehaviour
{
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string gameSceneName = "Game";

    public void StartHost()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) return;

        EnsureConnectionApprovalCallback(NetworkManager.Singleton);

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", port);
        }

        if (NetworkManager.Singleton.StartHost())
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
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

    private static void EnsureConnectionApprovalCallback(NetworkManager networkManager)
    {
        if (networkManager.ConnectionApprovalCallback != null) return;

        networkManager.ConnectionApprovalCallback = ApproveConnection;
    }

    private static void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Pending = false;
    }
}
