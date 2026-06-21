using System;
using System.Collections;
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
    private Coroutine doubleBarrelCoroutine;

    private bool  isPointerOverUI;
    private bool  duocTanCong;
    private float timer;       // cooldown người chơi thật
    private float timerBot;    // cooldown riêng cho bot
    private float henGioLoeNong;

    // Visual Double Barrel
    private GameObject leftBarrel;
    private GameObject rightBarrel;
    private SpriteRenderer originalTurretRenderer;

    private int TeamIndexHienTai()
    {
        TankPlayer ownerPlayer = player != null ? player : GetComponent<TankPlayer>();
        return ownerPlayer != null ? ownerPlayer.TeamIndex.Value : -1;
    }

    public override void OnNetworkSpawn()
    {
        // Visual hook cho MỌI TANK trên bản đồ (để người khác nhìn thấy mình có 2 nòng)
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

<<<<<<< HEAD
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Đặt tần suất bắn cho bot (viên/giây). Gọi từ BotBrain.GanConfig().
    /// Ví dụ: thoiGianGiuaHaiVien=1.5s → DatTanSuatBot(1f/1.5f ≈ 0.667/s)
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
    // ─────────────────────────────────────────────────────────────────────
=======
    private void UpdateDoubleBarrelVisuals(bool oldVal, bool newVal)
    {
        if (originalTurretRenderer == null)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.gameObject.name == "Turret")
                {
                    originalTurretRenderer = sr;
                    break;
                }
            }
        }

        if (originalTurretRenderer == null) return;

        if (newVal)
        {
            originalTurretRenderer.enabled = false;
            PlayerColourDisplay colorDisplay = GetComponent<PlayerColourDisplay>();

            if (leftBarrel == null)
            {
                leftBarrel = new GameObject("LeftBarrel");
                leftBarrel.transform.SetParent(originalTurretRenderer.transform.parent, false);
                leftBarrel.transform.localPosition = originalTurretRenderer.transform.localPosition + new Vector3(-0.3f, 0, 0);
                leftBarrel.transform.localRotation = originalTurretRenderer.transform.localRotation;
                leftBarrel.transform.localScale = originalTurretRenderer.transform.localScale;
                SpriteRenderer sr = leftBarrel.AddComponent<SpriteRenderer>();
                sr.sprite = originalTurretRenderer.sprite;
                sr.color = originalTurretRenderer.color;
                sr.sortingLayerID = originalTurretRenderer.sortingLayerID;
                sr.sortingOrder = originalTurretRenderer.sortingOrder;
            }

            if (rightBarrel == null)
            {
                rightBarrel = new GameObject("RightBarrel");
                rightBarrel.transform.SetParent(originalTurretRenderer.transform.parent, false);
                rightBarrel.transform.localPosition = originalTurretRenderer.transform.localPosition + new Vector3(0.3f, 0, 0);
                rightBarrel.transform.localRotation = originalTurretRenderer.transform.localRotation;
                rightBarrel.transform.localScale = originalTurretRenderer.transform.localScale;
                SpriteRenderer sr = rightBarrel.AddComponent<SpriteRenderer>();
                sr.sprite = originalTurretRenderer.sprite;
                sr.color = originalTurretRenderer.color;
                sr.sortingLayerID = originalTurretRenderer.sortingLayerID;
                sr.sortingOrder = originalTurretRenderer.sortingOrder;
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
            PlayerColourDisplay colorDisplay = GetComponent<PlayerColourDisplay>();

            if (leftBarrel != null)
            {
                leftBarrel.SetActive(false);
                if (colorDisplay != null) colorDisplay.RemoveDynamicSprite(leftBarrel.GetComponent<SpriteRenderer>());
            }
            if (rightBarrel != null)
            {
                rightBarrel.SetActive(false);
                if (colorDisplay != null) colorDisplay.RemoveDynamicSprite(rightBarrel.GetComponent<SpriteRenderer>());
            }
        }
    }
>>>>>>> origin/item

    private void Update()
    {
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

        if (timer > 0)
            timer -= Time.deltaTime;

        if (!duocTanCong) { return; }
        if (timer > 0)    { return; }
        if (wallet.TotalCoins.Value < ChiPhiBan) { return; }

<<<<<<< HEAD
=======

        if (timer > 0 ) { return; }

        int soLuongDanCheck = IsDoubleBarrelActive.Value ? 2 : 1;
        int tongChiPhiCheck = ChiPhiBan * soLuongDanCheck;

        if (wallet.TotalCoins.Value < tongChiPhiCheck) { return; }
        
>>>>>>> origin/item
        int teamIndex = TeamIndexHienTai();
        xuLyBanChinhServerRpc(DiemSpawnDan.position, DiemSpawnDan.up);
        spawnDanGia(DiemSpawnDan.position, DiemSpawnDan.up, teamIndex);
        timer = 1 / tanSuatTanCong;
    }

    private void xuLyTanCongChinh(bool duocTanCong)
    {
        if (duocTanCong && isPointerOverUI) { return; }
        this.duocTanCong = duocTanCong;
    }

