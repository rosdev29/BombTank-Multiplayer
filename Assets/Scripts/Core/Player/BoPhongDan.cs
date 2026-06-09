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
    [SerializeField] private int ChiPhiBan;

    private bool isPointerOverUI;
    private bool duocTanCong;
    private float timer;       // timer cho người chơi thật
    private float timerBot;    // timer cooldown riêng cho bot
    private float henGioLoeNong;

    private int TeamIndexHienTai()
    {
        TankPlayer ownerPlayer = player != null ? player : GetComponent<TankPlayer>();
        return ownerPlayer != null ? ownerPlayer.TeamIndex.Value : -1;
    }


    public override void OnNetworkSpawn()
    {
        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }
        inputReader.PrimaryFireEvent += xuLyTanCongChinh;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }
        inputReader.PrimaryFireEvent -= xuLyTanCongChinh;
    }

    private void Update()
    {
        if(henGioLoeNong > 0f)
        {
            henGioLoeNong -= Time.deltaTime;
            if(henGioLoeNong <= 0f)
            {
                hieuUngLoeNong.SetActive(false);
            }
        }

        if (IsServer && timerBot > 0f)
        {
            timerBot -= Time.deltaTime;
        }

        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }

        isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (timer > 0 )
        {
            timer -= Time.deltaTime;
        }

        if (!duocTanCong) { return; }


        if (timer > 0 ) { return; }

        if(wallet.TotalCoins.Value < ChiPhiBan) { return; }
        
        int teamIndex = TeamIndexHienTai();

        xuLyBanChinhServerRpc(DiemSpawnDan.position, DiemSpawnDan.up);

        spawnDanGia(DiemSpawnDan.position, DiemSpawnDan.up, teamIndex);

        timer = 1 / tanSuatTanCong;
    }

    private void xuLyTanCongChinh(bool duocTanCong)
    {
        if (duocTanCong)
        {
            if (isPointerOverUI) { return; }
        }

        this.duocTanCong = duocTanCong;
    }

    public void BanBot()
    {
        if (!IsServer) { return; }
        if (DiemSpawnDan == null) { return; }
        if (timerBot > 0f) { return; }
        if (wallet == null || wallet.TotalCoins.Value < ChiPhiBan) { return; }

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
        int teamIndex = TeamIndexHienTai();

        SpawnServerBullet(viTriSpawn, huongDi, teamIndex);

        spawnDanGiaClientRpc(viTriSpawn, huongDi, teamIndex);
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

        GameObject danInstance = Instantiate(
            ClientDanPrefab,
            viTriSpawn,
            Quaternion.identity);

        danInstance.transform.up = huongDi;

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
    public void BotFire()
    {
        if (!IsServer) { return; }

        Debug.Log("BOT TRY FIRE: " + gameObject.name);

        if (timer > 0f) { return; }
        if (wallet.TotalCoins.Value < ChiPhiBan) { return; }

        int teamIndex = TeamIndexHienTai();

        SpawnServerBullet(DiemSpawnDan.position, DiemSpawnDan.up, teamIndex);
        spawnDanGiaClientRpc(DiemSpawnDan.position, DiemSpawnDan.up, teamIndex);

        timer = 1 / tanSuatTanCong;
    }
    private void SpawnServerBullet(Vector3 viTriSpawn, Vector3 huongDi, int teamIndex)
    {
        if (wallet.TotalCoins.Value < ChiPhiBan) { return; }

        wallet.SpendCoins(ChiPhiBan);

        GameObject danInstance = Instantiate(
            ServerDanPrefab,
            viTriSpawn,
            Quaternion.identity);

        danInstance.transform.up = huongDi;

        Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

        if (danInstance.TryGetComponent<SatThuongHoiMauVaCham>(out SatThuongHoiMauVaCham gaySatThuong))
        {
            TankPlayer ownerTank = player != null ? player : GetComponent<TankPlayer>();
            gaySatThuong.SetOwner(ownerTank, teamIndex);
        }

        if (danInstance.TryGetComponent<Projectile>(out Projectile projectile))
        {
            projectile.Initialise(teamIndex);
        }

        if (danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.velocity = rb.transform.up * TocDoDan;
        }
    }
}
