using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach vào TankPlayer prefab.
/// Lắng nghe BountySystem.CrownedNetworkIds để hiện/ẩn icon vương miện 👑.
///
/// Setup trong Inspector:
///   • crownObject  → child GameObject chứa Sprite vương miện (ẩn mặc định)
/// </summary>
public class CrownDisplay : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Child GameObject chứa Sprite vương miện. Đặt ẩn (SetActive false) mặc định.")]
    [SerializeField] private GameObject crownObject;

    [Header("Animation (optional)")]
    [Tooltip("Bob amplitude (pixel).")]
    [SerializeField] private float bobAmplitude = 0.08f;
    [Tooltip("Bob speed.")]
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 _crownLocalOrigin;
    private bool    _hasCrown;

    // ─────────────────────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (crownObject != null)
        {
            _crownLocalOrigin = crownObject.transform.localPosition;
            crownObject.SetActive(false);
        }

        // Cả Server lẫn Client đều lắng nghe — đơn giản và chính xác
        BountySystem.OnCrownListChanged += RefreshCrown;

        // Kiểm tra ngay lần đầu (BountySystem có thể đã chạy rồi)
        RefreshCrown();
    }

    public override void OnNetworkDespawn()
    {
        BountySystem.OnCrownListChanged -= RefreshCrown;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void RefreshCrown()
    {
        if (BountySystem.Instance == null || !BountySystem.Instance.IsSpawned) { return; }

        bool shouldHaveCrown = BountySystem.Instance.HasCrown(NetworkObjectId);

        if (shouldHaveCrown == _hasCrown) { return; }  // không đổi → bỏ qua

        _hasCrown = shouldHaveCrown;
        if (crownObject != null)
        {
            crownObject.SetActive(_hasCrown);
        }
    }

    // ─── Bob animation khi đang có crown ─────────────────────────────────────
    private void Update()
    {
        if (!_hasCrown || crownObject == null) { return; }

        float offsetY = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        crownObject.transform.localPosition = _crownLocalOrigin + new Vector3(0f, offsetY, 0f);
    }
}
