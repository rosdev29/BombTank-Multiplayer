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

    [Header("Settings")]
    [SerializeField] private float TocDoDan;
    private bool duocTanCong;

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
        if (!IsOwner) { return; }
        if (!duocTanCong) { return; }

        xuLyBanChinhServerRpc(DiemSpawnDan.position, DiemSpawnDan.up);

        spawnDanGia(DiemSpawnDan.position, DiemSpawnDan.up);

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

    }
    [ClientRpc]

    private void spawnDanGiaClientRpc(Vector3 viTriSpawn, Vector3 huongDi)
    {
        if (!IsOwner) { return; }
        spawnDanGia(viTriSpawn, huongDi);
        spawnDanGiaClientRpc(viTriSpawn, huongDi);

    }

    private void spawnDanGia(Vector3 viTriSpawn, Vector3 huongDi)
    {
        GameObject danInstance = Instantiate(
            ClientDanPrefab,
            viTriSpawn,
            Quaternion.identity);

        danInstance.transform.up = huongDi;

    }
}
