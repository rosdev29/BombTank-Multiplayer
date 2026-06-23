using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HienThiMau : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Mau mau;
    [SerializeField] private Image ThanhMauImage;

    private TankPlayer player;

    public override void OnNetworkSpawn()
    {
        if (!IsClient) { return; }

        mau.MauHienTai.OnValueChanged += XuLyKhiMauThayDoi;
        mau.MauToiDaNet.OnValueChanged += XuLyKhiMauToiDaThayDoi;
        XuLyKhiMauThayDoi(0, mau.MauHienTai.Value);

        player = GetComponentInParent<TankPlayer>();
    }

    public override void OnNetworkDespawn()
    {
        if(!IsClient) { return; }
        mau.MauHienTai.OnValueChanged -= XuLyKhiMauThayDoi;
        mau.MauToiDaNet.OnValueChanged -= XuLyKhiMauToiDaThayDoi;
    }

    private void XuLyKhiMauToiDaThayDoi(int oldMax, int maxMoi)
    {
        XuLyKhiMauThayDoi(mau.MauHienTai.Value, mau.MauHienTai.Value);
    }

    private void XuLyKhiMauThayDoi(int mauOld, int mauMoi)
    {
        int maxMau = Mathf.Max(1, mau.MauToiDaNet.Value);
        ThanhMauImage.fillAmount = (float)mauMoi / maxMau;
    }
}
