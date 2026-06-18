using Unity.Netcode;
using UnityEngine;

public class CrownDisplay : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("CrownIcon — hiện phía trên tên player (ẩn mặc định).")]
    [SerializeField] private GameObject crownObject;

    [Tooltip("Crown minimap — thay thế MinimapIcon khi có bounty (ẩn mặc định).")]
    [SerializeField] private GameObject crownMinimapObject;

    [Tooltip("MinimapIcon gốc của tank — ẩn khi có bounty.")]
    [SerializeField] private GameObject minimapIcon;

    [Header("Animation (optional)")]
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed     = 2f;

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

        if (crownMinimapObject != null)
            crownMinimapObject.SetActive(false);

        BountySystem.OnCrownListChanged += RefreshCrown;
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
        if (shouldHaveCrown == _hasCrown) { return; }

        _hasCrown = shouldHaveCrown;

        // Icon trên tên
        if (crownObject != null)
            crownObject.SetActive(_hasCrown);

        // Swap minimap: ẩn icon gốc, hiện crown minimap (hoặc ngược lại)
        if (minimapIcon != null)
            minimapIcon.SetActive(!_hasCrown);

        if (crownMinimapObject != null)
            crownMinimapObject.SetActive(_hasCrown);
    }

    // ─── Bob animation khi đang có crown ─────────────────────────────────────
    private void Update()
    {
        if (!_hasCrown || crownObject == null) { return; }

        float offsetY = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        crownObject.transform.localPosition = _crownLocalOrigin + new Vector3(0f, offsetY, 0f);
    }
}