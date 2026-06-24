using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class BoPhongDan : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private TankPlayer player;
    [SerializeField] private InputReader inputReader;
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private Transform DiemSpawnDan;
    [SerializeField] private GameObject ServerDanPrefab;
    [SerializeField] private GameObject ClientDanPrefab;
    [SerializeField] private GameObject hieuUngLoeNong;
    [SerializeField] private Collider2D vaChamNguoiChoi;

    [Header("Settings")]
    [SerializeField] private float TocDoDan;
    [SerializeField] private float tanSuatTanCong;
    [SerializeField] private float thoiGianHieuUngBan;
    [SerializeField] private int ChiPhiBan = 5;

    public NetworkVariable<bool> IsDoubleBarrelActive = new NetworkVariable<bool>(false);

    private bool  isPointerOverUI;
    private bool  duocTanCong;
    private float timer;
    private float timerBot;
    private float henGioLoeNong;
    private float doubleBarrelTimer;

    private GameObject leftBarrel;
    private GameObject rightBarrel;
    private SpriteRenderer originalTurretRenderer;

    public int GetShootingCost()
    {
        int soLuongDan = IsDoubleBarrelActive.Value ? 2 : 1;
        return ChiPhiBan * soLuongDan;
    }

    private int TeamIndexHienTai()
    {
        TankPlayer ownerPlayer = player != null ? player : GetComponent<TankPlayer>();
        return ownerPlayer != null ? ownerPlayer.TeamIndex.Value : -1;
    }

    public override void OnNetworkSpawn()
    {
        IsDoubleBarrelActive.OnValueChanged += UpdateDoubleBarrelVisuals;
        UpdateDoubleBarrelVisuals(false, IsDoubleBarrelActive.Value);

        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }
        inputReader.PrimaryFireEvent += xuLyTanCongChinh;
    }

    public override void OnNetworkDespawn()
    {
        IsDoubleBarrelActive.OnValueChanged -= UpdateDoubleBarrelVisuals;

        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }
        inputReader.PrimaryFireEvent -= xuLyTanCongChinh;
    }

    /// <summary>
    /// Đặt tần suất bắn cho bot (viên/giây). Gọi từ BotBrain.GanConfig().
    /// </summary>
    public void DatTanSuatBot(float tanSuat)
    {
        if (tanSuat <= 0f)
        {
            Debug.LogWarning("[BoPhongDan] DatTanSuatBot: tanSuat phải > 0, bỏ qua.");
            return;
        }

        tanSuatTanCong = tanSuat;
        Debug.Log($"[BoPhongDan] DatTanSuatBot → {tanSuat:F3}/s (delay={1f / tanSuat:F2}s)");
    }

    public void ActivateDoubleBarrel(float duration)
    {
        if (!IsServer) { return; }

        if (doubleBarrelTimer > 0f)
            doubleBarrelTimer += duration;
        else
        {
            doubleBarrelTimer = duration;
            IsDoubleBarrelActive.Value = true;
        }
    }

    private void UpdateDoubleBarrelVisuals(bool oldVal, bool newVal)
    {
        if (originalTurretRenderer == null)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.gameObject.name == "Turret")
                {
                    originalTurretRenderer = sr;
                    break;
                }
            }
        }

        if (originalTurretRenderer == null) { return; }

        PlayerColourDisplay colorDisplay = GetComponent<PlayerColourDisplay>();

        if (newVal)
        {
            originalTurretRenderer.enabled = false;

            if (leftBarrel == null)
            {
                leftBarrel = TaoNongPhu(originalTurretRenderer, new Vector3(-0.3f, 0f, 0f));
            }

            if (rightBarrel == null)
            {
                rightBarrel = TaoNongPhu(originalTurretRenderer, new Vector3(0.3f, 0f, 0f));
            }

            leftBarrel.SetActive(true);
            rightBarrel.SetActive(true);

            if (colorDisplay != null)
            {
                colorDisplay.AddDynamicSprite(leftBarrel.GetComponent<SpriteRenderer>());
                colorDisplay.AddDynamicSprite(rightBarrel.GetComponent<SpriteRenderer>());
            }
        }
        else
        {
            originalTurretRenderer.enabled = true;

            if (leftBarrel != null)
            {
                leftBarrel.SetActive(false);
                if (colorDisplay != null)
                    colorDisplay.RemoveDynamicSprite(leftBarrel.GetComponent<SpriteRenderer>());
            }

            if (rightBarrel != null)
            {
                rightBarrel.SetActive(false);
                if (colorDisplay != null)
                    colorDisplay.RemoveDynamicSprite(rightBarrel.GetComponent<SpriteRenderer>());
            }
        }
    }

    private static GameObject TaoNongPhu(SpriteRenderer nongGoc, Vector3 localOffset)
    {
        GameObject nongPhu = new GameObject(nongGoc.gameObject.name == "Turret" ? "BarrelClone" : "ExtraBarrel");
        nongPhu.transform.SetParent(nongGoc.transform.parent, false);
        nongPhu.transform.localPosition = nongGoc.transform.localPosition + localOffset;
        nongPhu.transform.localRotation = nongGoc.transform.localRotation;
        nongPhu.transform.localScale    = nongGoc.transform.localScale;

        SpriteRenderer sr = nongPhu.AddComponent<SpriteRenderer>();
        sr.sprite         = nongGoc.sprite;
        sr.color          = nongGoc.color;
        sr.sortingLayerID = nongGoc.sortingLayerID;
        sr.sortingOrder   = nongGoc.sortingOrder;
        return nongPhu;
    }

    private void Update()
    {
        if (IsServer && doubleBarrelTimer > 0f)
        {
            doubleBarrelTimer -= Time.deltaTime;
            if (doubleBarrelTimer <= 0f)
            {
                doubleBarrelTimer = 0f;
                IsDoubleBarrelActive.Value = false;
            }
        }

        if (henGioLoeNong > 0f)
        {
            henGioLoeNong -= Time.deltaTime;
            if (henGioLoeNong <= 0f)
                hieuUngLoeNong.SetActive(false);
        }

        if (IsServer && timerBot > 0f)
            timerBot -= Time.deltaTime;

        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }

        isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (timer > 0f)
            timer -= Time.deltaTime;

        if (!duocTanCong) { return; }
        if (MatchEndBridge.IsMatchEnded) { return; }
        if (timer > 0f)    { return; }
        if (wallet.TotalCoins.Value < GetShootingCost()) { return; }

        int teamIndex = TeamIndexHienTai();
        xuLyBanChinhServerRpc(DiemSpawnDan.position, DiemSpawnDan.up);
        spawnDanGia(DiemSpawnDan.position, DiemSpawnDan.up, teamIndex);
        AudioManager.GetInstance()?.PlayGunshot();

        timer = 1f / tanSuatTanCong;
    }

    private void xuLyTanCongChinh(bool duocTanCong)
    {
        if (duocTanCong && isPointerOverUI) { return; }
        this.duocTanCong = duocTanCong;
    }

    /// <summary>BotShooter gọi method này. Cooldown được kiểm soát qua timerBot.</summary>
    public void BanBot()
    {
        if (!IsServer) { return; }
        if (MatchEndBridge.IsMatchEnded) { return; }
        if (DiemSpawnDan == null) { return; }
        if (timerBot > 0f) { return; }
        if (wallet == null || wallet.TotalCoins.Value < GetShootingCost()) { return; }

        Vector3 viTriSpawn = DiemSpawnDan.position;
        Vector3 huongDi    = DiemSpawnDan.up;
        int     teamIndex  = TeamIndexHienTai();

        SpawnServerBullets(viTriSpawn, huongDi, teamIndex);
        spawnDanGiaClientRpc(viTriSpawn, huongDi, teamIndex);

        hieuUngLoeNong?.SetActive(true);
        henGioLoeNong = thoiGianHieuUngBan;
        timerBot = 1f / tanSuatTanCong;
    }

    [ServerRpc]
    private void xuLyBanChinhServerRpc(Vector3 viTriSpawn, Vector3 huongDi)
    {
        if (MatchEndBridge.IsMatchEnded) { return; }

        SpawnServerBullets(viTriSpawn, huongDi, TeamIndexHienTai());
        spawnDanGiaClientRpc(viTriSpawn, huongDi, TeamIndexHienTai());
    }

    [ClientRpc]
    private void spawnDanGiaClientRpc(Vector3 viTriSpawn, Vector3 huongDi, int teamIndex)
    {
        if (IsOwner) { return; }
        spawnDanGia(viTriSpawn, huongDi, teamIndex);
    }

    private void SpawnServerBullets(Vector3 viTriSpawn, Vector3 huongDi, int teamIndex)
    {
        if (MatchEndBridge.IsMatchEnded) { return; }

        int soLuongDan = IsDoubleBarrelActive.Value ? 2 : 1;
        int tongChiPhi = ChiPhiBan * soLuongDan;

        if (wallet.TotalCoins.Value < tongChiPhi) { return; }

        if (!wallet.TrySpendCoins(tongChiPhi)) { return; }

        const float offsetTrucTiep = 0.3f;

        for (int i = 0; i < soLuongDan; i++)
        {
            Vector3 viTriBan = viTriSpawn;
            Vector3 huongBan = huongDi;

            if (soLuongDan == 2)
            {
                Vector3 vectorBenPhai = Vector3.Cross(huongDi, Vector3.forward).normalized;
                viTriBan += vectorBenPhai * (i == 0 ? -offsetTrucTiep : offsetTrucTiep);
            }

            GameObject danInstance = Instantiate(ServerDanPrefab, viTriBan, Quaternion.identity);
            danInstance.transform.up = huongBan;

            Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

            if (danInstance.TryGetComponent<SatThuongHoiMauVaCham>(out SatThuongHoiMauVaCham gaySatThuong))
            {
                TankPlayer ownerTank = player != null ? player : GetComponent<TankPlayer>();
                gaySatThuong.SetOwner(ownerTank, teamIndex);
            }

            if (danInstance.TryGetComponent<Projectile>(out Projectile projectile))
                projectile.Initialise(teamIndex);

            if (danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                rb.velocity = rb.transform.up * TocDoDan;
        }
    }

    private void spawnDanGia(Vector3 viTriSpawn, Vector3 huongDi, int teamIndex)
    {
        hieuUngLoeNong.SetActive(true);
        henGioLoeNong = thoiGianHieuUngBan;

        int soLuongDan = IsDoubleBarrelActive.Value ? 2 : 1;
        const float offsetTrucTiep = 0.3f;

        for (int i = 0; i < soLuongDan; i++)
        {
            Vector3 viTriBan = viTriSpawn;
            Vector3 huongBan = huongDi;

            if (soLuongDan == 2)
            {
                Vector3 vectorBenPhai = Vector3.Cross(huongDi, Vector3.forward).normalized;
                viTriBan += vectorBenPhai * (i == 0 ? -offsetTrucTiep : offsetTrucTiep);
            }

            GameObject danInstance = Instantiate(ClientDanPrefab, viTriBan, Quaternion.identity);
            danInstance.transform.up = huongBan;

            Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

            if (danInstance.TryGetComponent<Projectile>(out Projectile projectile))
                projectile.Initialise(teamIndex);

            if (danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                rb.velocity = rb.transform.up * TocDoDan;
        }
    }
}
