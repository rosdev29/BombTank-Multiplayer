using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ConnectionButtons : MonoBehaviour
{
    /// <summary>
    /// Trong Editor, transport đôi khi chưa sẵn sàng ngay khi Play → đợi 1 frame rồi mới Start.
    /// </summary>
    IEnumerator DelayedStartHost()
    {
        yield return new WaitForSeconds(0.2f); // tăng delay nếu vẫn lỗi 
        if (NetworkManager.Singleton == null) yield break;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) yield break;
        NetworkManager.Singleton.StartHost();
    }

    IEnumerator DelayedStartClient()
    {
        // Join cần đợi transport lâu hơn trong Editor
        yield return new WaitForSeconds(0.5f);
        if (NetworkManager.Singleton == null) yield break;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) yield break;
        NetworkManager.Singleton.StartClient();
    }

    public void StartHost()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) return;
        StartCoroutine(DelayedStartHost());
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient) return;
        StartCoroutine(DelayedStartClient());
    }
}
