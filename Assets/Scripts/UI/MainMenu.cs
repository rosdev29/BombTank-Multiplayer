using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeField;
    private bool isStartingHost;

    public async void StartHost()
    {
        if (isStartingHost) { return; }
        if (NetworkManager.Singleton == null) { return; }
        if (NetworkManager.Singleton.IsListening ||
            NetworkManager.Singleton.IsServer ||
            NetworkManager.Singleton.IsClient) { return; }

        isStartingHost = true;
        await HostSingleton.Instance.GameManager.StartHostAsync();
        isStartingHost = false;
    }

    public async void StartClient()
    {
        await ClientSingleton.Instance.GameManager.StartClientAsync(joinCodeField.text);

    }
}
