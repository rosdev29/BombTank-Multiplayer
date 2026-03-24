using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private TMP_InputField ipField;
    [SerializeField] private Button clientButton;
    [SerializeField] private TMP_Text statusText;
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

        if (ipField != null)
        {
            ipField.onValueChanged.AddListener(HandleIpChanged);
            if (ipField.placeholder is TMP_Text placeholder && string.IsNullOrWhiteSpace(placeholder.text))
            {
                placeholder.text = "192.168.1.7";
            }
        }

        HandleIpChanged(ipField != null ? ipField.text : string.Empty);
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        if (ipField != null)
        {
            ipField.onValueChanged.RemoveListener(HandleIpChanged);
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
            SetStatus($"Host dang mo. IP: {GetLocalIpv4Text()}  Port: {port}");
            networkManager.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            SetStatus("Khong the tao Host. Kiem tra lai mang/port.");
        }
        isStartingHost = false;
    }

    public void StartClient()
    {
        if (networkManager == null) { return; }
        if (networkManager.IsListening) { return; }

        string ip = ipField != null ? ipField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Ban chua nhap IP Host. Hay nhap IP truoc khi bam Client.");
            return;
        }
        if (!IsValidIpv4(ip))
        {
            SetStatus("IP khong hop le. Dung dinh dang 192.168.x.x");
            return;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(ip, port);
        }

        SetConnectionPayload();

        bool started = networkManager.StartClient();
        Debug.Log($"[LAN] StartClient result={started} ip={ip}:{port}");
        SetStatus(started
            ? $"Dang ket noi toi {ip}:{port}..."
            : "Khong the bat Client. Kiem tra IP/Port roi thu lai.");
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager == null) { return; }
        Debug.Log($"[LAN] Connected. LocalClientId={networkManager.LocalClientId}, EventClientId={clientId}");
        SetStatus("Ket noi thanh cong.");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"[LAN] Disconnected clientId={clientId}");
        SetStatus("Da ngat ket noi. Kiem tra IP Host va mang LAN.");
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

    private void HandleIpChanged(string rawValue)
    {
        string ip = rawValue == null ? string.Empty : rawValue.Trim();
        bool hasIp = !string.IsNullOrWhiteSpace(ip);
        bool isValid = hasIp && IsValidIpv4(ip);

        if (clientButton != null)
        {
            clientButton.interactable = isValid;
        }

        if (!hasIp)
        {
            SetStatus("Nhap IP cua may Host (vi du 192.168.1.7), sau do bam Client.");
            return;
        }

        if (!isValid)
        {
            SetStatus("IP khong hop le. Vi du dung: 192.168.1.7");
            return;
        }

        SetStatus($"San sang ket noi toi {ip}:{port}");
    }

    private static bool IsValidIpv4(string ip)
    {
        if (!IPAddress.TryParse(ip, out IPAddress parsed))
        {
            return false;
        }

        return parsed.AddressFamily == AddressFamily.InterNetwork;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[LAN UI] {message}");
    }

    private static string GetLocalIpv4Text()
    {
        try
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress address in hostEntry.AddressList)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork) { continue; }
                if (IPAddress.IsLoopback(address)) { continue; }
                return address.ToString();
            }
        }
        catch
        {
            // Ignore DNS lookup failure and fall back to unknown.
        }

        return "Khong ro";
    }
}
