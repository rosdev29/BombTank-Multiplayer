using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class HostSingleton : MonoBehaviour
{
    private static HostSingleton instance;

    public HostGameManager GameManager {  get; private set; }

    public static HostSingleton Instance
    {
        get
        {
            if (instance != null) { return instance; }

            instance = FindObjectOfType<HostSingleton>();
            if (instance == null)
            {
                return null;
            }
            return instance;
        }
        }


    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void CreateHost()
    {
        GameManager = new HostGameManager();
    }

    public void CreateHost(NetworkObject playerPrefab)
    {
        if (NetworkManager.Singleton != null && playerPrefab != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = playerPrefab.gameObject;
        }

        CreateHost();
    }

    private void OnDestroy()
    {
        GameManager?.Dispose();
        if (instance == this)
        {
            instance = null;
        }
    }
}
