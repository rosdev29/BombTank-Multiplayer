using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BoPhongDan : NetworkBehaviour
{
    [Header("References")]
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

    private bool duocTanCong;
    private float timer;
    private float henGioLoeNong;


    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }
        inputReader.PrimaryFireEvent += xuLyTanCongChinh;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) { return; }
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
        if (!IsOwner) { return; }

        if (timer > 0 )
        {
            timer -= Time.deltaTime;
        }

        if (!duocTanCong) { return; }


        if (timer > 0 ) { return; }

        if(wallet.TotalCoins.Value < ChiPhiBan) { return; }
        

        xuLyBanChinhServerRpc(DiemSpawnDan.position, DiemSpawnDan.up);

        spawnDanGia(DiemSpawnDan.position, DiemSpawnDan.up);

        timer = 1 / tanSuatTanCong;
    }

    private void xuLyTanCongChinh(bool duocTanCong)
    {
        this.duocTanCong = duocTanCong;
    }

    [ServerRpc]

    private void xuLyBanChinhServerRpc(Vector3 viTriSpawn, Vector3 huongDi)
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
            gaySatThuong.SetOwner(OwnerClientId);
        }    

        if (danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.velocity = rb.transform.up * TocDoDan;
        }

        spawnDanGiaClientRpc(viTriSpawn, huongDi);
    }

    [ClientRpc]

    private void spawnDanGiaClientRpc(Vector3 viTriSpawn, Vector3 huongDi)
    {
        if (!IsOwner) { return; }
        spawnDanGia(viTriSpawn, huongDi);
    }

    private void spawnDanGia(Vector3 viTriSpawn, Vector3 huongDi)
    {
        hieuUngLoeNong.SetActive(true);
        henGioLoeNong = thoiGianHieuUngBan;

        GameObject danInstance = Instantiate(
            ClientDanPrefab,
            viTriSpawn,
            Quaternion.identity);

        danInstance.transform.up = huongDi;

        Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

        if(danInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb ))
        {
            rb.velocity = rb.transform.up * TocDoDan;
        }
    }
}
