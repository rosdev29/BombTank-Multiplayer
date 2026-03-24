using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
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

        HostSingleton.Instance?.GameManager?.PrepareLanHost();
        transport.SetConnectionData("0.0.0.0", port);
        SetConnectionPayload();
        networkManager.StartHost();
    }

    public void StartClient()
    {
        if (networkManager == null || transport == null) { return; }
        if (networkManager.IsListening) { return; }

        string ip = string.IsNullOrWhiteSpace(ipInputField?.text) ? "127.0.0.1" : ipInputField.text.Trim();
        transport.SetConnectionData(ip, port);
        SetConnectionPayload();
        networkManager.StartClient();
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"Nguoi choi [{clientId}] da ket noi thanh cong");
    }

    private void SetConnectionPayload()
    {
        if (networkManager == null) { return; }

        UserData userData = new UserData
        {
            userName = PlayerPrefs.GetString("PlayerName", "Missing Name"),
            userAuthId = GetLanAuthId(),
            teamIndex = -1,
            userGamePreferences = new GameInfo()
        };

        string payload = JsonUtility.ToJson(userData);
        networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(payload);
    }

    private static string GetLanAuthId()
    {
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            return AuthenticationService.Instance.PlayerId;
        }

        const string key = "LanAuthId";
        string cached = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        cached = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, cached);
        return cached;
    }
}