<<<<<<< HEAD
    /// <summary>BotShooter gọi method này. Cooldown được kiểm soát qua timerBot.</summary>
    public void BanBot()
    {
        if (!IsServer) { return; }
        if (DiemSpawnDan == null) { return; }
        if (timerBot > 0f) { return; }
        if (wallet == null || wallet.TotalCoins.Value < ChiPhiBan) { return; }
=======
    public void ActivateDoubleBarrel(float duration)
    {
        if (!IsServer) { return; }
        if (doubleBarrelCoroutine != null)
        {
            StopCoroutine(doubleBarrelCoroutine);
        }
        doubleBarrelCoroutine = StartCoroutine(DoubleBarrelRoutine(duration));
    }

    private IEnumerator DoubleBarrelRoutine(float duration)
    {
        IsDoubleBarrelActive.Value = true;
        yield return new WaitForSeconds(duration);
        IsDoubleBarrelActive.Value = false;
    }

    [ServerRpc]
>>>>>>> origin/item

        Vector3 viTriSpawn = DiemSpawnDan.position;
        Vector3 huongDi    = DiemSpawnDan.up;
        int     teamIndex  = TeamIndexHienTai();

        SpawnServerBullet(viTriSpawn, huongDi, teamIndex);
        spawnDanGiaClientRpc(viTriSpawn, huongDi, teamIndex);

        hieuUngLoeNong?.SetActive(true);
        henGioLoeNong = thoiGianHieuUngBan;

        timerBot = 1f / tanSuatTanCong;
    }

    [ServerRpc]
    private void xuLyBanChinhServerRpc(Vector3 viTriSpawn, Vector3 huongDi)
    {
<<<<<<< HEAD
        int teamIndex = TeamIndexHienTai();
        SpawnServerBullet(viTriSpawn, huongDi, teamIndex);
        spawnDanGiaClientRpc(viTriSpawn, huongDi, teamIndex);
=======

        int soLuongDan = IsDoubleBarrelActive.Value ? 2 : 1;
        int tongChiPhi = ChiPhiBan * soLuongDan;

        if (wallet.TotalCoins.Value < tongChiPhi) { return; }

        wallet.SpendCoins(tongChiPhi);

        float offsetTrucTiep = 0.3f; // Khoảng cách giữa 2 nòng (khớp với hình ảnh 2 nòng súng) (khớp với hình ảnh 2 nòng súng)

        for (int i = 0; i < soLuongDan; i++)
        {
            Vector3 huongBan = huongDi;
            Vector3 viTriBan = viTriSpawn;

            if (soLuongDan == 2)
            {
                // Tính vector bên phải của nòng súng để dịch chuyển 2 viên đạn sang 2 bên
                Vector3 vectorBenPhai = Vector3.Cross(huongDi, Vector3.forward).normalized;
                viTriBan += vectorBenPhai * (i == 0 ? -offsetTrucTiep : offsetTrucTiep);
            }

            GameObject danInstance = Instantiate(
                ServerDanPrefab,
                viTriBan,
                Quaternion.identity);

            danInstance.transform.up = huongBan;

        Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

        if (danInstance.TryGetComponent<SatThuongHoiMauVaCham>(out SatThuongHoiMauVaCham gaySatThuong))
        {
            int teamIndex = TeamIndexHienTai();
            TankPlayer ownerTank = player != null ? player : GetComponent<TankPlayer>();
            gaySatThuong.SetOwner(ownerTank, teamIndex);
        }    

            if (danInstance.TryGetComponent<Projectile>(out Projectile projectile))
            {
                projectile.Initialise(TeamIndexHienTai());
            }

            if (danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.velocity = rb.transform.up * TocDoDan;
            }
        }

        spawnDanGiaClientRpc(viTriSpawn, huongDi, TeamIndexHienTai());
>>>>>>> origin/item
    }

    [ClientRpc]
    private void spawnDanGiaClientRpc(Vector3 viTriSpawn, Vector3 huongDi, int teamIndex)
    {
        if (IsOwner) { return; }
        spawnDanGia(viTriSpawn, huongDi, teamIndex);
    }

    private void spawnDanGia(Vector3 viTriSpawn, Vector3 huongDi, int teamIndex)
    {
        hieuUngLoeNong.SetActive(true);
        henGioLoeNong = thoiGianHieuUngBan;

<<<<<<< HEAD
        GameObject danInstance = Instantiate(ClientDanPrefab, viTriSpawn, Quaternion.identity);
        danInstance.transform.up = huongDi;

        Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

        if (danInstance.TryGetComponent<Projectile>(out Projectile projectile))
            projectile.Initialise(teamIndex);

        if (danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            rb.velocity = rb.transform.up * TocDoDan;
    }

    private void SpawnServerBullet(Vector3 viTriSpawn, Vector3 huongDi, int teamIndex)
    {
        if (wallet.TotalCoins.Value < ChiPhiBan) { return; }

        wallet.SpendCoins(ChiPhiBan);

        GameObject danInstance = Instantiate(ServerDanPrefab, viTriSpawn, Quaternion.identity);
        danInstance.transform.up = huongDi;

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
=======
        int soLuongDan = IsDoubleBarrelActive.Value ? 2 : 1;
        float offsetTrucTiep = 0.3f;

        for (int i = 0; i < soLuongDan; i++)
        {
            Vector3 huongBan = huongDi;
            Vector3 viTriBan = viTriSpawn;

            if (soLuongDan == 2)
            {
                Vector3 vectorBenPhai = Vector3.Cross(huongDi, Vector3.forward).normalized;
                viTriBan += vectorBenPhai * (i == 0 ? -offsetTrucTiep : offsetTrucTiep);
            }

            GameObject danInstance = Instantiate(
                ClientDanPrefab,
                viTriBan,
                Quaternion.identity);

            danInstance.transform.up = huongBan;

        Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

            if (danInstance.TryGetComponent<Projectile>(out Projectile projectile))
            {
                projectile.Initialise(teamIndex);
            }

            if(danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb ))
            {
                rb.velocity = rb.transform.up * TocDoDan;
            }
        }
>>>>>>> origin/item
    }
}
