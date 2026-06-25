using System.Collections;
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

    private GameObject errorPopupRoot;
    private Coroutine hideErrorPopupCoroutine;
    private Coroutine showErrorPopupCoroutine;
    private Coroutine joinMonitorCoroutine;
    private bool pendingClientJoin;
    private bool joinFailureHandled;

    [SerializeField] private float clientJoinTimeoutSeconds = 6f;

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        if (clientButton == null)
        {
            GameObject clientBtnGo = GameObject.Find("ClientButton");
            if (clientBtnGo != null)
                clientButton = clientBtnGo.GetComponent<Button>();
        }

        if (ipField != null)
        {
            SanitizeIpFieldOnLoad();
        }

        if (clientButton != null)
        {
            clientButton.interactable = true;
        }
    }

    private void SanitizeIpFieldOnLoad()
    {
        if (ipField == null) { return; }

        string raw = ipField.text ?? string.Empty;
        if (IsPlaceholderIpText(raw))
        {
            ipField.SetTextWithoutNotify(string.Empty);
        }
    }

    private static bool IsPlaceholderIpText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return false; }

        string trimmed = raw.Trim().Replace("\u200b", string.Empty);
        if (trimmed.Contains("Nhập Ip", System.StringComparison.OrdinalIgnoreCase)) { return true; }
        if (trimmed.Contains("Nhap Ip", System.StringComparison.OrdinalIgnoreCase)) { return true; }

        return false;
    }

    private string GetIpInputRaw()
    {
        return ipField != null ? ipField.text : string.Empty;
    }

    private bool TryGetClientIp(out string ip)
    {
        ip = GetIpInputRaw().Trim()
            .Replace("\u200b", string.Empty);

        if (string.IsNullOrWhiteSpace(ip) || IsPlaceholderIpText(ip))
        {
            ip = string.Empty;
            return false;
        }

        if (!IsValidIpv4(ip))
        {
            return false;
        }

        return true;
    }

    private void OnEnable()
    {
        if (clientButton != null)
        {
            clientButton.interactable = true;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic(AudioManager.Instance.menuMusic);
        }
    }

    private void OnDestroy()
    {
        StopJoinMonitor();

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
            AudioManager.Instance?.StopMusic();
            LogStatus($"Host đang mở. IP: {GetLocalIpv4Text()}  Port: {port}");

            networkManager.SceneManager.LoadScene(
                gameSceneName,
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );
        }
        else
        {
            LogStatus("Không thể tạo Host. Kiểm tra lại mạng/port.");
        }

        isStartingHost = false;
    }

    public void StartClient()
    {
        if (networkManager == null) { return; }
        if (networkManager.IsListening) { return; }

        string ip = GetIpInputRaw().Trim().Replace("\u200b", string.Empty);

        if (string.IsNullOrWhiteSpace(ip) || IsPlaceholderIpText(ip))
        {
            ShowConnectionErrorPopup(
                "Chưa nhập IP",
                "Hãy nhập địa chỉ IP của máy Host trước khi bấm Tham gia.");
            return;
        }

        if (!TryGetClientIp(out ip))
        {
            ShowConnectionErrorPopup(
                "IP không đúng",
                "Địa chỉ IP bạn nhập không hợp lệ.\nVí dụ: 192.168.1.7");
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

        if (!started)
        {
            ShowConnectionErrorPopup(
                "Chưa có phòng",
                "Không tìm thấy Host.\nHãy nhờ người chơi khác bấm Tạo phòng trước, rồi thử lại.");
            return;
        }

        pendingClientJoin = true;
        joinFailureHandled = false;
        StartJoinMonitor();
        LogStatus($"Đang kết nối tới {ip}:{port}...");
    }

    private void StartJoinMonitor()
    {
        if (joinMonitorCoroutine != null)
        {
            StopCoroutine(joinMonitorCoroutine);
        }

        joinMonitorCoroutine = StartCoroutine(MonitorClientJoin());
    }

    private void StopJoinMonitor()
    {
        if (joinMonitorCoroutine != null)
        {
            StopCoroutine(joinMonitorCoroutine);
            joinMonitorCoroutine = null;
        }

        pendingClientJoin = false;
    }

    private IEnumerator MonitorClientJoin()
    {
        float elapsed = 0f;

        while (elapsed < clientJoinTimeoutSeconds)
        {
            if (networkManager == null)
            {
                break;
            }

            if (networkManager.IsConnectedClient)
            {
                StopJoinMonitor();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (networkManager != null && pendingClientJoin && !networkManager.IsConnectedClient)
        {
            HandleJoinFailed(
                "Chưa có phòng",
                "Không tìm thấy Host.\nHãy nhờ người chơi khác bấm Tạo phòng trước, rồi thử lại.");
        }

        joinMonitorCoroutine = null;
    }

    private void HandleJoinFailed(string title, string body)
    {
        if (joinFailureHandled) { return; }
        joinFailureHandled = true;

        StopJoinMonitor();
        ShutdownClientIfNeeded();
        RestoreMenuMusic();
        ShowConnectionErrorPopup(title, body);
    }

    private void ShutdownClientIfNeeded()
    {
        if (networkManager == null) { return; }
        if (!networkManager.IsListening) { return; }

        networkManager.Shutdown();
    }

    private static void RestoreMenuMusic()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null || audio.menuMusic == null) { return; }

        audio.PlayMusic(audio.menuMusic);
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager == null) { return; }
        if (clientId != networkManager.LocalClientId) { return; }

        StopJoinMonitor();
        AudioManager.Instance?.StopMusic();

        Debug.Log($"[LAN] Connected. LocalClientId={networkManager.LocalClientId}, EventClientId={clientId}");
        LogStatus("Kết nối thành công.");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager == null) { return; }
        if (networkManager.IsHost) { return; }
        if (clientId != 0 && clientId != networkManager.LocalClientId) { return; }

        Debug.Log($"[LAN] Disconnected clientId={clientId}");

        if (!pendingClientJoin && !networkManager.IsConnectedClient)
        {
            return;
        }

        HandleJoinFailed(
            "Chưa có phòng",
            "Không kết nối được Host.\nKiểm tra IP đúng, cùng WiFi, và Host đã bấm Tạo phòng.");
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

    private static bool IsValidIpv4(string ip)
    {
        if (!IPAddress.TryParse(ip, out IPAddress parsed))
        {
            return false;
        }

        if (parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        string[] parts = ip.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int octet) || octet < 0 || octet > 255)
            {
                return false;
            }
        }

        return true;
    }

    private void LogStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[LAN UI] {message}");
    }

    private void ShowConnectionErrorPopup(string title, string body)
    {
        ShowInvalidIpPopup(title, body);
    }

    private void ShowInvalidIpPopup(string title, string body)
    {
        EnsureErrorPopup();
        if (errorPopupRoot == null) { return; }

        Transform panel = errorPopupRoot.transform.Find("Panel");
        if (panel != null)
        {
            TextMeshProUGUI titleText = panel.Find("Title")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI bodyText = panel.Find("Body")?.GetComponent<TextMeshProUGUI>();
            if (titleText != null) { titleText.text = title; }
            if (bodyText != null) { bodyText.text = body; }
        }

        if (showErrorPopupCoroutine != null)
        {
            StopCoroutine(showErrorPopupCoroutine);
        }

        if (hideErrorPopupCoroutine != null)
        {
            StopCoroutine(hideErrorPopupCoroutine);
        }

        showErrorPopupCoroutine = StartCoroutine(ShowErrorPopupDeferred());
    }

    private IEnumerator ShowErrorPopupDeferred()
    {
        // Chờ frame sau để popup không ăn cùng sự kiện nhấn/chuột với nút Tham gia.
        yield return null;
        showErrorPopupCoroutine = null;

        if (errorPopupRoot != null)
        {
            errorPopupRoot.SetActive(true);
        }

        hideErrorPopupCoroutine = StartCoroutine(HideErrorPopupAfterDelay(4f));
    }

    public void CloseErrorPopup()
    {
        if (errorPopupRoot != null)
        {
            errorPopupRoot.SetActive(false);
        }

        if (hideErrorPopupCoroutine != null)
        {
            StopCoroutine(hideErrorPopupCoroutine);
            hideErrorPopupCoroutine = null;
        }

        if (showErrorPopupCoroutine != null)
        {
            StopCoroutine(showErrorPopupCoroutine);
            showErrorPopupCoroutine = null;
        }
    }

    private void EnsureErrorPopup()
    {
        if (errorPopupRoot != null) { return; }

        Canvas canvas = ipField != null
            ? ipField.GetComponentInParent<Canvas>()
            : FindAnyObjectByType<Canvas>();
        if (canvas == null) { return; }

        errorPopupRoot = new GameObject("LanErrorPopup", typeof(RectTransform));
        RectTransform rootRect = errorPopupRoot.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = errorPopupRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        GameObject panelGo = new GameObject("Panel", typeof(RectTransform));
        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.SetParent(rootRect, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 280f);

        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.18f, 0.14f, 0.1f, 0.98f);

        GameObject titleGo = new GameObject("Title", typeof(RectTransform));
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.SetParent(panelRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(-40f, 48f);

        TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.font = TMP_Settings.defaultFontAsset;
        titleTmp.fontSize = 32f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.75f, 0.35f, 1f);

        GameObject bodyGo = new GameObject("Body", typeof(RectTransform));
        RectTransform bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.SetParent(panelRect, false);
        bodyRect.anchorMin = new Vector2(0f, 0.35f);
        bodyRect.anchorMax = new Vector2(1f, 0.85f);
        bodyRect.offsetMin = new Vector2(28f, 0f);
        bodyRect.offsetMax = new Vector2(-28f, 0f);

        TextMeshProUGUI bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTmp.font = TMP_Settings.defaultFontAsset;
        bodyTmp.fontSize = 24f;
        bodyTmp.alignment = TextAlignmentOptions.Center;
        bodyTmp.color = Color.white;

        GameObject btnGo = new GameObject("OkButton", typeof(RectTransform));
        RectTransform btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.SetParent(panelRect, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 24f);
        btnRect.sizeDelta = new Vector2(200f, 52f);

        Image btnBg = btnGo.AddComponent<Image>();
        btnBg.color = new Color(0.45f, 0.28f, 0.12f, 1f);

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        btn.onClick.AddListener(CloseErrorPopup);
        btnGo.AddComponent<ButtonClickSfx>();

        GameObject btnLabelGo = new GameObject("Text", typeof(RectTransform));
        RectTransform btnLabelRect = btnLabelGo.GetComponent<RectTransform>();
        btnLabelRect.SetParent(btnRect, false);
        btnLabelRect.anchorMin = Vector2.zero;
        btnLabelRect.anchorMax = Vector2.one;
        btnLabelRect.offsetMin = Vector2.zero;
        btnLabelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnLabel = btnLabelGo.AddComponent<TextMeshProUGUI>();
        btnLabel.font = TMP_Settings.defaultFontAsset;
        btnLabel.text = "Đồng ý";
        btnLabel.fontSize = 26f;
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.color = Color.white;
        btnLabel.raycastTarget = false;

        errorPopupRoot.SetActive(false);
    }

    private IEnumerator HideErrorPopupAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        CloseErrorPopup();
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

        return "Không rõ";
    }
}
