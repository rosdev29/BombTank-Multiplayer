using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Điều khiển bắn đạn của bot.
/// Script này delegate sang BoPhongDan thông qua public method BanBot().
///
/// Cần được thêm vào cùng GameObject với BotBrain và BoPhongDan.
/// </summary>
public class BotShooter : MonoBehaviour
{
    private BoPhongDan _boPhongDan;
    private NetworkObject _networkObject;

    private void Awake()
    {
        _boPhongDan = GetComponent<BoPhongDan>();
        _networkObject = GetComponent<NetworkObject>();
    }

    /// <summary>
    /// BotBrain gọi hàm này sau mỗi chu kỳ đánh giá (không phải mỗi frame).
    /// Truyền vào lệnh bắn hiện tại — chỉ bắn nếu cmd.Fire = true.
    /// Cooldown được handle bên trong BoPhongDan.BanBot().
    /// </summary>
    public void XuLyBan(bool coBopCo)
    {
        if (_networkObject == null || !_networkObject.IsSpawned) { return; }
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) { return; }
        if (_boPhongDan == null) { return; }

        if (coBopCo)
        {
            _boPhongDan.BanBot();
        }
    }
}
