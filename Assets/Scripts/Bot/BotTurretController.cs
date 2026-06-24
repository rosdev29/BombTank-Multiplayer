using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Điều khiển nòng súng (turret) của bot.
/// Tự động lấy turretTransform từ NguoiChoiNgamBan — không cần gán tay trong Inspector.
/// </summary>
public class BotTurretController : MonoBehaviour
{
    [Tooltip("Tốc độ xoay nòng súng bot (độ/giây).")]
    [SerializeField] private float tocDoXoayNong = 180f;

    private Transform  _turretTransform;
    private BotContext _ctx;
    private NetworkObject _networkObject;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();

        var nguoiChoiNgamBan = GetComponent<NguoiChoiNgamBan>();
        if (nguoiChoiNgamBan != null)
        {
            _turretTransform = nguoiChoiNgamBan.TurretTransform;
        }

        if (_turretTransform == null)
        {
            Debug.LogWarning("[BotTurretController] Không tìm thấy turretTransform — nòng súng bot sẽ không xoay.");
        }
    }

    /// <summary>
    /// BotBrain gọi hàm này sau mỗi chu kỳ đánh giá để cập nhật điểm ngắm.
    /// </summary>
    public void DatContext(BotContext ctx)
    {
        _ctx = ctx;
    }

    private void LateUpdate()
    {
        if (_networkObject == null || !_networkObject.IsSpawned) { return; }
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) { return; }
        if (_ctx == null) { return; }
        if (_turretTransform == null) { return; }

        Vector2 diemNgam = _ctx.OutputDiemNgam;
        Vector2 huong    = diemNgam - (Vector2)_turretTransform.position;

        if (huong.sqrMagnitude < 0.001f) { return; }

        float gocMucTieu = Mathf.Atan2(huong.y, huong.x) * Mathf.Rad2Deg - 90f;
        float gocHienTai = _turretTransform.eulerAngles.z;

        float gocMoi = Mathf.MoveTowardsAngle(gocHienTai, gocMucTieu, tocDoXoayNong * Time.deltaTime);
        _turretTransform.rotation = Quaternion.Euler(0f, 0f, gocMoi);
    }
}
