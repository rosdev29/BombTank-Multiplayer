using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private TMP_InputField ipField;
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string gameSceneName = "Game";
    private bool isStartingHost;
    private NetworkManager networkManager;

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    public void StartHost()
    {
        if (isStartingHost) { return; }
        if (networkManager == null) { return; }
        if (networkManager.IsListening) { return; }

        HostSingleton.Instance?.GameManager?.PrepareLanHost();

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", port);
        }

        SetConnectionPayload();

        isStartingHost = true;
        bool started = networkManager.StartHost();
        Debug.Log($"[LAN] StartHost result={started} port={port}");
        if (started)
        {
            networkManager.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        isStartingHost = false;
    }

    public void StartClient()
    {
        if (networkManager == null) { return; }
        if (networkManager.IsListening) { return; }

        string ip = string.IsNullOrWhiteSpace(ipField?.text) ? "127.0.0.1" : ipField.text.Trim();
        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(ip, port);
        }

        SetConnectionPayload();

        bool started = networkManager.StartClient();
        Debug.Log($"[LAN] StartClient result={started} ip={ip}:{port}");
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager == null) { return; }
        Debug.Log($"[LAN] Connected. LocalClientId={networkManager.LocalClientId}, EventClientId={clientId}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"[LAN] Disconnected clientId={clientId}");
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
