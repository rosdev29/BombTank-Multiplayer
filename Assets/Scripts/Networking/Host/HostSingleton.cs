using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
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
                Debug.LogError("No HostSingleton in the scence!");
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
   
}
