using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class SimpleLanManager : MonoBehaviour
{
    [Header("LAN UI")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private ushort port = 7777;

    private NetworkManager networkManager;
    private UnityTransport transport;

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            networkManager.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    public void StartHost()
    {
        if (networkManager == null || transport == null) { return; }
        if (networkManager.IsListening) { return; }

        transport.SetConnectionData("0.0.0.0", port);
        networkManager.StartHost();
    }

    public void StartClient()
    {
        if (networkManager == null || transport == null) { return; }
        if (networkManager.IsListening) { return; }

        string ip = string.IsNullOrWhiteSpace(ipInputField?.text) ? "127.0.0.1" : ipInputField.text.Trim();
        transport.SetConnectionData(ip, port);
        networkManager.StartClient();
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"Nguoi choi [{clientId}] da ket noi thanh cong");
    }
}
