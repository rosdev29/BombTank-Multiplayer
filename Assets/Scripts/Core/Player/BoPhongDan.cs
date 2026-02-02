using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BoPhongDan : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform DiemSpawnDan;
    [SerializeField] private GameObject ServerDanPrefab;
    [SerializeField] private GameObject ClientDanPrefab;
    [SerializeField] private GameObject hieuUngLoeNong;
    [SerializeField] private Collider2D vaChamNguoiChoi;

    [Header("Settings")]
    [SerializeField] private float TocDoDan;
    [SerializeField] private float tanSuatTanCong;
    [SerializeField] private float thoiGianHieuUngBan;

    private bool duocTanCong;
    private float thoiDiemTanCongTruoc;
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

        if (!duocTanCong) { return; }


        if (Time.time < (1 / tanSuatTanCong) + thoiDiemTanCongTruoc) { return; }
        

        xuLyBanChinhServerRpc(DiemSpawnDan.position, DiemSpawnDan.up);

        spawnDanGia(DiemSpawnDan.position, DiemSpawnDan.up);

        thoiDiemTanCongTruoc = Time.time;
    }

    private void xuLyTanCongChinh(bool duocTanCong)
    {
        this.duocTanCong = duocTanCong;
    }

    [ServerRpc]

    private void xuLyBanChinhServerRpc(Vector3 viTriSpawn, Vector3 huongDi)
    {
        GameObject danInstance = Instantiate(
            ServerDanPrefab,
            viTriSpawn,
            Quaternion.identity);

        danInstance.transform.up = huongDi;

        Physics2D.IgnoreCollision(vaChamNguoiChoi, danInstance.GetComponent<Collider2D>());

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
        // spawnDanGiaClientRpc(viTriSpawn, huongDi); // đây là recursion vô hạn

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
